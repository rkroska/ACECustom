global using HarmonyLib;
global using ACE.Server.WorldObjects;

namespace BalanceBench;
// Deliberately unsafe fixture. This project is NEVER included in the installable harness.
internal static class ActivePayoff { }
internal static class NegativeHooks
{
    [HarmonyPostfix,HarmonyPatch(typeof(Creature),nameof(Creature.ZcDotTickAmount))]
    internal static void MutatesDamage(ref float __result)=>__result*=2;
    [HarmonyPrefix,HarmonyPatch(typeof(Creature),nameof(Creature.ZcDotTickAmount))]
    internal static bool SkipsNative()=>false;
    [HarmonyPostfix,HarmonyPatch(typeof(Creature),nameof(Creature.ZcDotTickAmount))]
    internal static void MutatesSnapshot(Creature.ZcDotSnapshot snap)=>snap.NormalPre*=2;
}
