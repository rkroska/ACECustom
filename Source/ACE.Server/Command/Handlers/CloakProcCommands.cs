using System;
using System.Collections.Generic;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.WorldObjects;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// /cloakproc - developer testing for the T11+ cloak procs (ZcCloakProcs): status, test cloaks, force the next proc.
    /// The numbers are server properties (zc_cloak_*), tuned live with @modifybool / @modifylong / @modifydouble, and a zone or
    /// tier Default can re-tune them with the Zone Control stats cloak_* (/zonecontrol set ... cloak_proc_chance 0.03).
    /// Every line it prints is plain ASCII.
    /// </summary>
    public static class CloakProcCommands
    {
        private const uint TestCloakWcid = 44840;   // plain Cloak (LootTables.Cloaks)

        // retail cloak proc spells, one per new proc (LootTables.CloakSpells)
        private const uint CassiusRingOfFire = 1785;
        private const uint ShroudOfDarknessMelee = 5755;
        private const uint CloakedInSkill = 5753;

        [CommandHandler("cloakproc", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0,
            "T11+ cloak procs: status, test cloaks, force the next proc.",
            "status | create <breeze|smoke|dodger|grace|all> | force\n" +
            "Settings: zc_cloak_procs_enabled, zc_cloak_proc_chance, zc_cloak_proc_cooldown, zc_cloak_breeze_heal, zc_cloak_dodger_hits, zc_cloak_dodger_window, zc_cloak_dodger_stamina_mult, zc_cloak_grace_seconds\n" +
            "Zone Control stats (per zone / tier Default): cloak_proc_chance, cloak_proc_cooldown, cloak_breeze_heal, cloak_dodger_hits, cloak_dodger_window, cloak_dodger_stamina_mult, cloak_grace_seconds")]
        /// <summary>/cloakproc [status | create &lt;breeze|smoke|dodger|grace|all&gt; | force] - developer testing for the T11+
        /// cloak procs. No argument = status.</summary>
        public static void HandleCloakProc(Session session, params string[] parameters)
        {
            var player = session?.Player;
            if (player == null)
                return;

            var sub = parameters.Length > 0 ? parameters[0].ToLowerInvariant() : "status";
            switch (sub)
            {
                case "status":
                    Status(session, player);
                    break;

                case "create":
                    Create(session, player, parameters.Length > 1 ? parameters[1].ToLowerInvariant() : "all");
                    break;

                case "force":
                    player.ZcCloakProcForceNext = true;
                    Say(session, "The next hit you take procs your cloak (ignores chance and cooldown, once). Wear a T11+ cloak.");
                    break;

                default:
                    Say(session, "Usage: /cloakproc status | create <breeze|smoke|dodger|grace|all> | force");
                    break;
            }
        }

        /// <summary>Prints the numbers in force where the player stands (and the server's own when a zone changes them), the worn
        /// cloak's proc, and this player's cooldown, Dodger charges and Grace time.</summary>
        private static void Status(Session session, Player player)
        {
            var now = Time.GetUnixTime();

            var mode = !ServerConfig.zc_cloak_procs_enabled.Value ? "OFF (zc_cloak_procs_enabled)"
                : !ServerConfig.zonecontrol_enabled.Value ? "OFF (Zone Control is off)"
                : "ON";
            Say(session, $"T11+ cloak procs: {mode}");
            // the numbers where you stand (Zone Control dials cloak_* over the server settings zc_cloak_*), then the server's own
            var here = ZcCloakProcs.DialsFor(player);
            SayDials(session, "Here", here);
            if (here.Origin != ZcCloakProcs.DialSource.Server)
                SayDials(session, "Server", ZcCloakProcs.ServerDials());

            var cloak = player.EquippedCloak;
            if (cloak == null)
                Say(session, "Your cloak: none worn");
            else
            {
                var proc = ZcCloakProcs.Classify(cloak);
                var tier = cloak.GetProperty(PropertyInt.ZcTier) ?? 0;
                Say(session, $"Your cloak: {cloak.Name} (tier {tier}) -> {(proc != ZcCloakProc.None ? ZcCloakProcs.NameOf(proc) : "retail proc / not affected")}");
            }

            var parts = new List<string> { now < player.ZcCloakProcReadyAt ? $"cooldown {player.ZcCloakProcReadyAt - now:0.#}s" : "ready" };
            if (player.ZcCloakProcForceNext) parts.Add("next hit forced");
            if (player.ZcDodgerHitsLeft > 0 && now < player.ZcDodgerUntil) parts.Add($"Dodger {player.ZcDodgerHitsLeft} left ({player.ZcDodgerUntil - now:0.#}s)");
            if (now < player.ZcCloakGraceUntil) parts.Add($"Grace {player.ZcCloakGraceUntil - now:0.#}s");
            Say(session, "You: " + string.Join(", ", parts));
        }

        /// <summary>Two lines: one set of dials and where they came from.</summary>
        private static void SayDials(Session session, string label, ZcCloakProcs.Dials d)
        {
            Say(session, $"{label} ({d.Source}): chance {d.Chance * 100:0.##}% per hit, cooldown {d.Cooldown:0.#}s");
            Say(session, $"  Calming Breeze {d.BreezeHeal:N0} | Artful Dodger {d.DodgerHits} hits / {d.DodgerWindow:0.#}s at {d.DodgerStaminaMult:0.##}x stamina | Asheron's Grace {d.GraceSeconds:0.#}s");
        }

        /// <summary>Puts tier 11 test cloaks in the player's pack: the one proc named, or all four. Stops at a full pack.</summary>
        private static void Create(Session session, Player player, string which)
        {
            var procs = which switch
            {
                "breeze" or "calming" or "ring" => new[] { ZcCloakProc.CalmingBreeze },
                "smoke" or "shroud" => new[] { ZcCloakProc.SmokeScreen },
                "dodger" or "artful" or "weave" => new[] { ZcCloakProc.ArtfulDodger },
                "grace" or "asheron" or "skill" => new[] { ZcCloakProc.AsheronsGrace },
                "all" => new[] { ZcCloakProc.CalmingBreeze, ZcCloakProc.SmokeScreen, ZcCloakProc.ArtfulDodger, ZcCloakProc.AsheronsGrace },
                _ => null,
            };
            if (procs == null)
            {
                Say(session, "Usage: /cloakproc create <breeze|smoke|dodger|grace|all>");
                return;
            }

            foreach (var proc in procs)
            {
                var wo = WorldObjectFactory.CreateNewWorldObject(TestCloakWcid);
                if (wo == null)
                {
                    Say(session, $"Could not create weenie {TestCloakWcid}.");
                    return;
                }

                wo.SetProperty(PropertyInt.ZcTier, 11);
                switch (proc)
                {
                    case ZcCloakProc.CalmingBreeze: SetSpell(wo, CassiusRingOfFire); break;
                    case ZcCloakProc.SmokeScreen: SetSpell(wo, ShroudOfDarknessMelee); break;
                    case ZcCloakProc.AsheronsGrace: SetSpell(wo, CloakedInSkill); break;
                    case ZcCloakProc.ArtfulDodger:
                        wo.RemoveProperty(PropertyDataId.ProcSpell);
                        wo.CloakWeaveProc = 2;
                        break;
                }
                wo.Name = $"Test Cloak - {ZcCloakProcs.NameOf(proc)}";

                if (!player.TryCreateInInventoryWithNetworking(wo))
                {
                    wo.Destroy();
                    Say(session, "No room in your pack.");
                    return;
                }
                Say(session, $"Created {wo.Name} (tier 11).");
            }
        }

        /// <summary>Gives a test cloak a retail proc spell - the key ZcCloakProcs reads to pick the new proc.</summary>
        private static void SetSpell(WorldObject wo, uint spell)
        {
            wo.ProcSpell = spell;
            wo.CloakWeaveProc = 1;
            wo.ProcSpellSelfTargeted = spell == CloakedInSkill;
        }

        /// <summary>One line of command output.</summary>
        private static void Say(Session session, string text) => CommandHandlerHelper.WriteOutputInfo(session, text, ChatMessageType.Broadcast);
    }
}
