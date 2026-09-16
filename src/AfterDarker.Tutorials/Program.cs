using AfterDarker.Tutorials;
using AfterDarker.Tutorials.Lessons;

// Register each new lesson here. Keep selection separate from the lesson itself.
ITutorial[] tutorials = [new Tutorial01Addition(), new Tutorial02StackCall(), new Tutorial03FarCall()];

if (args is ["--list"])
{
    foreach (ITutorial tutorial in tutorials)
        Console.WriteLine($"{tutorial.Id}: {tutorial.Title}");
    return 0;
}

string selectedId = args.Length == 0 ? tutorials[0].Id : args[0];
ITutorial? selected = tutorials.SingleOrDefault(tutorial => tutorial.Id == selectedId);
if (args.Length > 1 || selected is null)
{
    Console.Error.WriteLine("Usage: AfterDarker.Tutorials [01 | 02 | 03 | --list]");
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
