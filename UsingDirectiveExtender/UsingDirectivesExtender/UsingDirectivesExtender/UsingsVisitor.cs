using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UsingDirectivesExtender
{
    public class UsingsVisitor : CSharpSyntaxWalker
    {
        private const string SystemUsing = "System";

        private bool isCandidate = false;

        public List<UsingDirectiveSyntax> SystemUsings { get; } = new List<UsingDirectiveSyntax>();

        public List<UsingDirectiveSyntax> GlobalOrExternalUsings { get; } = new List<UsingDirectiveSyntax>();

        public List<UsingDirectiveSyntax> Usings { get; } = new List<UsingDirectiveSyntax>();

        public List<UsingDirectiveSyntax> UsingsWithAlias { get; } = new List<UsingDirectiveSyntax>();

        public IEnumerable<UsingDirectiveSyntax> AllUsings
        {
            get
            {
                var allUsings = new List<UsingDirectiveSyntax>();
                allUsings.AddRange(this.SystemUsings);
                allUsings.AddRange(this.GlobalOrExternalUsings);
                allUsings.AddRange(this.Usings);
                allUsings.AddRange(this.UsingsWithAlias);

                return allUsings;
            }
        }

        public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            this.isCandidate = true;
            base.VisitNamespaceDeclaration(node);
            this.isCandidate = false;
        }

        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            if(!isCandidate)
            {
                base.VisitUsingDirective(node);
            }

            if (IsGlobalOrExternal(node))
            {
                this.GlobalOrExternalUsings.Add(node);
            }
            else if (node
                .ChildNodes()
                .OfType<NameEqualsSyntax>()
                .Any())
            {
                this.UsingsWithAlias.Add(node);
            }
            else if (this.IsSystemUsing(node))
            {
                this.SystemUsings.Add(node);
            }
            else
            {
                this.Usings.Add(node);
            }
        }

        private static bool IsGlobalOrExternal(UsingDirectiveSyntax node)
        {
            return node.DescendantNodes().OfType<AliasQualifiedNameSyntax>().Any()
                || node.DescendantNodes().OfType<ExternAliasDirectiveSyntax>().Any();
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
