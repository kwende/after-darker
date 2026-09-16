using AfterDarker.Tutorials;
using AfterDarker.Tutorials.Lessons;

namespace AfterDarker.Tests.Conformance;

[TestClass]
[TestCategory("Tutorial")]
public sealed class TutorialSmokeTests
{
    // Protect the educational entry points as well as the lower-level observations.
    [TestMethod]
    [DataRow("01")]
    [DataRow("02")]
    [DataRow("03")]
    [DataRow("04")]
    public void ConsoleLesson_RunsWithItsOriginalSuccessChecks(string id)
    {
        ITutorial lesson = id switch
        {
            "01" => new Tutorial01Addition(),
            "02" => new Tutorial02StackCall(),
            "03" => new Tutorial03FarCall(),
            "04" => new Tutorial04HostGateway(),
            _ => throw new ArgumentOutOfRangeException(nameof(id)),
        };
        lesson.Run();
    }
}
