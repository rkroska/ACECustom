namespace ACE.Server.Entity
{
    public class ItemProfile(int amount, uint objectGuid)
    {
        // original data struct
        public int Amount = amount;      // sent as int, not as uint -- needs to be verified > 0
        public uint ObjectGuid = objectGuid;

        // extended server data
        public uint WeenieClassId;
        public int? Palette;
        public double? Shade;

        /// <summary>
        /// If false, should be rejected as early as possible
        /// </summary>
        public bool IsValidAmount => Amount > 0;
    }
}
