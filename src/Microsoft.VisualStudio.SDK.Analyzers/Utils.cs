﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.VisualStudio.SDK.Analyzers
{
    /// <summary>
    /// Internal utilities for use by analyzers.
    /// </summary>
    internal static class Utils
    {
        /// <summary>
        /// Wraps an analyzer processor with a Debugger.Launch call in an exception filter for debug builds.
        /// </summary>
        /// <param name="handler">The handler to wrap.</param>
        /// <returns>The debug-ready handler.</returns>
        internal static Action<SyntaxNodeAnalysisContext> DebuggableWrapper(Action<SyntaxNodeAnalysisContext> handler)
        {
            return ctxt =>
            {
                try
                {
                    handler(ctxt);
                }
                catch (Exception ex) when (LaunchDebuggerExceptionFilter())
                {
                    throw new Exception($"Analyzer failure while processing syntax at {ctxt.Node.SyntaxTree.FilePath}({ctxt.Node.GetLocation()?.GetLineSpan().StartLinePosition.Line + 1},{ctxt.Node.GetLocation()?.GetLineSpan().StartLinePosition.Character + 1}): {ex.GetType()} {ex.Message}. Syntax: {ctxt.Node}", ex);
                }
            };
        }

        /// <summary>
        /// Wraps an analyzer processor with a Debugger.Launch call in an exception filter for debug builds.
        /// </summary>
        /// <param name="handler">The handler to wrap.</param>
        /// <returns>The debug-ready handler.</returns>
        internal static Action<OperationAnalysisContext> DebuggableWrapper(Action<OperationAnalysisContext> handler)
        {
            return ctxt =>
            {
                try
                {
                    handler(ctxt);
                }
                catch (Exception ex) when (LaunchDebuggerExceptionFilter())
                {
                    throw new Exception($"Analyzer failure while processing symbol {ctxt.Operation} at {ctxt.Operation.Syntax.GetLocation().SourceTree?.FilePath}({ctxt.Operation.Syntax.GetLocation().GetLineSpan().StartLinePosition.Line},{ctxt.Operation.Syntax.GetLocation().GetLineSpan().StartLinePosition.Character}): {ex.GetType()} {ex.Message}", ex);
                }
            };
        }

        /// <summary>
        /// Wraps an analyzer processor with a Debugger.Launch call in an exception filter for debug builds.
        /// </summary>
        /// <param name="handler">The handler to wrap.</param>
        /// <returns>The debug-ready handler.</returns>
        internal static Action<SymbolAnalysisContext> DebuggableWrapper(Action<SymbolAnalysisContext> handler)
        {
            return ctxt =>
            {
                try
                {
                    handler(ctxt);
                }
                catch (Exception ex) when (LaunchDebuggerExceptionFilter())
                {
                    throw new Exception($"Analyzer failure while processing symbol {ctxt.Symbol} at {ctxt.Symbol.Locations.FirstOrDefault()?.SourceTree?.FilePath}({ctxt.Symbol.Locations.FirstOrDefault()?.GetLineSpan().StartLinePosition.Line},{ctxt.Symbol.Locations.FirstOrDefault()?.GetLineSpan().StartLinePosition.Character}): {ex.GetType()} {ex.Message}", ex);
                }
            };
        }

        /// <summary>
        /// Gets the help link to use for each analyzer.
        /// </summary>
        /// <param name="analyzerId">The <see cref="DiagnosticDescriptor.Id"/> for the diagnostic to get help on.</param>
        /// <returns>The absolute URL with documentation on the specified analyzer.</returns>
        internal static string GetHelpLink(string analyzerId)
        {
            return $"https://github.com/Microsoft/VSSDK-Analyzers/blob/main/doc/{analyzerId}.md";
        }

        /// <summary>
        /// Checks whether a symbol is a <see cref="Task"/> or <see cref="Task{T}"/>.
        /// </summary>
        /// <param name="typeSymbol">The symbol to test.</param>
        /// <returns><see langword="true"/> if the symbol is a <see cref="Task"/>.</returns>
        internal static bool IsTask(ITypeSymbol typeSymbol) => typeSymbol?.Name == nameof(Task) && typeSymbol.BelongsToNamespace(Namespaces.SystemThreadingTasks);

        /// <summary>
        /// Tests whether a symbol belongs to a given namespace.
        /// </summary>
        /// <param name="symbol">The symbol whose namespace membership is being tested.</param>
        /// <param name="namespaces">A sequence of namespaces from global to most precise. For example: [System, Threading, Tasks].</param>
        /// <returns><see langword="true"/> if the symbol belongs to the given namespace; otherwise <see langword="false"/>.</returns>
        internal static bool BelongsToNamespace(this ISymbol symbol, IReadOnlyList<string> namespaces)
        {
            if (namespaces == null)
            {
                throw new ArgumentNullException(nameof(namespaces));
            }

            if (symbol == null)
            {
                return false;
            }

            INamespaceSymbol currentNamespace = symbol.ContainingNamespace;
            for (int i = namespaces.Count - 1; i >= 0; i--)
            {
                if (currentNamespace?.Name != namespaces[i])
                {
                    return false;
                }

                currentNamespace = currentNamespace.ContainingNamespace;
            }

            return currentNamespace?.IsGlobalNamespace ?? false;
        }

        /// <summary>
        /// Gets a value indicating whether one type is equal to or derives from another type.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <param name="baseType">The type to compare with, that may be a base type of <paramref name="type"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="baseType"/> is a base type or equal to <paramref name="type"/>.</returns>
        internal static bool IsEqualToOrDerivedFrom(ITypeSymbol? type, ITypeSymbol baseType)
        {
            return SymbolEqualityComparer.Default.Equals(type?.OriginalDefinition, baseType) || IsDerivedFrom(type, baseType);
        }

        /// <summary>
        /// Gets a value indicating whether one type derives from another type.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <param name="baseType">The type to compare with, that may be a base type of <paramref name="type"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="baseType"/> is a base type of <paramref name="type"/>.</returns>
        internal static bool IsDerivedFrom(ITypeSymbol? type, ITypeSymbol baseType)
        {
            type = type?.BaseType;
            while (type != null)
            {
                if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, baseType))
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        /// <summary>
        /// Gets whether any of the base types of <paramref name="type"/> is decorated with attribute
        /// equal to or derived from <paramref name="attributeBaseType"/>.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <param name="attributeBaseType">The type to compare attributes to.</param>
        /// <returns><see langword="true"/> if <paramref name="type"/> is decorated with attribute equal to or derived from <paramref name="attributeBaseType"/>.</returns>
        internal static bool HasInheritedAttribute(INamedTypeSymbol? type, INamedTypeSymbol attributeBaseType)
        {
            while (type != null)
            {
                if (type.GetAttributes().Any(attr => Utils.IsEqualToOrDerivedFrom(attr.AttributeClass, attributeBaseType)))
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        /// <summary>
        /// Produces the syntax necessary to qualify a simple name.
        /// </summary>
        /// <param name="qualifiers">The qualifiers (e.g. the namespace that qualifies a type).</param>
        /// <param name="simpleName">The simple type name.</param>
        /// <returns>The qualified type name.</returns>
        internal static NameSyntax QualifyName(IReadOnlyList<string> qualifiers, SimpleNameSyntax simpleName)
        {
            if (qualifiers == null)
            {
                throw new ArgumentNullException(nameof(qualifiers));
            }

            if (simpleName == null)
            {
                throw new ArgumentNullException(nameof(simpleName));
            }

            if (qualifiers.Count == 0)
            {
                throw new ArgumentException("At least one qualifier required.", nameof(qualifiers));
            }

            NameSyntax result = SyntaxFactory.IdentifierName(qualifiers[0]);
            for (int i = 1; i < qualifiers.Count; i++)
            {
                IdentifierNameSyntax rightSide = SyntaxFactory.IdentifierName(qualifiers[i]);
                result = SyntaxFactory.QualifiedName(result, rightSide);
            }

            return SyntaxFactory.QualifiedName(result, simpleName);
        }

        /// <summary>
        /// Checks whether a given syntax node has an ancestor that matches certain requirements.
        /// </summary>
        /// <typeparam name="T">The type of ancestor of interest.</typeparam>
        /// <param name="syntaxNode">The starting syntax node.</param>
        /// <param name="continueAscending">A function to determine whether to keep ascending the syntax tree.</param>
        /// <param name="isMatch">A function to determine whether a given ancestor is the target we're looking for.</param>
        /// <returns><see langword="true"/> if the target ancestor was found.</returns>
        internal static T? FindAncestor<T>(SyntaxNode syntaxNode, Func<SyntaxNode, bool> continueAscending, Func<T, SyntaxNode, bool> isMatch)
            where T : SyntaxNode
        {
            if (continueAscending == null)
            {
                throw new ArgumentNullException(nameof(continueAscending));
            }

            if (isMatch == null)
            {
                throw new ArgumentNullException(nameof(isMatch));
            }

            if (syntaxNode == null)
            {
                return default;
            }

            SyntaxNode? current = syntaxNode.Parent;
            SyntaxNode child = syntaxNode;
            while (current != null)
            {
                if (current is T t && isMatch(t, child))
                {
                    return t;
                }

                if (!continueAscending(current))
                {
                    return default;
                }

                child = current;
                current = current.Parent;
            }

            return default;
        }

        /// <summary>
        /// Finds the first ancestor syntax node that is one of a given set of types.
        /// </summary>
        /// <param name="syntaxNode">The syntax node to start the search at.</param>
        /// <param name="allowedTypes">The set of types that we should stop searching and return when we encounter it.</param>
        /// <returns>The matching syntax node, if any.</returns>
        internal static SyntaxNode? FindFirstAncestorOfTypes(SyntaxNode syntaxNode, params Type[] allowedTypes)
        {
            return FindAncestor<SyntaxNode>(syntaxNode, n => !allowedTypes.Contains(n.GetType()), (n, c) => allowedTypes.Contains(n.GetType()));
        }

        /// <summary>
        /// Adds "using Task = System.Threading.Tasks.Task;" to the document if it is not already present.
        /// </summary>
        /// <param name="syntaxNode">Any syntax node within the document.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The original <paramref name="syntaxNode"/> given, as it is represented in the updated syntax tree.</returns>
        internal static async Task<SyntaxNode> AddUsingTaskEqualsDirectiveAsync(SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            IEnumerable<UsingDirectiveSyntax> existingUsings = syntaxNode.AncestorsAndSelf().OfType<UsingDirectiveSyntax>().Concat(
                syntaxNode.DescendantNodes(n => n is CompilationUnitSyntax || n is NamespaceDeclarationSyntax).OfType<UsingDirectiveSyntax>());
            if (existingUsings.Any(u => u.Alias?.Name?.Identifier.ValueText == nameof(Task)))
            {
                // The user has already aliased Task.
                return syntaxNode;
            }

            var trackAnnotation = new SyntaxAnnotation();
            syntaxNode = syntaxNode.WithAdditionalAnnotations(trackAnnotation);

            UsingDirectiveSyntax usingTaskDirective = SyntaxFactory.UsingDirective(
                QualifyName(Namespaces.SystemThreadingTasks, SyntaxFactory.IdentifierName(nameof(Task))))
                .WithAlias(SyntaxFactory.NameEquals(nameof(Task)));

            var syntaxRoot = (CompilationUnitSyntax)await syntaxNode.SyntaxTree.GetRootAsync(cancellationToken);
            syntaxRoot = syntaxRoot.AddUsings(usingTaskDirective);

            return syntaxRoot.GetAnnotatedNodes(trackAnnotation).Single();
        }

        /// <summary>
        /// Whether the array contains element matching the <paramref name="symbol"/>.
        /// </summary>
        /// <param name="methods">Array of elements that specify members.</param>
        /// <param name="symbol">Symbol under test.</param>
        /// <returns><see langword="true" /> if the <paramref name="methods"/> contains element matching <paramref name="symbol"/>.</returns>
        internal static bool Contains(this ImmutableArray<QualifiedMember> methods, ISymbol symbol)
        {
            foreach (QualifiedMember method in methods)
            {
                if (method.IsMatch(symbol))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether the array contains element matching the <paramref name="typeSymbol"/> and <paramref name="memberSymbol"/>.
        /// </summary>
        /// <param name="types">Array of elements that specify types.</param>
        /// <param name="typeSymbol">Type symbol under test.</param>
        /// <param name="memberSymbol">Member symbol under test.</param>
        /// <returns><see langword="true" /> if the <paramref name="types"/> contains element matching <paramref name="typeSymbol"/> and <paramref name="memberSymbol"/>.</returns>
        internal static bool Contains(this ImmutableArray<TypeMatchSpec> types, ITypeSymbol? typeSymbol, ISymbol? memberSymbol)
        {
            TypeMatchSpec matching = default(TypeMatchSpec);
            foreach (TypeMatchSpec type in types)
            {
                if (type.IsMatch(typeSymbol, memberSymbol))
                {
                    if (matching.IsEmpty || matching.IsWildcard)
                    {
                        matching = type;
                        if (!matching.IsWildcard)
                        {
                            // It's an exact match, so return it immediately.
                            return !matching.InvertedLogic;
                        }
                    }
                }
            }

            return !matching.IsEmpty && !matching.InvertedLogic;
        }

        /// <summary>
        /// Returns whether <paramref name="node"/> is a child of a lambda or delegate.
        /// </summary>
        /// <param name="node">Syntax node to check.</param>
        /// <returns><see langword="true" /> if the <paramref name="node"/> is a child node of lambda or delegate syntax.</returns>
        internal static bool IsChildOfDelegateOrLambda(SyntaxNode? node)
        {
            while (node != null)
            {
                if (node is AnonymousFunctionExpressionSyntax)
                {
                    return true;
                }

                node = node.Parent;
            }

            return false;
        }

        private static bool LaunchDebuggerExceptionFilter()
        {
#if DEBUG
            System.Diagnostics.Debugger.Launch();
#endif
            return true;
        }
    }
}
