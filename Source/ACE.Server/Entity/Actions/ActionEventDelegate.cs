using System;

namespace ACE.Server.Entity.Actions
{
    public class ActionEventDelegate : ActionEventBase
    {
        public override ActionType Type { get; }
        public readonly Action Action;

        public ActionEventDelegate(ActionType type, Action action)
        {
            Type = type;
            Action = action;
        }

        public override Tuple<IActor, IAction> Act()
        {
            Action();

            return base.Act();
        }
    }
}
