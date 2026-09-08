using System;
using System.Collections.Generic;
using System.Numerics;
using ACE.Entity.Enum;
using ACE.Server.Physics.Animation;
using ACE.Server.Physics.Collision;

namespace ACE.Server.Physics.Common
{
    public class LandCell: SortCell
    {
        public List<Polygon> Polygons;
        //public bool InView;

        public LandCell(uint cellID): base(cellID)
        {
            Init();
        }

        public override TransitionState FindCollisions(Transition transition)
        {
            var transitState = FindEnvCollisions(transition);
            if (transitState == TransitionState.OK)
            {
                transitState = base.FindCollisions(transition);
                if (transitState == TransitionState.OK)
                    transitState = FindObjCollisions(transition);
            }
            return transitState;
        }

        public override TransitionState FindEnvCollisions(Transition transition)
        {
            var transitState = check_entry_restrictions(transition);

            if (transitState != TransitionState.OK)
                return transitState;

            var path = transition.SpherePath;

            var blockOffset = LandDefs.GetBlockOffset(path.CheckPos.ObjCellID, ID);
            var localPoint = transition.SpherePath.GlobalLowPoint - blockOffset;

            Polygon walkable = null;
            if (!find_terrain_poly(localPoint, ref walkable))
                return transitState;

            var objInfo = transition.ObjectInfo;

            if (get_block_water_type() == LandDefs.WaterType.EntirelyWater &&
                !objInfo.State.HasFlag(ObjectInfoState.IsViewer) && !objInfo.Object.State.HasFlag(PhysicsState.Missile))
            {
                return TransitionState.Collided;
            }
            var waterDepth = get_water_depth(localPoint);

            var checkPos = new Sphere(path.GlobalSphere[0]);
            checkPos.Center -= LandDefs.GetBlockOffset(path.CheckPos.ObjCellID, ID);

            return objInfo.ValidateWalkable(checkPos, walkable.Plane, WaterType != LandDefs.WaterType.NotWater, waterDepth, transition, ID);
        }

        public static LandCell Get(uint cellID, int? variationId)
        {
            return (LandCell)LScape.get_landcell(cellID, variationId);
        }

        public new void Init()
        {
            base.Init();

            // always 2 polys?
            Polygons = new List<Polygon>(2);
            for (var i = 0; i < 2; i++) Polygons.Add(null);
        }

        public static void add_all_outside_cells(Position position, int numSphere, List<Sphere> spheres, CellArray cellArray, int? variation = null)
        {
            if (cellArray.AddedOutside) return;

            // variation propagation: outdoor neighbor cells must be fetched at the MOVER's variation.
            // A null here creates/fetches the BASE landblock when crossing into an unloaded block on foot
            // (observed 2026-07-18: walking into an unloaded zone landblock at v11 loaded retail content
            // while the v11 instance never spawned). Callers on the movement path pass Position.Variation.
            variation ??= position.Variation;

            if (numSphere != 0)
            {
                foreach (var sphere in spheres)
                {
                    var cellPoint = position.ObjCellID;
                    var center = sphere.Center;

                    if (!LandDefs.AdjustToOutside(ref cellPoint, ref center))
                        break;

                    var point = new Vector2();
                    point.X = center.X - (float)Math.Floor(center.X / 24.0f) * 24.0f;
                    point.Y = center.Y - (float)Math.Floor(center.Y / 24.0f) * 24.0f;
                    var minRad = sphere.Radius;
                    var maxRad = 24.0f - minRad;

                    var lcoord = LandDefs.gid_to_lcoord(cellPoint);
                    if (lcoord != null)
                    {
                        add_outside_cell(cellArray, lcoord.Value, variation);
                        check_add_cell_boundary(cellArray, point, lcoord.Value, minRad, maxRad, variation);
                    }
                }
            }
            else
            {
                if (!LandDefs.AdjustToOutside(position)) return;

                var lcoord = LandDefs.gid_to_lcoord(position.ObjCellID);
                if (lcoord != null)
                    add_outside_cell(cellArray, lcoord.Value, variation);
            }
        }

        public static void add_all_outside_cells(int numParts, List<PhysicsPart> parts, CellArray cellArray, uint id)
        {
            if (cellArray.AddedOutside)
                return;

            cellArray.AddedOutside = true;

            if (numParts == 0)
                return;

            var min_x = 0;
            var min_y = 0;
            var max_x = 0;
            var max_y = 0;

            for (var i = 0; i < numParts; i++)
            {
                var curPart = parts[i];

                var loc = new Position(curPart.Pos);

                if (!LandDefs.AdjustToOutside(loc))
                    continue;

                var _lcoord = LandDefs.gid_to_lcoord(loc.ObjCellID);

                if (_lcoord == null)
                    continue;

                var lcoord = _lcoord.Value;

                var lx = (int)(((loc.ObjCellID & 0xFFFF) - 1) / 8);
                var ly = (int)(((loc.ObjCellID & 0xFFFF) - 1) % 8);

                for (var j = 0; j < numParts; j++)
                {
                    var otherPart = parts[j];

                    if (otherPart == null)
                        continue;

                    // add if missing: otherPart.Always2D, checks degrades.degradeMode != 1

                    var bbox = new BBox();
                    bbox.LocalToGlobal(otherPart.GetBoundingBox(), otherPart.Pos, loc);

                    var min_cx = (int)Math.Floor(bbox.Min.X / 24.0f);
                    var min_cy = (int)Math.Floor(bbox.Min.Y / 24.0f);

                    var max_cx = (int)Math.Floor(bbox.Max.X / 24.0f);
                    var max_cy = (int)Math.Floor(bbox.Max.Y / 24.0f);

                    min_x = Math.Min(min_cx - lx, min_x);
                    min_y = Math.Min(min_cy - ly, min_y);

                    max_x = Math.Max(max_cx - lx, max_x);
                    max_y = Math.Max(max_cy - ly, max_y);
                }

                add_cell_block(min_x + (int)lcoord.X, min_y + (int)lcoord.Y, max_x + (int)lcoord.X, max_y + (int)lcoord.Y, cellArray, id);
            }
        }

