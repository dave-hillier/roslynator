using CommandLine;

namespace Roslynator.CommandLine;

[Verb("refactor", HelpText = "Apply a code refactoring to a single file.")]
public class RefactorCommandLineOptions : BaseCommandLineOptions
{
    [Option(longName: OptionNames.File, Required = true, HelpText = "Path to C# file.")]
    public string File { get; set; }

    [Option(longName: OptionNames.Span, HelpText = "Text span in the form start:length.", MetaValue = "<SPAN>")]
    public string Span { get; set; }

    [Option(longName: OptionNames.RefactoringId, HelpText = "Refactoring id or title substring.")]
    public string RefactoringId { get; set; }
}
