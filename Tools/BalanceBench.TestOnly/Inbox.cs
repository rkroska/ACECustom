namespace BalanceBench;

/// <summary>
/// Polls ControlDir\inbox for *.cmd files. Each line runs on the world thread as the first online bench
/// character, with access checks skipped (dev server only). Results go to outbox.jsonl as kind "result".
/// A line starting with "bench" goes to the mod's own commands.
/// </summary>
internal static class Inbox
{
    private static System.Threading.Timer? timer;
    private static string dir = "";
    private static int busy;

    internal static void Start(string controlDir)
    {
        dir = controlDir;
        Directory.CreateDirectory(Path.Combine(dir, "inbox"));
        timer = new System.Threading.Timer(_ => Poll(), null, 1000, 250);
    }

    internal static void Stop()
    {
        timer?.Dispose();
        timer = null;
    }

    private static void Poll()
    {
        if (Interlocked.Exchange(ref busy, 1) == 1) return;
        try
        {
            var root = Path.Combine(dir, "inbox");
            var files = Directory.GetDirectories(root).Where(d=>NativeBenchAdapter.ApprovedName(Path.GetFileName(d)))
                .SelectMany(d=>Directory.GetFiles(d,"*.cmd").Select(f=>(File:f,Who:Path.GetFileName(d))))
                .OrderBy(x=>x.File,StringComparer.Ordinal).ToList();
            foreach (var (file, who) in files)
            {
                string[] lines;
                try { lines = File.ReadAllLines(file); File.Delete(file); }
                catch (IOException) { continue; }   // still being written
                var id = Path.GetFileNameWithoutExtension(file);
                var cmds = lines.Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).ToList();
                WorldManager.EnqueueAction(new ACE.Server.Entity.Actions.ActionEventDelegate(
                    ACE.Server.Entity.Actions.ActionType.AdminCommands_HandleActionQueryHouse, () => RunAll(id, cmds, who)));
            }
        }
        catch (Exception ex)
        {
            Mod.Log($"inbox poll failed: {ex.Message}", ModManager.LogLevel.Warn);
        }
        finally { Interlocked.Exchange(ref busy, 0); }
    }

    /// <summary>The online bench character with this name (the '+' admin prefix ignored).</summary>
    internal static Player? BenchPlayer(string who) =>
        PlayerManager.GetAllOnline().FirstOrDefault(p => PatchClass.IsBench(p) && string.Equals(p.Name.TrimStart('+'), who, StringComparison.OrdinalIgnoreCase));

    private static void RunAll(string id, List<string> cmds, string who)
    {
        var player = BenchPlayer(who);
        // the results go to the character's own outbox even when it is offline (the runner is waiting there)
        using var scope = player != null ? Bench.Use(player) : Bench.Use(Bench.All.GetOrAdd(who, n => new Ctx { Name = n }));
        for (int i = 0; i < cmds.Count; i++)
            Outbox.Write("result", Run(id, i, cmds[i], player));
    }

    internal static object Run(string id, int index, string line, Player? player)
    {
        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 3 && words[0] == "bench" && words[1] == "srcprops")
            return new { id, index, line, ok = true, output = Mirror.Dump(words[2].Replace('_', ' '), words.Length > 3 ? words[3] : "").ToArray() };
        if (player == null)
            return new { id, index, line, ok = false, output = new[] { "no bench character online" } };

        var text = line.TrimStart('/', '@');
        Capture.Lines = new List<string>();
        bool ok = true;
        string? error = null;
        try
        {
            CommandManager.ParseCommand(text, out var command, out var parameters);
            TestOnlyPolicy.CheckExternal(player,command,parameters);
            if (string.Equals(command, "bench", StringComparison.OrdinalIgnoreCase))
                Commands.Dispatch(player, parameters);
            else if (CommandManager.commandHandlers.TryGetValue(command, out var info))
                info.Handler.DynamicInvoke(player.Session, parameters);
            else { ok = false; error = $"unknown command {command}"; }
        }
        catch (Exception ex)
        {
            ok = false;
            error = (ex.InnerException ?? ex).ToString();
        }
        var output = Capture.Lines ?? new List<string>();
        Capture.Lines = null;
        if (error != null) output.Add(error);
        return new { id, index, line, ok, output = output.ToArray() };
    }
}
