namespace ACE.Server.Entity
{
    public class BaseDamage(int maxDamage, float variance)
    {
        public int MaxDamage = maxDamage;
        public float Variance = variance;
        public float MinDamage => MaxDamage * (1.0f - Variance);
    }
}
