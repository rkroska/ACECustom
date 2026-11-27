using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.Network
{
    /// <summary>
    /// The last objects a session was told to create, kept so a stuck load can say what the client was sent just
    /// before it stopped answering. A client that chokes on one object's data stops acknowledging packets right after
    /// it; the same object then shows up in every stuck load in that area.
    ///
    /// Fixed size (Capacity entries, a few KB per session); one small locked write per object sent.
    /// </summary>
    public sealed class SentObjectLog
    {
        public const int Capacity = 128;

        public readonly struct Entry
        {
            public Entry(DateTime utc, uint guid, string name, uint weenieClassId, int bytes)
            {
                Utc = utc;
                Guid = guid;
                Name = name;
                WeenieClassId = weenieClassId;
                Bytes = bytes;
            }

            public DateTime Utc { get; }
            public uint Guid { get; }
            public string Name { get; }
            public uint WeenieClassId { get; }
            public int Bytes { get; }
        }

        private readonly Entry[] entries = new Entry[Capacity];
        private readonly object sync = new object();
        private int next;
        private int count;

        public void Add(GameMessageCreateObject message)
        {
            Add(new Entry(DateTime.UtcNow, message.ObjectGuid, message.ObjectName, message.WeenieClassId, (int)message.Data.Length));
        }

        public void Add(Entry entry)
        {
            lock (sync)
            {
                entries[next] = entry;
                next = (next + 1) % Capacity;
                if (count < Capacity)
                    count++;
            }
        }

        /// <summary>The kept entries, oldest first.</summary>
        public List<Entry> Snapshot()
        {
            lock (sync)
            {
                var list = new List<Entry>(count);
                var start = (next - count + Capacity) % Capacity;
                for (var i = 0; i < count; i++)
                    list.Add(entries[(start + i) % Capacity]);
                return list;
            }
        }

        // ---------------------------------------------------------------- stuck-load report (pure, unit tested)

        private static string Describe(Entry e)
            => $"{e.Name ?? "?"} (wcid {e.WeenieClassId}, 0x{e.Guid:X8}, {FormatBytes(e.Bytes)})";

        private static string FormatBytes(long bytes)
            => bytes >= 1024 ? (bytes / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB" : bytes + " B";

        /// <summary>
        /// One log line about a load that never finished: how long since the client last acknowledged a packet, how
        /// much is still unacknowledged, what was sent since it entered portal space (and the largest objects), and the
        /// objects sent from just before its last acknowledgement on - where a client that choked would have stopped.
        /// </summary>
        public static string BuildStuckReport(string playerName, DateTime nowUtc, DateTime? portalEnteredUtc, DateTime? lastAckUtc,
            uint lastAckSequence, int unacknowledgedPackets, IReadOnlyList<Entry> sent, int capacity = Capacity)
        {
            var sb = new StringBuilder();
            sb.Append($"[PORTAL STUCK DIAG] {playerName}: ");

            if (lastAckUtc.HasValue)
                sb.Append($"client last acknowledged {(nowUtc - lastAckUtc.Value).TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)}s ago (seq {lastAckSequence}), ");
            else
                sb.Append("client has not acknowledged any packet, ");
            sb.Append($"{unacknowledgedPackets:N0} packets unacknowledged. ");

            var sinceEntry = portalEnteredUtc.HasValue ? sent.Where(e => e.Utc >= portalEnteredUtc.Value).ToList() : sent.ToList();
            // a full log whose oldest entry is already inside the load means more were sent than were kept
            var more = sent.Count >= capacity && portalEnteredUtc.HasValue && sent[0].Utc >= portalEnteredUtc.Value ? "+" : "";
            sb.Append($"Objects sent since entering portal space: {sinceEntry.Count}{more} ({FormatBytes(sinceEntry.Sum(e => (long)e.Bytes))}{more}). ");

            if (sinceEntry.Count > 0)
                sb.Append("Largest: " + string.Join(", ", sinceEntry.OrderByDescending(e => e.Bytes).Take(3).Select(Describe)) + ". ");

            // from 1 s before the last acknowledgement: objects the client may have been working on when it went quiet
            if (lastAckUtc.HasValue)
            {
                var after = sent.Where(e => e.Utc >= lastAckUtc.Value.AddSeconds(-1)).ToList();
                if (after.Count > 0)
                    sb.Append($"Sent around and after the last acknowledgement ({after.Count}, last 10): " + string.Join(", ", after.Skip(Math.Max(0, after.Count - 10)).Select(Describe)) + ".");
                else
                    sb.Append("Nothing was sent after the last acknowledgement.");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
