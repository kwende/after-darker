namespace AfterDarker.Tutorials;

/// <summary>A small, independently runnable lesson in the shared console host.</summary>
public interface ITutorial
{
    string Id { get; }
    string Title { get; }
    void Run();
}
