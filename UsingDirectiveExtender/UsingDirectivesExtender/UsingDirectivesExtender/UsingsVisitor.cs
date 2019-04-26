using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UsingDirectivesExtender
{
    public class UsingsVisitor : CSharpSyntaxWalker
    {
        private const string SystemUsing = "System";

        private readonly List<UsingDirectiveSyntax> systemUsings = new List<UsingDirectiveSyntax>();

        public List<UsingDirectiveSyntax> SystemUsings
        {
            get
            {
                return this.systemUsings.OrderBy(u => u.ToString()).ToList();
            }
        }

        public List<UsingDirectiveSyntax> Usings { get; } = new List<UsingDirectiveSyntax>();

        public List<UsingDirectiveSyntax> UsingsWithAlias { get; } = new List<UsingDirectiveSyntax>();

        public IEnumerable<UsingDirectiveSyntax> AllUsings
        {
            get
            {
                var allUsings = new List<UsingDirectiveSyntax>();
                allUsings.AddRange(this.systemUsings);
                allUsings.AddRange(this.Usings);
                allUsings.AddRange(this.UsingsWithAlias);

                return allUsings;
            }
        }

        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            if (node
                .ChildNodes()
                .OfType<NameEqualsSyntax>()
                .Any())
            {
                this.UsingsWithAlias.Add(node);
            }
            else if (this.IsSystemUsing(node))
            {
                this.systemUsings.Add(node);
            }
            else
            {
                this.Usings.Add(node);
            }
        }

        private bool IsSystemUsing(UsingDirectiveSyntax node)
        {
            var singleIdentifier = node.ChildNodes().OfType<IdentifierNameSyntax>().FirstOrDefault();
            if (singleIdentifier != null
                && string.Equals(UsingsVisitor.SystemUsing, singleIdentifier.ToString()))
            {
                return true;
            }

            var qualifier = node.ChildNodes().OfType<QualifiedNameSyntax>().FirstOrDefault();
            var duplicate = qualifier;
            if (qualifier == null)
            {
                return false;
            }

            while (qualifier != null)
            {
                duplicate = qualifier.ChildNodes().OfType<QualifiedNameSyntax>().FirstOrDefault();
                if (duplicate == null)
                {
                    return string.Equals(UsingsVisitor.SystemUsing, qualifier.Left.ToString());
                }
                qualifier = duplicate;
            }

            return false;
        }
    }
}
