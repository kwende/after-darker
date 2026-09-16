using AfterDarker.Core.Ne;
using AfterDarker.Tests.Fixtures;
using AfterDarker.Tutorials.Inspection;
using AfterDarker.Tutorials.Lessons;

namespace AfterDarker.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public sealed class NeInspectionReportTests
{
    [TestMethod]
    public void Report_DistinguishesFileFactsReferenceNamesAndAssumedContract()
    {
        using StringWriter output = new();
        NeInspectionReport.Write(NeReader.Read(NeFixture.Create()), output);
        string report = output.ToString();
        StringAssert.Contains(report, "Header startup: S1:0008");
        StringAssert.Contains(report, "Expected Windows version: 3.10");
        StringAssert.Contains(report, "KERNEL!#18 [reference: GlobalLock");
        StringAssert.Contains(report, "KERNEL!NamedProc");
        StringAssert.Contains(report, "FRAME_ALIAS -> S1:0020");
        StringAssert.Contains(report, "DRAWFRAME (repeat): enter S1:0010, message=2");
        StringAssert.Contains(report, "Assumed SDK contract");
        StringAssert.Contains(report, "constant 0xBEEF");
        StringAssert.Contains(report, "CUSTOM");
        StringAssert.Contains(report, "FRAME0");
    }

    [TestMethod]
    public void NoModuleExport_DoesNotGuessOrdinalOneIsTheDispatcher()
    {
        byte[] bytes = NeFixture.Create(); bytes[0x12B] = (byte)'X';
        using StringWriter output = new();
        NeInspectionReport.Write(NeReader.Read(bytes), output);
        StringAssert.Contains(output.ToString(), "No ordinal-1 guess is made");
        Assert.DoesNotContain("DRAWFRAME (repeat):", output.ToString());
    }

    [TestMethod]
    public void ArtifactControlCharacters_AreEscaped()
    {
        byte[] bytes = NeFixture.Create(); bytes[0x153] = 0x1B;
        using StringWriter output = new();
        NeInspectionReport.Write(NeReader.Read(bytes), output);
        StringAssert.Contains(output.ToString(), "\\x1B");
        Assert.DoesNotContain("\u001B", output.ToString());
    }

}

[TestClass]
[TestCategory("Tutorial")]
public sealed class NeInspectionTutorialTests
{
    [TestMethod]
    public void Tutorial_PromptsReadsTypedImageAndReportsWithoutNativeExecution()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, NeFixture.Create());
            NeImage image = Tutorial05NeInspection.Execute(path);
            Assert.AreEqual(2, image.Segments.Count);
            using StringReader input = new($"\"{path}\"\n");
            using StringWriter output = new();
            new Tutorial05NeInspection(input: input, output: output).Run();
            StringAssert.Contains(output.ToString(), "PASS: NE metadata read");
            using StringWriter explicitOutput = new();
            new Tutorial05NeInspection(path, output: explicitOutput).Run();
            StringAssert.Contains(explicitOutput.ToString(), "PASS: NE metadata read");
        }
        finally { File.Delete(path); }
    }
}
