using AfterDarker.Tutorials;
using AfterDarker.Tutorials.Lessons;

// Register each new lesson here. Keep selection separate from the lesson itself.
ITutorial[] tutorials =
[
    new Tutorial01Addition(),
    new Tutorial02StackCall(),
    new Tutorial03FarCall(),
    new Tutorial04HostGateway(),
    new Tutorial05NeInspection(args is ["05", var path] ? path : null),
    new Tutorial06LoadLibrary(
        args is ["06", var fixture] && fixture != "--trace" ? fixture :
        args is ["06", var tracedFixture, "--trace"] ? tracedFixture : null,
        args.Contains("--trace")),
    new Tutorial07Relocations(args.Length >= 2 && args[0] == "07" && args[1] != "--step" ? args[1] : null,
        args.Contains("--step")),
    new Tutorial08MondrianInitialize(args.Length >= 2 && args[0] == "08" && args[1] != "--trace" ? args[1] : null,
        args.Contains("--trace")),
    new Tutorial09MondrianFrames(args.Length >= 2 && args[0] == "09" ? args[1] : null,
        args.Length == 3 && args[0] == "09" ? args[2] : null),
    new Tutorial10LocalHeap(),
    new Tutorial11BitmapResources(args.Length >= 2 && args[0] == "11" ? args[1] : null,
        args.Length == 3 && args[0] == "11" ? args[2] : null),
    new Tutorial12Puzzle(args.Length >= 2 && args[0] == "12" ? args[1] : null,
        args.Length == 3 && args[0] == "12" ? args[2] : null),
];

if (args is ["--list"])
{
    foreach (ITutorial tutorial in tutorials)
        Console.WriteLine($"{tutorial.Id}: {tutorial.Title}");
    return 0;
}

string selectedId = args.Length == 0 ? tutorials[0].Id : args[0];
ITutorial? selected = tutorials.SingleOrDefault(tutorial => tutorial.Id == selectedId);
bool validArguments = selectedId switch
{
    "05" => args.Length <= 2,
    "06" => args is ["06"] or ["06", _] or ["06", _, "--trace"],
    "07" => args is ["07"] or ["07", _] or ["07", _, "--step"],
    "09" => args.Length <= 3,
    "11" or "12" => args.Length <= 3,
    "08" => args is ["08"] or ["08", _] or ["08", _, "--trace"],
    _ => args.Length <= 1,
};
if (selected is null || !validArguments)
{
    Console.Error.WriteLine("Usage: AfterDarker.Tutorials [01 | 02 | 03 | 04 | 05 [file.ad] | 06 [hello42.dll] [--trace] | 07 [file.ad | --file] [--step] | 08 [Mondrian.ad] [--trace] | 09 [Mondrian.ad] [frame-count] | 10 | 11 [file.ad] [output-directory] | 12 [Puzzle.ad] [output-directory] | --list]");
    return 1;
}

try
{
    Console.WriteLine($"Tutorial {selected.Id}: {selected.Title}");
    selected.Run();
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Tutorial failed: {exception.Message}");
    return 1;
}
