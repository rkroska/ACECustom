using System.Collections.Generic;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    // Owned by the Player object, not a global GUID cache: logout releases the bank.
    public sealed class NetherFractureState
    {
        internal readonly object Gate = new object();
        internal readonly List<(double Expires, double Amount)> Credits = new List<(double, double)>();   // each credit keeps its zone's window
        internal readonly HashSet<uint> Armed = new HashSet<uint>();
        internal double Next;
        internal bool Prime(uint target, double now)
        {
            lock (Gate) return now >= Next && Armed.Add(target);
        }
        internal void Credit(uint actual, double now, double share = NetherFractureMath.BankShare, double window = NetherFractureMath.BankSeconds)
        {
            lock (Gate) { Prune(now); Credits.Add((now + window, actual * share)); }
        }
        internal bool Release(uint target, double now, double multiplier, out double budget, double cooldown = NetherFractureMath.CooldownSeconds)
        {
            lock (Gate)
            {
                Prune(now); budget = 0;
                if (now < Next || !Armed.Remove(target)) return false;
                foreach (var c in Credits) budget += c.Amount;
                budget *= multiplier;
                if (!double.IsFinite(budget) || budget <= 0) return false;
                Credits.Clear(); Next = now + cooldown;
                return true;
            }
        }
        internal void Prune(double now)
        {
            Credits.RemoveAll(c => now > c.Expires);
        }
    }
    public partial class Player
    {
        internal readonly NetherFractureState NetherFracture = new NetherFractureState();
    }
}
