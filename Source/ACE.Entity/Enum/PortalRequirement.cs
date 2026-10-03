namespace ACE.Entity.Enum
 {
     public enum PortalRequirement
     {
         None        = 0,
         CreatureAug = 1,
         ItemAug     = 2,
         LifeAug     = 3,
         Enlighten   = 4,
         QuestBonus  = 5,
         XPMultiplier = 6,

         /// <summary>Triune Weave points (PropertyInt64.TriuneWeaveCount). In the owner's 50000 range so an
         /// upstream value 7+ can never collide. Owner 2026-10-02: the Tou Tou T16+ tier gate.</summary>
         TriuneWeave = 50000,

     }
 }
