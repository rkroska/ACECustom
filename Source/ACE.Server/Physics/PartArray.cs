using System;
using System.Collections.Generic;
using System.Numerics;
using ACE.DatLoader.Entity;
using ACE.Entity.Enum;
using ACE.Server.Physics.Animation;
using ACE.Server.Physics.Collision;
using ACE.Server.Physics.Common;

namespace ACE.Server.Physics
{
    /// <summary>
    /// A list of physics parts
    /// </summary>
    public class PartArray
    {
        //public uint State;
        public PhysicsObj Owner;
        public Sequence Sequence;
        public MotionTableManager MotionTableManager;
        public Setup Setup;
        public int NumParts;
        public List<PhysicsPart> Parts;
        public Vector3 Scale;
        //public AnimationFrame LastAnimFrame;

        public PartArray()
        {
            Sequence = new Sequence();
            Scale = Vector3.One;
        }

        public void AddPartsShadow(ObjCell objCell, int numShadowParts)
        {
            //List<int> clipPlaneList = null;

            //if (numShadowParts > 1)
            //    clipPlaneList = objCell.ClipPlanes;

            for (var i = 0; i < NumParts; i++)
            {
                if (Parts[i] != null)
                    objCell.AddPart(Parts[i], null, objCell.Pos.Frame, numShadowParts);
            }
        }

        public bool AllowsFreeHeading()
        {
            return Setup._dat.AllowFreeHeading;
        }

        public void AnimationDone(bool success)
        {
            if (MotionTableManager != null)
                MotionTableManager.AnimationDone(success);
        }

        public bool CacheHasPhysicsBSP()
        {
            foreach (var part in Parts)
            {
                if (part.GfxObj.PhysicsBSP != null)
                {
                    //State |= 0x10000;
                    return true;
                }
            }
            //State &= 0xFFFEFFFF;
            return false;
        }

        public void CheckForCompletedMotions()
        {
            if (MotionTableManager != null)
                MotionTableManager.CheckForCompletedMotions();
        }

        public static PartArray CreateMesh(PhysicsObj owner, uint setupDID)
        {
            var mesh = new PartArray();
            mesh.Owner = owner;
            mesh.Sequence.SetObject(owner);
            if (!mesh.SetMeshID(setupDID))
                return null;
            mesh.SetPlacementFrame(0x65);
            return mesh;
        }

        public static PartArray CreateParticle(PhysicsObj owner, int numParts, Sphere sortingSphere = null)
        {
            var particle = new PartArray();
            particle.Owner = owner;
            particle.Sequence.SetObject(owner);

            particle.Setup = Setup.MakeParticleSetup(numParts);

            if (particle.Setup == null || !particle.InitParts())
                return null;

            return particle;
        }

        public static PartArray CreateSetup(PhysicsObj owner, uint setupDID, bool createParts)
        {
            var setup = new PartArray();
            setup.Owner = owner;
            setup.Sequence.SetObject(owner);
            if (!setup.SetSetupID(setupDID, createParts))
                return null;
            setup.SetPlacementFrame(0x65);
            return setup;
        }


        public void DestroyParts()
        {
            Parts = null;
            NumParts = 0;
        }

        public void DestroySetup()
        {
            Setup = null;
        }

        public WeenieError DoInterpretedMotion(uint motion, MovementParameters movementParameters)
        {
            if (MotionTableManager == null) return WeenieError.NoAnimationTable;

            var mvs = new MovementStruct(MovementType.InterpretedCommand, motion, movementParameters);
            return MotionTableManager.PerformMovement(mvs, Sequence);
        }

        public TransitionState FindObjCollisions(Transition transition)
        {
            foreach (var part in Parts)
            {
                var result = part.FindObjCollisions(transition);
                if (result != TransitionState.OK)
                    return result;
            }
            return TransitionState.OK;
        }


        public List<CylSphere> GetCylSphere()
        {
            return Setup.CylSphere;
        }


        public float GetHeight()
        {
            return Setup._dat.Height * Scale.Z;
        }

