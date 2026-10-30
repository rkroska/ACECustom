namespace BalanceBench;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    internal static Settings Current => Settings ?? new Settings();

    public override Task OnWorldOpen()
    {
        Settings = SettingsContainer?.Settings ?? new Settings();
        TestOnlyAudit.AssertPure();
        Commands.Register(Settings.CommandAccess);
        Inbox.Start(Settings.ControlDir);
        Bench.Start();
        Mod.Log($"Ready. Bench characters: {string.Join(", ", Settings.Characters)}. Control: {Settings.ControlDir}");
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        Inbox.Stop();
        Bench.Stop();
        Commands.Unregister();
        base.Stop();
    }

    internal static bool IsBench(Player? p) =>
        p != null && NativeBenchAdapter.ApprovedName(p.Name.TrimStart('+')) && Current.Characters.Any(c => string.Equals(c, p.Name.TrimStart('+'), StringComparison.OrdinalIgnoreCase));
}
