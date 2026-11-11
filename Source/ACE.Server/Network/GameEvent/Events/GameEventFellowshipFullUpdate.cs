using System;
using System.Collections.Generic;
using ACE.Server.Entity;
using ACE.Server.Network.Structure;
using ACE.Server.WorldObjects;

namespace ACE.Server.Network.GameEvent.Events
{
    public class GameEventFellowshipFullUpdate : GameEventMessage
    {
        private static readonly HashComparer FellowComparer = new HashComparer(16);

        public GameEventFellowshipFullUpdate(Session session)
            : base(GameEventType.FellowshipFullUpdate, GameMessageGroup.UIQueue, session)
        {
            var fellowship = session.Player.Fellowship;

            // left (logout, quit, an Auto Fellow area's Leave on another landblock thread) between the caller's member snapshot and here:
            // an empty update instead of a NullReferenceException on the landblock tick, which nothing catches (review 2026-11-06 -
            // area fellowships of 100 members spread over many threads make this routine)
            if (fellowship == null)
            {
                PackableHashTable.WriteHeader(Writer, 0, FellowComparer.NumBuckets);
                Writer.WriteString16L("");
                Writer.Write(0u);
                Writer.Write(0u); Writer.Write(1u); Writer.Write(0u); Writer.Write(0u);
                Writer.Write(new Dictionary<uint, int>());
                Writer.Write(new Dictionary<string, FellowshipLockData>());
                return;
            }

            var fellows = fellowship.GetFellowshipMembers();

            PackableHashTable.WriteHeader(Writer, fellows.Count, FellowComparer.NumBuckets);

            var sorted = new SortedDictionary<uint, Player>(fellows, FellowComparer);

            foreach (var fellow in sorted.Values)
            {
                WriteFellow(fellow);
            }

            Writer.WriteString16L(fellowship.FellowshipName);
            Writer.Write(fellowship.FellowshipLeaderGuid);
            Writer.Write(Convert.ToUInt32(fellowship.ShareXP));
            Writer.Write(Convert.ToUInt32(/*EvenShare: */true));
            Writer.Write(Convert.ToUInt32(fellowship.Open));

            Writer.Write(Convert.ToUInt32(fellowship.IsLocked));

            Writer.Write(fellowship.DepartedMembers);

            Writer.Write(fellowship.FellowshipLocks);
        }

        public void WriteFellow(Player fellow)
        {
            Writer.WriteGuid(fellow.Guid);

            Writer.Write(0u); // TODO: cpCached - Perhaps cp stored up before distribution?
            Writer.Write(0u); // TODO: lumCached - Perhaps lum stored up before distribution?

            Writer.Write(fellow.Level ?? 1);

            Writer.Write(fellow.Health.MaxValue);
            Writer.Write(fellow.Stamina.MaxValue);
            Writer.Write(fellow.Mana.MaxValue);

            Writer.Write(fellow.Health.Current);
            Writer.Write(fellow.Stamina.Current);
            Writer.Write(fellow.Mana.Current);

            // todo: share loot with this fellow?
            Writer.Write((uint)0x10); // TODO: shareLoot - if 0 then noSharePhatLoot, if 16(0x0010) then sharePhatLoot

            Writer.WriteString16L(fellow.Name);
        }
    }
}
