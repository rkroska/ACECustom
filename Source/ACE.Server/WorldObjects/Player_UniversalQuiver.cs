using System;
using System.Collections.Generic;

using ACE.Entity.Enum;
using ACE.Server.Factories;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Universal Quiver charm (WCID 78780032, ability 36): swap between bow, crossbow and atlatl without swapping ammo.
    ///
    /// While the charm is active and the ammo slot is EMPTY, the server fires the matching Infinite Deadly Prismatic
    /// projectile (universal_quiver_projectiles) for the wielded launcher. No item is equipped or moved. Ammo the player
    /// equips is always used first, so the charm never changes a setup that already works.
    ///
    /// Why an empty slot rather than a "universal" ammo item: the unmodified retail client requires the equipped ammo's
    /// AmmoType to EQUAL the launcher's (CPlayerSystem::AutoWield / AutoWieldIsLegal) and moves anything else to the pack
    /// before it asks the server. So no single item can stay equipped across a bow / crossbow / atlatl swap. The client
    /// never checks ammo when it enters missile mode or attacks (ClientCombatSystem::ExecuteAttack), so firing with an
    /// empty slot works on retail. Tested on the retail client with vTank 2026-11-11.
    ///
    /// Kill switch: universal_quiver_enabled.
    /// </summary>
    partial class Player
    {
        /// <summary>Virtual ammo objects, one per launcher AmmoType. Never added to inventory or the world.</summary>
        private readonly Dictionary<int, WorldObject> quiverVirtualAmmo = new Dictionary<int, WorldObject>();

        /// <summary>The launcher of the last shot, so the debug log says once per launcher which ammo it used.</summary>
        private uint quiverLastLauncherGuid;

        /// <summary>True when the feature is on and the player's Universal Quiver charm is active.</summary>
        public bool HasUniversalQuiver()
        {
            return ServerConfig.universal_quiver_enabled.Value && HasUniversalQuiverCharm;
        }

        /// <summary>Projectile weenies named in universal_quiver_projectiles that don't exist (looked up and logged once per run).</summary>
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, byte> QuiverMissingProjectiles = new System.Collections.Concurrent.ConcurrentDictionary<uint, byte>();

        /// <summary>
        /// Set when the charm supplied virtual ammo for an empty slot, cleared when the out-of-ammo exit runs: only these
        /// players get that exit when the charm stops covering them (QuiverOutOfAmmo).
        /// </summary>
        private bool quiverCoveredEmptySlot;

        /// <summary>
        /// Missile mode refused for an empty slot while the charm is active: says so when the reason is the prismatic
        /// ammo's wield requirement (not just "out of ammunition"), and forgets that the charm was covering the slot.
        /// </summary>
        private void QuiverExplainNoAmmo(WorldObject launcher)
        {
            quiverCoveredEmptySlot = false;

            if (launcher?.AmmoType == null || !HasUniversalQuiver())
                return;

            if (quiverVirtualAmmo.TryGetValue((int)launcher.AmmoType.Value, out var ammo) && CheckWieldRequirements(ammo) != WeenieError.None)
                Session.Network.EnqueueSend(new ACE.Server.Network.GameEvent.Events.GameEventCommunicationTransientString(Session,
                    $"Your Universal Quiver Charm can't fire {ammo.Name}: you don't meet its wield requirements."));
        }

        /// <summary>Virtual ammo replaced after a universal_quiver_projectiles change: kept (a queued shot may hold one) until logout.</summary>
        private readonly List<WorldObject> quiverRetiredAmmo = new List<WorldObject>();

        /// <summary>At logout: destroys the virtual ammo objects (they never entered the world), so their GUIDs are freed.</summary>
        public void ReleaseQuiverVirtualAmmo()
        {
            foreach (var ammo in quiverVirtualAmmo.Values)
                ammo.Destroy();
            foreach (var ammo in quiverRetiredAmmo)
                ammo.Destroy();

            quiverVirtualAmmo.Clear();
            quiverRetiredAmmo.Clear();
        }

        /// <summary>
        /// A launcher with nothing to fire in missile mode (the charm went off, the kill switch, or the charm left the
        /// pack): the same exit as running out of ammo, the message and a drop to peace mode.
        /// </summary>
        private void QuiverOutOfAmmo()
        {
            // only for a player the charm was firing for: anyone else keeps the old silent attack-done
            if (CombatMode != CombatMode.Missile || !quiverCoveredEmptySlot)
                return;

            quiverCoveredEmptySlot = false;

            Session.Network.EnqueueSend(new ACE.Server.Network.GameEvent.Events.GameEventCommunicationTransientString(Session, "You are out of ammunition!"));
            SetCombatMode(CombatMode.NonCombat);
        }

        /// <summary>
        /// The virtual ammo for this launcher, or null when the player has no active quiver, or the launcher's AmmoType
        /// has no projectile configured (crystal / chorizite launchers are left alone).
        /// </summary>
        public WorldObject GetQuiverVirtualAmmo(WorldObject launcher)
        {
            if (launcher == null || !launcher.IsAmmoLauncher || launcher.AmmoType == null)
                return null;

            if (!HasUniversalQuiver())
                return null;

            var ammoType = (int)launcher.AmmoType.Value;
            var wcid = ACE.Server.Entity.QuiverLook.ProjectileWcid(ammoType);
            if (wcid == null)
                return null;

            if (!quiverVirtualAmmo.TryGetValue(ammoType, out var ammo) || ammo.WeenieClassId != wcid.Value)
            {
                // the projectile setting changed: retire the old object once (a queued shot may still hold it), freed at logout
                if (ammo != null)
                {
                    quiverRetiredAmmo.Add(ammo);
                    quiverVirtualAmmo.Remove(ammoType);
                }

                // a weenie that doesn't exist is looked up and logged once per server run, not on every attack
                if (QuiverMissingProjectiles.ContainsKey(wcid.Value))
                    return null;

                ammo = WorldObjectFactory.CreateNewWorldObject(wcid.Value);
                if (ammo == null)
                {
                    QuiverMissingProjectiles.TryAdd(wcid.Value, 0);
                    log.Warn($"[Quiver] {Name}: projectile weenie {wcid} for AmmoType {ammoType} doesn't exist (universal_quiver_projectiles)");
                    return null;
                }
                quiverVirtualAmmo[ammoType] = ammo;
            }

            // parity with the real ammo: only a player who could wield it (Fletching / Missile Weapons gates) fires it
            if (CheckWieldRequirements(ammo) != WeenieError.None)
                return null;

            quiverCoveredEmptySlot = true;
            return ammo;
        }

        /// <summary>True for a virtual ammo object: it must never be sent to clients or touched as inventory.</summary>
        public bool IsQuiverVirtualAmmo(WorldObject ammo)
        {
            if (ammo == null)
                return false;

            foreach (var virtualAmmo in quiverVirtualAmmo.Values)
            {
                if (ReferenceEquals(virtualAmmo, ammo))
                    return true;
            }
            foreach (var retired in quiverRetiredAmmo)
            {
                if (ReferenceEquals(retired, ammo))
                    return true;
            }
            return false;
        }

        /// <summary>The ammo in the slot, or else the virtual ammo for the wielded launcher, or null.</summary>
        public WorldObject GetEquippedOrQuiverAmmo()
        {
            var ammo = GetEquippedAmmo();
            if (ammo != null || !HasUniversalQuiver())
                return ammo;   // players without an active charm pay nothing more than before

            return GetQuiverVirtualAmmo(GetEquippedMissileLauncher());
        }

        private WorldObject quiverCharmCache;
        private DateTime quiverCharmNextScan;

        /// <summary>
        /// The active Universal Quiver charm item (cached, rescanned at most every 5 seconds), or null when the player
        /// has none active.
        /// </summary>
        /// <returns>The charm, or null.</returns>
        public WorldObject GetActiveQuiverCharm()
        {
            if (!HasUniversalQuiverCharm)
                return null;

            var charm = quiverCharmCache;
            if (charm != null && charm.IsCharmActivated && GetInventoryItem(charm.Guid) != null)
                return charm;

            // a full pack scan, at most every few seconds (shots are frequent, charm changes are rare)
            var now = DateTime.UtcNow;
            if (now < quiverCharmNextScan)
                return null;
            quiverCharmNextScan = now.AddSeconds(5);

            quiverCharmCache = null;
            foreach (var item in GetAllPossessionsDeep())
            {
                if (ACE.Server.Entity.QuiverLook.IsQuiverCharm(item) && item.IsCharmActivated)
                {
                    quiverCharmCache = item;
                    break;
                }
            }
            return quiverCharmCache;
        }

        /// <summary>
        /// Quiver looks (Entity/QuiverLook.cs): dresses a projectile this player launches. The launcher's own look wins,
        /// for every shot from it; otherwise an empty-slot shot takes the active charm's look. Else the ammo's own look.
        /// Arrow size comes from the Arrow Enlarging / Shrinking Stones, never the weapon's Size Stones: the firing
        /// launcher's arrow level always counts, and a charm look also takes the charm's arrow level.
        /// </summary>
        public void ApplyQuiverLook(WorldObject proj, WorldObject launcher, WorldObject ammo)
        {
            // the kill switch stops every Quiver look, the launchers' included
            if (proj == null || !ServerConfig.universal_quiver_enabled.Value)
                return;

            // the look is cosmetic: whatever goes wrong here must never cost the player the shot
            try
            {
                WorldObject source = null;
                if (launcher != null && launcher.IsAmmoLauncher && ACE.Server.Entity.QuiverLook.HasLook(launcher))
                {
                    if (ACE.Server.Entity.QuiverLook.ApplyToProjectile(launcher, proj))
                        source = launcher;
                }
                else if (IsQuiverVirtualAmmo(ammo))
                {
                    var charm = GetActiveQuiverCharm();
                    if (ACE.Server.Entity.QuiverLook.HasLook(charm) && ACE.Server.Entity.QuiverLook.ApplyToProjectile(charm, proj, ACE.Server.Entity.QuiverLook.ArrowLevelOf(launcher)))
                        source = charm;
                }

                QuiverTraceLook(launcher, source, proj);
            }
            catch (Exception ex)
            {
                log.Warn($"[QuiverLook] {Name}: look not applied to a shot from {launcher?.Name}: {ex.Message}");
            }
        }

        private string quiverLastLookTrace;

        /// <summary>Debug log of a shot's look and scale, only when it differs from the last shot's (testing aid).</summary>
        private void QuiverTraceLook(WorldObject launcher, WorldObject source, WorldObject proj)
        {
            if (!log.IsDebugEnabled)
                return;

            var trace = source == null
                ? $"{launcher?.Name}: no look"
                : $"{launcher?.Name}: look from {source.Name} (0x{source.Guid.Full:X8}), setup 0x{proj.SetupTableId:X8}, scale {proj.ObjScale:0.###}";
            if (trace == quiverLastLookTrace)
                return;

            quiverLastLookTrace = trace;
            log.Debug($"[QuiverLook] {Name} shot: {trace}");
        }

        /// <summary>Logs (debug) once per launcher which ammo the shots use, so a test can see the charm at work.</summary>
        private void QuiverLogFirstShot(WorldObject launcher, WorldObject ammo)
        {
            if (launcher == null || ammo == null || launcher.Guid.Full == quiverLastLauncherGuid)
                return;

            quiverLastLauncherGuid = launcher.Guid.Full;

            if (log.IsDebugEnabled && HasUniversalQuiverCharm)
                log.Debug($"[Quiver] {Name}: first shot with {launcher.Name} (AmmoType {launcher.AmmoType}) uses {ammo.Name} ({ammo.WeenieClassId}){(IsQuiverVirtualAmmo(ammo) ? " - virtual, slot empty" : " - equipped")}");
        }
    }
}
