using System.Collections.Generic;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    // Owned by the Player object, not a global GUID cache: logout releases the bank.
    public sealed class NetherFractureState
    {
        internal readonly object Gate = new object();
        internal readonly Queue<(double At, double Amount)> Credits = new Queue<(double, double)>();
        internal readonly HashSet<uint> Armed = new HashSet<uint>();
        internal double Next;
        internal bool Prime(uint target, double now)
        {
            lock (Gate) return now >= Next && Armed.Add(target);
        }
        internal void Credit(uint actual, double now)
        {
            lock (Gate) { Prune(now); Credits.Enqueue((now, actual * NetherFractureMath.BankShare)); }
        }
        internal bool Release(uint target, double now, double multiplier, out double budget)
        {
            lock (Gate)
            {
                Prune(now); budget = 0;
                if (now < Next || !Armed.Remove(target)) return false;
                foreach (var c in Credits) budget += c.Amount;
                budget *= multiplier;
                if (!double.IsFinite(budget) || budget <= 0) return false;
                Credits.Clear(); Next = now + NetherFractureMath.CooldownSeconds;
                return true;
            }
        }
        internal void Prune(double now)
        {
            while (Credits.Count > 0 && now - Credits.Peek().At > NetherFractureMath.BankSeconds) Credits.Dequeue();
        }
    }
    public partial class Player
    {
        internal readonly NetherFractureState NetherFracture = new NetherFractureState();
    }
}
