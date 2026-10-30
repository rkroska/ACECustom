namespace BalanceBench;

internal sealed class LabMonster
{
    public uint Guid {get;set;}
    public string Name {get;set;}="";
    public uint MaxHealth {get;set;}
    public Dictionary<string,int> DirectApplications {get;set;}=new();
    public Dictionary<string,int> SpreadApplications {get;set;}=new();
    public Dictionary<string,double> Damage {get;set;}=new();
    public Dictionary<string,int> Ticks {get;set;}=new();
    public string[] ActiveFamiliesOnLastHit {get;set;}=Array.Empty<string>();
    public bool Dead {get;set;}
    public double? FirstApplicationAt {get;set;}
    public double? DeathAt {get;set;}
    public string LastDamage {get;set;}="";
}

// Observer methods only. No banking, damage calculations, enchantment changes or visual calls.
[HarmonyPatch]
internal static class NativeTelemetry
{
    private sealed record Tag(uint Target,uint Caster,uint Spell,int Generation);
    [ThreadStatic] private static Tag? pending;
    [ThreadStatic] private static int receiveGeneration;
    internal static string Family(uint spell)=>Creature.IsCorrosionSpell(spell)?"corrosion":Creature.IsCorruptionSpell(spell)?"corruption":Creature.IsDestructiveCurseSpell(spell)?"curse":"other";
    private static LabMonster? Monster(Creature c,Player p)
    {
        if(!PatchClass.IsBench(p))return null;
        var ctx=Bench.For(p);
        if(ctx.Run==null||!ctx.Spawns.ContainsKey(c.Guid.Full))return null;
        if(!ctx.Run.LabMonsters.TryGetValue(c.Guid.Full,out var m))ctx.Run.LabMonsters[c.Guid.Full]=m=new(){Guid=c.Guid.Full,Name=c.Name,MaxHealth=c.Health.MaxValue};
        return m;
    }
    [HarmonyPostfix,HarmonyPatch(typeof(Creature),nameof(Creature.ZcDotTickAmount))]
    private static void Tick(Creature __instance,Creature.ZcDotSnapshot snap,WorldObject damager)
    {if(damager is Player p&&PatchClass.IsBench(p))pending=new(__instance.Guid.Full,p.Guid.Full,snap.SpellId,receiveGeneration>0?receiveGeneration:snap.Generation);}
    [HarmonyPrefix,HarmonyPatch(typeof(Creature),"ZcPandemicReceive")]
    private static void Receive(int generation,out int __state){__state=receiveGeneration;receiveGeneration=generation;}
    [HarmonyFinalizer,HarmonyPatch(typeof(Creature),"ZcPandemicReceive")]
    private static void ReceiveEnd(int __state)=>receiveGeneration=__state;
    [HarmonyPrefix,HarmonyPatch(typeof(ACE.Server.WorldObjects.Managers.EnchantmentManager),"ApplyDamageTick")]
    private static void Begin(out object? __state){__state=pending;pending=null;}
    [HarmonyFinalizer,HarmonyPatch(typeof(ACE.Server.WorldObjects.Managers.EnchantmentManager),"ApplyDamageTick")]
    private static void End(object? __state)=>pending=__state as Tag;
    [HarmonyPostfix,HarmonyPatch(typeof(Creature),"ZcPandemicArm")]
    private static void Applied(Creature __instance,Player caster,Spell spell,PropertiesEnchantmentRegistry entry)
    {
        if(entry==null||!Creature.IsVoidDotSpell(spell.Id))return;
        var m=Monster(__instance,caster);if(m==null)return;
        var f=Family(spell.Id);m.DirectApplications[f]=m.DirectApplications.GetValueOrDefault(f)+1;
    }
    [HarmonyPostfix,HarmonyPatch(typeof(DamageHistory),nameof(DamageHistory.Add))]
    private static void Damage(DamageHistory __instance,WorldObject attacker,uint amount)
    {
        if(amount==0||__instance.Creature is not Creature c)return;
        if(attacker is not Player p||!PatchClass.IsBench(p))
        {
            var owner=Bench.Owner(c.Guid.Full);
            if(owner?.Run!=null){using var scope=Bench.Use(owner);Outbox.Write("native_external_damage",new{target=c.Guid.Full,attacker=attacker?.Name,amount});}
            return;
        }
        var m=Monster(c,p);if(m==null)return;
        string key;var tag=pending;
        if(tag!=null&&tag.Target==c.Guid.Full&&tag.Caster==p.Guid.Full&&Hooks.Kind is "dot" or "dotland")
        {
            key=Family(tag.Spell)+":"+(tag.Generation==0?"direct":"spread-g"+tag.Generation)+":"+(Hooks.Kind=="dotland"?"landing":"periodic");
            m.Ticks[key]=m.Ticks.GetValueOrDefault(key)+1;
            var ctx=Bench.For(p);ctx.Run!.LabDotTicks[tag.Spell]=ctx.Run.LabDotTicks.GetValueOrDefault(tag.Spell)+1;
            ctx.Run.LabDotGenerations[tag.Generation]=ctx.Run.LabDotGenerations.GetValueOrDefault(tag.Generation)+1;
            if(tag.Generation>0&&Hooks.Kind=="dotland"){var f=Family(tag.Spell);m.SpreadApplications[f]=m.SpreadApplications.GetValueOrDefault(f)+1;}
            pending=null;
        }
        else key=Hooks.Kind?.StartsWith("spell:",StringComparison.Ordinal)==true&&Hooks.Kind.Contains("Nether Streak",StringComparison.Ordinal)?"streak":Hooks.Kind??"other";
        m.Damage[key]=m.Damage.GetValueOrDefault(key)+amount;m.LastDamage=key;
        m.FirstApplicationAt??=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()/1000d;
        m.ActiveFamiliesOnLastHit=(c.Biota.PropertiesEnchantmentRegistry.Clone(c.BiotaDatabaseLock)??new()).Where(e=>e.CasterObjectId==p.Guid.Full&&Creature.IsVoidDotSpell((uint)e.SpellId)).Select(e=>Family((uint)e.SpellId)).Distinct().ToArray();
    }
    [HarmonyPostfix,HarmonyPatch(typeof(Creature),"Die",new[]{typeof(DamageHistoryInfo),typeof(DamageHistoryInfo)})]
    private static void Death(Creature __instance)
    {
        foreach(var ctx in Bench.All.Values)
            if(ctx.Run?.LabMonsters.TryGetValue(__instance.Guid.Full,out var m)==true){m.Dead=true;m.DeathAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()/1000d;}
    }
}

