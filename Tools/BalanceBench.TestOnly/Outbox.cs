namespace BalanceBench;

/// <summary>Appends one JSON line per event to the selected character's outbox (control\outbox_<name>.jsonl; Balancer and
/// "no character" keep control\outbox.jsonl, so single-character runs read what they always read). Thread safe.</summary>
internal static class Outbox
{
    private static readonly object Gate = new();

    internal static string FileFor(string name) =>
        string.IsNullOrEmpty(name) || name.Equals("Balancer", StringComparison.OrdinalIgnoreCase) ? "outbox.jsonl" : $"outbox_{name.ToLowerInvariant()}.jsonl";

    internal static void Write(string kind, object data)
    {
        try
        {
            var row = new Dictionary<string, object?> { ["t"] = DateTime.Now.ToString("HH:mm:ss.fff"), ["kind"] = kind, ["data"] = data };
            var line = System.Text.Json.JsonSerializer.Serialize(row);
            var dir = PatchClass.Current.ControlDir;
            lock (Gate)
            {
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, FileFor(Bench.Selected ? Bench.C.Name : "")), line + "\n");
            }
        }
        catch (Exception ex)
        {
            Mod.Log($"outbox write failed: {ex.Message}", ModManager.LogLevel.Warn);
        }
    }
}

/// <summary>
/// Captures chat text: lines built while an inbox command runs go into its result; every chat line sent to a
/// bench character's session (delayed output, combat text) goes to the outbox as kind "chat".
/// </summary>
[HarmonyPatch]
internal static class Capture
{
    [ThreadStatic] internal static List<string>? Lines;
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameMessageSystemChat, string> Texts = new();
    internal static bool LogChat = true;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameMessageSystemChat), MethodType.Constructor, new[] { typeof(string), typeof(ChatMessageType) })]
    public static void OnChat(GameMessageSystemChat __instance, string message)
    {
        if (Lines != null) { Lines.Add(message); return; }
        if (LogChat) Texts.AddOrUpdate(__instance, message);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ACE.Server.Network.NetworkSession), nameof(ACE.Server.Network.NetworkSession.EnqueueSend), new[] { typeof(OutboundGameMessage[]) })]
    public static void OnSend(ACE.Server.Network.NetworkSession __instance, OutboundGameMessage[] messages)
    {
        if (!LogChat || messages == null) return;
        foreach (var m in messages)
        {
            if (m is not GameMessageSystemChat chat || !Texts.TryGetValue(chat, out var text)) continue;
            Texts.Remove(chat);
            var player = __instance.session?.Player;
            if (!PatchClass.IsBench(player)) continue;
            using var scope = Bench.Use(player);
            Outbox.Write("chat", text);
        }
    }
}
