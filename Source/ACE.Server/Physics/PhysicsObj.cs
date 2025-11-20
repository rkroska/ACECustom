using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Physics.Animation;
using ACE.Server.Physics.Collision;
using ACE.Server.Physics.Combat;
using ACE.Server.Physics.Common;
using ACE.Server.Physics.Hooks;
using ACE.Server.Physics.Managers;
using ACE.Server.WorldObjects;

using log4net;

using Landblock = ACE.Server.Physics.Common.Landblock;
using ObjectGuid = ACE.Entity.ObjectGuid;

namespace ACE.Server.Physics
{
    /// <summary>
    /// The base class for all physics objects
    /// </summary>
    public class PhysicsObj
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public uint ID;
        public ObjectGuid ObjID;
        public PartArray PartArray;
        //public Vector3 PlayerVector;
        //public float PlayerDistance;
        public float CYpt;
        //public Sound.SoundTable SoundTable;
        public bool ExaminationObject;
        public ScriptManager ScriptManager;
        //public PhysicsScriptTable PhysicsScriptTable;
        //public PlayScript? DefaultScript;
        //public float? DefaultScriptIntensity;
        //public bool HasDefaultAnimation;
        //public bool HasDefaultScript;
        public PhysicsObj Parent;
        public ChildList Children;
        public Position Position;
        public ObjCell CurCell;
        public Landblock CurLandblock;
        public int NumShadowObjects;
        public Dictionary<uint, ShadowObj> ShadowObjects;
        public PhysicsState State;
        public TransientStateFlags TransientState;
        public float Elasticity;
        //public float Translucency;
        //public float TranslucencyOriginal;
        public float Friction;
        //public float MassInv;
        public MovementManager MovementManager;
        public PositionManager PositionManager;
        public bool LastMoveWasAutonomous;
        //public bool JumpedThisFrame;
        public double UpdateTime;
        public Vector3 Velocity;
        public Vector3 Acceleration;
        public Vector3 Omega;
        //public List<PhysicsObjHook> Hooks;
        public List<DatLoader.Entity.AnimationHook> AnimHooks;
        public float Scale;
        //public float AttackRadius;
        //public DetectionManager DetectionManager;
        //public AttackManager AttackManager;
        public TargetManager TargetManager;
        //public ParticleManager ParticleManager;
        public WeenieObject WeenieObj;
        public Plane ContactPlane;
        public uint ContactPlaneCellID;
        public Vector3 SlidingNormal;
        public Vector3 CachedVelocity;
        public Dictionary<uint, CollisionRecord> CollisionTable;
        public bool CollidingWithEnvironment;
        public int[] UpdateTimes;
        public PhysicsObj ProjectileTarget;
        public double PhysicsTimer_CurrentTime;
        public bool DatObject = false;
        public int Order = 1;

        /// <summary>
        /// This is managed by MovementManager.MotionInterpreter, and should not be updated anywhere else.
        /// </summary>
        public bool IsAnimating;

        // this is used by the 1991 branch to determine when physics updates need to be run
        public bool IsMovingOrAnimating => IsAnimating || !PartArray.Sequence.is_first_cyclic() || CachedVelocity != Vector3.Zero || Velocity != Vector3.Zero ||
            MovementManager.MotionInterpreter.InterpretedState.HasCommands() || MovementManager.MoveToManager.Initialized;

        // server
        public Position RequestPos;

        public string Name
        {
            get
            {
                if (WeenieObj == null || WeenieObj.WorldObject == null)
                    return "NULL";
                else
                    return WeenieObj.WorldObject.Name;
            }
        }

        public CellArray CellArray;
        public ObjectMaint ObjMaint;
        public bool IsPlayer => ID >= 0x50000001 && ID <= 0x5FFFFFFF;

        public static readonly int UpdateTimeLength = 9;

        public bool IsSticky => PositionManager?.StickyManager != null && PositionManager.StickyManager.TargetID != 0;

        public PhysicsObj(int? VariationId)
        {
            //PlayerVector = new Vector3(0, 0, 1);
            //PlayerDistance = float.MaxValue;
            CYpt = float.MaxValue;
            Position = new Position();
            Position.Variation = VariationId;
            Elasticity = PhysicsGlobals.DefaultElasticity;
            //Translucency = PhysicsGlobals.DefaultTranslucency;
            Friction = PhysicsGlobals.DefaultFriction;
            State = PhysicsGlobals.DefaultState;
            //TranslucencyOriginal = PhysicsGlobals.DefaultTranslucency;
            //MassInv = 1.0f / PhysicsGlobals.DefaultMass;
            Velocity = Vector3.Zero;
            Acceleration = Vector3.Zero;
            Omega = Vector3.Zero;
            Scale = PhysicsGlobals.DefaultScale;
            SlidingNormal = Vector3.Zero;
            CachedVelocity = Vector3.Zero;
            //Hooks = new List<PhysicsObjHook>();
            AnimHooks = new List<DatLoader.Entity.AnimationHook>();
            Children = new ChildList();
            ShadowObjects = new Dictionary<uint, ShadowObj>();
            CollisionTable = new Dictionary<uint, CollisionRecord>();
            CellArray = new CellArray();
            UpdateTime = PhysicsTimer.CurrentTime;
            UpdateTimes = new int[UpdateTimeLength];
            PhysicsTimer_CurrentTime = PhysicsTimer.CurrentTime;

            // todo: only allocate these for server objects
            // get rid of 'DatObject', use the existing WeenieObj == null
            WeenieObj = WeenieObject.DummyObject;
            ObjMaint = new ObjectMaint(this);

            if (PhysicsEngine.Instance != null && PhysicsEngine.Instance.Server)
            {
                RequestPos = new Position();
            }
        }


        /// <summary>
        /// Called to completely remove a PhysicsObj from the server
        /// </summary>
        public void DestroyObject()
        {
            leave_cell(false);
            remove_shadows_from_cells();
            leave_world();
            exit_world();

            ObjMaint.DestroyObject();
        }

        public void AddPartToShadowCells(PhysicsPart part)
        {
            if (CurCell != null) part.Pos.ObjCellID = CurCell.ID;
            foreach (var shadowObj in ShadowObjects.Values)
            {
                var shadowCell = shadowObj.Cell;
                if (shadowCell != null)
                {
                    shadowCell.AddPart(part, null,
                        shadowCell.Pos.Frame, ShadowObjects.Count);
                }
            }
        }

        private static ObjCell AdjustPosition(Position position, Vector3 low_pt, bool searchCells)
        {
            var cellID = position.ObjCellID & 0xFFFF;
            //Console.WriteLine("AdjustPosition variation: {0:X4}", position.Variation);
            if ((cellID < 1 || cellID > 0x40) && (cellID < 0x100 || cellID > 0xFFFD) && cellID != 0xFFFF)
                return null;

            if (cellID < 0x100)
            {
                LandDefs.AdjustToOutside(position);
                return ObjCell.GetVisible(position.ObjCellID, position.Variation);
            }

            var visibleCell = (EnvCell)ObjCell.GetVisible(position.ObjCellID, position.Variation);
            if (visibleCell == null) return null;

            var point = position.LocalToGlobal(low_pt);
            var child = visibleCell.find_visible_child_cell(point, searchCells);
            if (child != null)
            {
                position.ObjCellID = child.ID;
                return child;
            }

            if (!visibleCell.SeenOutside)
                return null;

            position.adjust_to_outside();
            return ObjCell.GetVisible(position.ObjCellID, position.Variation);
        }

        public bool CacheHasPhysicsBSP()
        {
            if (PartArray != null && PartArray.CacheHasPhysicsBSP())
            {
                State |= PhysicsState.HasPhysicsBSP;
                return true;
            }
            else
            {
                State &= ~PhysicsState.HasPhysicsBSP;
                return false;
            }
        }

        public void CallPESInternal(uint pes, float curValue)
        {
            if (CurCell != null && curValue >= 1.0f)
                play_script_internal(pes);
        }

        public void CheckForCompletedMotions()
        {
            if (PartArray != null)
                PartArray.CheckForCompletedMotions();
        }

        private static bool CheckPositionInternal(ObjCell newCell, Position newPos, Transition transition, SetPosition setPos)
        {
            transition.InitPath(newCell, null, newPos);

            if (!setPos.Flags.HasFlag(SetPositionFlags.Slide))
                transition.SpherePath.PlacementAllowsSliding = false;

            if (!transition.FindValidPosition()) return false;

            if (setPos.Flags.HasFlag(SetPositionFlags.Slide))
                return true;

            var diff = transition.SpherePath.CurPos.Frame.Origin - newPos.Frame.Origin;

            // should be using Math.Abs(), bug in original?
            if (transition.SpherePath.CurPos.ObjCellID == newCell.ID && diff.X < 0.05f && diff.Y < 0.05f)
            {
                newPos.Frame.Origin = transition.SpherePath.CurPos.Frame.Origin;
                return true;
            }
            return false;
        }

        public WeenieError DoInterpretedMotion(uint motion, MovementParameters movementParams)
        {
            if (PartArray == null)
                return WeenieError.GeneralMovementFailure;

            return PartArray.DoInterpretedMotion(motion, movementParams);
        }

        public WeenieError DoMotion(uint motion, MovementParameters movementParams)
        {
            LastMoveWasAutonomous = true;
            if (MovementManager == null) return WeenieError.NoAnimationTable;
            var mvs = new MovementStruct(MovementType.RawCommand, motion, movementParams);
            return MovementManager.PerformMovement(mvs);
        }

        public TransitionState FindObjCollisions(Transition transition)
        {
            bool ethereal = false;

            if (State.HasFlag(PhysicsState.Ethereal) && State.HasFlag(PhysicsState.IgnoreCollisions))
                return TransitionState.OK;

            if (WeenieObj != null && transition.ObjectInfo.State.HasFlag(ObjectInfoState.IsViewer) && WeenieObj.IsCreature)
                return TransitionState.OK;

            if (State.HasFlag(PhysicsState.Ethereal) || !State.HasFlag(PhysicsState.Static) && transition.ObjectInfo.Ethereal)
            {
                if (transition.SpherePath.StepDown)
                    return TransitionState.OK;
                ethereal = true;
            }
            transition.SpherePath.ObstructionEthereal = ethereal;

            var state = transition.ObjectInfo.State;

            // TODO: reverse this check to make it more readable
            // TODO: investigate not initting WeenieObj for DatObjects
            var exemption = !( /*WeenieObj == null*/ DatObject || !WeenieObj.IsPlayer || !state.HasFlag(ObjectInfoState.IsPlayer) ||
                state.HasFlag(ObjectInfoState.IsImpenetrable) || WeenieObj.IsImpenetrable() ||
                state.HasFlag(ObjectInfoState.IsPK) && WeenieObj.IsPK() || state.HasFlag(ObjectInfoState.IsPKLite) && WeenieObj.IsPKLite());

            var missileIgnore = transition.ObjectInfo.MissileIgnore(this);

            var isCreature = State.HasFlag(PhysicsState.Missile) || WeenieObj != null && WeenieObj.IsCreature;
            //isCreature = false; // hack?

            if (!State.HasFlag(PhysicsState.HasPhysicsBSP) || missileIgnore || exemption)
            {
                if (PartArray == null || PartArray.GetNumCylsphere() == 0 || missileIgnore || exemption)
                {
                    if (PartArray != null && PartArray.GetNumSphere() != 0 && !missileIgnore && !exemption)
                    {
                        var spheres = PartArray.GetSphere();
                        for (var i = 0; i < PartArray.GetNumSphere(); i++)
                        {
                            var intersects = spheres[i].IntersectsSphere(Position, Scale, transition, isCreature);
                            if (intersects != TransitionState.OK)
                            {
                                return FindObjCollisions_Inner(transition, intersects, ethereal, isCreature);
                            }
                        }
                    }
                }
                else
                {
                    if (PartArray != null && PartArray.GetNumCylsphere() != 0 && !missileIgnore && !exemption)
                    {
                        var cylSpheres = PartArray.GetCylSphere();
                        for (var i = 0; i < PartArray.GetNumCylsphere(); i++)
                        {
                            var intersects = cylSpheres[i].IntersectsSphere(Position, Scale, transition);
                            if (intersects != TransitionState.OK)
                            {
                                return FindObjCollisions_Inner(transition, intersects, ethereal, isCreature);
                            }
                        }
                    }
                }
            }
            else if (PartArray != null)
            {
                var collided = PartArray.FindObjCollisions(transition);
                if (collided != TransitionState.OK)
                    return FindObjCollisions_Inner(transition, collided, ethereal, isCreature);
            }

            transition.SpherePath.ObstructionEthereal = false;
            return TransitionState.OK;
        }