// Native pulses queue their damage, so inspect the actual caller at impact rather than guessing
// that every untagged Nether hit is a Fracture. This prefix changes observer metadata only.
[HarmonyPatch(typeof(Creature),nameof(Creature.TakeDamage),new[]{typeof(WorldObject),typeof(DamageType),typeof(float),typeof(bool)})]
internal static class NativeFractureHitObserver
{
    private sealed record Scope(string? Kind,bool Fracture);
    [HarmonyPrefix] private static void Begin(Creature __instance,WorldObject source,DamageType damageType,out object __state)
    {
        var fracture=false;
        if(source is Player p&&PatchClass.IsBench(p)&&Bench.For(p).Run!=null&&Bench.For(p).Spawns.ContainsKey(__instance.Guid.Full)&&damageType==DamageType.Nether)
            fracture=new System.Diagnostics.StackTrace().GetFrames().Any(f=>Root(f.GetMethod()?.DeclaringType)==typeof(NetherFractureManager));
        __state=new Scope(Hooks.Kind,fracture);
        if(fracture)Hooks.Kind="nether-fracture";
    }
    private static Type? Root(Type? type){while(type?.DeclaringType!=null)type=type.DeclaringType;return type;}
    [HarmonyPostfix] private static void Hit(Creature __instance,WorldObject source,float amount,uint __result,object __state)
    {
        if(__state is not Scope{Fracture:true}||source is not Player p)return;
        using var scope=Bench.Use(Bench.For(p));
        Outbox.Write("native_fracture_hit",new{caster=p.Name,target=__instance.Guid.Full,name=__instance.Name,requested=amount,actualDamage=__result});
    }
    [HarmonyFinalizer] private static void End(object __state){if(__state is Scope s)Hooks.Kind=s.Kind;}
}

[HarmonyPatch(typeof(NetherFractureState),"Release")]
internal static class NativeReleaseObserver
{
    [HarmonyPostfix] private static void Released(NetherFractureState __instance,uint target,double budget,bool __result)
    {
        if(!__result)return;
        var p=PlayerManager.GetAllOnline().FirstOrDefault(p=>PatchClass.IsBench(p)&&ReferenceEquals(p.NetherFracture,__instance));if(p==null)return;
        using var scope=Bench.Use(Bench.For(p));Outbox.Write("native_fracture_release",new{caster=p.Name,target,budget});
    }
}
