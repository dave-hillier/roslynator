using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Roslynator.CSharp.Refactorings;
using static Roslynator.Logger;

namespace Roslynator.CommandLine;

internal class RefactorCommand
{
    public RefactorCommand(RefactorCommandLineOptions options)
    {
        Options = options;
    }

    public RefactorCommandLineOptions Options { get; }

    public async Task<CommandStatus> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(Options.File))
        {
            WriteLine($"File not found: '{Options.File}'", ConsoleColors.Yellow, Verbosity.Quiet);
            return CommandStatus.Fail;
        }

        string text = await File.ReadAllTextAsync(Options.File, cancellationToken);
        (int start, int length) = ParseSpan(Options.Span, text.Length);
        var workspace = new AdhocWorkspace();
        var references = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES").ToString().Split(Path.PathSeparator).Select(f => MetadataReference.CreateFromFile(f));
        Project project = workspace.CurrentSolution.AddProject("Refactor", "Refactor", LanguageNames.CSharp)
            .WithMetadataReferences(references)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Document document = project.AddDocument(Path.GetFileName(Options.File), SourceText.From(text), filePath: Options.File);

        var provider = new RoslynatorCodeRefactoringProvider();
        CodeAction action = null;
        var span = new TextSpan(start, length);
        var context = new CodeRefactoringContext(document, span, a =>
        {
            if (action == null)
            {
                if (Options.RefactoringId == null || a.EquivalenceKey == Options.RefactoringId || a.Title.IndexOf(Options.RefactoringId, StringComparison.OrdinalIgnoreCase) >= 0)
                    action = a;
            }
        }, cancellationToken);
        await provider.ComputeRefactoringsAsync(context);

        if (action == null)
        {
            WriteLine("No matching refactoring found.", Verbosity.Minimal);
            return CommandStatus.NotSuccess;
        }

        var operations = await action.GetOperationsAsync(cancellationToken);
        var apply = operations.OfType<ApplyChangesOperation>().FirstOrDefault();
        if (apply is null)
            return CommandStatus.NotSuccess;

        Document newDocument = apply.ChangedSolution.GetDocument(document.Id);
        SourceText newText = await newDocument.GetTextAsync(cancellationToken);
        await File.WriteAllTextAsync(Options.File, newText.ToString(), cancellationToken);
        return CommandStatus.Success;
    }

    private static (int start, int length) ParseSpan(string s, int maxLength)
    {
        if (!string.IsNullOrEmpty(s))
        {
            var parts = s.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int start) && int.TryParse(parts[1], out int length))
            {
                start = Math.Max(0, Math.Min(start, maxLength));
                length = Math.Max(0, Math.Min(length, maxLength - start));
                return (start, length);
            }
        }
        return (0, 0);
    }
}
