using System.Collections.Generic;
using ACE.Entity.Enum;

namespace ACE.Server.Entity
{
    public class StaminaCost(int burden, float stamina)
    {
        public int Burden = burden;
        public float Stamina = stamina;
    }

    public static class StaminaTable
    {
        public static Dictionary<PowerAccuracy, List<StaminaCost>> Costs;

        static StaminaTable()
        {
            BuildTable();
        }

        public static void BuildTable()
        {
            Costs = [];

            // must be in descending order
            var lowCosts = new List<StaminaCost>
            {
                new(1600, 1.5f),
                new(1200, 1),
                new(700, 1)
            };

            var midCosts = new List<StaminaCost>
            {
                new(1600, 3),
                new(1200, 2),
                new(700, 1)
            };

            var highCosts = new List<StaminaCost>
            {
                new(1600, 6),
                new(1200, 4),
                new(700, 2)
            };

            Costs.Add(PowerAccuracy.Low, lowCosts);
            Costs.Add(PowerAccuracy.Medium, midCosts);
            Costs.Add(PowerAccuracy.High, highCosts);
        }

        public static float GetStaminaCost(PowerAccuracy powerAccuracy, int burden)
        {
            var baseCost = 0.0f;
            var attackCosts = Costs[powerAccuracy];
            foreach (var attackCost in attackCosts)
            {
                if (burden >= attackCost.Burden)
                {
                    var numTimes = burden / attackCost.Burden;
                    baseCost += attackCost.Stamina * numTimes;
                    burden -= attackCost.Burden * numTimes;
                }
            }
            return baseCost;
        }
    }
}
