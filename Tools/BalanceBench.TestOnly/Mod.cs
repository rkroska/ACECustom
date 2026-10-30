namespace BalanceBench;

public class Mod : BasicMod
{
    public Mod() : base() => Setup("BalanceBench.TestOnly", new PatchClass(this));

    internal static void Log(string message, ModManager.LogLevel level = ModManager.LogLevel.Info)
    {
        ModManager.Log($"[BalanceBench.TestOnly] {message}", level);
    }
}
