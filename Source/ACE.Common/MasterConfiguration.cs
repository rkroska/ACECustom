namespace ACE.Common
{
    public class MasterConfiguration
    {
        public GameConfiguration Server { get; set; }

        public DatabaseConfiguration MySql { get; set; }

        public MetricsConfiguration Metrics { get; set; } = new MetricsConfiguration();

        public OfflineConfiguration Offline { get; set; } = new OfflineConfiguration();

        public ChatConfiguration Chat { get; set; }

        public DDDConfiguration DDD { get; set; } = new DDDConfiguration();
    }
}
