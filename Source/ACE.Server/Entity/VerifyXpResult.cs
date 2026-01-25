namespace ACE.Server.Entity
{
    public class VerifyXpResult(OfflinePlayer player, long calculated, long current)
    {
        public OfflinePlayer Player = player;
        public long Calculated = calculated;
        public long Current = current;
        public long Diff => Current - Calculated;
    }
}