        public static void add_cell_block(int min_x, int min_y, int max_x, int max_y, CellArray cellArray, uint id)
        {
            for (var i = min_x; i <= max_x; i++)
            {
                for (var j = min_y; j <= max_y; j++)
                {
                    if (i < 0 || j < 0 || i >= LandDefs.LandLength || j >= LandDefs.LandLength)
                        continue;

                    var ui = (uint)i;
                    var uj = (uint)j;

                    var cellID = (((uj >> 3) | 32 * (ui & 0xFFFFFFF8)) << 16) | ((uj & 7) + 8 * (ui & 7) + 1);

                    // FIXME!
                    if (id >> 16 != cellID >> 16)
                        continue;

                    if (!cellArray.Cells.ContainsKey(cellID))
                    {
                        var cell = LScape.get_landcell(cellID, cellArray.Cells[id].CurLandblock.VariationId);

                        cellArray.add_cell(cellID, cell);
                    }
                }
            }
        }

        public static void add_outside_cell(CellArray cellArray, float _x, float _y, int? variation = null)
        {
            var x = (uint)_x;
            var y = (uint)_y;

            if (x >= 0 && y >= 0 && x < 2040 && y < 2040)
            {
                var cellID = (((y >> 3) | 32 * (x & 0xFFFFFFF8)) << 16) | ((y & 7) + 8 * (x & 7) + 1);
                // variation propagation: was hard-coded null, which created BASE landblocks on foot-crossings
                var landCell = Get(cellID, variation);
                if (landCell != null)
                    cellArray.add_cell(cellID, landCell);
            }
        }

        public static void add_outside_cell(CellArray cellArray, Vector2 lcoord, int? variation = null)
        {
            add_outside_cell(cellArray, lcoord.X, lcoord.Y, variation);
        }

        /// <summary>
        /// Checks if this sphere exceeds the boundaries of the cell
        /// if it does, adds the neighboring cells to cellArray
        /// </summary>
        public static void check_add_cell_boundary(CellArray cellArray, Vector2 point, Vector2 lcoord, float minRad, float maxRad, int? variation = null)
        {
            float x = lcoord.X, y = lcoord.Y;

            if (point.X > maxRad)
            {
                add_outside_cell(cellArray, x + 1, y, variation);
                if (point.Y > maxRad)
                    add_outside_cell(cellArray, x + 1, y + 1, variation);
                if (point.Y < minRad)
                    add_outside_cell(cellArray, x + 1, y - 1, variation);
            }
            if (point.X < minRad)
            {
                add_outside_cell(cellArray, x - 1, y, variation);
                if (point.Y > maxRad)
                    add_outside_cell(cellArray, x - 1, y + 1, variation);
                if (point.Y < minRad)
                    add_outside_cell(cellArray, x - 1, y - 1, variation);
            }
            if (point.Y > maxRad)
                add_outside_cell(cellArray, x, y + 1, variation);

            if (point.Y < minRad)
                add_outside_cell(cellArray, x, y - 1, variation);
        }

        public bool find_terrain_poly(Vector3 origin, ref Polygon walkable)
        {
            for (var i = 0; i < 2; i++)
            {
                if (Polygons[i].point_in_poly2D(origin, Sidedness.Positive))
                {
                    walkable = Polygons[i];
                    return true;
                }
            }
            return false;
        }

        public override void find_transit_cells(int numParts, List<PhysicsPart> parts, CellArray cellArray)
        {
            add_all_outside_cells(numParts, parts, cellArray, ID);
            base.find_transit_cells(numParts, parts, cellArray);
        }

        public override void find_transit_cells(Position position, int numSphere, List<Sphere> sphere, CellArray cellArray, SpherePath path)
        {
            add_all_outside_cells(position, numSphere, sphere, cellArray);
            base.find_transit_cells(position, numSphere, sphere, cellArray, path);
        }

        public override bool point_in_cell(Vector3 point)
        {
            Polygon poly = null;
            return find_terrain_poly(point, ref poly);
        }

        public override bool handle_move_restriction(Transition transition)
        {
            var offset = Pos.GetOffset(transition.SpherePath.CurPos);

            if (offset.Y >= -LandDefs.HalfSquareLength)
            {
                if (offset.Y <= LandDefs.HalfSquareLength)
                {
                    offset.Y = 0;
                    if (offset.X < -LandDefs.HalfSquareLength)
                        offset.X = -1.0f;
                    else
                        offset.X = 1.0f;
                }
                else
                {
                    offset.X = 0;
                    offset.Y = 1.0f;
                }
            }
            else
            {
                offset.X = 0;
                offset.Y = -1.0f;
            }
            var normal = new Vector3(offset.X, offset.Y, 0);

            transition.CollisionInfo.SetCollisionNormal(normal);

            return true;
        }
    }
}
