using System;

using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    public class MoveToParams(Action<bool> callback, WorldObject target, float? useRadius = null)
    {
        public Action<bool> Callback = callback;

        public WorldObject Target = target;

        public float? UseRadius = useRadius;
    }
}
