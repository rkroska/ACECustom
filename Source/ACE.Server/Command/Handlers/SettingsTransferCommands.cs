using System;
using System.IO;
using System.Linq;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Managers.WeaponScaling;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Mods;
using ACE.Server.Network;
using ACE.Server.WorldObjects;

using log4net;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// /settings (owner 2026-10-25): move the balance settings from the test server to prod in one file.
    ///   export &lt;name&gt;                  writes SettingsExports/&lt;name&gt;.json (next to the Mods folder)
    ///   list                           the files in that folder
    ///   import &lt;name&gt;                  shows what would change - nothing is applied
    ///   import &lt;name&gt; apply            saves a backup of this server's settings, then applies the file, live
    ///   ... copyserver                 also copy the server-owned Zone Control parts (bounties, gear-lock spots, ladders)
    /// Covers the Zone Control store, the weapon scaling store and every charm's settings. Not server config, not
    /// world content, not characters. File names only, never paths. See Managers/SettingsTransfer.cs.
    /// </summary>
    public static class SettingsTransferCommands
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private const string Usage =
            "Usage: /settings export <name> | list | import <name> [apply] [copyserver]\n" +
            "  export: writes this server's Zone Control, weapon scaling and charm settings to one file.\n" +
            "  import: shows what the file would change; add 'apply' to back up this server's settings and apply it live.\n" +
            "  copyserver: also copy bounties, gear-lock spots and ladder state (normally kept as this server has them).";

        /// <summary>SettingsExports, beside the Mods folder (C:\ACE\SettingsExports, /ace/SettingsExports).</summary>
        public static string FolderPath =>
            Path.GetFullPath(Path.Combine(ModManager.ModPath, "..", SettingsTransfer.Folder));

        [CommandHandler("settings", AccessLevel.Admin, CommandHandlerFlag.None, 0,
            "Export or import the balance settings (Zone Control, weapon scaling, charms) as one file.", Usage)]
        public static void HandleSettings(Session session, params string[] parameters)
        {
            void Say(string s) => CommandHandlerHelper.WriteOutputInfo(session, s, ChatMessageType.Broadcast);

            var verb = parameters.Length > 0 ? parameters[0].ToLowerInvariant() : "";
            try
            {
                switch (verb)
                {
                    case "export":
                        Export(parameters.Length > 1 ? parameters[1] : null, Say);
                        return;
                    case "list":
                        List(Say);
                        return;
                    case "import":
                    {
                        var flags = parameters.Skip(2).Select(p => p.ToLowerInvariant()).ToList();
                        var unknown = flags.Where(f => f != "apply" && f != "copyserver").ToList();
                        if (unknown.Count > 0) { Say($"settings import: unknown option '{unknown[0]}'.\n{Usage}"); return; }
                        Import(session, parameters.Length > 1 ? parameters[1] : null, flags.Contains("apply"), flags.Contains("copyserver"), Say);
                        return;
                    }
                    default:
                        Say(Usage);
                        return;
                }
            }
            catch (Exception ex)
            {
                log.Error($"/settings {string.Join(" ", parameters)} failed: {ex}");
                Say($"settings {verb}: failed - {Ascii(ex.GetBaseException().Message)}. Nothing more was done; see the server log.");
            }
        }

        /// <summary>This server's settings now, as a bundle.</summary>
        private static SettingsTransfer.Bundle Capture() =>
            SettingsTransfer.Make(ConfigManager.Config.Server.WorldName, ZoneControlManager.ExportStoreJson(),
                WeaponScalingManager.ExportJson(), CharmSettingsManager.ExportAll(), DateTime.UtcNow);

        private static string Write(string fileName, SettingsTransfer.Bundle bundle)
        {
            Directory.CreateDirectory(FolderPath);
            var path = Path.Combine(FolderPath, fileName);
            File.WriteAllText(path, SettingsTransfer.Serialize(bundle));
            return path;
        }

        private static void Export(string name, Action<string> say)
        {
            var file = SettingsTransfer.FileNameFor(name);
            if (file == null) { say("settings export: give a name of letters, digits, '.', '-' or '_' (no folders), e.g. /settings export balance-1025"); return; }
            if (File.Exists(Path.Combine(FolderPath, file))) { say($"settings export: {file} already exists - pick another name."); return; }

            var bundle = Capture();
            var path = Write(file, bundle);
            say($"settings export: wrote {path} ({new FileInfo(path).Length:N0} bytes) - Zone Control, weapon scaling and {bundle.Charms.Count} charms.");
            say("Copy it to the other server's SettingsExports folder, then run /settings import " + file.Substring(0, file.Length - 5) + " there.");
        }

        private static void List(Action<string> say)
        {
            if (!Directory.Exists(FolderPath)) { say($"settings: no {FolderPath} folder yet - /settings export <name> makes it."); return; }
            var files = new DirectoryInfo(FolderPath).GetFiles("*.json").OrderByDescending(f => f.LastWriteTimeUtc).ToList();
            say($"settings: {files.Count} file(s) in {FolderPath}:");
            foreach (var f in files.Take(30))
                say($"  {Ascii(f.Name)}  {f.Length:N0} bytes  {f.LastWriteTime:yyyy-MM-dd HH:mm}");
        }

        private static void Import(Session session, string name, bool apply, bool copyServer, Action<string> say)
        {
            var file = SettingsTransfer.FileNameFor(name);
            if (file == null) { say("settings import: give the file's name (no folders), e.g. /settings import balance-1025"); return; }
            var path = Path.Combine(FolderPath, file);
            if (!File.Exists(path))
            {
                say($"settings import: no {file} in {FolderPath} (names are case-sensitive on Linux).");
                List(say);
                return;
            }

            SettingsTransfer.Bundle incoming;
            try { incoming = SettingsTransfer.Parse(File.ReadAllText(path)); }
            catch (FormatException ex) { say($"settings import: {Ascii(ex.Message)}"); return; }

            var current = Capture();
            say($"settings import: {file} from '{Ascii(incoming.Source)}', exported {incoming.CreatedUtc:yyyy-MM-dd HH:mm} UTC.");
            foreach (var line in SettingsTransfer.Preview(current, incoming, copyServer, charmDefaults: CharmSettingsManager.ExportDefaults()))
                say(line);

            if (!apply)
            {
                say($"Nothing was changed. To apply: /settings import {file.Substring(0, file.Length - 5)} apply" + (copyServer ? " copyserver" : ""));
                return;
            }

            // the undo first: this server's settings as they are now
            var backup = $"backup-{DateTime.Now:yyyyMMdd-HHmmss}.json";
            Write(backup, current);
            say($"Backed up this server's settings to {backup}. Undo with: /settings import {backup.Substring(0, backup.Length - 5)} apply copyserver");

            if (incoming.ZoneControl != null)
            {
                var merged = SettingsTransfer.MergeZoneControl(current.ZoneControl, incoming.ZoneControl, copyServer);
                try { ZoneControlManager.ImportStoreJson(merged.ToString(Newtonsoft.Json.Formatting.None)); }
                catch (Exception ex)
                {
                    say($"Zone Control: NOT applied - {Ascii(ex.GetBaseException().Message)}. Nothing else was imported; this server's store is unchanged.");
                    return;
                }
                say("Zone Control: applied and reloaded.");
            }
            if (incoming.WeaponScaling != null)
            {
                WeaponScalingManager.ImportJson(incoming.WeaponScaling.ToString(Newtonsoft.Json.Formatting.None));
                say("Weapon scaling: applied.");
            }
            if (incoming.Charms.Count > 0)
            {
                var skipped = CharmSettingsManager.ImportAll(incoming.Charms);
                say($"Charms: {incoming.Charms.Count - skipped.Count} applied and saved" + (skipped.Count > 0 ? $"; not on this server (skipped): {Ascii(string.Join(", ", skipped))}" : "."));

                // a levelled charm's price curve sets its level from the coins put in: work everyone's out again now
                foreach (var online in PlayerManager.GetAllOnline())
                {
                    var p = online;
                    LandblockManager.RunOnThreadFor(p, ACE.Server.Entity.Actions.ActionType.Charm_RefreshLevels, () => p.SyncLevelledCharms());
                }
            }

            var who = session?.Player?.Name ?? "console";
            log.Info($"[SETTINGS] {who} imported {file} (from '{incoming.Source}', {incoming.CreatedUtc:u}); backup {backup}.");
            PlayerManager.BroadcastToAuditChannel(session?.Player, $"[SETTINGS] {who} imported {file} from '{Ascii(incoming.Source)}'. Backup: {backup}.");
            say("settings import: done.");
        }

        private static string Ascii(string s) => new string((s ?? "").Select(c => c < 32 || c > 126 ? '?' : c).ToArray());
    }
}
