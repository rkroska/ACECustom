using System.Collections.Generic;

namespace ACE.Server.Entity
{
    public class AttackQueue
    {

        public Queue<float> PowerAccuracy = [];

        public void Add(float powerAccuracy)
        {
            PowerAccuracy.Enqueue(powerAccuracy);
        }

        public float Fetch()
        {
            if (PowerAccuracy.Count > 1)
                PowerAccuracy.Dequeue();

            if (!PowerAccuracy.TryPeek(out var powerAccuracy))
            {
                return 0.5f;
            }
            return powerAccuracy;
        }

        public void Clear()
        {
            PowerAccuracy.Clear();
        }
    }
}
