namespace BalanceBench;

internal static class NativeBenchAdapter
{
    internal static readonly string[] Names = { "Balanceeleven", "Balancetwelve", "Balancethirteen", "Balancefourteen", "Balancefifteen" };
    internal static bool ApprovedName(string name) => Names.Contains(name.TrimStart('+'), StringComparer.OrdinalIgnoreCase);
    internal static string Probe(Player p) => $"BalanceBench.TestOnly v1; gameplayOverrides=False; realSustainRequired=True; nativeAssembly={typeof(NetherFractureManager).Assembly.GetName().Version}; spellmath={ServerConfig.zc_dot_spell_math.Value}; " + NetherFractureManager.DeveloperStatus(p);
    internal static bool NeedsRearm(Player p, Creature target)
    {
        if (!p.IsAlive || !NetherFractureManager.CanTarget(p,target)) return false;
        var entries = target.Biota.PropertiesEnchantmentRegistry.Clone(target.BiotaDatabaseLock) ?? new();
        var families = entries.Where(e=>e.CasterObjectId==p.Guid.Full && (e.Duration<0 || e.Duration+e.StartTime>0)
                && target.ZcDotSnapshotFor(e) is {FractureHandCast:true,Generation:0})
            .Select(e=>NativeTelemetry.Family((uint)e.SpellId)).Distinct().ToArray();
        if(!new[]{"corrosion","corruption","curse"}.All(families.Contains)) return false;
        var state=p.NetherFracture;var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()/1000d;
        lock(state.Gate) return now>=state.Next && !state.Armed.Contains(target.Guid.Full) && state.Credits.Any(c=>now-c.At<=8&&c.Amount>0);
    }
}

internal static class TestOnlyPolicy
{
    internal static bool BlocksMeasuredSetup(string sub) =>
        new[]{"refill","charm","mirror","suitlines","surges","deedsrefresh","strip","dropgear","dropweapons","equip","cloneitem","healat","pack"}.Contains(sub,StringComparer.OrdinalIgnoreCase);
    internal static void CheckBench(Player p,string[] args)
    {
        if(!PatchClass.IsBench(p)) throw new InvalidOperationException("Only the five designated test characters are allowed.");
        if(args.Length==0)return;
        var sub=args[0].ToLowerInvariant();
        if(Bench.For(p).Run!=null && BlocksMeasuredSetup(sub))
            throw new InvalidOperationException("Stop the run before changing its kit, charm level or vitals.");
    }
    internal static void CheckExternal(Player p,string command,string[] args)
    {
        if(!PatchClass.IsBench(p))throw new InvalidOperationException("Unapproved character.");
        command=command.ToLowerInvariant();
        if(command=="bench")return;
        if(command=="charm" && args.Length==1 && new[]{"pandemic","chain","armorbreaker","shieldarc","improvedcleave","splitarrow","fork","splitcast"}.Contains(args[0].ToLowerInvariant()))return;
        if(command=="netherfracture" && (args.Length==0 || args.Length==1&&args[0].Equals("show",StringComparison.OrdinalIgnoreCase)))return;
        if(Bench.For(p).Run!=null)throw new InvalidOperationException("External setup commands are blocked during a measured run.");
        if(new[]{"testchar","attackable","asforge","wsforge","buff","teleloc"}.Contains(command))return;
        throw new InvalidOperationException("This test-only inbox cannot change global settings or run arbitrary server commands.");
    }
}
