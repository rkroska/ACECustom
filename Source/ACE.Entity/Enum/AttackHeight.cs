namespace ACE.Entity.Enum
{
    public enum AttackHeight
    {
        High    = 1,
        Medium  = 2,
        Low     = 3
    }

    public static class AttackHeightExtensions
    {
        public static Quadrant ToQuadrant(this AttackHeight attackHeight)
        {
            return attackHeight switch
            {
                AttackHeight.High => Quadrant.High,
                AttackHeight.Medium => Quadrant.Medium,
                AttackHeight.Low => Quadrant.Low,
                _ => Quadrant.None,
            };
        }
    }
}