        public TransitionState FindObjCollisions_Inner(Transition transition, TransitionState result, bool ethereal, bool isCreature)
        {
            if (!transition.SpherePath.StepDown)
            {
                if (State.HasFlag(PhysicsState.Static))
                {
                    if (!transition.ObjectInfo.State.HasFlag(ObjectInfoState.Contact))
                        transition.CollisionInfo.CollidedWithEnvironment = true;
                }
                else if (ethereal || isCreature && transition.ObjectInfo.State.HasFlag(ObjectInfoState.IgnoreCreatures))
                {
                    result = TransitionState.OK;
                    transition.CollisionInfo.CollisionNormalValid = false;
                    transition.CollisionInfo.AddObject(this, TransitionState.OK);
                }
                else
                    transition.CollisionInfo.AddObject(this, result);
            }
            transition.SpherePath.ObstructionEthereal = false;
            return result;
        }

        public bool IsTouching(PhysicsObj obj,
    Func<Sphere, Sphere, bool> sphereIntersectionCheck,
    Func<Sphere, CylSphere, Vector3, float, bool> cylSphereIntersectionCheck)
        {
            // custom for hotspots

            // possible collision detection object types:
            // - bsp
            // - sphere
            // - cylsphere

            // player has 2 spheres
            // hotspots appear to sphere or cylsphere?

            // ensure same landblock
            // no cross-landblock collision detection here,
            // although it could be added if needed

            if (CurLandblock != obj.CurLandblock)
                return false;

            var pSpheres = PartArray.GetSphere();

            var spheres = obj.PartArray.GetSphere();
            var cylspheres = obj.PartArray.GetCylSphere();

            if (pSpheres.Count == 0 || (spheres.Count == 0 && cylspheres.Count == 0))
                return false;

            foreach (var pSphere in pSpheres)
            {
                foreach (var sphere in spheres)
                {
                    // convert to landblock coordinates
                    var playerSphere = new Sphere(Position.Frame.LocalToGlobal(pSphere.Center), pSphere.Radius);
                    var globSphere = new Sphere(obj.Position.Frame.LocalToGlobal(sphere.Center), sphere.Radius);

                    if (sphereIntersectionCheck(playerSphere, globSphere))
                        return true;
                }

                foreach (var cylsphere in cylspheres)
                {
                    // convert to landblock coordinates
                    var center = Position.Frame.LocalToGlobal(pSphere.Center);
                    var lowpoint = obj.Position.Frame.LocalToGlobal(cylsphere.LowPoint);

                    var disp = center - lowpoint;
                    var radsum = pSphere.Radius + cylsphere.Radius - PhysicsGlobals.EPSILON;

                    if (cylSphereIntersectionCheck(pSphere, cylsphere, disp, radsum))
                        return true;
                }
            }
            return false;
        }

        public bool is_touching(PhysicsObj obj)
        {
            return IsTouching(obj,
                (Sphere s1, Sphere s2) => s1.Intersects(s2),
                (Sphere s, CylSphere cs, Vector3 d, float r) => cs.CollidesWithSphere(s, d, r));
        }

        public bool is_touchingEnragedHotspot(PhysicsObj obj)
        {
            return IsTouching(obj,
                (Sphere s1, Sphere s2) => s1.IntersectsEnragedHotspot(s2),
                (Sphere s, CylSphere cs, Vector3 d, float r) => cs.CollidesWithSphereEnragedHotspot(s, d, r));
        }


        public SetPositionError ForceIntoCell(ObjCell newCell, Position pos)
        {
            if (newCell == null) return SetPositionError.NoCell;
            set_frame(pos.Frame);
            if (CurCell != newCell)
            {
                change_cell(newCell);
                calc_cross_cells(pos.Variation);
            }
            return SetPositionError.OK;
        }

        public double GetAutonomyBlipDistance()
        {
            if ((Position.ObjCellID & 0xFFFF) < 0x100) return 100.0f;

            return IsPlayer ? 25.0f : 20.0f;
        }


        public float GetHeight()
        {
            if (PartArray == null) return 0.0f;
            return PartArray.GetHeight();
        }

        public static PhysicsObj GetObjectA(uint objectID)
        {
            return ServerObjectManager.GetObjectA(objectID);
        }

        public float GetRadius()
        {
            if (PartArray != null)
                return PartArray.GetRadius();
            else
                return 0;
        }

        public float GetPhysicsRadius()
        {
            if (State.HasFlag(PhysicsState.HasPhysicsBSP))
                return 0.0f;

            if (PartArray.GetNumCylsphere() > 0)
            {
                var cylSpheres = PartArray.GetCylSphere();
                return cylSpheres[0].Radius * Scale;
            }
            if (PartArray.GetNumSphere() > 0)
            {
                var spheres = PartArray.GetSphere();
                return spheres[0].Radius * Scale;
            }
            return 0.0f;
        }


        public float GetStepDownHeight()
        {
            if (PartArray == null) return 0;
            return PartArray.GetStepDownHeight();
        }

        public float GetStepUpHeight()
        {
            if (PartArray == null) return 0;
            return PartArray.GetStepUpHeight();
        }

        public void HandleUpdateTarget(TargetInfo targetInfo)
        {
            if (targetInfo.ContextID != 0) return;

            if (MovementManager != null)
                MovementManager.HandleUpdateTarget(targetInfo);
            if (PositionManager != null)
                PositionManager.HandleUpdateTarget(targetInfo);
        }


        public void Hook_AnimDone()
        {
            if (PartArray != null)
                PartArray.AnimationDone(true);
        }

        /// <summary>
        /// Initializes the physics object from defaults in setup
        /// </summary>
        public void InitDefaults(Setup setup)
        {
            if (setup._dat.DefaultScript != 0)
                play_script_internal(setup._dat.DefaultScript);

            if (setup._dat.DefaultMotionTable != 0)
                SetMotionTableID(setup._dat.DefaultMotionTable);

            if (setup._dat.DefaultSoundTable != 0)
            {
                //var qdid = new QualifiedDataID(0x22, setup.DefaultSTableID);
                //SoundTable = (SoundTable)DBObj.Get(qdid);
                //log.Warn($"PhysicsObj has DefaultSTableID, (SoundTable)DBObj.Get(qdid) not implemented yet, qdid = new QualifiedDataID(0x22, {setup.DefaultSTableID});");
            }

            if (setup._dat.DefaultScriptTable != 0)
            {
                //    var qdid = new QualifiedDataID(0x2C, setup.DefaultPhsTableID);
                //    PhysicsScriptTable = (PhysicsScriptTable)DBObj.Get(qdid);
                //log.Warn($"PhysicsObj has DefaultPhsTableID, (PhysicsScriptTable)DBObj.Get(qdid) not implemented yet, qdid = new QualifiedDataID(0x2C, {setup.DefaultPhsTableID});");
            }

            if (State.HasFlag(PhysicsState.Static))
            {
                if (setup._dat.DefaultAnimation != 0)
                    State |= PhysicsState.HasDefaultAnim;

                if (setup._dat.DefaultScript != 0)
                    State |= PhysicsState.HasDefaultScript;

                //PhysicsEngine.AddStaticAnimatingObject(this);
            }
        }


        /// <summary>
        /// Initializes a static or dynamic object from input ID
        /// </summary>
        public bool InitObjectBegin(uint objectIID, bool dynamic)
        {
            ID = objectIID;

            if (!dynamic)
                State |= PhysicsState.Static;
            else
                State &= ~PhysicsState.Static;

            TransientState &= ~TransientStateFlags.Active;
            UpdateTime = PhysicsTimer.CurrentTime;

            return true;
        }

        /// <summary>
        /// Sets the placement frame for a part frame
        /// </summary>
        public bool InitObjectEnd()
        {
            if (PartArray != null)
            {
                PartArray.SetPlacementFrame(0x65);

                if (!State.HasFlag(PhysicsState.ParticleEmitter))
                    PartArray.SetFrame(Position.Frame);
            }
            return true;
        }

        /// <summary>
        /// Initializes a new PartArray from a dataDID
        /// </summary>
        public bool InitPartArrayObject(uint dataDID, bool createParts)
        {
            if (dataDID == 0) return false;     // stru_843D84
            var ethereal = false;

            var divineType = MasterDBMap.DivineType(dataDID);
            if (divineType == 6)
            {
                PartArray = PartArray.CreateMesh(this, dataDID);
            }
            else
            {
                if (divineType != 7)
                {
                    if ((dataDID & 0xFF000000) != 0)
                        return false;

                    ethereal = true;

                    if (!makeAnimObject(dataDID | 0x2000000, createParts))
                        return false;
                }

                if (!ethereal)
                    PartArray = PartArray.CreateSetup(this, dataDID, createParts);
            }

            if (PartArray == null) return false;
            CacheHasPhysicsBSP();
            if (ethereal)
            {
                State |= PhysicsState.Ethereal;
                TransientState &= ~TransientStateFlags.CheckEthereal;
                SetTranslucencyInternal(0.25f);
                State |= PhysicsState.IgnoreCollisions;
            }

            return true;
        }

        /// <summary>
        /// Initializes the motion tables for the physics parts
        /// </summary>
        public void InitializeMotionTables()
        {
            if (PartArray != null)
                PartArray.InitializeMotionTables();
        }

        public InterpretedMotionState InqInterpretedMotionState()
        {
            if (MovementManager == null)
                return null;
            else
                return MovementManager.InqInterpretedMotionState();
        }


        public void InterpolateTo(Position p, bool keepHeading)
        {
            MakePositionManager();
            PositionManager.InterpolateTo(p, keepHeading);
        }

        public bool IsFullyConstrained()
        {
            if (PositionManager == null)
                return false;
            else
                return PositionManager.IsFullyConstrained();
        }

        public bool IsInterpolating()
        {
            if (PositionManager == null)
                return false;
            else
                return PositionManager.IsInterpolating();
        }

        public bool IsMovingTo()
        {
            if (MovementManager == null)
                return false;
            else
                return MovementManager.IsMovingTo();
        }

        public void MakeMovementManager(bool init_motion)
        {
            if (MovementManager != null) return;

            MovementManager = MovementManager.Create(this, WeenieObj);

            if (init_motion)
                MovementManager.EnterDefaultState();

            if (!State.HasFlag(PhysicsState.Static))
            {
                if (!TransientState.HasFlag(TransientStateFlags.Active))
                    UpdateTime = PhysicsTimer.CurrentTime;

                TransientState |= TransientStateFlags.Active;
            }
        }

        public void MakePositionManager()
        {
            if (PositionManager != null) return;

            PositionManager = PositionManager.Create(this);

            if (!State.HasFlag(PhysicsState.Static))
            {
                if (!TransientState.HasFlag(TransientStateFlags.Active))
                    UpdateTime = PhysicsTimer.CurrentTime;

                TransientState |= TransientStateFlags.Active;
            }
        }


        public void MotionDone(uint motion, bool success)
        {
            if (MovementManager != null)
                MovementManager.MotionDone(motion, success);
        }


        public void MoveToObject(PhysicsObj obj, MovementParameters movementParams)
        {
            if (MovementManager == null)
            {
                MovementManager = MovementManager.Create(this, WeenieObj);
                MovementManager.EnterDefaultState();
                if (!State.HasFlag(PhysicsState.Static))
                {
                    if (!TransientState.HasFlag(TransientStateFlags.Active))
                        UpdateTime = PhysicsTimer.CurrentTime;

                    TransientState &= ~TransientStateFlags.Active;
                }
            }

            var height = obj.PartArray != null ? obj.PartArray.GetHeight() : 0;
            var radius = obj.PartArray != null ? obj.PartArray.GetRadius() : 0;
            var parent = obj.Parent != null ? obj.Parent : obj;

            MoveToObject_Internal(obj, parent.ID, radius, height, movementParams);
        }

        public void MoveToObject_Internal(PhysicsObj obj, uint topLevelID, float objRadius, float objHeight, MovementParameters movementParams)
        {
            if (MovementManager == null)
            {
                MovementManager = MovementManager.Create(this, WeenieObj);
                MovementManager.EnterDefaultState();
                if (!State.HasFlag(PhysicsState.Static))
                {
                    if (!TransientState.HasFlag(TransientStateFlags.Active))
                        UpdateTime = PhysicsTimer.CurrentTime;

                    TransientState &= ~TransientStateFlags.Active;
                }
            }
            var mvs = new MovementStruct();
            // packobj vtable
            mvs.TopLevelId = topLevelID;
            mvs.Radius = objRadius;
            mvs.ObjectId = obj.ID;
            mvs.Params = movementParams;
            mvs.Type = MovementType.MoveToObject;
            mvs.Height = objHeight;
            MovementManager.PerformMovement(mvs);
        }

        public void MoveToPosition(Position pos, MovementParameters movementParams)
        {
            var mvs = new MovementStruct();
            mvs.Position = new Position(pos);
            mvs.Type = MovementType.MoveToPosition;
            mvs.Params = movementParams;
            MovementManager.PerformMovement(mvs);
        }