        public int GetNumCylsphere()
        {
            return Setup.NumCylsphere;
        }

        public int GetNumSphere()
        {
            return Setup.NumSphere;
        }

        public float GetRadius()
        {
            return Setup._dat.Radius * Scale.Z;
        }


        public Sphere GetSortingSphere()
        {
            if (Setup == null)
                return PhysicsGlobals.DefaultSortingSphere;
            else
                return Setup.SortingSphere;
        }

        public List<Sphere> GetSphere()
        {
            return Setup.Sphere;
        }

        public float GetStepDownHeight()
        {
            if (Setup == null) return PhysicsGlobals.DefaultStepHeight;

            return Setup._dat.StepDownHeight * Scale.Z;
        }

        public float GetStepUpHeight()
        {
            if (Setup == null) return PhysicsGlobals.DefaultStepHeight;

            return Setup._dat.StepUpHeight * Scale.Z;
        }

        public void HandleEnterWorld()
        {
            if (MotionTableManager != null)
                MotionTableManager.HandleEnterWorld(Sequence);
        }

        public void HandleExitWorld()
        {
            if (MotionTableManager != null)
                MotionTableManager.HandleExitWorld();
        }

        public void HandleMovement()
        {
            if (MotionTableManager != null)
                MotionTableManager.UseTime();
        }

        public void InitDefaults()
        {
            if (Setup._dat.DefaultAnimation != 0)
            {
                Sequence.clear_animations();
                var animData = new Animation.AnimData();
                animData.AnimID = Setup._dat.DefaultAnimation;
                animData.LowFrame = 0;
                animData.HighFrame = Int32.MaxValue;
                Sequence.append_animation(animData);
                WeenieDesc.Destroy(animData);
            }

            if (Owner != null)
                Owner.InitDefaults(Setup);
        }

        public void InitializeMotionTables()
        {
            if (MotionTableManager != null)
                MotionTableManager.initialize_state(Sequence);
        }

        public void InitPals()
        {
            // palettes omitted for server
        }

        public bool InitParts()
        {
            NumParts = Setup.NumParts;
            if (NumParts == 0) return false;

            Parts = new List<PhysicsPart>(NumParts);
            for (var i = 0; i < NumParts; i++)
                Parts.Add(null);

            if (Setup.Parts == null) return true;

            // does this need to be pre-initialized /
            // can this part fail?
            var created = 0;
            for (var i = 0; i < NumParts; i++)
            {
                Parts[i] = PhysicsPart.MakePhysicsPart(Setup.Parts[i]);
                if (Parts[i] == null)
                    break;

                created++;
            }

            if (created == NumParts)
            {
                for (var i = 0; i < NumParts; i++)
                {
                    Parts[i].PhysicsObj = Owner;
                    //Parts[i].PhysObjIndex = i;
                }
                
                if (Setup._dat.DefaultScale != null && Setup._dat.DefaultScale.Count == NumParts)
                {
                    for (var i = 0; i < NumParts; i++)
                        Parts[i].GfxObjScale = Setup._dat.DefaultScale[i];  // mutable?
                }
                return true;
            }

            return false;
        }

        public void RemoveParts(ObjCell cell)
        {
            foreach (var part in Parts)
                cell.RemovePart(part);
        }

        public void SetCellID(uint cellID)
        {
            foreach (var part in Parts)
            {
                if (part != null)
                    part.Pos.ObjCellID = cellID;
            }
        }

        public void SetFrame(AFrame frame)
        {
            UpdateParts(frame);
            // remove lights
        }

        public void SetLuminosityInternal(float luminosity)
        {
            // gfx omitted from server
        }


        public bool SetMeshID(uint meshDID)
        {
            if (meshDID == 0) return false;
            var setup = Setup.MakeSimpleSetup(meshDID);
            if (setup == null) return false;
            DestroyParts();
            DestroySetup();
            Setup = setup;
            return InitParts();
        }

