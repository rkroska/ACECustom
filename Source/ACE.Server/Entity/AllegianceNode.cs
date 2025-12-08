using ACE.Entity;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ACE.Server.Entity
{
    public class AllegianceNode
    {
        public readonly ObjectGuid PlayerGuid;
        public IPlayer Player => PlayerManager.FindByGuid(PlayerGuid);
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public readonly Allegiance Allegiance;

        public readonly AllegianceNode Monarch;
        public readonly AllegianceNode Patron;
        public Dictionary<uint, AllegianceNode> Vassals;

        public uint Rank;

        public bool IsMonarch => Patron == null;

        public bool HasVassals => TotalVassals > 0;

        public int TotalVassals => Vassals != null ? Vassals.Count : 0;

        public int BuildChainCalls = 0;

        public int TotalFollowers
        {
            get
            {
                var totalFollowers = 0;

                foreach (var vassal in Vassals.Values)
                    totalFollowers += vassal.TotalFollowers + 1;

                return totalFollowers;
            }
        }

        public AllegianceNode(ObjectGuid playerGuid, Allegiance allegiance, AllegianceNode monarch = null, AllegianceNode patron = null)
        {
            PlayerGuid = playerGuid;
            Allegiance = allegiance;
            Monarch = monarch ?? this;
            Patron = patron;
            BuildChainCalls = 0;
        }

        public void BuildChain(Allegiance allegiance, List<IPlayer> players, Dictionary<uint, List<IPlayer>> patronVassals, HashSet<uint> visited = null)
        {
            visited ??= [];

            if (visited.Contains(PlayerGuid.Full))
            {
                // Loop detected! Break the chain by removing this player's patron
                var player = PlayerManager.FindByGuid(PlayerGuid);
                string loopStartName = "unknown";
                if (player != null)
                {
                    // Try to find the player where the loop started
                    loopStartName = player.Name;
                    player.PatronId = null;
                    player.SaveBiotaToDatabase();
                }
                // Find the name of the player who originally started the loop (first in visited)
                string firstInLoopName = "unknown";
                if (visited.Count > 0)
                {
                    var firstGuid = visited.First();
                    var firstPlayer = PlayerManager.FindByGuid(new ObjectGuid(firstGuid));
                    if (firstPlayer != null)
                        firstInLoopName = firstPlayer.Name;
                }
                log.Warn($"[ALERT] Allegiance loop detected and broken between players {loopStartName} and {firstInLoopName} (GUIDs: {PlayerGuid.Full:X8}, {visited.First():X8})");
                return;
            }
            visited.Add(PlayerGuid.Full);

            if (BuildChainCalls > 500)
            {
                log.Error($"AllegianceNode.BuildChain called too many times for {Player.Name} ({PlayerGuid.Full}) in allegiance {allegiance.Name}");
                return;
            }
            BuildChainCalls++;
            patronVassals.TryGetValue(PlayerGuid.Full, out var vassals);

            Vassals = new Dictionary<uint, AllegianceNode>();

            if (vassals != null)
            {
                foreach (var vassal in vassals)
                {
                    try
                    {
                        var node = new AllegianceNode(vassal.Guid, allegiance, Monarch, this);
                        node.BuildChain(allegiance, players, patronVassals, visited);

                        Vassals.Add(vassal.Guid.Full, node);
                    }
                    catch
                    {
                        Console.WriteLine($"Allegiance crashed: {allegiance.Name}, player: {vassal.Name}, monarch: {Monarch.Player.Name}");
                        return;
                    }
                }
            }
            CalculateRank();
        }

        public void CalculateRank()
        {
            // http://asheron.wikia.com/wiki/Rank

            // A player's allegiance rank is a function of the number of Vassals and how they are
            // organized. First, take the two highest ranked vassals. Now the Patron's rank will either be
            // one higher than the lower of the two, or equal to the highest rank vassal, whichever is greater.

            // sort vassals by rank
            var sortedVassals = Vassals.Values.OrderByDescending(v => v.Rank).ToList();

            // get 2 highest rank vassals
            var r1 = sortedVassals.Count > 0 ? sortedVassals[0].Rank : 0;
            var r2 = sortedVassals.Count > 1 ? sortedVassals[1].Rank : 0;

            var lower = Math.Min(r1, r2);
            var higher = Math.Max(r1, r2);

            Rank = Math.Min(10, Math.Max(lower + 1, higher));
        }

        public void Walk(Action<AllegianceNode> action, bool self = true)
        {
            if (self)
                action(this);

            foreach (var vassal in Vassals.Values)
                vassal.Walk(action, true);
        }

        public void ShowInfo(int depth = 0)
        {
            var prefix = "".PadLeft(depth * 2, ' ');
            Console.WriteLine($"{prefix}- {Player.Name}");
            foreach (var vassal in Vassals.Values)
                vassal.ShowInfo(depth + 1);
        }

        public void OnLevelUp()
        {
            // patron = self node
            var patronLevel = Player.Level ?? 1;

            // find vassals who are not passing xp
            foreach (var vassal in Vassals.Values.Where(i => !i.Player.ExistedBeforeAllegianceXpChanges))
            {
                var vassalLevel = vassal.Player.Level ?? 1;

                // check if vassal now meets criteria for passing xp
                if (patronLevel >= vassalLevel)
                    vassal.Player.ExistedBeforeAllegianceXpChanges = true;
            }
        }
    }
}