        public void RemoveLinkAnimations()
        {
            if (PartArray != null)
                PartArray.HandleEnterWorld();
        }

        public void RemovePartFromShadowCells(PhysicsPart part)
        {
            if (part == null) return;

            if (CurCell != null) part.Pos.ObjCellID = CurCell.ID;
            foreach (var shadowObj in ShadowObjects.Values)
            {
                if (shadowObj.Cell != null)
                    shadowObj.Cell.RemovePart(part);
            }
        }

        public bool SetMotionTableID(uint mtableID)
        {
            if (PartArray == null) return false;
            if (!PartArray.SetMotionTableID(mtableID)) return false;

            MovementManager = null;
            if (mtableID != 0) MakeMovementManager(true);

            return true;
        }

        public void SetNoDraw(bool noDraw)
        {
            if (PartArray != null)
                PartArray.SetNoDrawInternal(noDraw);
        }

        public SetPositionError SetPosition(SetPosition setPos)
        {
            var transition = Transition.MakeTransition();
            if (transition == null)
            {
                Console.WriteLine("PhysicsObj::SetPosition: MakeTransition failed, transition null");
                return SetPositionError.GeneralFailure;
            }
                

            transition.InitObject(this, ObjectInfoState.Default);

            if (PartArray != null && PartArray.GetNumSphere() != 0)
                transition.InitSphere(PartArray.GetNumSphere(), PartArray.GetSphere(), Scale);
            else
                transition.InitSphere(1, PhysicsGlobals.DummySphere, 1.0f);

            var result = SetPositionInternal(setPos, transition);
            transition.CleanupTransition();

            return result;
        }

        public bool SetPositionInternal(Transition transition)
        {
            var prevOnWalkable = (TransientState & TransientStateFlags.OnWalkable) != 0;
            var transitCell = transition.SpherePath.CurCell;
            var prevContact = (TransientState & TransientStateFlags.Contact) != 0;
            var curPos = transition.SpherePath.CurPos;

            if (transitCell == null)
            {
                prepare_to_leave_visibility();
                store_position(curPos);

                //ObjMaint.GotoLostCell(this, Position.ObjCellID);

                set_active(false);
                return true;
            }

            // modified: maintain consistency for Position.Frame in change_cell
            set_frame(curPos.Frame);

            if (transitCell.VariationId != CurCell?.VariationId)
            {
                change_cell(transitCell);
            }
            else if (transitCell.Equals(CurCell))
            {
                Position.ObjCellID = curPos.ObjCellID;
                if (PartArray != null && !State.HasFlag(PhysicsState.ParticleEmitter))
                    PartArray.SetCellID(curPos.ObjCellID);
                if (Children != null)
                {
                    for (var i = 0; i < Children.NumObjects; i++)
                    {
                        var child = Children.Objects[i];
                        child.Position.ObjCellID = curPos.ObjCellID;
                        if (child.PartArray != null && !child.State.HasFlag(PhysicsState.ParticleEmitter))
                            child.PartArray.SetCellID(curPos.ObjCellID);
                    }
                }
            }
            else
            {
                change_cell(transitCell);
            }

            //set_frame(curPos.Frame);

            var collisions = transition.CollisionInfo;

            ContactPlaneCellID = collisions.ContactPlaneCellID;
            ContactPlane = collisions.ContactPlane;

            if (collisions.ContactPlaneValid)
                TransientState |= TransientStateFlags.Contact;
            else
                TransientState &= ~TransientStateFlags.Contact;

            calc_acceleration();

            if (collisions.ContactPlaneIsWater)
                TransientState |= TransientStateFlags.WaterContact;
            else
                TransientState &= ~TransientStateFlags.WaterContact;

            if (TransientState.HasFlag(TransientStateFlags.Contact))
            {
                if (ContactPlane.Normal.Z < PhysicsGlobals.FloorZ)
                    set_on_walkable(false);
                else
                    set_on_walkable(true);
            }
            else
            {
                TransientState &= ~TransientStateFlags.OnWalkable;

                if (MovementManager != null && prevOnWalkable)
                    MovementManager.LeaveGround();

                calc_acceleration();
            }

            SlidingNormal = collisions.SlidingNormal;

            if (collisions.SlidingNormalValid)
                TransientState |= TransientStateFlags.Sliding;
            else
                TransientState &= ~TransientStateFlags.Sliding;

            handle_all_collisions(collisions, prevContact, prevOnWalkable);

            if (CurCell != null)
            {
                if (State.HasFlag(PhysicsState.HasPhysicsBSP))
                {
                    calc_cross_cells(curPos.Variation);
                    return true;
                }

                if (transition.CellArray.Cells.Count > 0)
                {
                    remove_shadows_from_cells();
                    add_shadows_to_cell(transition.CellArray);

                    return true;
                }
            }
            return true;
        }

        public SetPositionError SetPositionInternal(Position pos, SetPosition setPos, Transition transition)
        {
            if (CurCell == null) prepare_to_enter_world();

            var newCell = AdjustPosition(pos, transition.SpherePath.LocalSphere[0].Center, true);

            if (newCell == null)
            {
                prepare_to_leave_visibility();
                store_position(pos);
                //ObjMaint.GotoLostCell(this, Position.ObjCellID);
                set_active(false);
                return SetPositionError.OK;
            }

            if (WeenieObj != null && (WeenieObj.IsStorage || WeenieObj.IsCorpse))
                return ForceIntoCell(newCell, pos);

            //if (setPos.Flags.HasFlag(SetPositionFlags.DontCreateCells))
            //transition.CellArray.DoNotLoadCells = true;

            if (!CheckPositionInternal(newCell, pos, transition, setPos))
                return handle_all_collisions(transition.CollisionInfo, false, false) ?
                    SetPositionError.Collided : SetPositionError.NoValidPosition;

            if (transition.SpherePath.CurCell == null) return SetPositionError.NoCell;

            // custom:
            // test for non-ethereal spell projectile collision on world entry
            var spellCollide = WeenieObj.WorldObject is SpellProjectile && transition.CollisionInfo.CollideObject.Count > 0 && !PropertyManager.GetBool("spell_projectile_ethereal");

            if (spellCollide)
            {
                // send initial CO as ethereal
                WeenieObj.WorldObject.SetProperty(PropertyBool.Ethereal, true);
            }

            if (entering_world && transition.SpherePath.CurPos.Landblock != pos.Landblock)
            {
                // AdjustToOutside and find_cell_list can inconsistently result in 2 different cells for edges
                // if something directly on a landblock edge has resulted in a different landblock from find_cell_list, discard completely

                // this can also (more legitimately) happen even if the object isn't directly on landblock edge, but is near it
                // an object trying to spawn on a hillside near a landblock edge might get pushed slightly during spawning,
                // resulting in a successful spawn in a neighboring landblock. we don't handle adjustments to the actual landblock reference in here

                // ideally CellArray.LoadCells = false would be passed to find_cell_list to prevent it from even attempting to load an unloaded neighboring landblock

                log.Debug($"{Name} ({ID:X8}) AddPhysicsObj() - {pos.ShortLoc()} resulted in {transition.SpherePath.CurPos.ShortLoc()}, discarding");
                return SetPositionError.NoValidPosition;
            }

            if (!SetPositionInternal(transition))
                return SetPositionError.GeneralFailure;

            if (spellCollide)
                handle_all_collisions(transition.CollisionInfo, false, false);

            return SetPositionError.OK;
        }

        public SetPositionError SetPositionInternal(SetPosition setPos, Transition transition)
        {
            var wo = WeenieObj.WorldObject;

            if (wo == null)
            {
                Console.WriteLine("SetPositionInternal: WorldObject null");
                return SetPositionError.GeneralFailure;
            }
                
            if (setPos.Pos.Variation != null)
            {
                transition.VariationId = setPos.Pos.Variation;
            }
            //if (setPos.Flags.HasFlag(SetPositionFlags.RandomScatter))
            //return SetScatterPositionInternal(setPos, transition);
            if (wo.ScatterPos != null)
            {
                wo.ScatterPos.Flags |= setPos.Flags;
                return SetScatterPositionInternal(wo.ScatterPos, transition);
            }

            // frame ref?
            //Console.WriteLine($"Set position internal - var: {setPos.Pos.Variation}");
            var result = SetPositionInternal(setPos.Pos, setPos, transition);

            if (result != SetPositionError.OK && setPos.Flags.HasFlag(SetPositionFlags.Scatter))
                return SetScatterPositionInternal(setPos, transition);

            return result;
        }

        public SetPositionError SetPositionSimple(Position pos, bool sliding)
        {
            var setPos = new SetPosition();
            setPos.Pos = new Position(pos);
            setPos.Flags = SetPositionFlags.Teleport | SetPositionFlags.SendPositionEvent;

            if (sliding)
                setPos.Flags |= SetPositionFlags.Slide;

            return SetPosition(setPos);
        }


        public void SetScaleStatic(float scale)
        {
            Scale = scale;
            if (PartArray != null)
                PartArray.SetScaleInternal(new Vector3(scale, scale, scale));
        }

        private static float ScatterThreshold_Z = 10.0f;

        public SetPositionError SetScatterPositionInternal(SetPosition setPos, Transition transition)
        {
            var result = SetPositionError.GeneralFailure;

            for (var i = 0; i < setPos.NumTries; i++)
            {
                var newPos = new Position(setPos.Pos);

                newPos.Frame.Origin.X += (float)ThreadSafeRandom.Next(-1.0f, 1.0f) * setPos.RadX;
                newPos.Frame.Origin.Y += (float)ThreadSafeRandom.Next(-1.0f, 1.0f) * setPos.RadY;

                // customized
                if ((newPos.ObjCellID & 0xFFFF) < 0x100)
                {
                    newPos.Frame.Origin.X = Math.Clamp(newPos.Frame.Origin.X, 0.5f, 191.5f);
                    newPos.Frame.Origin.Y = Math.Clamp(newPos.Frame.Origin.Y, 0.5f, 191.5f);
                }

                // get cell for this position
                var indoors = (newPos.ObjCellID & 0xFFFF) >= 0x100;
                if ((newPos.ObjCellID & 0xFFFF) < 0x100)
                {
                    LandDefs.AdjustToOutside(newPos);

                    // lets only call get_landcell once
                    var objCell = LScape.get_landcell(newPos.ObjCellID, newPos.Variation);

                    // ensure walkable slope
                    var landcell = objCell as LandCell;

                    Polygon walkable = null;
                    landcell.find_terrain_poly(newPos.Frame.Origin, ref walkable);
                    if (walkable == null || !is_valid_walkable(walkable.Plane.Normal)) continue;

                    // account for buildings
                    // if original position was outside, and scatter position is in a building, should we even try to spawn?
                    // compare: rabbits occasionally spawning in buildings in yaraq,
                    // vs. lich tower @ 3D31FFFF

                    if (objCell is not SortCell sortCell || !sortCell.has_building())
                    {
                        // set to ground pos
                        var landblock = LScape.get_landblock(newPos.ObjCellID, newPos.Variation);
                        var groundZ = landblock.GetZ(newPos.Frame.Origin) + 0.05f;

                        if (Math.Abs(newPos.Frame.Origin.Z - groundZ) > ScatterThreshold_Z)
                            log.Debug($"{Name} ({ID:X8}).SetScatterPositionInternal() - tried to spawn outdoor object @ {newPos} ground Z {groundZ} (diff: {newPos.Frame.Origin.Z - groundZ}), investigate ScatterThreshold_Z");
                        else
                            newPos.Frame.Origin.Z = groundZ;

                    }
                    //else
                    //indoors = true;

                    /*if (sortCell != null && sortCell.has_building())
                    {
                        var building = sortCell.Building;

                        var minZ = building.GetMinZ();

                        if (minZ > 0 && minZ < float.MaxValue)
                            newPos.Frame.Origin.Z += minZ;

                        //indoors = true;
                    }*/
                }
                if (indoors)
                {
                    var landblock = LScape.get_landblock(newPos.ObjCellID, newPos.Variation);
                    var envcells = landblock.get_envcells();
                    var found = false;
                    foreach (var envCell in envcells)
                    {
                        if (envCell.point_in_cell(newPos.Frame.Origin))
                        {
                            newPos.ObjCellID = envCell.ID;
                            found = true;
                            break;
                        }
                    }
                    if (!found) continue;
                }

                result = SetPositionInternal(newPos, setPos, transition);
                if (result == SetPositionError.OK) break;
            }

            //if (result != SetPositionError.OK)
            //Console.WriteLine($"Couldn't spawn {Name} after {setPos.NumTries} retries @ {setPos.Pos}");

            return result;
        }

        public void SetTranslucencyInternal(float translucency)
        {
            if (PartArray != null)
                PartArray.SetTranslucencyInternal(translucency);
        }

