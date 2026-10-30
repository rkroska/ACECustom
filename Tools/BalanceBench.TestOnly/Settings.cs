namespace BalanceBench;

public class Settings
{
    public AccessLevel CommandAccess { get; set; } = AccessLevel.Developer;

    /// <summary>Only these characters are benched: logged, driven, and allowed to run inbox commands.</summary>
    public List<string> Characters { get; set; } = new() { "Balancer" };

    /// <summary>Holds inbox\*.cmd (one command per line, run as the bench character) and outbox.jsonl.</summary>
    public string ControlDir { get; set; } = @"C:\ACE\BalanceBench\control";
}
