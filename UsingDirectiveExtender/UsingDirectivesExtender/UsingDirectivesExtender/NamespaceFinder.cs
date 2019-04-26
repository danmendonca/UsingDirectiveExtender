using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UsingDirectivesExtender
{
    public class NamespaceFinder : CSharpSyntaxWalker
    {
        public SyntaxNode Namespace { get; private set; }

        public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            this.Namespace = node;
        }
    }
}
