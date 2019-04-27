using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace UsingDirectivesExtender
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(UsingDirectivesExtenderCodeRefactoringProvider)), Shared]
    internal class UsingDirectivesExtenderCodeRefactoringProvider : CodeRefactoringProvider
    {
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null)
            {
                return;
            }

            var node = root.FindNode(context.Span);
            var usingDirective = node.AncestorsAndSelf().OfType<UsingDirectiveSyntax>().FirstOrDefault();

            // Only offer a refactoring if the selected node is a using directive node.
            if (usingDirective == null)
            {
                return;
            }

            var semanticModel = await this.GetCachedSemanticModelOrComputeAsync(context.Document).ConfigureAwait(false);
            if (semanticModel != null)
            {
                var usingDirectiveSymbol = this.GetNamespaceSymbol(usingDirective, semanticModel);

                var usingDirectiveName =
                    usingDirective.ChildNodes().OfType<IdentifierNameSyntax>().FirstOrDefault()?.ToString()
                    ?? usingDirective.ChildNodes().OfType<QualifiedNameSyntax>().FirstOrDefault()?.ToString();

                if (usingDirectiveName != null
                    && usingDirectiveSymbol != null
                    && !string.Equals(usingDirectiveName, usingDirectiveSymbol.ToString()))
                {
                    this.RegisterSingularRefactorAction(context, usingDirective, semanticModel);
                    this.RegisterRefactorForDocument(context, root);
                }
            }

            //this.RegisterRefactorForProject(context);
            this.RegisterRefactorForSolution(context);
        }        

        private void RegisterRefactorForSolution(CodeRefactoringContext context)
        {
            var action = CodeAction.Create(
                "Convert solution using directives to FullName",
                c => this.GetSolutionWithFullNameUsingsAsync(context.Document.Project.Solution, c));

            context.RegisterRefactoring(action);
        }
        
        private void RegisterRefactorForProject(CodeRefactoringContext context)
        {
            var action = CodeAction.Create("Convert Project usings to FullName",
                c => this.GetProjectWithFullNameUsingsAsync(context.Document.Project, c));

            // Register this code action.
            context.RegisterRefactoring(action);
        }

        private void RegisterRefactorForDocument(CodeRefactoringContext context, SyntaxNode root)
        {
            var action = CodeAction.Create(
            "Convert document using directives to full name",
            c => this.GetDocumentUsingToFullName(context.Document, c));

            // Register this code action.
            context.RegisterRefactoring(action);
        }

        private void RegisterSingularRefactorAction(
            CodeRefactoringContext context,
            UsingDirectiveSyntax usingDirective,
            SemanticModel semanticModel)
        {
            var usingDirectiveSymbol = this.GetNamespaceSymbol(usingDirective, semanticModel);
            var action = CodeAction.Create(
                "Convert using directive to full name",
                c => this.GetDocumentWithFullNameUsing(
                        context.Document,
                        usingDirective,
                        usingDirectiveSymbol.ToString(),
                        c));

            // Register this code action.
            context.RegisterRefactoring(action);
        }

        private async Task<Solution> GetSolutionWithFullNameUsingsAsync(Solution solution, CancellationToken c)
        {
            var newSolution = solution;
            var projectsIds = solution.ProjectIds;
            foreach (var projectId in projectsIds)
            {
                var project = newSolution.Projects.FirstOrDefault(p => p.Id == projectId);
                if(project != default(Project))
                {
                    newSolution = await this.GetProjectWithFullNameUsingsAsync(project, c).ConfigureAwait(false);
                }
            }

            return newSolution;
        }

        private async Task<Solution> GetProjectWithFullNameUsingsAsync(Project project, CancellationToken c)
        {
            if (!project.Documents.Any())
            {
                return project.Solution;
            }

            var originalDocuments = project.Documents.ToList();
            Project editedProject = null;

            foreach (var original in originalDocuments)
            {
                var document = editedProject != null
                    ? editedProject.GetDocument(original.Id)
                    : project.GetDocument(original.Id);

                var editedDocument = await this.GetDocumentUsingToFullName(document, c).ConfigureAwait(false);
                editedProject = editedDocument.Project;
            }

            return editedProject.Solution;
        }

        private async Task<Document> GetDocumentUsingToFullName(Document document, CancellationToken c)
        {
            var rootTask = document.GetSyntaxRootAsync(c);
            var semanticModelTask = document.GetSemanticModelAsync(c);

            await Task.WhenAll(rootTask, semanticModelTask).ConfigureAwait(false);

            var root = rootTask.Result;
            var semanticModel = semanticModelTask.Result;

            if (root == null || semanticModel == null)
            {
                return document;
            }

            return await this.GetDocumentWithFullNameUsings(document, semanticModel, c).ConfigureAwait(false);
        }

        private async Task<Document> GetDocumentWithFullNameUsing(
            Document document,
            UsingDirectiveSyntax oldUsingDirective,
            string fullName,
            CancellationToken cancellationToken)
        {
            var newUsingDirective = GetNewUsingDirectiveSyntax(oldUsingDirective, fullName);

            var editor = await DocumentEditor.CreateAsync(document, cancellationToken);
            editor.ReplaceNode(oldUsingDirective, newUsingDirective);

            return editor.GetChangedDocument();
        }

        private async Task<Document> GetDocumentWithFullNameUsings(
            Document document,
            SemanticModel semanticModel,
            CancellationToken c)
        {
            if (semanticModel == null || document == null)
            {
                return document;
            }

            var root = await document.GetSyntaxRootAsync().ConfigureAwait(false);
            var usingVisitor = new UsingsVisitor();
            usingVisitor.Visit(root);

            var newUsings = this.GetNewUsings(usingVisitor.Usings, semanticModel);
            var aliasUsings = this.GetNewUsings(usingVisitor.UsingsWithAlias, semanticModel);
            var newSystemusings = this.GetNewUsings(usingVisitor.SystemUsings, semanticModel);

            var usingsToAdd = new List<UsingDirectiveSyntax>();
            usingsToAdd.AddRange(newSystemusings);
            usingsToAdd.AddRange(newUsings);
            usingsToAdd.AddRange(aliasUsings);

            var usingsRemover = new UsingRemoverRewriter();
            root = usingsRemover.Visit(root);

            var usingsWriter = new UsingsWriter(usingsToAdd);
            root = usingsWriter.Visit(root);

            return document.WithSyntaxRoot(root);
        }

        private IEnumerable<UsingDirectiveSyntax> GetNewUsings(List<UsingDirectiveSyntax> usings, SemanticModel semanticModel)
        {
            var renamedUsings = new List<UsingDirectiveSyntax>();

            foreach (var directive in usings)
            {
                var symbol = this.GetNamespaceSymbol(directive, semanticModel);
                var newUsing = GetNewUsingDirectiveSyntax(
                    directive,
                    symbol.ToString());

                renamedUsings.Add(newUsing);
            }

            return renamedUsings.OrderBy(u => u.ToString().Trim(';'));
        }

        private async Task<Document> GetDocumentWithFullNameUsings(
            Document document,
            IEnumerable<UsingDirectiveSyntax> usingDirectives,
            SemanticModel semanticModel,
            CancellationToken c)
        {
            if (semanticModel == null || document == null)
            {
                return document;
            }

            var editor = await DocumentEditor.CreateAsync(document, c).ConfigureAwait(false);
            var changed = false;

            foreach (var usingDirective in usingDirectives)
            {
                if (usingDirective == null)
                {
                    continue;
                }

                var symbol = this.GetNamespaceSymbol(usingDirective, semanticModel);
                if (symbol != null
                    && !string.Equals(usingDirective.ToString(), symbol.ToString()))
                {
                    var newUsing = GetNewUsingDirectiveSyntax(usingDirective, symbol.ToString());
                    editor.ReplaceNode(usingDirective, newUsing);
                    changed = true;
                }
            }

            return changed
                ? editor.GetChangedDocument()
                : document;
        }

        private static UsingDirectiveSyntax GetNewUsingDirectiveSyntax(
            UsingDirectiveSyntax oldUsingDirective,
            string newName)
        {
            var isGlobalOrExternal = oldUsingDirective.DescendantNodes().OfType<AliasQualifiedNameSyntax>().Any()
                || oldUsingDirective.DescendantNodes().OfType<ExternAliasDirectiveSyntax>().Any();

            if (newName == null || isGlobalOrExternal)
            {
                return oldUsingDirective;
            }

            var newUsingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(newName))
                .WithUsingKeyword(oldUsingDirective.UsingKeyword)
                .WithSemicolonToken(oldUsingDirective.SemicolonToken);

            var nameEquals = oldUsingDirective
                .ChildNodes()
                .OfType<NameEqualsSyntax>()
                .FirstOrDefault();

            if (nameEquals != null)
            {
                newUsingDirective = newUsingDirective.WithAlias(nameEquals);
            }

            return newUsingDirective;
        }

        private async Task<SemanticModel> GetCachedSemanticModelOrComputeAsync(Document document)
        {
            document.TryGetSemanticModel(out var semanticModel);
            var refactorCts = new CancellationTokenSource();

            return semanticModel ?? await document.GetSemanticModelAsync(refactorCts.Token).ConfigureAwait(false);
        }

        private ISymbol GetNamespaceSymbol(UsingDirectiveSyntax usingDirective, SemanticModel semanticModel)
        {
            // Scenario 1 Single IdentifierName
            IdentifierNameSyntax identifierName = usingDirective
                .ChildNodes()
                .OfType<IdentifierNameSyntax>()
                .FirstOrDefault();

            if (identifierName != null)
            {
                return semanticModel.GetSymbolInfo(identifierName).Symbol;
            }

            SyntaxNode qName = usingDirective.ChildNodes().OfType<QualifiedNameSyntax>().FirstOrDefault();
            qName = qName ?? usingDirective.ChildNodes().OfType<AliasQualifiedNameSyntax>().First();

            return semanticModel.GetSymbolInfo(qName).Symbol;
        }
    }
}
