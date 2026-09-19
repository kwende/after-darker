using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;
using AfterDarker.Tutorials.Runtime;

namespace AfterDarker.Tutorials.Lessons;

/// <summary>Lesson 08 stops after initialization; step into MondrianRunner.Execute to follow the mechanism.</summary>
public sealed class Tutorial08MondrianInitialize(string? path = null, bool trace = false,
    TextReader? input = null, TextWriter? output = null) : ITutorial
{
    public string Id => "08";
    public string Title => "Load original Mondrian: DLL startup -> PREINITIALIZE -> INITIALIZE";

    public void Run()
    {
        TextWriter writer = output ?? Console.Out;
        string? filePath = path;
        if (filePath is null)
        {
            writer.Write("Path to your local Mondrian.ad (the analyzed version): ");
            filePath = (input ?? Console.In).ReadLine()?.Trim().Trim('"');
        }
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A local Mondrian path is required.");
        using FileStream stream = File.OpenRead(filePath);
        if (stream.Length > NeReader.MaximumFileBytes) throw new InvalidDataException("Module exceeds the NE input limit.");
        byte[] file = new byte[(int)stream.Length];
        stream.ReadExactly(file);
        Execute(file, writer, trace);
        writer.WriteLine("PASS: original Mondrian startup, PREINITIALIZE, and INITIALIZE returned with verified guest state.");
        writer.WriteLine("Stops before BLANK/DRAWFRAME. No pixels, settings dialogs, or general Windows heap implementation.");
    }

    public static MondrianRunner.Result Execute(byte[] file, TextWriter? output = null, bool trace = false,
        MondrianInitialization.Options? options = null, int instructionLimit = 50_000)
        => MondrianRunner.Execute(file, output, trace, options, instructionLimit);
}
