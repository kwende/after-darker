using AfterDarker.Core.Ne;
using AfterDarker.Tutorials.Inspection;

namespace AfterDarker.Tutorials.Lessons;

/// <summary>File bytes -> typed metadata -> readable report. No CPU engine is created.</summary>
public sealed class Tutorial05NeInspection(string? inputPath = null,
    TextReader? input = null, TextWriter? output = null) : ITutorial
{
    public string Id => "05";
    public string Title => "Read a Windows NE module without executing it";

    // Put a breakpoint here to explore the returned records in the debugger.
    public static NeImage Execute(string path) => NeReader.ReadFile(path);

    public void Run()
    {
        TextWriter writer = output ?? Console.Out;
        string? path = inputPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            writer.Write("Path to a local .AD / Windows NE file: ");
            path = (input ?? Console.In).ReadLine()?.Trim().Trim('"');
        }
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Supply a local file: AfterDarker.Tutorials 05 <path-to-module.ad>");

        NeImage image = Execute(path);
        NeInspectionReport.Write(image, writer);
    }
}
