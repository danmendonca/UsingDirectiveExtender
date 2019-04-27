using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UsingDirectivesExtender
{
    public class UsingRemoverRewriter : CSharpSyntaxRewriter
    {
        bool isToRemove = false;

        public override SyntaxNode VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            this.isToRemove = true;
            var syntaxNode = base.VisitNamespaceDeclaration(node);
            this.isToRemove = false;

            return syntaxNode;
        }

        public override SyntaxNode VisitUsingDirective(UsingDirectiveSyntax node)
        {
            if (isToRemove)
            {
                return null;
            }

            return base.VisitUsingDirective(node);
        }
    }
}
