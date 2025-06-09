using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using Roslynator.CSharp.Refactorings;
using Roslynator;
using static Roslynator.Logger;

namespace Roslynator.CommandLine;

internal class RefactorCommand : MSBuildWorkspaceCommand<RefactorCommandResult>
{
    public RefactorCommand(
        RefactorCommandLineOptions options,
        in ProjectFilter projectFilter,
        FileSystemFilter fileSystemFilter) : base(projectFilter, fileSystemFilter)
    {
        Options = options;
    }

    public RefactorCommandLineOptions Options { get; }

    public override async Task<RefactorCommandResult> ExecuteAsync(ProjectOrSolution projectOrSolution, CancellationToken cancellationToken = default)
    {
        Solution solution = projectOrSolution.AsSolution();
        string solutionDir = Path.GetDirectoryName(solution.FilePath);
        string filePath = Options.FilePath;
        if (!Path.IsPathRooted(filePath))
            filePath = Path.GetFullPath(Path.Combine(solutionDir, filePath));

        DocumentId documentId = solution.GetDocumentIdsWithFilePath(filePath).FirstOrDefault();
        if (documentId == null)
        {
            WriteLine($"Document '{filePath}' not found.", ConsoleColors.Yellow, Verbosity.Quiet);
            return new RefactorCommandResult(CommandStatus.Fail);
        }

        Document document = solution.GetDocument(documentId);
        if (document == null)
        {
            WriteLine($"Unable to get document '{filePath}'.", ConsoleColors.Yellow, Verbosity.Quiet);
            return new RefactorCommandResult(CommandStatus.Fail);
        }

        TextSpan span = ParsePosition(await document.GetTextAsync(cancellationToken));
        var provider = new RoslynatorCodeRefactoringProvider();
        var actions = new List<CodeAction>();
        var context = new CodeRefactoringContext(document, span, a => actions.Add(a), cancellationToken);
        await provider.ComputeRefactoringsAsync(context);

        RefactoringDescriptor descriptor = GetDescriptor(Options.RefactoringId);
        string eqKey = "Roslynator." + descriptor.Id;
        CodeAction action = actions.FirstOrDefault(a => string.Equals(a.EquivalenceKey, eqKey, StringComparison.Ordinal));
        if (action == null)
        {
            WriteLine("Refactoring not found at the specified location.", ConsoleColors.Yellow, Verbosity.Quiet);
            return new RefactorCommandResult(CommandStatus.Fail);
        }

        WriteLine($"Applying '{action.Title}'", ConsoleColors.Cyan, Verbosity.Normal);
        ImmutableArray<CodeActionOperation> operations = await action.GetOperationsAsync(cancellationToken);
        ApplyChangesOperation applyOperation = operations.OfType<ApplyChangesOperation>().First();
        if (!solution.Workspace.TryApplyChanges(applyOperation.ChangedSolution))
        {
            WriteLine("Failed to apply changes to workspace.", ConsoleColors.Yellow, Verbosity.Quiet);
            return new RefactorCommandResult(CommandStatus.Fail);
        }

        return new RefactorCommandResult(CommandStatus.Success);

        TextSpan ParsePosition(SourceText text)
        {
            string[] parts = Options.Position.Split(':');
            int line = int.Parse(parts[0]) - 1;
            int character = int.Parse(parts[1]) - 1;
            int start = text.Lines[line].Start + character;
            return new TextSpan(start, 0);
        }

        static RefactoringDescriptor GetDescriptor(string id)
        {
            foreach (FieldInfo field in typeof(RefactoringDescriptors).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.GetValue(null) is RefactoringDescriptor d && d.Id == id)
                    return d;
            }
            throw new InvalidOperationException($"Refactoring descriptor '{id}' not found.");
        }
    }
}
