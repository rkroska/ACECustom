using ACE.Server.Managers;

namespace ACE.Server.Physics.Common
{
    /// <summary>
    /// Runtime-toggle verbose tracing for indoor placement / env-cell resolution (colosseum 0x00B0 by default).
    /// Toggle without restart: <c>/modifybool indoor_spawn_placement_diag_verbose true</c>
    /// </summary>
    public static class IndoorPlacementDiagLogging
    {
        public const ushort ColosseumLandblock = 0x00B0;

        public static bool Enabled => ServerConfig.indoor_spawn_placement_diag_verbose.Value;

        public static bool IsColo(uint fullCellOrBlockCellId) => (fullCellOrBlockCellId >> 16) == ColosseumLandblock;
    }
}
