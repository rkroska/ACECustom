using System;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Command.Handlers
{
    /// <summary>
    /// Diagnose the character-panel display of the T11+ gear lines (ZcGearDisplay, owner 2026-10-19).
    /// </summary>
    public static class ZcPanelCommands
    {
        /// <summary>/zcpanel show | resend.</summary>
        [CommandHandler("zcpanel", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1,
            "Character-panel display of the T11+ gear lines (All Attributes, skill lines, Fortify Vitals, Max Health Pct).",
            "show   - the server's numbers (what the panel should read) and the display entries sent to your client\n" +
            "resend - send every display entry again")]
        public static void HandleZcPanel(Session session, params string[] parameters)
        {
            void Msg(string s) => session.Network.EnqueueSend(new GameMessageSystemChat(s, ChatMessageType.Broadcast));

            var player = session.Player;
            if (player == null)
                return;

            switch (parameters[0].ToLowerInvariant())
            {
                case "show":
                    Show(player, Msg);
                    return;

                case "resend":
                    player.SyncZcGearDisplay(clientListReset: true);
                    Msg($"Sent {player.ZcGearDisplaySent.Count} display entr{(player.ZcGearDisplaySent.Count == 1 ? "y" : "ies")}.");
                    return;

                default:
                    Msg("Usage: /zcpanel show | resend");
                    return;
            }
        }

        /// <summary>Prints the server's attribute, vital and skill numbers (what the panel should read) and the entries sent to the client.</summary>
        private static void Show(Player player, Action<string> msg)
        {
            msg($"--- T11+ gear lines on the panel (spell id {ZcGearDisplay.SpellId}, {(ZcGearDisplay.CheckSpellIdFree() ? "free" : "IN USE - display is OFF")}) ---");
            msg($"Worn gear lock: {(ZoneControlManager.WornPowerSuppressed(player) ? "ON here - worn lines contribute nothing" : "off")}");

            msg("Attributes (the panel should read 'now'):");
            foreach (var attr in ZcGearDisplay.Attributes)
            {
                var a = player.Attributes[attr];
                var gear = player.GetZoneModifierBonus(ZoneModifiers.AttrBonusBase + (int)attr);
                msg($"  {attr,-12} now {a.Current,5}  (gear line +{gear}, without it {(int)a.Current - gear})");
            }

            var fortify = player.GetZoneModifierMax(ZoneModifiers.FortifyVitalsPct);
            var pct = player.GetZoneModifierBonus(ZoneModifiers.PctMaxHealthPct);
            msg($"Max vitals (the panel should read 'max'): Fortify Vitals {fortify}%, Max Health Pct {pct}%, flat gear Max Health {player.GetGearMaxHealth()}");
            msg($"  Health  {player.Health.Current,6} / max {player.Health.MaxValue}");
            msg($"  Stamina {player.Stamina.Current,6} / max {player.Stamina.MaxValue}");
            msg($"  Mana    {player.Mana.Current,6} / max {player.Mana.MaxValue}");

            var skillProps = player.ZoneModifierSkillPropIds();
            if (skillProps.Count > 0)
            {
                msg("Skill lines (the panel should read 'now'):");
                foreach (var propId in skillProps)
                {
                    var skill = (Skill)(propId - ZoneModifiers.SkillBonusBase);
                    var cs = player.GetCreatureSkill(skill, false);
                    msg($"  {skill,-22} now {cs?.Current.ToString() ?? "-",5}  (gear line +{player.GetZoneModifierBonus(propId)})");
                }
            }

            var sent = player.ZcGearDisplaySent.OrderBy(e => e.Layer).ToList();
            msg(sent.Count == 0 ? "Display entries sent to your client: none" : $"Display entries sent to your client ({sent.Count}):");
            foreach (var e in sent)
                msg($"  layer {e.Layer,3}  category 0x{e.Category:X4}  {e.StatModType} key {e.StatModKey} value {e.StatModValue:0.###}  ({Describe(e)})");
        }

        /// <summary>One entry in words, e.g. "Strength +70" or "MaxHealth x1.25".</summary>
        private static string Describe(ZcGearDisplay.Entry e)
        {
            if ((e.StatModType & EnchantmentTypeFlags.Attribute) != 0)
                return $"{(PropertyAttribute)e.StatModKey} +{e.StatModValue:0}";
            if ((e.StatModType & EnchantmentTypeFlags.SecondAtt) != 0)
                return $"{(PropertyAttribute2nd)e.StatModKey} x{e.StatModValue:0.###}";
            if ((e.StatModType & EnchantmentTypeFlags.Skill) != 0)
                return $"{(Skill)e.StatModKey} +{e.StatModValue:0}";
            return "?";
        }
    }
}
