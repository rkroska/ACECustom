using System;
using System.Collections.Generic;
using ACE.Server.Physics.Common;

namespace ACE.Server.Physics
{
    [Flags]
    public enum TransientStateFlags
    {
        Contact = 0x1,
        OnWalkable = 0x2,
        Sliding = 0x4,
        WaterContact = 0x8,
        StationaryFall = 0x10,
        StationaryStop = 0x20,
        StationaryStuck = 0x40,
        StationaryComplete = StationaryStuck | StationaryStop | StationaryFall,
        Active = 0x80,
        CheckEthereal = 0x100
    };

    public enum PhysicsTimeStamp
    {
        Position = 0x0,
        Movement = 0x1,
        State = 0x2,
        Vector = 0x3,
        Teleport = 0x4,
        ServerControlledMove = 0x5,
        ForcePosition = 0x6,
        ObjDesc = 0x7,
        Instance = 0x8,
        NumPhysics = 0x9
    };

    public class PhysicsEngine
    {
        //public ObjectMaint ObjMaint;
        public SmartBox SmartBox;
        //public PhysicsObj Player;
        //public List<PhysicsObj> Iter;

        public static PhysicsEngine Instance;
        public bool Server;

        //public static List<PhysicsObj> StaticAnimatingObjects; // This is not used
        //public static double LastUpdate;

        static PhysicsEngine()
        {
            //StaticAnimatingObjects = new List<PhysicsObj>();
        }

        public PhysicsEngine(SmartBox smartBox)
        {
            //ObjMaint = objMaint;
            SmartBox = smartBox;

            SmartBox.Physics = this;
            Instance = this;
        }

        /*public static void AddStaticAnimatingObject(PhysicsObj obj) // Was used in PhysicsObj.InitDefaults
        {
            StaticAnimatingObjects.Add(obj);
        }*/

        /*public static void RemoveStaticAnimatingObject(PhysicsObj obj) // Was used in PhysicsObj.Destroy
        {
            StaticAnimatingObjects.Remove(obj);
        }*/


    }
}