        public bool ShouldDrawParticles(float degradeDistance)
        {
            if (!ExaminationObject) return true;
            return !(CYpt > degradeDistance || CurCell == null /*|| vftable unknown release */);
        }

        public void StopCompletely(bool sendEvent)
        {
            if (MovementManager == null) return;
            var mvs = new MovementStruct(MovementType.StopCompletely);
            MovementManager.PerformMovement(mvs);
        }

        public void StopCompletely_Internal()
        {
            if (PartArray != null)
                PartArray.StopCompletelyInternal();
        }

        public void StopInterpolating()
        {
            if (PositionManager != null)
                PositionManager.StopInterpolating();
        }

        public WeenieError StopInterpretedMotion(uint motion, MovementParameters movementParams)
        {
            if (PartArray == null) return WeenieError.GeneralMovementFailure;
            return PartArray.StopInterpretedMotion(motion, movementParams);
        }

        public WeenieError StopMotion(uint motion, MovementParameters movementParams, bool sendEvent)
        {
            LastMoveWasAutonomous = true;
            if (MovementManager == null) return WeenieError.NoAnimationTable;
            var mvs = new MovementStruct(MovementType.StopRawCommand);
            mvs.Motion = motion;
            mvs.Params = movementParams;
            return MovementManager.PerformMovement(mvs);
        }

        public bool TurnToObject(PhysicsObj obj, MovementParameters movementParams)
        {
            if (obj == null) return false;

            var parent = obj.Parent != null ? obj.Parent : obj;

            TurnToObject_Internal(obj.ID, parent.ID, movementParams);

            return true;
        }

        public void TurnToObject_Internal(uint objectID, uint topLevelID, MovementParameters movementParams)
        {
            if (MovementManager == null)
            {
                // refactor into common method
                MovementManager = MovementManager.Create(this, WeenieObj);
                MovementManager.EnterDefaultState();
                if (!State.HasFlag(PhysicsState.Static))
                {
                    if (!TransientState.HasFlag(TransientStateFlags.Active))
                        UpdateTime = PhysicsTimer.CurrentTime;

                    TransientState &= ~TransientStateFlags.Active;
                }
            }
            var mvs = new MovementStruct(MovementType.TurnToObject);
            mvs.ObjectId = objectID;
            mvs.TopLevelId = topLevelID;
            mvs.Params = movementParams;
            MovementManager.PerformMovement(mvs);
        }

        public void UpdateChild(PhysicsObj childObj, int partIdx, AFrame childFrame)
        {
            AFrame frame;
            if (partIdx >= PartArray.NumParts)
            {
                frame = AFrame.Combine(Position.Frame, childFrame);
            }
            else
            {
                frame = AFrame.Combine(PartArray.Parts[partIdx].Pos.Frame, childFrame);
            }

            childObj.set_frame(frame);

            if (childObj.ScriptManager != null)
            {
                childObj.ScriptManager.UpdateScripts();
            }
        }


        public void UpdateChildrenInternal()
        {
            if (PartArray == null || Children?.NumObjects == 0) return;

            var childrenObjects = Children.Objects;
            var childrenPartNumbers = Children.PartNumbers;
            var childrenFrames = Children.Frames;

            for (var i = 0; i < childrenObjects.Count; i++)
                UpdateChild(childrenObjects[i], childrenPartNumbers[i], childrenFrames[i]);
        }

        public int InitialUpdates;

        public void UpdateObjectInternal(double quantum)
        {
            if ((TransientState & TransientStateFlags.Active) == 0 || CurCell == null)
                return;

            if ((TransientState & TransientStateFlags.CheckEthereal) != 0)
                set_ethereal(false, false);

            var newPos = new Position(Position.ObjCellID)
            {
                Variation = Position.Variation
            };
            UpdatePositionInternal(quantum, ref newPos.Frame);

            if (PartArray != null && PartArray.GetNumSphere() != 0)
            {
                if (newPos.Frame.Equals(Position.Frame))
                {
                    CachedVelocity = Vector3.Zero;
                    set_frame(newPos.Frame);
                    InitialUpdates++;
                }
                else
                {
                    if ((State & PhysicsState.AlignPath) != 0)
                    {
                        var diff = newPos.Frame.Origin - Position.Frame.Origin;
                        newPos.Frame.set_vector_heading(Vector3.Normalize(diff));
                    }
                    else if ((State & PhysicsState.Sledding) != 0 && Velocity != Vector3.Zero)
                        newPos.Frame.set_vector_heading(Vector3.Normalize(Velocity));

                    if (GetBlockDist(Position, newPos) > 1)
                    {
                        log.Warn($"WARNING: failed transition for {Name} from {Position} to {newPos}");
                        return;
                    }

                    var transit = transition(Position, newPos, false);


                    // temporarily modified while debug path is examined
                    if (transit?.SpherePath.CurCell != null)
                    {
                        CachedVelocity = Position.GetOffset(transit.SpherePath.CurPos) / (float)quantum;

                        SetPositionInternal(transit);
                    }
                    else
                    {
                        if (IsPlayer)
                            log.Debug($"{Name} ({ID:X8}).UpdateObjectInternal({quantum}) - failed transition from {Position} to {newPos}");
                        else if (transit != null && transit.SpherePath.CurCell == null)
                            log.Warn($"{Name} ({ID:X8}).UpdateObjectInternal({quantum}) - avoided CurCell=null from {Position} to {newPos}");

                        newPos.Frame.Origin = Position.Frame.Origin;
                        set_initial_frame(newPos.Frame);
                        CachedVelocity = Vector3.Zero;
                    }
                }
            }
            else
            {
                if (MovementManager == null && (TransientState & TransientStateFlags.OnWalkable) != 0)
                    TransientState &= ~TransientStateFlags.Active;

                newPos.Frame.Origin = Position.Frame.Origin;
                set_frame(newPos.Frame);
                CachedVelocity = Vector3.Zero;
                InitialUpdates++;
            }

            TargetManager?.HandleTargetting();
            MovementManager?.UseTime();
            PartArray?.HandleMovement();
            PositionManager?.UseTime();
            ScriptManager?.UpdateScripts();
        }

