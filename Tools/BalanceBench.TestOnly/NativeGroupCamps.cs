namespace BalanceBench;

internal static class NativeGroupCamps
{
    private static Player? owner;
    private static string[] actors=Array.Empty<string>();
    internal static void Command(Player p,string[] args)
    {
        if(!p.Name.TrimStart('+').Equals("Balancefourteen",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Fourteen owns shared camps.");
        var action=args.Length>1?args[1]:"status";
        if(action=="stop")
        {
            foreach(var who in PlayerManager.GetAllOnline().Where(w=>actors.Contains(w.Name.TrimStart('+'),StringComparer.OrdinalIgnoreCase)))
            {
                var c=Bench.For(who);c.Fighting=false;c.TrialDeadline=null;c.Run=null;
                if(who!=owner)c.Spawns.Clear();
            }
            if(owner!=null){using var scope=Bench.Use(Bench.For(owner));Bench.Clear(owner,0);}
            owner=null;actors=Array.Empty<string>();Commands.Say(p,"Shared native test camp stopped.");return;
        }
        if(action=="camp"&&args.Length==3)
        {
            var chosen=args[2].Split(',');
            if(owner!=null||!chosen.Contains("Balancefourteen",StringComparer.OrdinalIgnoreCase)||chosen.Any(n=>!NativeBenchAdapter.ApprovedName(n)))throw new InvalidOperationException("Stop existing group first; approved characters including Fourteen only.");
            foreach(var name in chosen)
            {
                var who=PlayerManager.GetAllOnline().Single(w=>w.Name.TrimStart('+').Equals(name,StringComparison.OrdinalIgnoreCase));var c=Bench.For(who);
                if(c.Run!=null||c.Fighting||who.Location.Variation!=p.Location.Variation||who.Location.Distance2D(p.Location)>20)throw new InvalidOperationException("Each caster must be idle and nearby in the same variation.");
            }
            owner=p;actors=chosen;Commands.Say(p,"Native ordinary-health shared camp armed; real sustain only, no native bank resets.");return;
        }
        Commands.Say(p,"Test-only group: /bench fracturelab camp Balancefourteen,Balancethirteen,Balancefifteen | stop");
    }
    internal static void Start(Player p)
    {
        if(p!=owner||actors.Length==0||Bench.For(p).Run==null)return;
        var main=Bench.For(p);
        foreach(var who in PlayerManager.GetAllOnline().Where(w=>actors.Contains(w.Name.TrimStart('+'),StringComparer.OrdinalIgnoreCase)&&w!=p))
        {
            var c=Bench.For(who);c.Spawns.Clear();foreach(var pair in main.Spawns)c.Spawns[pair.Key]=pair.Value;
            c.Run=new Stats{Start=main.Run!.Start};c.RunLabel=main.RunLabel;c.TrialDeadline=null;c.RealSustain=true;c.CastSeconds=2;c.HealAt=.5;
            c.SpellId=5394;c.DotSpell2=5402;c.DotSpell3=5338;c.SpellId2=5348;c.DotRefresh=10;c.DotMax=0;c.LabCombo=true;c.LabCatalyst=true;
            c.LastCast.Clear();c.LastCast2.Clear();c.LastCast3.Clear();c.LabComboReleased.Clear();c.LastIssue=default;c.BusyUntil=default;c.Fighting=true;
        }
    }
    internal static void Finish(Player p,string outcome)
    {
        if(p!=owner||actors.Length==0)return;
        var main=Bench.For(p);var last=main.Spawns.Values.Where(t=>t.Died!=null).Select(t=>t.Died).Max();
        foreach(var who in PlayerManager.GetAllOnline().Where(w=>actors.Contains(w.Name.TrimStart('+'),StringComparer.OrdinalIgnoreCase)))
        {
            var c=Bench.For(who);if(c.Run==null)continue;c.Fighting=false;c.Run.LastKill=last;
            using var scope=Bench.Use(c);Outbox.Write("native_group_summary",new{caster=who.Name,label=main.RunLabel,summary=Bench.Summary(outcome,who)});
            if(who!=p){c.Run=null;c.Spawns.Clear();}
        }
    }
}
[HarmonyPatch(typeof(Bench),nameof(Bench.StartTrial))]
internal static class NativeGroupStartObserver{[HarmonyPostfix]private static void Start(Player p)=>NativeGroupCamps.Start(p);}
[HarmonyPatch(typeof(Bench),"FinishTrial")]
internal static class NativeGroupFinishObserver{[HarmonyPrefix]private static void End(Player p,string outcome)=>NativeGroupCamps.Finish(p,outcome);}
