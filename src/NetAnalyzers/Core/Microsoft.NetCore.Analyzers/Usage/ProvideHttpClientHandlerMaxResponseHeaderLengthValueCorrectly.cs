// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Usage
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2262: <inheritdoc cref="ProvideHttpClientHandlerMaxResponseHeaderLengthValueCorrectly"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class ProvideHttpClientHandlerMaxResponseHeaderLengthValueCorrectly : DiagnosticAnalyzer
    {
        private const string PropertyTypeName = "System.Net.Http.HttpClientHandler.MaxResponseHeadersLength";
        private const int MaximumAlertLimit = 128;
        internal const string RuleId = "CA2262";

        internal static readonly DiagnosticDescriptor EnsureMaxResponseHeaderLengthRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(ProvideHttpClientHandlerMaxResponseHeaderLengthValueCorrectlyTitle)),
            CreateLocalizableResourceString(nameof(ProvideHttpClientHandlerMaxResponseHeaderLengthValueCorrectlyMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(ProvideHttpClientHandlerMaxResponseHeaderLengthValueCorrectlyDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(EnsureMaxResponseHeaderLengthRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                context.RegisterOperationAction(context =>
                {
                    var propertyAssignment = (ISimpleAssignmentOperation)context.Operation;
                    if (!IsHttpClientMaxResponseHeadersLengthAssignment(propertyAssignment))
                    {
                        return;
                    }

                    if (propertyAssignment.Value is null || !propertyAssignment.Value.ConstantValue.HasValue || propertyAssignment.Value.ConstantValue.Value is not int)
                    {
                        return;
                    }

                    int propertyValue = Convert.ToInt32(propertyAssignment.Value.ConstantValue.Value, System.Globalization.CultureInfo.CurrentCulture);

                    if (propertyValue > MaximumAlertLimit)
                    {
                        context.ReportDiagnostic(context.Operation.CreateDiagnostic(EnsureMaxResponseHeaderLengthRule, propertyValue));
                    }
                }, OperationKind.SimpleAssignment);
            });
        }

        private static bool IsHttpClientMaxResponseHeadersLengthAssignment(ISimpleAssignmentOperation operation)
        {
            if (operation.Target is not IPropertyReferenceOperation propertyReferenceOperation)
            {
                return false;
            }

            if (propertyReferenceOperation?.Member?.ToString()?.Equals(PropertyTypeName, System.StringComparison.Ordinal) is not true)
            {
                return false;
            }

            return operation.Value is IFieldReferenceOperation or ILiteralOperation;
        }
    }
}