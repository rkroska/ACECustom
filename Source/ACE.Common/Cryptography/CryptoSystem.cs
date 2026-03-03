using System;
using System.Collections.Generic;

namespace ACE.Common.Cryptography
{
    public class CryptoSystem : ISAAC
    {
        public const int MaximumEffortLevel = 1024;

        private System.Threading.Lock _lock = new(); 
        private readonly HashSet<uint> xors = new(MaximumEffortLevel);
        private uint CurrentKey;

        public CryptoSystem(byte[] seed) : base(seed)
        {
            lock (_lock)
            {
                CurrentKey = Next();
            }
        }
        public void ConsumeKey(uint x)
        {
            lock (_lock)
            {
                if (CurrentKey == x)
                {
                    CurrentKey = Next();
                }
                else
                {
                    xors.Remove(x);
                }
            }
        }
        public bool Search(uint x)
        {
            lock (_lock)
            {
                if (CurrentKey == x)
                {
                    return true;
                }
                if (xors.Contains(x))
                {
                    return true;
                }
                int g = xors.Count;
                for (int i = 0; i < MaximumEffortLevel - g; i++)
                {
                    xors.Add(CurrentKey);
                    ConsumeKey(CurrentKey);
                    if (CurrentKey == x)
                        return true;
                }
                return false;
            }
        }
        public new void ReleaseResources()
        {
            lock (_lock)
            {
                xors.Clear();
            }
            base.ReleaseResources();
        }
    }
}
