using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        /// <summary>The stored look text and what it parsed to. Replaced whole, never changed, so any thread can read it.</summary>
        private sealed class DressingRoomCache
        {
            public readonly string Stored;
            public readonly DressingRoomLook Look;

            public DressingRoomCache(string stored, DressingRoomLook look)
            {
                Stored = stored;
                Look = look;
            }
        }

        private volatile DressingRoomCache dressingRoomCache;

        /// <summary>
        /// Re-sends the wealth figure after banked pyreals change. On this server it includes the bank (UpdateCoinValue),
        /// and nothing else refreshes it when only the bank moved.
        /// </summary>
        public void RefreshCoinValueAfterBankChange() => UpdateCoinValue();

        /// <summary>
        /// This character's Dressing Room look, or null when they have none (or the stored text is not a usable look).
        /// Parsed once per stored value: the draw code asks on every ObjDesc. The returned look is shared - do not change it.
        /// </summary>
        public DressingRoomLook GetDressingRoomLook()
        {
            var stored = GetProperty(PropertyString.DressingRoomLook);
            if (string.IsNullOrEmpty(stored))
                return null;

            var cache = dressingRoomCache;
            if (cache != null && cache.Stored == stored)
                return cache.Look;

            var look = DressingRoomLook.Parse(stored);
            if (look == null)
                DressingRoom.ReportUnreadableLook(this, stored);
            dressingRoomCache = new DressingRoomCache(stored, look);
            return look;
        }
    }
}
