using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Json;
using Foxfire;

// Standalone Mono microbenchmarks, NOT Unity player, Linux, or real-time soak results.
class PerformanceProbe
{
    static void Measure(string name, int n, Action<int> body)
    {
        body(-1); // JIT/warm-up, excluded
        var times = new double[n];
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
        for (int i = 0; i < n; i++)
        {
            long start = Stopwatch.GetTimestamp();
            body(i);
            times[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        gc0 = GC.CollectionCount(0) - gc0;
        gc1 = GC.CollectionCount(1) - gc1;
        gc2 = GC.CollectionCount(2) - gc2;
        Array.Sort(times);
        double sum = 0; foreach (double t in times) sum += t;
        Console.WriteLine("{0},{1},{2:F6},{3:F6},{4:F6},{5:F2},{6},{7},{8}", name, n,
            sum/n, times[(int)Math.Ceiling(n*.99)-1], times[n-1], allocated/(double)n, gc0, gc1, gc2);
    }
    static void Main(string[] args)
    {
        Console.WriteLine("case,operations,mean_ms,p99_ms,max_ms,allocated_bytes_per_operation,gc0,gc1,gc2");
        Balance b;
        using (var input = File.OpenRead("artifacts/player-balance.json"))
            b = (Balance)new DataContractJsonSerializer(typeof(Balance)).ReadObject(input);
        var game = new Simulation(b, 1);
        Measure("normal_core_tick", 180000, i => game.Step(1f/30));
        // Synthetic sustained collision/AI ceiling. Fixture setup/reset is included and identical in A/B.
        b = new Balance { introSeconds = 0, bossAt = 1000000, maxSeconds = 1000001,
            foxHP = 1000000, phaseCaps = new[] {40,40,40,40} };
        game = new Simulation(b, 1); game.Step(1f/30);
        for (int i=0;i<40;i++) game.AddEnemy((EnemyKind)(i%3),new V(i%10-5,i/10-2),false);
        for (int i=0;i<80;i++) game.DropXP(new V(12,i%12-6),1);
        for (int i=0;i<60;i++) game.Effect(new V(i%10-5,i/10-3),1,0,1000000);
        for (int i=0;i<200;i++) game.Shots.Add(new Shot { pos=new V(-12,i%12-6),
            velocity=new V(1,0),life=1000000,hostile=i%2==0,damage=0,pierce=1000000 });
        Measure("synthetic_saturated_tick",18000,i => {
            game.Fox = new V(); game.HP=game.MaxHP;
            foreach(var e in game.Enemies) { e.hp=1000000; e.pos=new V(e.id%10-5,e.id/10-2); }
            foreach(var s in game.Shots) { s.pos=new V(-12,s.hostile ? 5 : -5); s.life=1000000; }
            game.Step(1f/30);
        });
        string dir = args.Length>0 ? args[0] : Path.Combine(Path.GetTempPath(),"foxfire-probe-"+Guid.NewGuid());
        Directory.CreateDirectory(dir);
        string path=Path.Combine(dir,"receipts.tsv");
        using(var writer=new StreamWriter(path)) for(int i=0;i<10000;i++) writer.WriteLine("seed-"+i+"\texecuted\t");
        var journal=new ReceiptJournal(10000,path);
        int receiptCount=args.Length>1?int.Parse(args[1]):1000;
        Measure("full_journal_terminal_receipt",receiptCount,i => journal.Remember(new Receipt("probe-"+i,"rejected","shared_cooldown")));
        Measure("full_journal_startup",20,i => { var loaded=new ReceiptJournal(10000,path); if(loaded.Count!=10000) throw new Exception("journal count"); });
    }
}
