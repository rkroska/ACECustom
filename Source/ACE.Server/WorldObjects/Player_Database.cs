using System;
using System.Collections.ObjectModel;
using System.Threading;

using ACE.Common;
using ACE.Database;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        public static readonly long DefaultPlayerSaveIntervalSecs = 300; // default to 5 minutes

        public DateTime CharacterLastRequestedDatabaseSave { get; protected set; }

        /// <summary>
        /// This variable is set to true when a change is made, and set to false before a save is requested.<para />
        /// The primary use for this is to trigger save on add/modify/remove of properties.
        /// </summary>
        public bool CharacterChangesDetected { get; set; }

        /// <summary>
        /// Set to true when SaveCharacter() returns a failure
        /// </summary>
        public bool CharacterSaveFailed { get; set; }

        /// <summary>
        /// Set to true when SaveBiotaToDatabase() returns a failure
        /// </summary>
        public bool BiotaSaveFailed { get; set; }

        /// <summary>
        /// The time period between automatic saving of player character changes
        /// </summary>
        public long PlayerSaveIntervalSecs
        {
            get => PropertyManager.GetLong("player_save_interval", DefaultPlayerSaveIntervalSecs).Item;
        }

        /// <summary>
        /// Best practice says you should use this lock any time you read/write the Character.<para />
        /// <para />
        /// For absolute maximum performance, if you're willing to assume (and risk) the following:<para />
        ///  - that the character in the database will not be modified (in a way that adds or removes properties) outside of ACE while ACE is running with a reference to that character<para />
        ///  - that the character will only be read/modified by a single thread in ACE<para />
        /// You can remove the lock usage for any Get/GetAll Property functions. You would simply use it for Set/Remove Property functions because each of these could end up adding/removing to the collections.<para />
        /// The critical thing is that the collections are not added to or removed from while Entity Framework is iterating over them.<para />
        /// Mag-nus 2018-08-19
        /// </summary>
        public readonly ReaderWriterLockSlim CharacterDatabaseLock = new ReaderWriterLockSlim();

        private void SetPropertiesAtLogOut()
        {
            LogoffTimestamp = Time.GetUnixTime();
            // These properties are used with offline players to determine passup rates
            SetProperty(PropertyInt.CurrentLoyaltyAtLastLogoff, (int)GetCreatureSkill(Skill.Loyalty).Current);
            SetProperty(PropertyInt.CurrentLeadershipAtLastLogoff, (int)GetCreatureSkill(Skill.Leadership).Current);
        }

        /// <summary>
        /// This will make sure a player save happens no later than the current time + seconds
        /// </summary>
        public void RushNextPlayerSave(int seconds)
        {
            if (LastRequestedDatabaseSave.AddSeconds(PlayerSaveIntervalSecs) <= DateTime.UtcNow.AddSeconds(seconds))
                return;

            LastRequestedDatabaseSave = DateTime.UtcNow.AddSeconds(seconds).AddSeconds(-1 * PlayerSaveIntervalSecs);
        }

        /// <summary>
        /// Saves the character to the persistent database. Includes Stats, Position, Skills, etc.<para />
        /// Will also save any possessions that are marked with ChangesDetected.
        /// </summary>
        public void SavePlayerToDatabase(bool duringLogout = false)
        {
            if (CharacterChangesDetected)
                SaveCharacterToDatabase();

            var biotas = new Collection<(Biota biota, ReaderWriterLockSlim rwLock)>();

            SaveBiotaToDatabase(false);
            biotas.Add((Biota, BiotaDatabaseLock));

            foreach (var possession in GetAllPossessions())
            {
                if (possession.ChangesDetected)
                {
                    possession.SaveBiotaToDatabase(false);
                    biotas.Add((possession.Biota, possession.BiotaDatabaseLock));
                }
            }

            var requestedTime = DateTime.UtcNow;

            DatabaseManager.Shard.SaveBiotasInParallel(biotas, result =>
            {
                if (duringLogout)
                {
                    // Don't set the player offline until they have been successfully saved
                    PlayerManager.SwitchPlayerFromOnlineToOffline(this);
                }
                log.Debug($"{Name} has been saved. It took {(DateTime.UtcNow - requestedTime).TotalMilliseconds:N0} ms to process the request.");

                if (!result)
                {
                    // This will trigger a boot on next player tick
                    BiotaSaveFailed = true;
                }
            }, "SavePlayerToDatabase");
        }

        public void SaveCharacterToDatabase(bool duringLogout = false)
        {
            CharacterLastRequestedDatabaseSave = DateTime.UtcNow;
            CharacterChangesDetected = false;

            if (duringLogout)
            {
                DatabaseManager.Shard.SaveCharacter(Character, CharacterDatabaseLock, null);
            }
            else
            {
                DatabaseManager.Shard.SaveCharacter(Character, CharacterDatabaseLock, result =>
                {
                    if (!result)
                    {
                        if (this is Player player)
                        {
                            // This will trigger a boot on next player tick
                            CharacterSaveFailed = true;
                        }
                    }
                });
            }
            
        }
    }
}
