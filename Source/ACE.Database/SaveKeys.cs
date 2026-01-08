using System;

namespace ACE.Database
{
    /// <summary>
    /// Standardized save key generation to prevent accidental collisions across subsystems.
    /// All save keys should be generated through this class to ensure consistency and avoid naming conflicts.
    /// </summary>
    public static class SaveKeys
    {
        public static string Player(uint playerGuid) => $"player:{playerGuid}";
        public static string Item(uint itemGuid) => $"item:{itemGuid}";
        public static string StorageTx(Guid tx) => $"storage_tx:{tx}";
        public static string VendorTx(Guid tx) => $"vendor_tx:{tx}";
        public static string BankTx(Guid tx) => $"bank_tx:{tx}";
    }
}

