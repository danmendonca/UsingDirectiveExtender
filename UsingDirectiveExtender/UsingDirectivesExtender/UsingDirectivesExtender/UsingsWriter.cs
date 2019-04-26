using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UsingDirectivesExtender
{
    public class UsingsWriter : CSharpSyntaxRewriter
    {
        private readonly IList<UsingDirectiveSyntax> Usings;

        public UsingsWriter(IList<UsingDirectiveSyntax> usings)
        {
            this.Usings = usings?.ToList() ?? new List<UsingDirectiveSyntax>();
        }

        public override SyntaxNode VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            var ns = node.WithUsings(new SyntaxList<UsingDirectiveSyntax>(Usings));

            return ns;
        }
    }
}
