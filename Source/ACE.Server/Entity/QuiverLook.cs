using System;
using System.Collections.Generic;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Quiver looks (2026-11-11): arrows, quarrels and darts that look like a captured creature.
    ///
    ///  1. Buy a Quiver Tailoring Kit (78780430) from Sorrel the Dyer.
    ///  2. Use the kit on a Siphoned or Hollowed Essence (or the essence on the kit). A Quiver Pattern (78780431) holding
    ///     the creature's look is made, and the essence and the kit are consumed.
    ///  3. Use the pattern on either:
    ///     - the Universal Quiver Charm: the look of every empty-slot shot, or
    ///     - one bow, crossbow or atlatl: the look of every shot from that launcher. This wins over the charm.
    ///     The pattern is consumed. A new pattern replaces the old look; to change it, repeat 1-3.
    ///  4. A Quiver Unraveling Kit (78780434) takes the look and arrow size off again, back to normal.
    ///
    /// Only the look changes. The projectile keeps its own damage. Its model is sized to about universal_quiver_look_size
    /// meters across (default 1.5, the player's pick on 2026-11-11), so it reads clearly in flight. The admins' creature-model infinite arrows
    /// are mostly 0.42-0.57 m across; players found 0.5 m a little small (2026-11-11).
    ///
    /// The look is stored with the same VisualOverride* properties a pet essence uses.
    /// </summary>
    public static class QuiverLook
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>Quiver Tailoring Kit, sold by Sorrel the Dyer (docs/WCID_ALLOCATION_7878.md).</summary>
        public const uint KitWcid = 78780430;

        /// <summary>Quiver Pattern: a kit holding one creature's look.</summary>
        public const uint PatternWcid = 78780431;

        /// <summary>
        /// The size a stored look's scale is computed for (meters across). A shot rescales it to the live server
        /// setting universal_quiver_look_size (ProjectileScale), so changing the setting resizes every look at once.
        /// </summary>
        public const float LookSizeM = 0.5f;

        /// <summary>Lower limit of a shot's scale (1/50 of a model's natural size), so a huge model can't vanish.</summary>
        public const float MinScale = 0.02f;
        /// <summary>
        /// Upper limit of a shot's scale (10x a model's natural size), so a tiny model can't balloon. A model under
        /// 0.3 m across therefore stays a little under the full 3 m of a 1.5 m look at 200%.
        /// </summary>
        public const float MaxScale = 10.0f;

        /// <summary>True when the object stores a Quiver look (a pattern, a charm or a launcher).</summary>
        public static bool HasLook(WorldObject wo) => (wo?.GetProperty(PropertyDataId.VisualOverrideSetup) ?? 0) != 0;

        /// <summary>
        /// The scale that makes a model of this size (meters, at scale 1) about LookSizeM across. Not clamped: the
        /// limits apply once, to the shot (ProjectileScale), so a huge or tiny model isn't clamped twice.
        /// </summary>
        public static float ScaleForSize(float modelSizeM)
        {
            if (!(modelSizeM > 0.01f) || float.IsInfinity(modelSizeM))
                return 0.3f;

            return LookSizeM / modelSizeM;
        }

        /// <summary>
        /// The ObjScale a shot uses: the stored scale (made for LookSizeM) rescaled to the wanted size in meters,
        /// clamped. No stored scale = 0.3.
        /// </summary>
        public static float ProjectileScale(double? storedScale, double sizeM)
        {
            if (!storedScale.HasValue || !(storedScale.Value > 0) || double.IsInfinity(storedScale.Value))
                return 0.3f;

            var factor = sizeM > 0 && !double.IsInfinity(sizeM) ? sizeM / LookSizeM : 1.0;
            return Math.Clamp((float)(storedScale.Value * factor), MinScale, MaxScale);
        }

        /// <summary>A parsed "id:value,..." server setting with the text it came from: one immutable snapshot, swapped whole.</summary>
        private sealed class ParsedSetting<T>
        {
            public readonly string Text;
            public readonly Dictionary<int, T> Map;

            public ParsedSetting(string text, Dictionary<int, T> map)
            {
                Text = text;
                Map = map;
            }
        }

        private static ParsedSetting<double> sizeOverrides = new ParsedSetting<double>("", new Dictionary<int, double>());
        private static ParsedSetting<uint> projectiles = new ParsedSetting<uint>("", new Dictionary<int, uint>());

        /// <summary>
        /// A setting's parsed map, parsed again only when the setting's text changes. Thread-safe for the parallel
        /// landblock groups: the text and the map travel together in one snapshot, read and published as one reference.
        /// </summary>
        private static Dictionary<int, T> Cached<T>(ref ParsedSetting<T> cache, string text, Func<string, Dictionary<int, T>> parse)
        {
            text ??= "";
            var snapshot = System.Threading.Volatile.Read(ref cache);
            if (!string.Equals(snapshot.Text, text, StringComparison.Ordinal))
            {
                snapshot = new ParsedSetting<T>(text, parse(text));
                System.Threading.Volatile.Write(ref cache, snapshot);
            }
            return snapshot.Map;
        }

        /// <summary>
        /// The admin's size factor for one creature's look (universal_quiver_look_size_overrides, "wcid:factor,..."),
        /// 1.0 when it has none.
        /// </summary>
        public static double ModelSizeFactor(int? creatureWcid)
        {
            if (!creatureWcid.HasValue)
                return 1.0;

            var map = Cached(ref sizeOverrides, ServerConfig.universal_quiver_look_size_overrides.Value, ParseSizeOverrides);
            return map.TryGetValue(creatureWcid.Value, out var f) ? f : 1.0;
        }

        /// <summary>The projectile weenie fired for a launcher AmmoType (universal_quiver_projectiles, "1:4395100,..."), or null.</summary>
        public static uint? ProjectileWcid(int ammoType)
        {
            var map = Cached(ref projectiles, ServerConfig.universal_quiver_projectiles.Value, ParseProjectiles);
            return map.TryGetValue(ammoType, out var wcid) ? wcid : (uint?)null;
        }

        private delegate bool TryParseValue<T>(string text, out T value);

        /// <summary>"id:value,id:value" -> map; entries whose id or value don't parse are skipped.</summary>
        private static Dictionary<int, T> ParsePairs<T>(string text, TryParseValue<T> tryValue)
        {
            var map = new Dictionary<int, T>();
            foreach (var pair in (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = pair.Split(':', StringSplitOptions.TrimEntries);
                if (parts.Length == 2 && int.TryParse(parts[0], out var id) && tryValue(parts[1], out var value))
                    map[id] = value;
            }
            return map;
        }

        /// <summary>"wcid:factor,wcid:factor" -> map; bad or non-positive factors are skipped.</summary>
        public static Dictionary<int, double> ParseSizeOverrides(string text)
            => ParsePairs(text, (string s, out double f) =>
                double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)
                && f > 0 && !double.IsInfinity(f));

        /// <summary>"ammoType:wcid,ammoType:wcid" -> map; bad or zero wcids are skipped.</summary>
        public static Dictionary<int, uint> ParseProjectiles(string text)
            => ParsePairs(text, (string s, out uint wcid) => uint.TryParse(s, out wcid) && wcid != 0);

        /// <summary>Arrow-sized scale for a model: its largest dimension (height or width) from the client dat.</summary>
        private static float ArrowScale(uint setupId, float? capturedScale)
        {
            try
            {
                var setup = DatManager.PortalDat.ReadFromDat<SetupModel>(setupId);
                var size = Math.Max(setup.Height, 2 * setup.Radius);
                if (size > 0.01f)
                    return ScaleForSize(size);
            }
            catch (Exception ex)
            {
                log.Warn($"[QuiverLook] setup 0x{setupId:X8} could not be read: {ex.Message}");
            }

            // the essence's own scale makes the creature 0.75 m tall
            return capturedScale.HasValue && capturedScale.Value > 0 ? capturedScale.Value * LookSizeM / 0.75f : 0.3f;
        }

        /// <summary>Sets a property, or removes it when there is no value (0 counts as none for a data id).</summary>
        private static void SetOrRemove(WorldObject wo, PropertyDataId p, uint? v) { if (v.HasValue && v.Value != 0) wo.SetProperty(p, v.Value); else wo.RemoveProperty(p); }
        /// <summary>Sets a property, or removes it when there is no value.</summary>
        private static void SetOrRemove(WorldObject wo, PropertyInt p, int? v) { if (v.HasValue) wo.SetProperty(p, v.Value); else wo.RemoveProperty(p); }
        /// <summary>Sets a property, or removes it when there is no value.</summary>
        private static void SetOrRemove(WorldObject wo, PropertyFloat p, double? v) { if (v.HasValue) wo.SetProperty(p, v.Value); else wo.RemoveProperty(p); }
        /// <summary>Sets a property, or removes it when the text is empty.</summary>
        private static void SetOrRemove(WorldObject wo, PropertyString p, string v) { if (!string.IsNullOrEmpty(v)) wo.SetProperty(p, v); else wo.RemoveProperty(p); }

        /// <summary>Copies a stored look (pattern -> charm or launcher). One list, so the steps can't drift apart.</summary>
        private static void CopyLook(WorldObject from, WorldObject to)
        {
            foreach (var p in LookDids)
                SetOrRemove(to, p, from.GetProperty(p));
            foreach (var p in LookInts)
                SetOrRemove(to, p, from.GetProperty(p));
            foreach (var p in LookFloats)
                SetOrRemove(to, p, from.GetProperty(p));
            foreach (var p in LookStrings)
                SetOrRemove(to, p, from.GetProperty(p));
        }

        // The properties a stored Quiver look is made of: one list for copying (pattern -> charm / launcher) and clearing
        // (Unraveling Kit), so the two can't drift apart. HandleExtract fills the same set from an essence's Captured*.
        private static readonly PropertyDataId[] LookDids =
            { PropertyDataId.VisualOverrideSetup, PropertyDataId.VisualOverrideIcon, PropertyDataId.VisualOverridePaletteBase, PropertyDataId.VisualOverrideClothingBase };
        private static readonly PropertyInt[] LookInts =
            { PropertyInt.VisualOverridePaletteTemplate, PropertyInt.CapturedCreatureWCID };
        private static readonly PropertyFloat[] LookFloats =
            { PropertyFloat.VisualOverrideShade, PropertyFloat.VisualOverrideScale };
        private static readonly PropertyString[] LookStrings =
            { PropertyString.CapturedCreatureName, PropertyString.CapturedObjDescAnimParts, PropertyString.CapturedObjDescPalettes, PropertyString.CapturedObjDescTextures };

        /// <summary>True when the item is in the player's pack (any side pack) or wielded.</summary>
        private static bool InMyPossession(Player player, WorldObject item)
        {
            return player.FindObject(item.Guid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) != null;
        }

        /// <summary>The Universal Quiver Charm (any copy of it).</summary>
        public static bool IsQuiverCharm(WorldObject wo)
        {
            return wo != null && wo.IsAbilityCharm && wo.CharmGrantsAbility == CharmAbilityRegistry.UniversalQuiverAbilityId;
        }

        // ------------------------------------------------------------------
        // Step 2: kit + essence -> pattern
        // ------------------------------------------------------------------

        /// <summary>
        /// Step 2: a Quiver Tailoring Kit and a captured essence make a Quiver Pattern holding the creature's look,
        /// sized for arrows. The pattern is put in the pack first; only then are the essence and the kit consumed, so a
        /// failure never loses or duplicates anything.
        /// </summary>
        /// <param name="player">The player tailoring.</param>
        /// <param name="kit">The Quiver Tailoring Kit.</param>
        /// <param name="essence">A Siphoned or Hollowed Essence.</param>
        public static void HandleExtract(Player player, WorldObject kit, WorldObject essence)
        {
            if (essence == null || !MonsterCapture.IsCapturedAppearance(essence))
            {
                player.SendTransientError("Use the Quiver Tailoring Kit on a Siphoned or Hollowed Essence.");
                return;
            }

            var setup = essence.GetProperty(PropertyDataId.CapturedSetup) ?? 0;
            if (setup == 0)
            {
                player.SendTransientError($"{essence.Name} holds no appearance.");
                return;
            }

            if (!InMyPossession(player, kit) || !InMyPossession(player, essence))
            {
                player.SendTransientError("The kit and the essence must both be in your pack.");
                return;
            }

            if (kit.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow) || essence.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow))
            {
                player.SendTransientError("You cannot tailor an item that is in the trade window.");
                return;
            }

            // Build the pattern from a snapshot of the essence. Nothing is consumed yet.
            var pattern = WorldObjectFactory.CreateNewWorldObject(PatternWcid);
            if (pattern == null)
            {
                player.SendTransientError("The Quiver Pattern could not be created.");
                log.Error($"[QuiverLook] Weenie {PatternWcid} is missing from the world database.");
                return;
            }

            var creatureName = essence.GetProperty(PropertyString.CapturedCreatureName) ?? essence.Name;

            SetOrRemove(pattern, PropertyDataId.VisualOverrideSetup, setup);
            SetOrRemove(pattern, PropertyDataId.VisualOverridePaletteBase, essence.GetProperty(PropertyDataId.CapturedPaletteBase));
            SetOrRemove(pattern, PropertyDataId.VisualOverrideClothingBase, essence.GetProperty(PropertyDataId.CapturedClothingBase));
            SetOrRemove(pattern, PropertyInt.VisualOverridePaletteTemplate, essence.GetProperty(PropertyInt.CapturedPaletteTemplate));
            SetOrRemove(pattern, PropertyFloat.VisualOverrideShade, essence.GetProperty(PropertyFloat.CapturedShade));
            SetOrRemove(pattern, PropertyFloat.VisualOverrideScale, ArrowScale(setup, (float?)essence.GetProperty(PropertyFloat.CapturedScale)));
            SetOrRemove(pattern, PropertyString.CapturedCreatureName, creatureName);
            SetOrRemove(pattern, PropertyInt.CapturedCreatureWCID, essence.GetProperty(PropertyInt.CapturedCreatureWCID));
            SetOrRemove(pattern, PropertyString.CapturedObjDescAnimParts, essence.GetProperty(PropertyString.CapturedObjDescAnimParts));
            SetOrRemove(pattern, PropertyString.CapturedObjDescPalettes, essence.GetProperty(PropertyString.CapturedObjDescPalettes));
            SetOrRemove(pattern, PropertyString.CapturedObjDescTextures, essence.GetProperty(PropertyString.CapturedObjDescTextures));

            pattern.Name = $"Quiver Pattern ({creatureName})";
            // the creature's portrait, so a pack of patterns is legible (a siphoned essence keeps it as its overlay);
            // also stored as VisualOverrideIcon, which carries it on to the charm
            if (essence.IconOverlayId.HasValue && essence.IconOverlayId.Value != 0)
            {
                pattern.IconOverlayId = essence.IconOverlayId;
                pattern.SetProperty(PropertyDataId.VisualOverrideIcon, essence.IconOverlayId.Value);
            }

            if (!player.TryCreateInInventoryWithNetworking(pattern))
            {
                player.SendTransientError("Your pack is full. Make room for the pattern first.");
                pattern.Destroy();
                return;
            }

            // The pattern is in the pack: now consume the essence and the kit. If the essence can't be consumed, back
            // the pattern out so nothing is duplicated.
            if (!player.TryConsumeFromInventoryWithNetworking(essence, 1))
            {
                player.TryConsumeFromInventoryWithNetworking(pattern, 1);
                player.SendTransientError("The essence could not be used. Nothing was changed.");
                return;
            }
            if (!player.TryConsumeFromInventoryWithNetworking(kit, 1))
                log.Warn($"[QuiverLook] consumed {essence.Name} but not kit 0x{kit.Guid.Full:X8} for {player.Name}; pattern kept.");

            player.PlayParticleEffect(PlayScript.EnchantUpPurple, player.Guid);
            player.SendMessage($"You stitch the likeness of {creatureName} into a Quiver Pattern. Use it on your Universal Quiver Charm or on a bow, crossbow or atlatl.");
            log.Info($"[QuiverLook] {player.Name} made a pattern of {creatureName} (setup 0x{setup:X8}, scale {pattern.GetProperty(PropertyFloat.VisualOverrideScale):0.###}).");
        }

        // ------------------------------------------------------------------
        // Step 3: pattern -> charm or launcher
        // ------------------------------------------------------------------

        /// <summary>
        /// Step 3: a Quiver Pattern's look goes onto the Universal Quiver Charm (which also takes the creature's name
        /// and portrait) or onto one bow, crossbow or atlatl. The pattern is consumed before anything changes.
        /// </summary>
        /// <param name="player">The player tailoring.</param>
        /// <param name="pattern">The Quiver Pattern.</param>
        /// <param name="target">The charm or launcher.</param>
        public static void HandleApply(Player player, WorldObject pattern, WorldObject target)
        {
            if (!HasLook(pattern))
            {
                player.SendTransientError("This pattern holds no appearance.");
                return;
            }

            var onCharm = target != null && IsQuiverCharm(target);
            var onLauncher = target != null && target.IsAmmoLauncher;
            if (!onCharm && !onLauncher)
            {
                player.SendTransientError("Use the Quiver Pattern on your Universal Quiver Charm or on a bow, crossbow or atlatl.");
                return;
            }

            if (!InMyPossession(player, pattern) || !InMyPossession(player, target))
            {
                player.SendTransientError("The pattern and the item must both be in your possession.");
                return;
            }

            if (pattern.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow) || target.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow))
            {
                player.SendTransientError("You cannot tailor an item that is in the trade window.");
                return;
            }

            // Consume the pattern before anything changes: a pattern can never be used twice. Its properties stay
            // readable in memory after it is destroyed.
            if (!player.TryConsumeFromInventoryWithNetworking(pattern, 1))
            {
                player.SendTransientError("The pattern could not be used. Nothing was changed.");
                return;
            }

            CopyLook(pattern, target);

            var creatureName = pattern.GetProperty(PropertyString.CapturedCreatureName) ?? "the creature";

            // The charm wears its look like a pet device does: the creature's portrait (in place of the charm-marker
            // overlay) and its name. Launchers keep their own name and art; their ID panel shows the arrow look.
            if (onCharm)
                ShowLookOnCharm(player, target, creatureName);

            target.SaveBiotaToDatabase();
            target.ChangesDetected = true;
            player.PlayParticleEffect(PlayScript.EnchantUpPurple, player.Guid);
            player.SendMessage(onCharm
                ? $"Your Universal Quiver Charm now looses the likeness of {creatureName} from an empty ammunition slot."
                : $"Your {target.Name} now looses the likeness of {creatureName}, whatever ammunition it fires.");
            log.Info($"[QuiverLook] {player.Name} applied {creatureName} to {target.Name} (0x{target.Guid.Full:X8}).");
        }

        /// <summary>
        /// The charm's name and art, pushed to the client. With a look: "Universal Quiver Charm (creature)" and the
        /// creature's portrait. Without one (<paramref name="creatureName"/> null, after an Unraveling Kit), or for a look
        /// without a portrait: the charm's own name / art from its weenie.
        /// </summary>
        private static void ShowLookOnCharm(Player player, WorldObject charm, string creatureName)
        {
            var weenie = ACE.Database.DatabaseManager.World.GetCachedWeenie(charm.WeenieClassId);

            if (creatureName != null)
                charm.Name = $"Universal Quiver Charm ({creatureName})";
            else if (weenie?.PropertiesString != null && weenie.PropertiesString.TryGetValue(PropertyString.Name, out var ownName))
                charm.Name = ownName;

            // the creature's portrait is the icon; the charm-marker overlay would cover it, so it goes (player 2026-11-11)
            var portrait = creatureName != null ? charm.GetProperty(PropertyDataId.VisualOverrideIcon) ?? 0 : 0;
            if (portrait != 0)
            {
                charm.IconId = portrait;
                charm.IconOverlayId = null;
            }
            else if (weenie?.PropertiesDID != null)
            {
                // no portrait: back to the charm's own art rather than keep the last creature's
                if (weenie.PropertiesDID.TryGetValue(PropertyDataId.Icon, out var icon))
                    charm.IconId = icon;
                charm.IconOverlayId = weenie.PropertiesDID.TryGetValue(PropertyDataId.IconOverlay, out var overlay) ? overlay : (uint?)null;
            }

            // The client caches inventory names and art: per-property updates, a full object snapshot, and the slot
            // move it performs on a snapshot (the same as PetTailoring / capture do for a pet device).
            player.UpdateProperty(charm, PropertyString.Name, charm.Name);
            player.UpdateProperty(charm, PropertyDataId.Icon, charm.IconId);
            player.UpdateProperty(charm, PropertyDataId.IconOverlay, charm.IconOverlayId ?? 0u);
            player.Session.Network.EnqueueSend(new ACE.Server.Network.GameMessages.Messages.GameMessageUpdateObject(charm));
            if (player.FindObject(charm.Guid.Full, Player.SearchLocations.MyInventory) != null)
                player.MoveItemToFirstContainerSlot(charm);
        }

        // ------------------------------------------------------------------
        // Quiver Unraveling Kit: back to normal
        // ------------------------------------------------------------------

        /// <summary>Quiver Unraveling Kit, sold by Sorrel the Dyer (docs/WCID_ALLOCATION_7878.md).</summary>
        public const uint UnravelKitWcid = 78780434;

        /// <summary>True for a Quiver Unraveling Kit.</summary>
        public static bool IsUnravelKit(WorldObject wo) => wo != null && wo.WeenieClassId == UnravelKitWcid;

        /// <summary>True when a charm or launcher carries anything an Unraveling Kit would remove.</summary>
        private static bool HasQuiverCustomization(WorldObject wo)
            => HasLook(wo) || wo.GetProperty(PropertyFloat.QuiverArrowSizeLevel).HasValue;

        /// <summary>Removes every Quiver look property (the CopyLook list) and the arrow size level.</summary>
        private static void ClearLook(WorldObject wo)
        {
            foreach (var p in LookDids)
                wo.RemoveProperty(p);
            foreach (var p in LookInts)
                wo.RemoveProperty(p);
            foreach (var p in LookFloats)
                wo.RemoveProperty(p);
            foreach (var p in LookStrings)
                wo.RemoveProperty(p);
            wo.RemoveProperty(PropertyFloat.QuiverArrowSizeLevel);
        }

        /// <summary>
        /// A Quiver Unraveling Kit used on the Universal Quiver Charm or a launcher: removes its Quiver look and arrow size,
        /// back to normal (the charm also gets its own name and art back). The kit is consumed before anything changes;
        /// when there is nothing to remove, it is not used.
        /// </summary>
        /// <param name="player">The player tailoring.</param>
        /// <param name="kit">The Quiver Unraveling Kit.</param>
        /// <param name="target">The charm or launcher.</param>
        public static void HandleUnravel(Player player, WorldObject kit, WorldObject target)
        {
            var onCharm = target != null && IsQuiverCharm(target);
            if (!onCharm && (target == null || !target.IsAmmoLauncher))
            {
                player.SendTransientError("Use the Quiver Unraveling Kit on your Universal Quiver Charm or on a bow, crossbow or atlatl.");
                return;
            }

            if (!HasQuiverCustomization(target))
            {
                player.SendTransientError($"The {target.Name} has no Quiver look or arrow size to remove. The kit was not used.");
                return;
            }

            if (!InMyPossession(player, kit) || !InMyPossession(player, target))
            {
                player.SendTransientError("The kit and the item must both be in your possession.");
                return;
            }

            if (kit.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow) || target.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow))
            {
                player.SendTransientError("You cannot tailor an item that is in the trade window.");
                return;
            }

            if (!player.TryConsumeFromInventoryWithNetworking(kit, 1))
            {
                player.SendTransientError("The kit could not be used. Nothing was changed.");
                return;
            }

            ClearLook(target);
            if (onCharm)
                ShowLookOnCharm(player, target, null);

            target.SaveBiotaToDatabase();
            target.ChangesDetected = true;

            player.PlayParticleEffect(PlayScript.EnchantDownPurple, player.Guid);
            player.SendMessage(onCharm
                ? $"You unravel the Quiver look and arrow size from your {target.Name}."
                : $"You unravel the Quiver look and arrow size from your {target.Name}. (An active Universal Quiver Charm's look still shows on its empty-slot shots.)");
            log.Info($"[QuiverLook] {player.Name} unraveled the look from {target.Name} (0x{target.Guid.Full:X8}).");
        }

        // ------------------------------------------------------------------
        // Arrow Enlarging / Shrinking Stones: arrow size only, never the launcher
        // ------------------------------------------------------------------

        /// <summary>Arrow Enlarging / Shrinking Stones, sold by Sorrel the Dyer (docs/WCID_ALLOCATION_7878.md).</summary>
        public const uint ArrowEnlargingStoneWcid = 78780432;
        public const uint ArrowShrinkingStoneWcid = 78780433;

        public const double ArrowMinLevel = 0.5;
        public const double ArrowMaxLevel = 2.0;

        /// <summary>True for an Arrow Enlarging or Arrow Shrinking Stone.</summary>
        public static bool IsArrowStone(WorldObject wo)
            => wo != null && (wo.WeenieClassId == ArrowEnlargingStoneWcid || wo.WeenieClassId == ArrowShrinkingStoneWcid);

        /// <summary>An arrow size level in whole tenths, clamped to the stones' range (NaN = 1.0).</summary>
        private static int ArrowTenths(double level)
            => (int)Math.Round((double.IsNaN(level) ? 1.0 : Math.Clamp(level, ArrowMinLevel, ArrowMaxLevel)) * 10.0, MidpointRounding.AwayFromZero);

        /// <summary>An item's arrow size level (QuiverArrowSizeLevel): 1.0 when no arrow stone was used, always in range.</summary>
        public static double ArrowLevelOf(WorldObject wo)
            => wo == null ? 1.0 : ArrowTenths(wo.GetProperty(PropertyFloat.QuiverArrowSizeLevel) ?? 1.0) / 10.0;

        /// <summary>The level one more stone gives, or false when already at that end. Whole tenths: ten up + ten down = 1.0.</summary>
        public static bool TryStepArrow(double current, bool enlarge, out double next)
        {
            var tenths = ArrowTenths(current) + (enlarge ? 1 : -1);
            next = tenths / 10.0;
            return tenths >= ArrowTenths(ArrowMinLevel) && tenths <= ArrowTenths(ArrowMaxLevel);
        }

        /// <summary>"Arrow look: X." and "Arrow size: 130% of normal." for the ID panel, or null.</summary>
        public static string BuildAppraisalLines(WorldObject wo)
        {
            if (wo == null || (!IsQuiverCharm(wo) && !wo.IsAmmoLauncher))
                return null;

            var lines = new System.Collections.Generic.List<string>();
            if (HasLook(wo))
                lines.Add($"Arrow look: {wo.GetProperty(PropertyString.CapturedCreatureName) ?? "a creature"}.");
            var tenths = ArrowTenths(ArrowLevelOf(wo));
            if (tenths != 10)
                lines.Add($"Arrow size: {tenths * 10}% of normal.");
            return lines.Count > 0 ? string.Join("\n", lines) : null;
        }

        /// <summary>
        /// An Arrow Enlarging / Shrinking Stone used on the Universal Quiver Charm or a launcher: steps that item's
        /// arrow size by a tenth (50%-200%). The launcher itself is never resized. At a limit the stone is not used.
        /// </summary>
        /// <param name="player">The player using the stone.</param>
        /// <param name="stone">The stone.</param>
        /// <param name="target">The charm or launcher.</param>
        public static void HandleArrowStone(Player player, WorldObject stone, WorldObject target)
        {
            var enlarge = stone.WeenieClassId == ArrowEnlargingStoneWcid;

            if (target == null || (!IsQuiverCharm(target) && !target.IsAmmoLauncher))
            {
                player.SendTransientError($"The {stone.Name} only works on your Universal Quiver Charm or on a bow, crossbow or atlatl.");
                return;
            }

            if (!InMyPossession(player, target))
            {
                player.SendTransientError($"The {target.Name} must be in your pack or in your hands.");
                return;
            }

            if (stone.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow) || target.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow))
            {
                player.SendTransientError("You cannot use that on an item in the trade window.");
                return;
            }

            var current = ArrowLevelOf(target);
            if (!TryStepArrow(current, enlarge, out var next))
            {
                player.SendTransientError(enlarge
                    ? $"The arrows of your {target.Name} are already as large as they can be made ({ArrowTenths(ArrowMaxLevel) * 10}%). The stone was not used."
                    : $"The arrows of your {target.Name} are already as small as they can be made ({ArrowTenths(ArrowMinLevel) * 10}%). The stone was not used.");
                return;
            }

            if (!player.TryConsumeFromInventoryWithNetworking(stone, 1))
            {
                player.SendTransientError($"Failed to consume the {stone.Name}.");
                return;
            }

            if (ArrowTenths(next) == 10)
                target.RemoveProperty(PropertyFloat.QuiverArrowSizeLevel);
            else
                target.SetProperty(PropertyFloat.QuiverArrowSizeLevel, next);
            target.SaveBiotaToDatabase();
            target.ChangesDetected = true;

            log.Info($"[QuiverLook] {player.Name} used {stone.Name} on {target.Name} (0x{target.Guid.Full:X8}): arrow size {current:0.0} -> {next:0.0}");

            player.PlayParticleEffect(enlarge ? PlayScript.EnchantUpPurple : PlayScript.EnchantDownPurple, player.Guid);
            player.SendMessage(ArrowTenths(next) == 10
                ? $"The arrows of your {target.Name} are back to their normal size."
                : $"The arrows of your {target.Name} are now {ArrowTenths(next) * 10}% of their normal size.");
        }

        // ------------------------------------------------------------------
        // Firing
        // ------------------------------------------------------------------

        /// <summary>
        /// Dresses a projectile in a stored look: after its physics object is built (the server keeps the arrow's own
        /// collision shape) and before it enters the world (clients draw the look). Its size is the
        /// live universal_quiver_look_size x the admin's per-creature factor x the look item's arrow size level x
        /// <paramref name="extraSize"/> (the firing launcher's arrow size level when the look comes from the charm).
        /// </summary>
        public static bool ApplyToProjectile(WorldObject look, WorldObject proj, double extraSize = 1.0)
        {
            var setup = look?.GetProperty(PropertyDataId.VisualOverrideSetup) ?? 0;
            if (setup == 0 || proj == null)
                return false;

            // the ammo's own look must not leak through
            proj.RemoveProperty(PropertyDataId.ClothingBase);
            proj.RemoveProperty(PropertyDataId.PaletteBase);
            proj.RemoveProperty(PropertyInt.PaletteTemplate);
            proj.RemoveProperty(PropertyFloat.Shade);
            proj.Biota.PropertiesAnimPart?.Clear();
            proj.Biota.PropertiesPalette?.Clear();
            proj.Biota.PropertiesTextureMap?.Clear();

            proj.SetupTableId = setup;

            var paletteBase = look.GetProperty(PropertyDataId.VisualOverridePaletteBase);
            if (paletteBase.HasValue && paletteBase.Value != 0)
                proj.PaletteBaseId = paletteBase.Value;

            var clothingBase = look.GetProperty(PropertyDataId.VisualOverrideClothingBase);
            if (clothingBase.HasValue && DatDatabase.IsClothingBaseId(clothingBase.Value))
                proj.ClothingBase = clothingBase.Value;

            var paletteTemplate = look.GetProperty(PropertyInt.VisualOverridePaletteTemplate);
            if (paletteTemplate.HasValue)
                proj.PaletteTemplate = paletteTemplate.Value;

            var shade = look.GetProperty(PropertyFloat.VisualOverrideShade);
            if (shade.HasValue)
                proj.Shade = (float)shade.Value;

            var size = ServerConfig.universal_quiver_look_size.Value
                * ModelSizeFactor(look.GetProperty(PropertyInt.CapturedCreatureWCID))
                * ArrowLevelOf(look)
                * extraSize;
            proj.ObjScale = ProjectileScale(look.GetProperty(PropertyFloat.VisualOverrideScale), size);

            PetDevice.ApplyCapturedObjDescStrings(proj,
                look.GetProperty(PropertyString.CapturedObjDescAnimParts),
                look.GetProperty(PropertyString.CapturedObjDescPalettes),
                look.GetProperty(PropertyString.CapturedObjDescTextures));

            return true;
        }
    }
}
