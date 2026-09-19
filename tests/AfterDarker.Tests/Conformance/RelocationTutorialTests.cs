using AfterDarker.Tutorials.Fixtures;
using AfterDarker.Tutorials.Lessons;

namespace AfterDarker.Tests.Conformance;

[TestClass]
[TestCategory("Tutorial")]
public sealed class RelocationTutorialTests
{
    [TestMethod]
    public void BuiltInLessonShowsLookupChainAndWriteWithoutPrivateFiles()
    {
        using var output = new StringWriter();
        new Tutorial07Relocations(output: output).Run();
        string report = output.ToString();
        StringAssert.Contains(report, "internal entry #2 -> S2:0020");
        StringAssert.Contains(report, "0010 = next source offset");
        StringAssert.Contains(report, "Before: 10 00 00 00 -> After: 12 00 10 00");
        StringAssert.Contains(report, "NO implementation, ABI, or executable memory");
        StringAssert.Contains(report, "No CPU, initialization, or API execution");
    }

    [TestMethod]
    public void StepModePausesOnceForEveryPatch()
    {
        using var output = new StringWriter();
        new Tutorial07Relocations(step: true, input: new StringReader("\n\n\n\n\n"), output: output).Run();
        Assert.AreEqual(5, output.ToString().Split("Enter: next patch").Length - 1);
        StringAssert.Contains(output.ToString(), "PASS:");
    }

    [TestMethod]
    [DataRow("q\n")]
    [DataRow("")]
    public void QuitOrEndOfInputDoesNotClaimSuccess(string answer)
    {
        using var output = new StringWriter();
        Assert.Throws<OperationCanceledException>(() =>
            new Tutorial07Relocations(step: true, input: new StringReader(answer), output: output).Run());
        Assert.DoesNotContain("PASS:", output.ToString());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExplicitOrPromptedFilePathUsesTheSamePipeline(bool prompt)
    {
        string path = Path.GetTempFileName();
        try
        {
            byte[] file = RelocationDemo.Create(); File.WriteAllBytes(path, file);
            using var output = new StringWriter();
            new Tutorial07Relocations(prompt ? "--file" : path, input: new StringReader(path + "\n"), output: output).Run();
            StringAssert.Contains(output.ToString(), "Prepared 3 segments");
            CollectionAssert.AreEqual(file, File.ReadAllBytes(path));
        }
        finally { File.Delete(path); }
    }
}
