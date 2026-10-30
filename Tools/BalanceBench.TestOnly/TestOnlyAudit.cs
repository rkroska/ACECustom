using System.Reflection;
using System.Reflection.Emit;

namespace BalanceBench;

public static class TestOnlyAudit
{
    public static string[] Inspect()
    {
        var errors=new List<string>();var assembly=typeof(TestOnlyAudit).Assembly;
        if(assembly.GetName().Name!="BalanceBench.TestOnly")errors.Add("Wrong harness assembly identity.");
        var types=assembly.GetTypes();
        foreach(var forbidden in new[]{"VoidLab","LiveVoidDamage","ActivePayoff","ManualRuptureCredits","IndependentVoidStacks","FractureGroupLab"})
            if(types.Any(t=>t.Name==forbidden))errors.Add("Prototype type compiled: "+forbidden);
        var roots=new List<MethodBase>();
        foreach(var method in types.SelectMany(t=>t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance|BindingFlags.DeclaredOnly)))
        {
            var attrs=method.CustomAttributes.Select(a=>a.AttributeType.Name).ToArray();
            if(attrs.Any(a=>a is "HarmonyTranspiler" or "HarmonyReversePatch"))errors.Add("Code-rewriting patch: "+method.Name);
            if(!attrs.Any(a=>a is "HarmonyPrefix" or "HarmonyPostfix" or "HarmonyFinalizer"))continue;
            roots.Add(method);
            if(method.ReturnType!=typeof(void))errors.Add("Non-observer patch return type: "+method.Name);
            foreach(var p in method.GetParameters())
                if(p.ParameterType.IsByRef&&p.Name!="__state")errors.Add("Patch can write native arguments/results: "+method.Name+"."+p.Name);
        }
        var pending=new Queue<MethodBase>(roots);var seen=new HashSet<MethodBase>();
        var game=typeof(Creature).Assembly;
        var forbiddenCalls=new HashSet<string>{"TakeDamage","TakeDamageOverTime","ApplyDamageTick","ZcDotLandingTick","ZcPandemicArm","ZcPandemicReceive","ZcPandemicOnTick","BeforeStreak","SetZone","Credit","Release","Prime","Prune","Trigger","Pulse","BurstVisual","SingleVisual","SetProperty","UpdateVitalDelta","Destroy","AddEnchantment","CreateEnchantment"};
        while(pending.Count>0)
        {
            var method=pending.Dequeue();if(!seen.Add(method))continue;
            foreach(var instruction in Members(method))
            {
                var member=instruction.Member;
                if(member is MethodBase called)
                {
                    if(called.Module.Assembly==assembly)pending.Enqueue(called);
                    if(called.DeclaringType?.Assembly==game&&(forbiddenCalls.Contains(called.Name)||called.Name.StartsWith("set_",StringComparison.Ordinal)))
                        errors.Add("Observer invokes gameplay mutation: "+method.Name+" -> "+called.DeclaringType.Name+"."+called.Name);
                }
                if(member is FieldInfo field&&(instruction.Op==OpCodes.Stfld||instruction.Op==OpCodes.Stsfld)&&field.DeclaringType?.Assembly==game)
                    errors.Add("Observer writes native game field: "+method.Name+" -> "+field.Name);
            }
        }
        return errors.Distinct().ToArray();
    }
    public static void AssertPure()
    {
        var errors=Inspect();if(errors.Length>0)throw new InvalidOperationException(string.Join("; ",errors));
    }
    private sealed record Instruction(OpCode Op,MemberInfo Member);
    private static IEnumerable<Instruction> Members(MethodBase method)
    {
        var bytes=method.GetMethodBody()?.GetILAsByteArray();if(bytes==null)yield break;
        var codes=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)!).ToDictionary(o=>(ushort)o.Value);
        var i=0;
        while(i<bytes.Length)
        {
            ushort value=bytes[i++];if(value==0xfe)value=(ushort)(0xfe00|bytes[i++]);var op=codes[value];var offset=i;
            switch(op.OperandType)
            {
                case OperandType.InlineNone:break;
                case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineI:case OperandType.ShortInlineVar:i++;break;
                case OperandType.InlineVar:i+=2;break;
                case OperandType.InlineI8:case OperandType.InlineR:i+=8;break;
                case OperandType.InlineSwitch:i+=4+4*BitConverter.ToInt32(bytes,i);break;
                default:i+=4;break;
            }
            if(op.OperandType is not (OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineTok))continue;
            var member=method.Module.ResolveMember(BitConverter.ToInt32(bytes,offset),method.DeclaringType?.GetGenericArguments(),method is MethodInfo m?m.GetGenericArguments():null);
            if(member!=null)yield return new(op,member);
        }
    }
}