        public static int GetBlockDist(Position a, Position b)
        {
            // protection, figure out FastTeleport state
            if (a == null || b == null)
                return 0;

            return GetBlockDist(a.ObjCellID, b.ObjCellID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetBlockDist(uint a, uint b)
        {
            int lbx_a = (int)(a >> 24);
            int lby_a = (int)((a >> 16) & 0xFF);

            int lbx_b = (int)(b >> 24);
            int lby_b = (int)((b >> 16) & 0xFF);

            int dx = Math.Abs(lbx_a - lbx_b);
            int dy = Math.Abs(lby_a - lby_b);

            return Math.Max(dx, dy);
        }


        /// <summary>
        /// This is for legacy movement system
        /// </summary>
        public bool UpdateObjectInternalServer(double quantum)
        {
            //var offsetFrame = new AFrame();
            //UpdatePhysicsInternal((float)quantum, ref offsetFrame);
            if (GetBlockDist(Position, RequestPos) > 1)
            {
                log.Warn($"WARNING: failed transition for {Name} from {Position} to {RequestPos}");
                return false;
            }

            var requestCell = RequestPos.ObjCellID;

            var transit = transition(Position, RequestPos, false);
            if (transit != null)
            {
                CachedVelocity = Position.GetOffset(transit.SpherePath.CurPos) / (float)quantum;
                SetPositionInternal(transit);
            }
            else
                log.Debug($"{Name}.UpdateObjectInternalServer({quantum}) - failed transition from {Position} to {RequestPos}");

            //if (DetectionManager != null) DetectionManager.CheckDetection();

            if (TargetManager != null) TargetManager.HandleTargetting();

            if (MovementManager != null) MovementManager.UseTime();

            if (PartArray != null) PartArray.HandleMovement();

            if (PositionManager != null) PositionManager.UseTime();

            //if (ParticleManager != null) ParticleManager.UpdateParticles();

            if (ScriptManager != null) ScriptManager.UpdateScripts();
            //Console.WriteLine($"UpdateObjectInternalServer: {Name} ({ID:X8}) {requestCell >> 16} -> {CurCell?.ID >> 16}");
            return requestCell >> 16 != 0x18A || CurCell?.ID >> 16 == requestCell >> 16;
        }


        public void UpdatePhysicsInternal(float quantum, ref AFrame frameOffset)
        {
            var velocity_mag2 = Velocity.LengthSquared();

            if (velocity_mag2 <= 0.0f)
            {
                if (MovementManager == null && TransientState.HasFlag(TransientStateFlags.OnWalkable))
                    TransientState &= ~TransientStateFlags.Active;
            }
            else
            {
                if (velocity_mag2 > PhysicsGlobals.MaxVelocitySquared)
                {
                    Velocity = Vector3.Normalize(Velocity) * PhysicsGlobals.MaxVelocity;
                    velocity_mag2 = PhysicsGlobals.MaxVelocitySquared;
                }
                // todo: collision normals
                calc_friction(quantum, velocity_mag2);

                if (velocity_mag2 - PhysicsGlobals.SmallVelocitySquared < PhysicsGlobals.EPSILON)
                    Velocity = Vector3.Zero;

                var movement = Acceleration * 0.5f * quantum * quantum + Velocity * quantum;
                frameOffset.Origin += movement;
            }

            Velocity += Acceleration * quantum;
            frameOffset.GRotate(Omega * quantum);
        }

        public void UpdatePositionInternal(double quantum, ref AFrame newFrame)
        {
            var offsetFrame = new AFrame();

            if (!State.HasFlag(PhysicsState.Hidden))
            {
                if (PartArray != null) PartArray.Update(quantum, ref offsetFrame);

                if (TransientState.HasFlag(TransientStateFlags.OnWalkable))
                    offsetFrame.Origin *= Scale;
                else
                    offsetFrame.Origin *= 0.0f;     // OnWalkable getting reset?
            }
            if (PositionManager != null)
                PositionManager.AdjustOffset(offsetFrame, quantum);

            newFrame = AFrame.Combine(Position.Frame, offsetFrame);

            if (!State.HasFlag(PhysicsState.Hidden))
                UpdatePhysicsInternal((float)quantum, ref newFrame);

            process_hooks();
        }


        public void add_anim_hook(DatLoader.Entity.AnimationHook hook)
        {
            AnimHooks.Add(hook);
        }


        public bool add_child(PhysicsObj obj, int partIdx, AFrame frame)
        {
            if (obj.Equals(this)) return false;

            if (PartArray == null || partIdx != -1 && partIdx >= PartArray.NumParts)
                return false;

            if (Children == null) Children = new ChildList();
            Children.AddChild(obj, frame, partIdx, 0);
            return true;
        }

        public void add_obj_to_cell(ObjCell newCell, AFrame newFrame, int? variation)
        {
            enter_cell(newCell);

            Position.Frame = newFrame;
            if (PartArray != null && !State.HasFlag(PhysicsState.ParticleEmitter))
                PartArray.SetFrame(Position.Frame);

            UpdateChildrenInternal();
            //Console.WriteLine($"add_obj_to_cell v:{variation}");
            calc_cross_cells_static(variation);
        }

        public void add_particle_shadow_to_cell()
        {
            NumShadowObjects = 1;

            var shadowObj = new ShadowObj(this, CurCell);
            ShadowObjects.Add(1, shadowObj);

            if (PartArray != null)
                PartArray.AddPartsShadow(CurCell, 1);
        }

        public void add_shadows_to_cell(CellArray cellArray)
        {
            if (State.HasFlag(PhysicsState.ParticleEmitter))
                add_particle_shadow_to_cell();
            else
            {
                foreach (var cell in cellArray.Cells.Values)
                {
                    if (cell == null) continue; // fixme

                    var shadowObj = new ShadowObj(this, cell);
                    ShadowObjects[cell.ID] = shadowObj;

                    cell.AddShadowObject(shadowObj);

                    if (PartArray != null)
                        PartArray.AddPartsShadow(cell, NumShadowObjects);

                }
            }
            if (Children != null)
            {
                foreach (var child in Children.Objects)
                    child.add_shadows_to_cell(cellArray);
            }
        }

        public void add_voyeur(uint objectID, float radius, double quantum)
        {
            if (TargetManager == null) TargetManager = new TargetManager(this);
            TargetManager.AddVoyeur(objectID, radius, quantum);
        }

        public void calc_acceleration()
        {
            if (TransientState.HasFlag(TransientStateFlags.Contact) && TransientState.HasFlag(TransientStateFlags.OnWalkable))
                Omega = Acceleration = Vector3.Zero;
            else
            {
                if (State.HasFlag(PhysicsState.Gravity))
                    Acceleration = new Vector3(0, 0, PhysicsGlobals.Gravity);
                else
                    Acceleration = Vector3.Zero;
            }
        }

        public void calc_cross_cells(int? variation)
        {
            CellArray.SetDynamic();

            if (State.HasFlag(PhysicsState.HasPhysicsBSP))
                find_bbox_cell_list(CellArray);
            else
            {
                if (PartArray != null && PartArray.GetNumCylsphere() != 0)
                    ObjCell.find_cell_list(Position, PartArray.GetNumCylsphere(), PartArray.GetCylSphere(), CellArray, null, variation);
                else
                {
                    // added sorting sphere null check
                    var sphere = PartArray != null && PartArray.Setup.SortingSphere != null ? PartArray.GetSortingSphere() : PhysicsGlobals.DummySphere;
                    ObjCell.find_cell_list(Position, sphere, CellArray, null, variation);
                }
            }
            remove_shadows_from_cells();
            add_shadows_to_cell(CellArray);
        }

        public void calc_cross_cells_static(int? variation)
        {
            CellArray.SetStatic();

            if (PartArray != null && PartArray.GetNumCylsphere() != 0 && !State.HasFlag(PhysicsState.HasPhysicsBSP))
                ObjCell.find_cell_list(Position, PartArray.GetNumCylsphere(), PartArray.GetCylSphere(), CellArray, null, variation);
            else
                find_bbox_cell_list(CellArray);

            remove_shadows_from_cells();
            add_shadows_to_cell(CellArray);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void calc_friction(float quantum, float velocity_mag2)
        {
            if (!TransientState.HasFlag(TransientStateFlags.OnWalkable)) return;

            var angle = Vector3.Dot(Velocity, ContactPlane.Normal);
            if (angle >= 0.25f) return;

            Velocity -= ContactPlane.Normal * angle;

            var friction = Friction;
            if (State.HasFlag(PhysicsState.Sledding))
            {
                if (velocity_mag2 < 1.5625f)
                    friction = 1.0f;

                else if (velocity_mag2 >= 6.25f && ContactPlane.Normal.Z > 0.99999536f)
                    friction = 0.2f;
            }

            var scalar = MathF.Pow(1.0f - friction, quantum);
            Velocity *= scalar;
        }

        public void cancel_moveto()
        {
            if (MovementManager != null)
                MovementManager.CancelMoveTo(WeenieError.ActionCancelled);
        }

        public void change_cell(ObjCell newCell)
        {
            if (PhysicsEngine.Instance.Server)
            {
                change_cell_server(newCell);
                return;
            }

            if (CurCell != null) leave_cell(true);
            if (newCell == null)
            {
                Position.ObjCellID = 0;
                if (PartArray != null && !State.HasFlag(PhysicsState.ParticleEmitter))
                    PartArray.SetCellID(0);

                CurCell = null;
            }
            else
                enter_cell(newCell);
        }

        public void change_cell_server(ObjCell newCell)
        {
            if (CurCell != null) leave_cell(true);
            if (newCell == null)
            {
                Position.ObjCellID = 0;
                if (PartArray != null && !State.HasFlag(PhysicsState.ParticleEmitter))
                    PartArray.SetCellID(0);

                CurCell = null;
            }
            else
                enter_cell_server(newCell);
        }


        public bool check_collision(PhysicsObj obj)
        {
            if (State.HasFlag(PhysicsState.Static))
                return false;

            var trans = Transition.MakeTransition();
            var objectInfo = get_object_info(trans, false);
            trans.InitObject(this, objectInfo.State);

            if (PartArray != null && PartArray.GetNumSphere() != 0)
                trans.InitSphere(PartArray.GetNumSphere(), PartArray.GetSphere(), Scale);
            else
                trans.InitSphere(1, PhysicsGlobals.DummySphere, Scale);

            trans.InitPath(CurCell, Position, Position);
            return trans.CheckCollisions(obj);
        }

        public bool check_contact(bool contact)
        {
            if (TransientState.HasFlag(TransientStateFlags.Contact) && Vector3.Dot(Velocity, ContactPlane.Normal) > PhysicsGlobals.EPSILON)
                return false;
            else
                return contact;
        }
        public void clear_target()
        {
            if (TargetManager != null) TargetManager.ClearTarget();
        }

        public void clear_transient_states()
        {
            TransientState &= ~TransientStateFlags.Contact;
            calc_acceleration();
            var walkable = TransientState.HasFlag(TransientStateFlags.OnWalkable);
            TransientState &= ~(TransientStateFlags.OnWalkable | TransientStateFlags.WaterContact);
            if (MovementManager != null && walkable) MovementManager.LeaveGround();
            calc_acceleration();
            TransientState = 0;
        }

        /// <summary>
        /// This is to mitigate possible decal crashes w/ CO messages being sent
        /// for objects when the client landblock is very early in the loading state
        /// </summary>
        private static TimeSpan TeleportCreateObjectDelay = TimeSpan.FromSeconds(1);

        public void enqueue_objs(IEnumerable<PhysicsObj> newlyVisible)
        {
            if (!IsPlayer || WeenieObj.WorldObject is not Player player)
                return;

            if (DateTime.UtcNow - player.LastTeleportTime < TeleportCreateObjectDelay)
            {
                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(TeleportCreateObjectDelay.TotalSeconds);
                actionChain.AddAction(player, ActionType.PhysicsObj_TrackObjects, () =>
                {
                    foreach (var obj in newlyVisible)
                    {
                        var wo = obj.WeenieObj.WorldObject;
                        if (wo != null)
                            player.TrackObject(wo, true);
                    }
                });
                actionChain.EnqueueChain();
            }
            else
            {
                foreach (var obj in newlyVisible)
                {
                    var wo = obj.WeenieObj.WorldObject;

                    if (wo == null)
                        continue;

                    if (wo.Teleporting)
                    {
                        // ensure post-teleport position is sent
                        var actionChain = new ActionChain();
                        actionChain.AddDelayForOneTick();
                        actionChain.AddAction(player, ActionType.PhysicsObj_TrackObject, () => player.TrackObject(wo));
                        actionChain.EnqueueChain();
                    }
                    else
                        player.TrackObject(wo);
                }
            }
        }

        public void enqueue_obj(PhysicsObj newlyVisible)
        {
            if (!IsPlayer || WeenieObj.WorldObject is not Player player)
                return;

            var wo = newlyVisible.WeenieObj.WorldObject;
            if (wo == null)
                return;

            if (DateTime.UtcNow - player.LastTeleportTime < TeleportCreateObjectDelay)
            {
                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(TeleportCreateObjectDelay.TotalSeconds);
                actionChain.AddAction(player, ActionType.PhysicsObj_TrackObject, () => player.TrackObject(wo, true));
                actionChain.EnqueueChain();
            }
            else
            {
                if (wo.Teleporting)
                {
                    // ensure post-teleport position is sent
                    var actionChain = new ActionChain();
                    actionChain.AddDelayForOneTick();
                    actionChain.AddAction(player, ActionType.PhysicsObj_TrackObject, () => player.TrackObject(wo));
                    actionChain.EnqueueChain();
                }
                else
                    player.TrackObject(wo);
            }
        }

        public void enter_cell(ObjCell newCell)
        {
            if (PartArray == null) return;
            newCell.AddObject(this);
            foreach (var child in Children.Objects)
                child.enter_cell(newCell);

            CurCell = newCell;
            Position.ObjCellID = newCell.ID;        // warning: Position will be in an inconsistent state here, until set_frame() is run!
            if (PartArray != null && !State.HasFlag(PhysicsState.ParticleEmitter))
                PartArray.SetCellID(newCell.ID);

            if (!DatObject && newCell != null)
            {
                CurLandblock = LScape.get_landblock(newCell.ID, newCell.VariationId);
                if (CurLandblock != null && CurLandblock.VariationId == newCell.VariationId)
                    CurLandblock.add_server_object(this);
            }
        }

        public void enter_cell_server(ObjCell newCell)
        {
            //Console.WriteLine($"{Name}.enter_cell_server({newCell.ID:X8})");

            enter_cell(newCell);
            RequestPos.ObjCellID = newCell.ID;      // document this control flow better

            // sync location for initial CO
            if (entering_world)
                WeenieObj.WorldObject.SyncLocation(newCell.VariationId);

            // handle self
            if (IsPlayer)
            {
                var newlyVisible = handle_visible_cells();
                enqueue_objs(newlyVisible);
            }
            else
            {
                handle_visible_cells_non_player();
            }

            // handle known players
            foreach (var player in ObjMaint.GetKnownPlayersValues())
            {
                var added = player.handle_visible_obj(this); //DONE: make variant change here?

                if (added)
                    player.enqueue_obj(this);
            }
        }

        public bool entering_world;

        public bool enter_world(Position pos)
        {
            entering_world = true;

            store_position(pos);
            bool slide = ProjectileTarget == null || WeenieObj.WorldObject is SpellProjectile;
            var result = enter_world(slide, pos);

            entering_world = false;
            return result;
        }

        public bool enter_world(bool slide, Position pos)
        {
            if (Parent != null) return false;

            UpdateTime = PhysicsTimer.CurrentTime;
            
            var setPos = new SetPosition();
            setPos.Pos = pos;
            setPos.Flags = SetPositionFlags.Placement;

            if (slide)
                setPos.Flags |= SetPositionFlags.Slide;
            //Console.WriteLine($"enter_world {this.Name} setPos v: {setPos.Pos.Variation}");
            var result = SetPosition(setPos);
            if (result != SetPositionError.OK)
                return false;

            if (!State.HasFlag(PhysicsState.Static))
                TransientState |= TransientStateFlags.Active;

            if (PartArray != null)
                PartArray.HandleEnterWorld();

            if (MovementManager != null)
                MovementManager.HandleEnterWorld();

            return true;
        }

        public bool ethereal_check_for_collisions()
        {
            foreach (var shadowObj in ShadowObjects.Values)
            {
                if (shadowObj.Cell != null && shadowObj.Cell.check_collisions(this))
                    return true;
            }
            return false;
        }

        public void exit_world()
        {
            if (PartArray != null)
                PartArray.HandleExitWorld();
            if (MovementManager != null)
                MovementManager.HandleExitWorld();
            if (PositionManager != null)
                PositionManager.Unstick();
            if (TargetManager != null)
            {
                TargetManager.ClearTarget();
                TargetManager.NotifyVoyeurOfEvent(TargetStatus.ExitWorld);
            }
            //if (DetectionManager != null)
            //    DetectionManager.DestroyDetectionCylsphere(0);

            report_collision_end(true);
        }

        public void find_bbox_cell_list(CellArray cellArray)
        {
            if (PartArray == null || CurCell == null) return;
            //cellArray.NumCells = 0;
            cellArray.AddedOutside = false;
            cellArray.add_cell(CurCell.ID, CurCell);

            //var checkCells = cellArray.Cells.Values.ToList();
            for (var i = 0; i < cellArray.Cells.Count; i++)
            //foreach (var cell in checkCells)
            {
                var cell = cellArray.Cells.Values.ElementAt(i);
                if (cell == null) continue;

                PartArray.calc_cross_cells_static(cell, cellArray);
            }
        }


        public double get_distance_to_object(PhysicsObj obj, bool use_cyls)
        {
            if (!use_cyls)
                return Position.Distance(obj.Position);

            var height = obj.PartArray != null ? obj.PartArray.GetHeight() : 0.0f;
            var radius = obj.PartArray != null ? obj.PartArray.GetRadius() : 0.0f;

            var curHeight = PartArray != null ? PartArray.GetHeight() : 0.0f;
            var curRadius = PartArray != null ? PartArray.GetRadius() : 0.0f;

            return Position.CylinderDistance(curRadius, curHeight, Position, radius, height, obj.Position);
        }

        // custom, based on above
        public double get_distance_sq_to_object(PhysicsObj obj, bool use_cyls)
        {
            if (!use_cyls)
                return Position.DistanceSquared(obj.Position);

            var height = obj.PartArray != null ? obj.PartArray.GetHeight() : 0.0f;
            var radius = obj.PartArray != null ? obj.PartArray.GetRadius() : 0.0f;

            var curHeight = PartArray != null ? PartArray.GetHeight() : 0.0f;
            var curRadius = PartArray != null ? PartArray.GetRadius() : 0.0f;

            return Position.CylinderDistanceSq(curRadius, curHeight, Position, radius, height, obj.Position);
        }


        public float get_heading()
        {
            return Position.Frame.get_heading();
        }


        public MotionInterp get_minterp()
        {
            if (MovementManager == null)
            {
                MovementManager = MovementManager.Create(this, WeenieObj);
                MovementManager.EnterDefaultState();
                if (State.HasFlag(PhysicsState.Static))
                {
                    if (TransientState.HasFlag(TransientStateFlags.Active))
                    {
                        // loword = cmd, hiword = param
                        // refactor...
                        UpdateTime = PhysicsTimer.CurrentTime;
                    }
                    TransientState |= TransientStateFlags.Active;
                }
            }
            return MovementManager.get_minterp();
        }

        public ObjectInfo get_object_info(Transition transition, bool adminMove)
        {
            var objInfo = new ObjectInfo();
            if (State.HasFlag(PhysicsState.EdgeSlide))
                objInfo.State |= ObjectInfoState.EdgeSlide;

            if (!adminMove)
            {
                if (TransientState.HasFlag(TransientStateFlags.Contact))
                {
                    var isWater = TransientState.HasFlag(TransientStateFlags.WaterContact);

                    if (check_contact(true))
                    {
                        transition.InitContactPlane(ContactPlaneCellID, ContactPlane, isWater);

                        objInfo.State |= ObjectInfoState.Contact;
                        if (TransientState.HasFlag(TransientStateFlags.OnWalkable))
                            objInfo.State |= ObjectInfoState.OnWalkable;
                    }
                    else
                        transition.InitLastKnownContactPlane(ContactPlaneCellID, ContactPlane, isWater);
                }

                if (TransientState.HasFlag(TransientStateFlags.Sliding))
                    transition.InitSlidingNormal(SlidingNormal);
            }

            if (PartArray != null && PartArray.AllowsFreeHeading())
                objInfo.State |= ObjectInfoState.FreeRotate;

            if (State.HasFlag(PhysicsState.Missile))
                objInfo.State |= ObjectInfoState.PathClipped;

            return objInfo;

        }

        public PositionManager get_position_manager()
        {
            MakePositionManager();

            return PositionManager;
        }

        public uint get_sticky_object()
        {
            if (PositionManager == null) return 0;

            return PositionManager.GetStickyObjectID();
        }

        public double get_target_quantum()
        {
            if (TargetManager == null || TargetManager.TargetInfo == null)
                return 0.0f;

            return TargetManager.TargetInfo.Quantum;
        }

        public Vector3 get_velocity()
        {
            return CachedVelocity;
        }

        public static float get_walkable_z()
        {
            return PhysicsGlobals.FloorZ;
        }

        public bool handle_all_collisions(CollisionInfo collisions, bool prev_has_contact, bool prev_on_walkable)
        {
            var apply_bounce = true;
            if (prev_on_walkable && TransientState.HasFlag(TransientStateFlags.OnWalkable) && !State.HasFlag(PhysicsState.Sledding))
                apply_bounce = false;

            var retval = false;
            foreach (var collideObject in collisions.CollideObject)
                if (collideObject != null && track_object_collision(collideObject, prev_has_contact))
                    retval = true;

            report_collision_end(false);

            if (CollidingWithEnvironment)
            {
                CollidingWithEnvironment = collisions.CollidedWithEnvironment;
            }
            else if (collisions.CollidedWithEnvironment || !prev_on_walkable && TransientState.HasFlag(TransientStateFlags.OnWalkable))
            {
                if (report_environment_collision(prev_has_contact))
                    retval = true;
            }

            if (collisions.FramesStationaryFall <= 1)
            {
                if (apply_bounce && collisions.CollisionNormalValid)
                {
                    if (State.HasFlag(PhysicsState.Inelastic))
                    {
                        Velocity = Vector3.Zero;

                        // custom for spell projectiles: explode on server
                        report_environment_collision(prev_has_contact);
                    }
                    else
                    {
                        var collisionAngle = Vector3.Dot(Velocity, collisions.CollisionNormal);
                        if (collisionAngle < 0.0f)
                        {
                            var elasticAngle = -(collisionAngle * (Elasticity + 1.0f));
                            Velocity += collisions.CollisionNormal * elasticAngle;
                        }
                    }
                }
            }
            else
            {
                Velocity = Vector3.Zero;
                if (collisions.FramesStationaryFall == 3)
                {
                    TransientState &= ~TransientStateFlags.StationaryComplete;
                    return retval;
                }
            }

            if (collisions.FramesStationaryFall == 0)
                TransientState &= ~TransientStateFlags.StationaryComplete;
            else if (collisions.FramesStationaryFall == 1)
                TransientState |= TransientStateFlags.StationaryFall;
            else if (collisions.FramesStationaryFall == 2)
                TransientState |= TransientStateFlags.StationaryStop;
            else
                TransientState |= TransientStateFlags.StationaryStuck;

            return retval;
        }

        /// <summary>
        /// Maintains the list of visible objects for a player
        /// </summary>
        /// <returns>The list of newly visible objects since last call</returns>
        public List<PhysicsObj> handle_visible_cells()
        {
            //return new List<PhysicsObj>();

            //Console.WriteLine($"handle_visible_cells({CurCell.ID:X8}) for {Name}");

            // remove any objects that have been in the destruction queue > 25s
            _ = ObjMaint.DestroyObjects();
            //Console.WriteLine("Destroyed objects: " + expiredObjs.Count);
            //foreach (var expiredObj in expiredObjs)
            //Console.WriteLine(expiredObj.Name);

            // get the list of visible objects from this cell
            var visibleObjects = ObjMaint.GetVisibleObjects(CurCell, ObjectMaint.VisibleObjectType.All, this.Position.Variation);

            //Console.WriteLine("Visible objects from this cell: " + visibleObjects.Count);
            //foreach (var visibleObject in visibleObjects)
            //Console.WriteLine(visibleObject.Name);

            // get the difference between current and previous visible
            //var newlyVisible = visibleObjects.Except(ObjMaint.VisibleObjects.Values).ToList();
            var newlyOccluded = ObjMaint.GetVisibleObjectsValues(this.Position.Variation).Except(visibleObjects).ToList();
            //Console.WriteLine("Newly visible objects: " + newlyVisible.Count);
            //Console.WriteLine("Newly occluded objects: " + newlyOccluded.Count);
            //foreach (var obj in newlyOccluded)
            //Console.WriteLine(obj.Name);

            // add newly visible objects, and get the previously unknowns
            var createObjs = ObjMaint.AddVisibleObjects(visibleObjects);
            //Console.WriteLine("Create objects: " + createObjs.Count);
            /*if (createObjs.Count != newlyVisible.Count)
            {
                Console.WriteLine($"Create objs differs from newly visible ({createObjs.Count} vs. {newlyVisible.Count})");
                Console.WriteLine("CreateObjs:");
                for (var i = 0; i < createObjs.Count; i++)
                    Console.WriteLine($"{i} - {createObjs[i].Name}");
                Console.WriteLine("Newly Visible:");
                for (var i = 0; i < newlyVisible.Count; i++)
                    Console.WriteLine($"{i} = {newlyVisible[i].Name}");
            }*/

            // add newly occluded objects to the destruction queue
            ObjMaint.AddObjectsToBeDestroyed(newlyOccluded);

            return createObjs;
        }

        public void handle_visible_cells_non_player()
        {
            if (WeenieObj.IsMonster)
            {
                // players and combat pets
                var visibleTargets = ObjMaint.GetVisibleObjects(CurCell, ObjectMaint.VisibleObjectType.AttackTargets, this.Position.Variation);
                _ = ObjMaint.AddVisibleTargets(visibleTargets);
            }
            else
            {
                // everything except monsters
                // usually these are server objects whose position never changes
                var knownPlayers = ObjectMaint.InitialClamp ? ObjMaint.GetVisibleObjectsDist(CurCell, ObjectMaint.VisibleObjectType.Players, this.Position.Variation)
                    : ObjMaint.GetVisibleObjects(CurCell, ObjectMaint.VisibleObjectType.Players, this.Position.Variation);

                ObjMaint.AddKnownPlayers(knownPlayers);
            }

            if (WeenieObj.IsCombatPet)
            {
                var visibleMonsters = ObjMaint.GetVisibleObjects(CurCell, ObjectMaint.VisibleObjectType.AttackTargets);
                _ = ObjMaint.AddVisibleTargets(visibleMonsters);
            }
        }

        public bool handle_visible_obj(PhysicsObj obj)
        {
            if (CurCell == null || obj.CurCell == null)
            {
                if (CurCell == null)
                    log.Error($"{Name}({ID:X8}).handle_visible_obj({obj.Name}): CurCell null");
                else
                    log.Error($"{Name}({ID:X8}).handle_visible_obj({obj.Name}): obj.CurCell null");

                return false;
            }

            var isVisible = CurCell.IsVisible(obj.CurCell);
            if (isVisible && obj.Position.Variation != this.Position.Variation)  //todo I hate this?
            {
                isVisible = false;
            }

            if (isVisible)
            {
                var prevKnown = ObjMaint.KnownObjectsContainsKey(obj.ID);

                var newlyVisible = ObjMaint.AddVisibleObject(obj);

                if (newlyVisible)
                {
                    ObjMaint.AddKnownObject(obj);
                    ObjMaint.RemoveObjectToBeDestroyed(obj);
                }

                return !prevKnown && newlyVisible;
            }
            else
            {
                var newlyOccluded = ObjMaint.VisibleObjectsContainsKey(obj.ID);

                if (newlyOccluded)
                    ObjMaint.AddObjectToBeDestroyed(obj);

                return false;
            }
        }

        public bool is_completely_visible()
        {
            if (CurCell == null || NumShadowObjects == 0)
                return false;

            foreach (var shadowObj in ShadowObjects.Values)
            {
                if (shadowObj.Cell == null)
                    return false;
            }
            return true;
        }

        public static bool is_valid_walkable(Vector3 normal)
        {
            return normal.Z >= PhysicsGlobals.FloorZ;
        }

        public void leave_cell(bool is_changing_cell)
        {
            if (CurCell == null) return;
            CurCell.RemoveObject(this);
            foreach (var child in Children.Objects)
                child.leave_cell(is_changing_cell);
            // removed lighting
            CurCell = null;

            if (CurLandblock != null && !DatObject)
            {
                CurLandblock.remove_server_object(this);
                CurLandblock = null;
            }

        }

        public void leave_world()
        {
            report_collision_end(true);
            if (ObjMaint != null)
            {
                //ObjMaint.RemoveFromLostCell(this);
                ObjMaint.RemoveObjectToBeDestroyed(this);
            }
            TransientState &= ~TransientStateFlags.Active;
            remove_shadows_from_cells();
            leave_cell(false);

            Position.ObjCellID = 0;
            if (PartArray != null && !State.HasFlag(PhysicsState.ParticleEmitter))
                PartArray.SetCellID(0);

            TransientState &= ~TransientStateFlags.Contact;
            calc_acceleration();
            var walkable = TransientState.HasFlag(TransientStateFlags.OnWalkable);
            TransientState &= ~(TransientStateFlags.OnWalkable | TransientStateFlags.WaterContact);
            if (MovementManager != null && walkable) MovementManager.LeaveGround();
            calc_acceleration();
            TransientState = 0;
        }

        public bool makeAnimObject(uint setupID, bool createParts)
        {
            PartArray = PartArray.CreateSetup(this, setupID, createParts);
            return PartArray != null;
        }

        public bool IsSightObj;

        public static PhysicsObj makeObject(uint dataDID, uint objectIID, bool dynamic, bool sightObj = false)
        {
            var obj = new PhysicsObj(null);
            obj.InitObjectBegin(objectIID, dynamic);
            obj.InitPartArrayObject(dataDID, true);
            obj.InitObjectEnd();

            // for direct visibility testing
            obj.IsSightObj = sightObj;

            return obj;
        }

        public static PhysicsObj makeParticleObject(int numParts, Sphere sortingSphere, int? VariationId)
        {
            var particle = new PhysicsObj(VariationId);
            particle.State = PhysicsState.Static | PhysicsState.ReportCollisions;
            particle.PartArray = PartArray.CreateParticle(particle, numParts, sortingSphere);
            return particle;
        }

        public bool movement_is_autonomous()
        {
            return LastMoveWasAutonomous;
        }

        public bool obj_within_block()
        {
            var sortingSphere = PartArray != null ? PartArray.GetSortingSphere() : PhysicsGlobals.DummySphere;
            var globCenter = Position.Frame.LocalToGlobal(sortingSphere.Center);

            if (State.HasFlag(PhysicsState.HasPhysicsBSP))
            {
                if (globCenter.X >= sortingSphere.Radius && globCenter.Y >= sortingSphere.Radius)
                {
                    var blockRadius = LandDefs.BlockLength - sortingSphere.Radius;
                    if (globCenter.X < blockRadius)
                    {
                        if (globCenter.Y >= blockRadius)
                            return false;
                        return true;
                    }
                }
                return false;
            }

            if (PartArray != null && PartArray.GetNumCylsphere() > 0)
            {
                for (var i = 0; i < PartArray.GetNumCylsphere(); i++)
                {
                    var cylSphere = PartArray.GetCylSphere()[i];
                    globCenter = Position.Frame.LocalToGlobal(cylSphere.LowPoint);
                    if (globCenter.X < cylSphere.Radius || globCenter.Y < cylSphere.Radius)
                        return false;

                    var blockRadius = LandDefs.BlockLength - cylSphere.Radius;
                    if (globCenter.X >= blockRadius || globCenter.Y >= blockRadius)
                        return false;
                }
                return true;
            }

            if (PartArray != null && PartArray.GetNumSphere() > 0)
                return LandDefs.InBlock(globCenter, sortingSphere.Radius);
            else
                return LandDefs.InBlock(Position.Frame.Origin, 0.0f);
        }

        public bool play_script_internal(uint scriptID)
        {
            if (scriptID == 0) return false;
            if (ScriptManager == null) ScriptManager = new ScriptManager(this);

            return ScriptManager.AddScript(scriptID);
        }

        public void prepare_to_enter_world()
        {
            UpdateTime = PhysicsTimer.CurrentTime;

            //ObjMaint.RemoveFromLostCell(this);
            ObjMaint.RemoveObjectToBeDestroyed(this);

            foreach (var child in Children.Objects)
                ObjMaint.RemoveObjectToBeDestroyed(child);

            if (!State.HasFlag(PhysicsState.Static))
            {
                if (!TransientState.HasFlag(TransientStateFlags.Active))
                    UpdateTime = PhysicsTimer.CurrentTime;     // hiword?

                TransientState |= TransientStateFlags.Active;
            }
        }

        public bool prepare_to_leave_visibility()
        {
            remove_shadows_from_cells();
            //ObjMaint.RemoveFromLostCell(this);
            leave_cell(false);

            ObjMaint.AddObjectToBeDestroyed(this);
            foreach (var child in Children.Objects)
            {
                ObjMaint.AddObjectToBeDestroyed(child);
            }
            return true;
        }

        public void process_fp_hook(PhysicsHookType type, float curr_value, Object userData)
        {
            switch (type)
            {
                case PhysicsHookType.Scale:
                    SetScaleStatic(curr_value);
                    break;
                case PhysicsHookType.Translucency:
                    SetTranslucencyInternal(curr_value);
                    break;
                case PhysicsHookType.PartTranslucency:
                    if (PartArray != null) PartArray.SetPartTranslucencyInternal((int)userData, curr_value);
                    break;
                case PhysicsHookType.Luminosity:
                    if (PartArray != null) PartArray.SetLuminosityInternal(curr_value);
                    break;
                case PhysicsHookType.PartLuminosity:
                    if (PartArray != null) PartArray.SetPartLuminosityInternal((int)userData, curr_value);
                    break;
                case PhysicsHookType.Diffusion:
                    if (PartArray != null) PartArray.SetDiffusionInternal(curr_value);
                    break;
                case PhysicsHookType.PartDiffusion:
                    if (PartArray != null) PartArray.SetPartDiffusionInternal((int)userData, curr_value);
                    break;
                case PhysicsHookType.CallPES:
                    CallPESInternal((uint)userData, curr_value);
                    break;
            }
        }

        public void process_hooks()
        {

            for (var i = 0; i < AnimHooks.Count; i++)
            {
                var animHook = AnimHooks[i];
                AnimHook.Execute(this, animHook);
            }

            AnimHooks.Clear();
        }

        public void recalc_cross_cells()
        {
            if (PartArray == null) return;
            if (Position.ObjCellID != 0)
                calc_cross_cells(Position.Variation);
            else
            {
                if (!ExaminationObject || !State.HasFlag(PhysicsState.ParticleEmitter)) return;
                add_particle_shadow_to_cell();
            }
            foreach (var child in Children.Objects)
                child.recalc_cross_cells();
        }

        public void receive_detection_update()
        {
            //if (DetectionManager == null) return;
            //DetectionManager.ReceiveDetectionUpdate(info);

            if (State.HasFlag(PhysicsState.Static)) return;
            if (!TransientState.HasFlag(TransientStateFlags.Active))
                UpdateTime = PhysicsTimer.CurrentTime;

            TransientState |= TransientStateFlags.Active;
        }

        public void receive_target_update(TargetInfo info)
        {
            if (TargetManager != null)
                TargetManager.ReceiveUpdate(info);
        }


        public void remove_parts(ObjCell objCell)
        {
            if (PartArray != null)
                PartArray.RemoveParts(objCell);
        }

        public void remove_shadows_from_cells()
        {
            foreach (var shadowObj in ShadowObjects.Values)
            {
                if (shadowObj.Cell == null) continue;
                var cell = shadowObj.Cell;
                shadowObj.Cell.remove_shadow_object(shadowObj);
                PartArray?.RemoveParts(cell);
            }
            NumShadowObjects = ShadowObjects.Count;

            if (Children == null) return;
            for (int i = 0; i < Children.NumObjects; i++)
            {
                Children.Objects[i].remove_shadows_from_cells();
            }
        }

        public bool remove_voyeur(uint objectID)
        {
            if (TargetManager == null) return false;
            return TargetManager.RemoveVoyeur(objectID);
        }

        public void report_collision_end(bool forceEnd)
        {
            if (CollisionTable == null) return;

            var ends = new List<uint>();

            foreach (var kvp in CollisionTable)
            {
                var collision_id = kvp.Key;
                var collision = kvp.Value;

                var deltaTime = PhysicsTimer.CurrentTime - collision.TouchedTime;

                if (deltaTime > 1.0f || collision.Ethereal && deltaTime > 0.0f || forceEnd)
                    ends.Add(collision_id);
            }

            foreach (var end in ends)
            {
                CollisionTable.Remove(end);
                report_object_collision_end(end);
            }
        }

        public bool report_environment_collision(bool prev_has_contact)
        {
            if (CollidingWithEnvironment) return false;

            var result = false;
            if (State.HasFlag(PhysicsState.ReportCollisions) && WeenieObj != null)
            {
                var collision = new EnvCollisionProfile();
                collision.Velocity = Velocity;
                collision.SetMeInContact(prev_has_contact);
                WeenieObj.DoCollision(collision, ObjID, this);
                result = true;
            }
            CollidingWithEnvironment = true;
            if (State.HasFlag(PhysicsState.Missile)) State &= ~PhysicsState.Missile;
            return result;
        }

        public void report_exhaustion()
        {
            if (MovementManager != null) MovementManager.ReportExhaustion();
        }

        public bool report_object_collision(PhysicsObj obj, bool prev_has_contact)
        {
            if (obj.State.HasFlag(PhysicsState.ReportCollisionsAsEnvironment))
                return report_environment_collision(prev_has_contact);

            //var velocityCollide = Velocity - obj.Velocity;

            bool collided = false;

            if (!obj.State.HasFlag(PhysicsState.IgnoreCollisions))
            {
                if (State.HasFlag(PhysicsState.ReportCollisions) && WeenieObj != null)
                {
                    //var profile = build_collision_profile(obj, prev_has_contact, velocityCollide);

                    WeenieObj.DoCollision(ObjID, obj);

                    collided = true;
                }

                if (State.HasFlag(PhysicsState.Missile))
                    State &= ~(PhysicsState.Missile | PhysicsState.AlignPath | PhysicsState.PathClipped);
            }

            if (obj.State.HasFlag(PhysicsState.ReportCollisions) && !State.HasFlag(PhysicsState.IgnoreCollisions) && obj.WeenieObj != null)
            {
                // acclient might have a bug here,
                // prev_has_contact and missile state params swapped?
                //var profile = obj.build_collision_profile(this, obj.TransientState.HasFlag(TransientStateFlags.Contact), velocityCollide);

                // ObjID and obj are custom parameters added by ace
                // if obj. and obj) are the same, all of these calls seem to effectively get dropped
                // is this intended for 1-way collisions??
                obj.WeenieObj.DoCollision(ObjID, obj);

                collided = true;
            }

            return collided;
        }

        public bool report_object_collision_end(uint objectID)
        {
            if (ObjMaint != null)
            {
                var collision = ServerObjectManager.GetObjectA(objectID);
                if (collision != null)
                {
                    if (!collision.State.HasFlag(PhysicsState.ReportCollisionsAsEnvironment))
                    {
                        if (State.HasFlag(PhysicsState.ReportCollisions) && WeenieObj != null)
                            WeenieObj.DoCollisionEnd(new ObjectGuid(objectID));

                        if (collision.State.HasFlag(PhysicsState.ReportCollisions) && collision.WeenieObj != null)
                            collision.WeenieObj.DoCollisionEnd(ObjID);
                    }
                }
                return true;
            }
            if (State.HasFlag(PhysicsState.ReportCollisions) && WeenieObj != null)
                WeenieObj.DoCollisionEnd(new ObjectGuid(objectID));

            return false;
        }


        /// <summary>
        /// Sets the active transient state flags
        /// </summary>
        public bool set_active(bool active)
        {
            if (active)
            {
                if (State.HasFlag(PhysicsState.Static))
                    return false;

                if (!TransientState.HasFlag(TransientStateFlags.Active))
                    UpdateTime = PhysicsTimer.CurrentTime;

                TransientState |= TransientStateFlags.Active;
                return true;
            }
            else
            {
                TransientState &= ~TransientStateFlags.Active;
                return true;
            }
        }

        public bool is_active()
        {
            return (TransientState & TransientStateFlags.Active) != 0;
        }

        public void set_current_pos(Position newPos)
        {
            Position.ObjCellID = newPos.ObjCellID;
            Position.Variation = newPos.Variation;
            Position.Frame = new AFrame(newPos.Frame);

            if (CurCell == null || CurCell.ID != Position.ObjCellID || Position.Variation != newPos.Variation)
            {
                var newCell = LScape.get_landcell(newPos.ObjCellID, newPos.Variation);

                if (WeenieObj.WorldObject is Player player && player.LastContact && newCell is LandCell landCell)
                {
                    Polygon walkable = null;
                    if (landCell.find_terrain_poly(newPos.Frame.Origin, ref walkable))
                    {
                        ContactPlaneCellID = newPos.ObjCellID;
                        ContactPlane = walkable.Plane;
                    }
                }
                change_cell_server(newCell);
            }

            CachedVelocity = requestCachedVelocity;
        }

        /// <summary>
        /// Sets the cell ID for an object and all its parts
        /// </summary>
        public void set_cell_id(uint newCellID)
        {
            Position.ObjCellID = newCellID;
            if (!State.HasFlag(PhysicsState.ParticleEmitter) && PartArray != null)
                PartArray.SetCellID(newCellID);
        }


        /// <summary>
        /// Sets the ethereal (semi-transparent/walkthrough) flags
        /// </summary>
        public bool set_ethereal(bool ethereal, bool sendEvent)
        {
            if (ethereal)
            {
                State |= PhysicsState.Ethereal;
                TransientState &= ~TransientStateFlags.CheckEthereal;
                return true;
            }

            State &= ~PhysicsState.Ethereal;

            if (Parent != null || CurCell == null || !ethereal_check_for_collisions())
            {
                TransientState &= ~TransientStateFlags.CheckEthereal;
                return true;
            }

            // error path - go back to ethereal, start loop in CheckEthereal state
            State |= PhysicsState.Ethereal;
            TransientState |= TransientStateFlags.CheckEthereal;

            return false;
        }

        /// <summary>
        /// Sets the current frame of animation for this object
        /// </summary>
        public void set_frame(AFrame frame)
        {
            if (!frame.IsValid() && frame.IsValidExceptForHeading())
                frame.Orientation = Quaternion.Identity;

            Position.Frame = new AFrame(frame);
            //Position.Frame.Origin = frame.Origin;
            //Position.Frame.Orientation = frame.Orientation;

            // custom for server:
            // only update part frames for objects with physics bsp
            if (PartArray != null && !State.HasFlag(PhysicsState.ParticleEmitter)
                && (State.HasFlag(PhysicsState.HasPhysicsBSP) || !PhysicsEngine.Instance.Server))
            {
                PartArray.SetFrame(frame);
            }

            UpdateChildrenInternal();
        }

        /// <summary>
        /// Sets the angle this object is facing
        /// </summary>
        public void set_heading(float degrees, bool sendEvent)
        {
            Position.Frame.set_heading(degrees);
            set_frame(Position.Frame);
        }


        /// <summary>
        /// Sets the initial frame of animation for this object
        /// </summary>
        public void set_initial_frame(AFrame frame)
        {
            Position.Frame = frame;

            if (PartArray != null && !State.HasFlag(PhysicsState.ParticleEmitter))
                PartArray.SetFrame(frame);

            UpdateChildrenInternal();
        }

        /// <summary>
        /// Converts the local to global velocity for this object
        /// </summary>
        public void set_local_velocity(Vector3 newVel, bool sendEvent)
        {
            var globalVec = Position.LocalToGlobalVec(newVel);
            set_velocity(globalVec, sendEvent);
        }
        

        public void set_object_guid(ObjectGuid guid)
        {
            ObjID = guid;
            ID = guid.Full;

            ServerObjectManager.AddServerObject(this);
        }


        /// <summary>
        /// Handles leaving / hitting ground in movement manager,
        /// and calculates acceleration
        /// </summary>
        /// <param name="isOnWalkable">Flag indicates if this object is currently standing on the ground</param>
        public void set_on_walkable(bool isOnWalkable)
        {
            var prevOnWalkable = TransientState.HasFlag(TransientStateFlags.OnWalkable);

            if (isOnWalkable)
                TransientState |= TransientStateFlags.OnWalkable;
            else
                TransientState &= ~TransientStateFlags.OnWalkable;

            if (MovementManager != null)
            {
                if (prevOnWalkable)
                {
                    if (!isOnWalkable)
                        MovementManager.LeaveGround();
                }
                else
                {
                    if (isOnWalkable)
                        MovementManager.HitGround();
                }
            }
            calc_acceleration();
        }


        /// <summary>
        /// Sets the parent object for this physics object
        /// </summary>
        public bool set_parent(PhysicsObj obj, int partIdx, AFrame frame)
        {
            if (obj == null) return false;
            if (!obj.add_child(this, partIdx, frame)) return false;

            ExaminationObject = obj.ExaminationObject;

            unset_parent();
            leave_world();

            Parent = obj;

            if (obj.CurCell != null)
            {
                change_cell(obj.CurCell);
                obj.UpdateChild(this, partIdx, frame);
                recalc_cross_cells();
            }

            return true;
        }

        private Vector3 requestCachedVelocity;

        /// <summary>
        /// Sets the requested position to the AutonomousPosition
        /// received from the client
        /// </summary>
        public void set_request_pos(Vector3 pos, Quaternion rotation, ObjCell cell, uint blockCellID, int? VariationId = null)
        {
            RequestPos.Frame.Origin = pos;
            RequestPos.Frame.Orientation = rotation;
            RequestPos.Variation = VariationId;

            if (CurCell == null)
            {
                CurCell = LScape.get_landcell(blockCellID, VariationId);
                if (CurCell == null)
                    return;
            }

            if (cell == null)
                RequestPos.ObjCellID = RequestPos.GetCell(CurCell.ID);
            else
                RequestPos.ObjCellID = cell.ID;

            requestCachedVelocity = CachedVelocity;
        }


        public void set_target(uint contextID, uint objectID, float radius, double quantum)
        {
            if (TargetManager == null)
                TargetManager = new TargetManager(this);

            TargetManager.SetTarget(contextID, objectID, radius, quantum);
        }

        public void set_target_quantum(double new_quantum)
        {
            if (TargetManager != null)
                TargetManager.SetTargetQuantum(new_quantum);
        }

        /// <summary>
        /// Sets the global velocity for this physics object
        /// </summary>
        /// <param name="velocity">The velocity in global space</param>
        /// <param name="sendEvent">Flag indicates if this event should send a network broadcast</param>
        public void set_velocity(Vector3 velocity, bool sendEvent)
        {
            if (!velocity.Equals(Velocity))
            {
                Velocity = velocity;

                if (Velocity.Length() > PhysicsGlobals.MaxVelocity)
                {
                    Velocity /= Velocity.Length();    // todo: add normalize method
                    Velocity *= PhysicsGlobals.MaxVelocity;
                }
                //JumpedThisFrame = true;
            }

            if (!State.HasFlag(PhysicsState.Static))
            {
                if (!TransientState.HasFlag(TransientStateFlags.Active))
                    UpdateTime = PhysicsTimer.CurrentTime;

                TransientState |= TransientStateFlags.Active;
            }
        }

        /// <summary>
        /// Sets the weenie for this physics object
        /// </summary>
        public void set_weenie_obj(WeenieObject wobj)
        {
            WeenieObj = wobj;
            if (MovementManager != null)
                MovementManager.SetWeenieObject(wobj);
        }

        public void stick_to_object(uint objectID)
        {
            MakePositionManager();
            if (ObjMaint == null) return;

            var objectA = ServerObjectManager.GetObjectA(objectID);
            if (objectA == null) return;
            if (objectA.Parent != null)
                objectA = Parent;

            if (objectA.PartArray != null)
                PositionManager.StickTo(objectA.ID, objectA.PartArray.GetRadius(), objectA.PartArray.GetHeight());
            else
                PositionManager.StickTo(objectA.ID, 0, 0);
        }

        public void store_position(Position pos)
        {
            // position ref?
            if ((pos.ObjCellID & 0xFFFF) < 0x100)
                LandDefs.AdjustToOutside(pos);

            if (PartArray != null && !State.HasFlag(PhysicsState.ParticleEmitter))
                PartArray.SetCellID(pos.ObjCellID);

            set_cell_id(pos.ObjCellID);
            set_frame(pos.Frame);
        }


        public bool track_object_collision(PhysicsObj obj, bool prev_has_contact)
        {
            if (obj.State.HasFlag(PhysicsState.Static))
                return report_environment_collision(prev_has_contact);

            if (CollisionTable == null)
                CollisionTable = new Dictionary<uint, CollisionRecord>();

            CollisionTable[obj.ID] = new CollisionRecord(PhysicsTimer.CurrentTime, obj.State.HasFlag(PhysicsState.Ethereal));

            return report_object_collision(obj, prev_has_contact);
        }

        public Transition transition(Position oldPos, Position newPos, bool adminMove)
        {
            var trans = Transition.MakeTransition();
            if (trans == null) return null;

            var objectInfo = get_object_info(trans, adminMove);
            trans.InitObject(this, objectInfo.State);
            trans.VariationId = newPos.Variation;

            if (PartArray == null || PartArray.GetNumSphere() == 0)
            {
                trans.InitSphere(1, PhysicsGlobals.DummySphere, 1.0f);
            }
            else
            {
                var numSpheres = PartArray.GetNumSphere();
                var sphere = PartArray.GetSphere();
                trans.InitSphere(numSpheres, sphere, Scale);
            }

            trans.InitPath(CurCell, oldPos, newPos);

            if ((TransientState & TransientStateFlags.StationaryStuck) != 0)
            {
                trans.CollisionInfo.FramesStationaryFall = 3;
            }
            else if ((TransientState & TransientStateFlags.StationaryStop) != 0)
            {
                trans.CollisionInfo.FramesStationaryFall = 2;
            }
            else if ((TransientState & TransientStateFlags.StationaryFall) != 0)
            {
                trans.CollisionInfo.FramesStationaryFall = 1;
            }

            var validPos = trans.FindValidPosition();
            trans.CleanupTransition();
            return validPos ? trans : null;
        }


        public void unset_parent()
        {
            if (Parent == null) return;
            if (Parent.Children != null)
                Parent.Children.RemoveChild(this);
            if (Parent.State.HasFlag(PhysicsState.Hidden))
            {
                State &= ~PhysicsState.Hidden;
                if (PartArray != null)
                    PartArray.SetNoDrawInternal(false);
            }
            Parent = null;
            UpdateTime = PhysicsTimer.CurrentTime;
            clear_transient_states();
        }

        public void unstick_from_object()
        {
            if (PositionManager != null)
                PositionManager.Unstick();
        }

        private static float TickRate = 1.0f / 30.0f;

        public bool update_object()
        {
            if (Parent != null || CurCell == null || State.HasFlag(PhysicsState.Frozen))
            {
                TransientState &= ~TransientStateFlags.Active;
                return false;
            }
            /*if (PlayerObject != null)
            {
                PlayerVector = PlayerObject.Position.GetOffset(Position);
                PlayerDistance = PlayerVector.Length();
                if (PlayerDistance > 96.0f && ObjMaint.IsActive)
                    TransientState &= ~TransientStateFlags.Active;
                else
                    set_active(true);   // sets UpdateTime
            }*/

            PhysicsTimer_CurrentTime = UpdateTime;

            var deltaTime = PhysicsTimer.CurrentTime - UpdateTime;

            if (deltaTime < TickRate)
                return false;

            //Console.WriteLine("deltaTime: " + deltaTime);

            // commented out for debugging
            if (deltaTime > PhysicsGlobals.HugeQuantum)
            {
                UpdateTime = PhysicsTimer.CurrentTime;   // consume time?
                return false;
            }

            while (deltaTime > PhysicsGlobals.MaxQuantum)
            {
                PhysicsTimer_CurrentTime += PhysicsGlobals.MaxQuantum;
                UpdateObjectInternal(PhysicsGlobals.MaxQuantum);
                deltaTime -= PhysicsGlobals.MaxQuantum;
            }

            if (deltaTime > PhysicsGlobals.MinQuantum)
            {
                PhysicsTimer_CurrentTime += deltaTime;
                UpdateObjectInternal(deltaTime);
            }

            UpdateTime = PhysicsTimer_CurrentTime;
            return true;
        }

        public void StartTimer(double delta = 0)
        {
            UpdateTime = PhysicsTimer.CurrentTime - delta;
        }


        /// <summary>
        /// This is for legacy movement system
        /// </summary>
        public bool update_object_server(bool forcePos = true)
        {
            var deltaTime = PhysicsTimer.CurrentTime - UpdateTime;

            var wo = WeenieObj.WorldObject;
            var success = true;
            if (wo != null && !wo.Teleporting)
                success = UpdateObjectInternalServer(deltaTime);

            if (forcePos && success)
                set_current_pos(RequestPos);

            // temp for players
            if ((TransientState & TransientStateFlags.Contact) != 0)
                CachedVelocity = Vector3.Zero;

            if (wo != null && wo.Teleporting)
            {
                //Console.WriteLine($"*** SETTING TELEPORT *** {RequestPos.ShortLoc()}");

                var setPosition = new SetPosition();
                setPosition.Pos = RequestPos;
                setPosition.Flags = SetPositionFlags.SendPositionEvent | SetPositionFlags.Slide | SetPositionFlags.Placement | SetPositionFlags.Teleport;

                SetPosition(setPosition);

                // hack...
                if (!TransientState.HasFlag(TransientStateFlags.OnWalkable))
                {
                    //Console.WriteLine($"Setting velocity");
                    Velocity = new Vector3(0, 0, -PhysicsGlobals.EPSILON);
                }
            }

            UpdateTime = PhysicsTimer.CurrentTime;

            return success;
        }

        /// <summary>
        /// This is for full / updated movement system
        /// </summary>
        public bool update_object_server_new(bool forcePos = true)
        {
            if (Parent != null || CurCell == null || State.HasFlag(PhysicsState.Frozen))
            {
                TransientState &= ~TransientStateFlags.Active;
                return false;
            }

            PhysicsTimer_CurrentTime = UpdateTime;

            var deltaTime = PhysicsTimer.CurrentTime - UpdateTime;

            //Console.WriteLine($"{Name}.update_object_server({forcePos}) - deltaTime: {deltaTime}");

            var isTeleport = WeenieObj.WorldObject?.Teleporting ?? false;

            // commented out for debugging
            if (deltaTime > PhysicsGlobals.HugeQuantum && !isTeleport)
            {
                UpdateTime = PhysicsTimer.CurrentTime;   // consume time?
                return false;
            }

            var requestCell = RequestPos.ObjCellID;

            var success = true;

            if (!isTeleport)
            {
                if (GetBlockDist(Position, RequestPos) > 1)
                {
                    log.Warn($"WARNING: failed transition for {Name} from {Position} to {RequestPos}");
                    success = false;
                }

                while (deltaTime > PhysicsGlobals.MaxQuantum)
                {
                    PhysicsTimer_CurrentTime += PhysicsGlobals.MaxQuantum;
                    UpdateObjectInternal(PhysicsGlobals.MaxQuantum);
                    deltaTime -= PhysicsGlobals.MaxQuantum;
                }

                if (deltaTime > PhysicsGlobals.MinQuantum)
                {
                    PhysicsTimer_CurrentTime += deltaTime;
                    UpdateObjectInternal(deltaTime);
                }

                success &= requestCell >> 16 != 0x18A || CurCell?.ID >> 16 == requestCell >> 16;
            }

            RequestPos.ObjCellID = requestCell;

            if (forcePos && success)
            {
                // attempt transition to request pos,
                // to trigger any collision detection
                var transit = transition(Position, RequestPos, false);

                if (transit != null)
                {
                    var prevContact = (TransientState & TransientStateFlags.Contact) != 0;

                    foreach (var collideObject in transit.CollisionInfo.CollideObject)
                        track_object_collision(collideObject, prevContact);
                }

                set_current_pos(RequestPos);
            }

            // for teleport, use SetPosition?
            if (isTeleport)
            {
                //Console.WriteLine($"*** SETTING TELEPORT ***");

                var setPosition = new SetPosition();
                setPosition.Pos = RequestPos;
                setPosition.Flags = SetPositionFlags.SendPositionEvent | SetPositionFlags.Slide | SetPositionFlags.Placement | SetPositionFlags.Teleport;

                SetPosition(setPosition);
            }

            UpdateTime = PhysicsTimer_CurrentTime;

            return success;
        }

        public bool Equals(PhysicsObj obj)
        {
            if (obj == null) return false;
            return ID == obj.ID;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ID);
        }
    }
}
