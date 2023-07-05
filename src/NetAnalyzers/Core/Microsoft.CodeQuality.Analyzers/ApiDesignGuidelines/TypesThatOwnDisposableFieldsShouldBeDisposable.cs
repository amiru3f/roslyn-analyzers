﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1001: <inheritdoc cref="TypesThatOwnDisposableFieldsShouldBeDisposableTitle"/>
    /// </summary>
    public abstract class TypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer<TTypeDeclarationSyntax> : DiagnosticAnalyzer
            where TTypeDeclarationSyntax : SyntaxNode
    {
        internal const string RuleId = "CA1001";
        internal const string Dispose = "Dispose";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(TypesThatOwnDisposableFieldsShouldBeDisposableTitle)),
            CreateLocalizableResourceString(nameof(TypesThatOwnDisposableFieldsShouldBeDisposableMessageNonBreaking)),
            DiagnosticCategory.Design,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: CreateLocalizableResourceString(nameof(TypesThatOwnDisposableFieldsShouldBeDisposableDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationContext.Compilation);
                if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIDisposable, out _))
                {
                    return;
                }

                DisposableFieldAnalyzer analyzer = GetAnalyzer(compilationContext.Compilation);
                compilationContext.RegisterSymbolAction(analyzer.AnalyzeSymbol, SymbolKind.NamedType);
            });
        }

        protected abstract DisposableFieldAnalyzer GetAnalyzer(Compilation compilation);

        protected abstract class DisposableFieldAnalyzer
        {
            private readonly DisposeAnalysisHelper _disposeAnalysisHelper;

            protected DisposableFieldAnalyzer(Compilation compilation)
            {
                DisposeAnalysisHelper.TryGetOrCreate(compilation, out _disposeAnalysisHelper!);
                RoslynDebug.Assert(_disposeAnalysisHelper != null);
            }

            public void AnalyzeSymbol(SymbolAnalysisContext symbolContext)
            {
                INamedTypeSymbol namedType = (INamedTypeSymbol)symbolContext.Symbol;
                if (_disposeAnalysisHelper.IsDisposable(namedType))
                {
                    return;
                }

                IEnumerable<IFieldSymbol> disposableFields = from member in namedType.GetMembers()
                                                             where member.Kind == SymbolKind.Field && !member.IsStatic
                                                             let field = member as IFieldSymbol
                                                             where _disposeAnalysisHelper.IsDisposable(field.Type)
                                                             select field;
                if (!disposableFields.Any())
                {
                    return;
                }

                var disposableFieldsHashSet = new HashSet<ISymbol>(disposableFields);
                IEnumerable<TTypeDeclarationSyntax> classDecls = GetClassDeclarationNodes(namedType, symbolContext.CancellationToken);
                foreach (TTypeDeclarationSyntax classDecl in classDecls)
                {
                    SemanticModel model = symbolContext.Compilation.GetSemanticModel(classDecl.SyntaxTree);
                    List<string> disposableFieldNames = classDecl.DescendantNodes(n => n is not TTypeDeclarationSyntax || ReferenceEquals(n, classDecl))
                        .SelectMany(n => GetDisposableFieldCreations(n, model, disposableFieldsHashSet, symbolContext.CancellationToken))
                        .Where(field => !symbolContext.Options.IsConfiguredToSkipAnalysis(Rule, field.Type, namedType, symbolContext.Compilation))
                        .Select(field => field.Name)
                        .ToList();

                    if (disposableFieldNames.Count > 0)
                    {
                        disposableFieldNames.Sort();
                        // Type '{0}' owns disposable field(s) '{1}' but is not disposable
                        symbolContext.ReportDiagnostic(
                            namedType.CreateDiagnostic(Rule, namedType.Name, string.Join("', '", disposableFieldNames)));
                        return;
                    }
                }
            }

            private static IEnumerable<TTypeDeclarationSyntax> GetClassDeclarationNodes(INamedTypeSymbol namedType, CancellationToken cancellationToken)
            {
                foreach (SyntaxNode syntax in namedType.DeclaringSyntaxReferences.Select(s => s.GetSyntax(cancellationToken)))
                {
                    if (syntax != null)
                    {
                        TTypeDeclarationSyntax? classDecl = syntax.FirstAncestorOrSelf<TTypeDeclarationSyntax>(ascendOutOfTrivia: false);
                        if (classDecl != null)
                        {
                            yield return classDecl;
                        }
                    }
                }
            }

            protected abstract IEnumerable<IFieldSymbol> GetDisposableFieldCreations(SyntaxNode node, SemanticModel model,
                HashSet<ISymbol> disposableFields, CancellationToken cancellationToken);
        }
    }
}
