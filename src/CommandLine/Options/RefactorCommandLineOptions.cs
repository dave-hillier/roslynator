using System.Collections.Generic;
using CommandLine;

namespace Roslynator.CommandLine;

[Verb("refactor", HelpText = "Applies C# refactoring at specified location in a project or solution.")]
public class RefactorCommandLineOptions : MSBuildCommandLineOptions
{
    [Value(index:0, HelpText="Path to one or more project/solution files.", MetaName="<PROJECT|SOLUTION>")]
    public IEnumerable<string> Paths { get; set; }

    [Option(longName: OptionNames.RefactoringId, Required = true, HelpText="Identifier of the refactoring to apply (e.g. RR0011).", MetaValue = "<ID>")]
    public string RefactoringId { get; set; }

    [Option(longName: OptionNames.FilePath, Required = true, HelpText="Path to the document where refactoring should be applied.", MetaValue = "<FILE_PATH>")]
    public string FilePath { get; set; }

    [Option(longName: OptionNames.Position, Required = true, HelpText="Position defined as 'line:char' (1-based).", MetaValue = "<LINE:CHAR>")]
    public string Position { get; set; }
}
