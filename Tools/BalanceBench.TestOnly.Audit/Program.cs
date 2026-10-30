using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

if(args.Length is <2 or >3)throw new ArgumentException("Usage: audit <harness DLL> <native ACE bin directory> [--patch-smoke]");
var mod=Path.GetFullPath(args[0]);var native=Path.GetFullPath(args[1]);
AssemblyLoadContext.Default.Resolving+=(context,name)=>
{
    foreach(var dir in new[]{Path.GetDirectoryName(mod)!,native})
    {
        var path=Path.Combine(dir,name.Name+".dll");
        if(File.Exists(path))return context.LoadFromAssemblyPath(path);
    }
    return null;
};
try
{
    var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(mod);
    var audit=assembly.GetType("BalanceBench.TestOnlyAudit",true)!.GetMethod("Inspect",BindingFlags.Public|BindingFlags.Static)!;
    var errors=(string[])audit.Invoke(null,null)!;
    bool? patchSmoke=null;int? patchedMethods=null;
    if(errors.Length==0&&args.Length==3&&args[2]=="--patch-smoke")
    {
        var entry=assembly.GetType(assembly.GetName().Name+".Mod",true)!;
        if(entry.GetConstructor(Type.EmptyTypes)==null)throw new InvalidOperationException("ACE plugin entry requires a public default constructor.");
        var game=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(native,"ACE.Server.dll"));
        double[] Calculate()
        {
            var math=game.GetType("ACE.Server.Managers.NetherFractureMath",true)!;
            var factor=math.GetMethod("GroupFactor")!;
            var stateType=game.GetType("ACE.Server.WorldObjects.NetherFractureState",true)!;
            var state=Activator.CreateInstance(stateType)!;
            var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
            stateType.GetMethod("Prime",flags)!.Invoke(state,new object[]{100u,100d});
            stateType.GetMethod("Credit",flags)!.Invoke(state,new object[]{1000u,101d});
            var releaseArgs=new object[]{100u,102d,6d,0d};
            var released=(bool)stateType.GetMethod("Release",flags)!.Invoke(state,releaseArgs)!;
            return new[]{(double)factor.Invoke(null,new object[]{1,100d,100d})!, (double)factor.Invoke(null,new object[]{3,300d,100d})!,released?1d:0d,(double)releaseArgs[3]};
        }
        var before=Calculate();
        var harmonyType=Assembly.Load("0Harmony").GetType("HarmonyLib.Harmony",true)!;
        var harmony=Activator.CreateInstance(harmonyType,new object[]{"BalanceBench.TestOnly.IsolatedSmoke"})!;
        try
        {
            harmonyType.GetMethod("PatchAll",new[]{typeof(Assembly)})!.Invoke(harmony,new object[]{assembly});
            var originals=(IEnumerable<MethodBase>)harmonyType.GetMethod("GetAllPatchedMethods",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,null)!;
            patchedMethods=originals.Count();
            var after=Calculate();
            patchSmoke=before.SequenceEqual(after)&&before[0]==1&&before[1]==2d/3&&before[2]==1&&before[3]==1200&&patchedMethods>0;
            if(patchSmoke!=true)throw new InvalidOperationException("Native math/banking changed under the observer hooks.");
        }
        finally
        {
            var cleanup=harmonyType.GetMethod("UnpatchSelf")??harmonyType.GetMethod("UnpatchAll",new[]{typeof(string)});
            if(cleanup!=null)cleanup.Invoke(cleanup.IsStatic?null:harmony,cleanup.GetParameters().Length==0?null:new object[]{"BalanceBench.TestOnly.IsolatedSmoke"});
        }
    }
    var setup=assembly.GetType("BalanceBench.Mod")?.BaseType?.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).FirstOrDefault(m=>m.Name=="Setup");
    var setupArguments=setup?.GetParameters().Select(p=>p.Name+":"+p.ParameterType.Name).ToArray();
    Console.WriteLine(JsonSerializer.Serialize(new{assembly=assembly.GetName().Name,observerOnly=errors.Length==0,errors,noGameWorldStarted=true,patchSmoke,patchedMethods,setupArguments},new JsonSerializerOptions{WriteIndented=true}));
    Environment.ExitCode=errors.Length==0?0:2;
}
catch(Exception ex)
{
    Console.Error.WriteLine(ex.GetBaseException());Environment.ExitCode=3;
}
