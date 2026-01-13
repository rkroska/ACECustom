using System;

namespace ACE.Database
{
    /// <summary>
    /// Standardized save key generation to prevent accidental collisions across subsystems.
    /// All save keys should be generated through this class to ensure consistency and avoid naming conflicts.
    /// </summary>
    public static class SaveKeys
    {
        /// <summary>
        /// Creates a save key for player biota saves (inventory, wielded items, etc.)
        /// Format: character:<id>:player
        /// </summary>
        public static string Player(uint characterId) => $"character:{characterId}:player";
        
        /// <summary>
        /// Creates a save key for character table saves (stats, position, skills, etc.)
        /// Format: character:<id>:character
        /// </summary>
        public static string Character(uint characterId) => $"character:{characterId}:character";
        
        /// <summary>
        /// Creates a save key for item saves that belong to a character
        /// Format: character:<id>:item:<guid>
        /// </summary>
        public static string Item(uint characterId, uint itemGuid) => $"character:{characterId}:item:{itemGuid}";
        
        /// <summary>
        /// Creates a save key for storage transaction saves
        /// Format: character:<id>:storage_tx:<guid>
        /// </summary>
        public static string StorageTx(uint characterId, uint storageGuid) => $"character:{characterId}:storage_tx:{storageGuid}";
        
        /// <summary>
        /// Creates a save key for vendor transaction saves
        /// Format: character:<id>:vendor_tx:<guid>
        /// </summary>
        public static string VendorTx(uint characterId, Guid tx) => $"character:{characterId}:vendor_tx:{tx}";
        
        /// <summary>
        /// Creates a save key for bank transaction saves
        /// Format: character:<id>:bank_tx:<guid>
        /// </summary>
        public static string BankTx(uint characterId, Guid tx) => $"character:{characterId}:bank_tx:{tx}";
        
        /// <summary>
        /// Creates a save key for logout saves (player biota during logout)
        /// Format: character:<id>:logout
        /// </summary>
        public static string Logout(uint characterId) => $"character:{characterId}:logout";
    }
}

