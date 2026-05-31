using System;

using ACE.Server.Physics.Common;
using ACE.Server.WorldObjects;
using log4net;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Optional WARN diagnostics for CreateObject / visibility paths (see <see cref="ServerConfig.visibility_create_object_diag_verbose"/>).
    /// </summary>
    public static class VisibilityCreateObjectDiag
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static void LogAddVisibleObject(Player viewer, Physics.PhysicsObj target, bool added, bool rejectedByClamp, float dist2D, bool wasKnown, bool wasVisible)
        {
            if (!ShouldLog(viewer, dist2D))
                return;

            var clampNote = rejectedByClamp ? "REJECTED_BY_CLAMP" : "CLAMP_PASSED";
            var targetStr = target != null ? FormatTarget(target) : "<null>";
            log.Warn($"[VisibilityCO] AddVisibleObject {clampNote} viewer={FormatViewer(viewer)} target={targetStr} dist2D={dist2D:F1}m clampMax={ObjectMaint.InitialClamp_Dist:F1}m wasKnown={wasKnown} wasVisible={wasVisible} added={added}");
        }

        public static void LogCreateObject(Player viewer, WorldObject target, string path, float dist2D, bool? clampWouldApply = null, string extra = null)
        {
            if (viewer == null || target == null || string.IsNullOrEmpty(path))
                return;

            if (!ShouldLog(viewer, dist2D))
                return;

            var clampPart = clampWouldApply.HasValue
                ? $" clampGate={(clampWouldApply.Value ? "would_apply" : "BYPASSED")}"
                : string.Empty;
            var extraPart = string.IsNullOrEmpty(extra) ? string.Empty : $" {extra}";

            log.Warn($"[VisibilityCO] CreateObject path={path} viewer={FormatViewer(viewer)} target={FormatTarget(target)} dist2D={dist2D:F1}m clampMax={ObjectMaint.InitialClamp_Dist:F1}m{clampPart}{extraPart}");
        }

        public static void LogHandleVisibleCells(Player viewer, int candidateCount, int createCount, int occludedCount, float maxCandidateDist2D)
        {
            if (!ServerConfig.visibility_create_object_diag_verbose.Value || viewer == null)
                return;

            var minDist = ServerConfig.visibility_create_object_diag_min_distance.Value;
            if (minDist > 0 && maxCandidateDist2D < minDist)
                return;

            log.Warn($"[VisibilityCO] handle_visible_cells viewer={FormatViewer(viewer)} candidates={candidateCount} createObjs={createCount} newlyOccluded={occludedCount} maxCandidateDist2D={maxCandidateDist2D:F1}m (outdoor PVS uses 9 landblocks; clamp only on first AddVisibleObject when not known)");
        }

        public static void LogReconcileSummary(Player viewer, int visibleCount, int resendCount, int resendBeyondClamp, float maxResendDist2D)
        {
            if (!ServerConfig.visibility_create_object_diag_verbose.Value || viewer == null)
                return;

            log.Warn($"[VisibilityCO] ReconcileVisibilityAfterArrival viewer={FormatViewer(viewer)} visibleUnclamped={visibleCount} staleResendCO={resendCount} resendBeyondClamp={resendBeyondClamp} maxResendDist2D={maxResendDist2D:F1}m (#467 path: TrackObject bypasses AddVisibleObject clamp)");
        }

        public static float Distance2D(Player viewer, WorldObject target)
        {
            if (viewer?.PhysicsObj?.Position == null || target?.PhysicsObj?.Position == null)
                return -1f;

            return (float)Math.Sqrt(viewer.PhysicsObj.Position.Distance2DSquared(target.PhysicsObj.Position));
        }

        public static float Distance2D(Player viewer, Physics.PhysicsObj target)
        {
            if (viewer?.PhysicsObj?.Position == null || target?.Position == null)
                return -1f;

            return (float)Math.Sqrt(viewer.PhysicsObj.Position.Distance2DSquared(target.Position));
        }

        private static bool ShouldLog(Player viewer, float dist2D)
        {
            if (!ServerConfig.visibility_create_object_diag_verbose.Value || viewer == null)
                return false;

            var minDist = ServerConfig.visibility_create_object_diag_min_distance.Value;
            return minDist <= 0 || dist2D < 0 || dist2D >= minDist;
        }

        private static string FormatViewer(Player viewer) =>
            $"{viewer.Name}({viewer.Guid.Full:X8})";

        private static string FormatTarget(WorldObject target) =>
            $"{target.Name}({target.Guid.Full:X8}) wcid={target.WeenieClassId}";

        private static string FormatTarget(Physics.PhysicsObj target) =>
            target == null ? "<null>" : $"{target.Name}({target.ID:X8})";
    }
}