        public bool SetMotionTableID(uint mtableID)
        {
            if (MotionTableManager != null)
            {
                if (MotionTableManager.GetMotionTableID(mtableID) == mtableID)
                    return true;

                MotionTableManager = null;
            }
            if (mtableID == 0) return true;

            MotionTableManager = MotionTableManager.Create(mtableID);
            if (MotionTableManager == null) return false;

            MotionTableManager.SetPhysicsObject(Owner);

            // chat blob?
            return true;
        }

        public void SetNoDrawInternal(bool noDraw)
        {
            if (Setup == null) return;
            foreach (var part in Parts)
            {
                if (part != null)
                    part.SetNoDraw(noDraw);
            }
        }

        public void SetDiffusionInternal(float diff)
        {
            // gfx omitted from server
        }

        public void SetPartDiffusionInternal(int partIdx, float diff)
        {
            // gfx omitted from server
        }



        public void SetPartLuminosityInternal(int partIdx, float luminosity)
        {
            // gfx omitted from server
        }


        public void SetPartTranslucencyInternal(int partIdx, float translucency)
        {
            // gfx omitted from server
        }

        public bool SetPlacementFrame(int placementID)
        {

            // try to get placementID
            Setup._dat.PlacementFrames.TryGetValue(placementID, out PlacementType placementFrame);
            if (placementFrame != null)
            {
                Sequence.SetPlacementFrame(placementFrame.AnimFrame, placementID);
                return true;
            }

            // if failed, try to get index 0
            Setup._dat.PlacementFrames.TryGetValue(0, out placementFrame);
            if (placementFrame != null)
            {
                Sequence.SetPlacementFrame(placementFrame.AnimFrame, 0);
                return true;
            }

            // error out
            Sequence.SetPlacementFrame(null, 0);
            return false;
        }

        public bool SetScaleInternal(Vector3 newScale)
        {
            Scale = newScale;

            for (var i = 0; i < NumParts; i++)
            {
                var part = Parts[i];
                if (part != null)
                {
                    if (Setup != null && Setup._dat.DefaultScale != null && Setup._dat.DefaultScale.Count > i)
                        part.GfxObjScale = Setup._dat.DefaultScale[i] * newScale;
                    else
                        part.GfxObjScale = newScale;
                }
            }
            return true;
        }

        public bool SetSetupID(uint setupID, bool createParts)
        {
            if (Setup != null && Setup._dat.Id == setupID)
                return true;

            Setup = Setup.Get(setupID);
            if (Setup == null) return false;

            DestroyParts();

            if (createParts && !InitParts()) return false;

            InitDefaults();
            return true;
        }


        public void SetTranslucencyInternal(float translucency)
        {
            // gfx omitted from server
        }

        public WeenieError StopCompletelyInternal()
        {
            if (MotionTableManager == null) return WeenieError.NoAnimationTable;
            var mvs = new MovementStruct(MovementType.StopCompletely);
            return MotionTableManager.PerformMovement(mvs, Sequence);
        }

        public WeenieError StopInterpretedMotion(uint motion, MovementParameters movementParameters)
        {
            if (MotionTableManager == null) return WeenieError.NoAnimationTable;
            var mvs = new MovementStruct(MovementType.StopInterpretedCommand);
            mvs.Motion = motion;
            mvs.Params = movementParameters;
            return MotionTableManager.PerformMovement(mvs, Sequence);
        }

        public void Update(double quantum, ref AFrame offsetFrame)
        {
            Sequence.Update((float)quantum, ref offsetFrame);
        } 

        public void UpdateParts(AFrame frame)
        {
            var curFrame = Sequence.GetCurrAnimFrame();
            if (curFrame == null)
            {
                if (Parts.Count == 1)
                    Parts[0].Pos = Owner.Position;
                return;
            }
            var numParts = Math.Min(NumParts, curFrame.Frames.Count);
            for (var i = 0; i < numParts; i++)
                Parts[i].Pos.Frame.Combine(frame, new AFrame(curFrame.Frames[i]), Scale);
        }

        public void calc_cross_cells_static(ObjCell cell, CellArray cellArray)
        {
            if (cell != null)   // fixme
                cell.find_transit_cells(NumParts, Parts, cellArray);
        }
    }
}
