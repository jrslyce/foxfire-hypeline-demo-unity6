using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Collections.Generic;
using Foxfire;
class CoreTests
{
    static Balance TrialBalance()
    {
        if (!File.Exists("artifacts/player-balance.json"))
            return new Balance();
        using (var stream = File.OpenRead("artifacts/player-balance.json")) return (
            Balance) new DataContractJsonSerializer(typeof(Balance))
            .ReadObject(stream);
    }
    static int checks;
    const float Dt = 1f / 30;
    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition)
            throw new Exception("FAIL: " + message);
    }
    static Simulation Start(Balance b = null, int seed = 1)
    {
        var s = new Simulation(b ?? new Balance(), seed);
        while (!s.Active)
            s.Step(Dt);
        return s;
    }
    static void Advance(Simulation s, float t, InteractionDispatcher d = null)
    {
        for (int i = 0; i < (int)(t / Dt); i++)
        {
            s.Step(Dt);
            if (d != null)
                d.Tick();
        }
    }
    static ActionRequest Request(Simulation s, ActionKind k, string session = "a", string id = null)
    {
        return new ActionRequest { receiptId = id ?? Guid.NewGuid().ToString("N"), sessionId = session,
                                   actionId = Actions.Scoped(s.RunId, k) };
    }
    static void Main(string[] args)
    {
        try
        {
            Tests();
            Trials(args.Length > 0 ? int.Parse(args[0]) : 100);
            Console.WriteLine("PASS " + checks + " assertions");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            Environment.Exit(1);
        }
    }
    static void Tests()
    {
        Check(Actions.Label(ActionKind.SprigSwarm) == "Shroom Swarm", "shroom swarm display name");
        Check(Actions.Ids[(int)ActionKind.SprigSwarm] == "sprig_swarm", "shroom swarm action key preserved");
        var s = Start();
        Check(s.Phase == Phase.Combat, "auto intro");
        s.HP = 0;
        s.Step(Dt);
        Check(s.Outcome == Outcome.Defeat, "death");
        string run = s.RunId;
        s.Laser = 8;
        s.XP = 50;
        Advance(s, 11);
        Check(s.RunId != run && s.XP == 0 && s.Laser == 0 && s.Level == 0, "automatic clean reset");
        var b = new Balance { bossAt = .1f, maxSeconds = .3f, foxHP = 10000 };
        s = Start(b);
        Advance(s, .5f);
        Check(s.Outcome == Outcome.Timeout, "timeout");
        s = Start(b);
        Advance(s, .15f);
        s.Boss.hp = 0;
        s.Step(Dt);
        Check(s.Outcome == Outcome.Victory, "boss victory");
        s = Start(b);
        Advance(s, .15f);
        s.Boss.hp = 0;
        s.HP = 0;
        s.Step(Dt);
        Check(s.Outcome == Outcome.Defeat, "simultaneous death precedence");
        s = Start(new Balance { bossAt = .1f, maxSeconds = .2f });
        Advance(s, .15f);
        while (s.Time + Dt < s.B.maxSeconds)
            s.Step(Dt);
        s.Boss.hp = 0;
        s.Step(Dt);
        Check(s.Outcome == Outcome.Victory, "deadline victory precedence");
        s = Start();
        s.GrantUpgrade();
        Check(s.Upgrades[4] == 1, "pierce first");
        s.GrantUpgrade();
        Check(s.Upgrades[5] == 1, "orbit second");
        s.HP = 20;
        s.GrantUpgrade();
        Check(s.Upgrades[3] == 1 && s.MaxHP == 120 && s.HP == 40, "vitality prioritization");
        s = Start();
        for (int i = 0; i < 200; i++)
            s.DropXP(new V(i % 10, 0), 3);
        int total = 0;
        foreach (var p in s.Pickups)
            total += p.xp;
        Check(s.Pickups.Count == 80 && total == 600, "XP merging preserves total");
        for (int k = 0; k < 6; k++)
        {
            s = Start();
            s.HP = 60;
            s.AddEnemy(EnemyKind.Sprig, new V(6, 0), false);
            var g = new LocalSimulatorGateway();
            using (var d = new InteractionDispatcher(s, g, new ReceiptJournal(100)))
            {
                var r = Request(s, (ActionKind)k);
                Check(d.Submit(r).status == "queued", "action accepted " + k);
                d.Tick();
                Check(g.LastReceipt.status == "executed" && s.ExecutedActions == 1, "action executed " + k);
                d.Submit(r);
                Check(s.ExecutedActions == 1 && g.LastReceipt.status == "executed", "dedup " + k);
                Check(d.Cooldown((ActionKind)k) > 0, "execution starts cooldown " + k);
                switch ((ActionKind)k)
                {
                case ActionKind.HealPod:
                    Check(s.Pickups.Exists(p => p.heal), "pod spawned");
                    break;
                case ActionKind.LaserMode:
                    Check(s.Laser == 8, "laser active");
                    break;
                case ActionKind.StarStrike:
                    Check(s.Hazards.Exists(h => h.star), "strike tell");
                    break;
                case ActionKind.SprigSwarm:
                    Check(s.RegularCount == 4, "three swarm sprigs");
                    break;
                case ActionKind.ThornPatch:
                    Check(s.Hazards.Exists(h => h.viewer), "thorn tell");
                    break;
                case ActionKind.EnemyRush:
                    Check(s.Rush == 8, "rush active");
                    break;
                }
            }
        }
        // Shared cooldowns expire at five seconds even when an action has a longer-lived effect.
        foreach (var kind in new[] { ActionKind.StarStrike, ActionKind.SprigSwarm,
                  ActionKind.ThornPatch, ActionKind.EnemyRush, ActionKind.OrangeChampion })
        {
            s = Start();
            s.AddEnemy(EnemyKind.Boar, new V(8, 5), false);
            using (var d = new InteractionDispatcher(s, new LocalSimulatorGateway(), new ReceiptJournal(100)))
            {
                Check(d.Submit(Request(s, kind)).status == "queued", "cooldown fixture queues " + kind);
                d.Tick();
                Check(s.ExecutedActions == 1 && d.Cooldown(kind) == 5, "five-second cooldown starts " + kind);
                double deadline = s.Clock + 5;
                while (s.Clock < deadline - .1) s.Step(Dt);
                Check(d.Availability(kind) == "shared_cooldown", "cooldown blocks before deadline " + kind);
                while (s.Clock < deadline) s.Step(Dt);
                Check(d.Cooldown(kind) == 0 && d.Availability(kind) != "shared_cooldown",
                      "cooldown expires at deadline " + kind);
            }
        }
        s = Start();
        var sim = new LocalSimulatorGateway();
        using (var d = new InteractionDispatcher(s, sim, new ReceiptJournal(100)))
        {
            Check(d.Submit(Request(s, ActionKind.HealPod)).reason == "full_health", "reject full HP");
            Check(d.Submit(Request(s, ActionKind.StarStrike)).reason == "no_target", "reject no target");
            var stale = Request(s, ActionKind.LaserMode);
            stale.actionId = "fox_old_laser_mode";
            Check(d.Submit(stale).status == "rejected", "stale run rejected");
            Check(d.Cooldown(ActionKind.HealPod) == 0, "rejected action no cooldown");
            var req = Request(s, ActionKind.LaserMode);
            d.Submit(req);
            req.actionId = "mutated";
            d.Tick();
            Check(s.Laser > 0, "queued request copied");
            Check(d.Submit(Request(s, ActionKind.ThornPatch)).reason == "session_cooldown",
                  "session cooldown");
            s.HP = 50;
            Check(d.Submit(Request(s, ActionKind.HealPod, "second-anonymous-session")).status == "queued",
                  "distinct anonymous sessions");
            Advance(s, 1.1f, d);
            Check(s.ExecutedActions == 2, "second session execution");
            s.Reset();
            Check(d.QueueCount == 0 && d.Cooldown(ActionKind.LaserMode) == 0, "dispatcher reset");
        }
        s = Start(new Balance { bossAt = .1f });
        Advance(s, .2f);
        sim = new LocalSimulatorGateway();
        using (var d = new InteractionDispatcher(s, sim, new ReceiptJournal(100)))
        {
            s.AddEnemy(EnemyKind.Sprig, new V(8, 5), false);
            Check(d.Availability(ActionKind.SprigSwarm) == null, "boss permits swarm");
            Check(d.Availability(ActionKind.EnemyRush) == null, "boss permits rush");
            s.HP = 20;
            Check(d.Availability(ActionKind.ThornPatch) == null, "boss keeps hinder actions at low HP");
        }
        s = Start();
        s.HP = 70;
        sim = new LocalSimulatorGateway();
        using (var d = new InteractionDispatcher(s, sim, new ReceiptJournal(100)))
        {
            d.Submit(Request(s, ActionKind.HealPod));
            s.HP = 100;
            d.Tick();
            Check(sim.LastReceipt.reason == "full_health" && s.ExecutedActions == 0,
                  "revalidate before execution");
            d.Submit(Request(s, ActionKind.LaserMode));
            Advance(s, 10.1f);
            d.Tick();
            Check(sim.LastReceipt.reason == "queue_expired" && s.Laser == 0, "queue expires");
            for (int i = 0; i < s.Cap - 2; i++)
                s.AddEnemy(EnemyKind.Sprig, new V(10, 5), true);
            Check(d.Availability(ActionKind.SprigSwarm) == "enemy_capacity", "swarm capacity");
        }
        s = Start(new Balance { queueCap = 1 });
        sim = new LocalSimulatorGateway();
        using (var d = new InteractionDispatcher(s, sim, new ReceiptJournal(100)))
        {
            d.Submit(Request(s, ActionKind.LaserMode));
            Check(d.Submit(Request(s, ActionKind.ThornPatch, "b")).reason == "queue_full", "queue cap");
        }
        s = Start();
        sim = new LocalSimulatorGateway();
        using (var d = new InteractionDispatcher(s, sim, new ReceiptJournal(100)))
        {
            s.AddEnemy(EnemyKind.Sprig, new V(6, 4), false);
            d.Submit(Request(s, ActionKind.ThornPatch));
            Check(d.Submit(Request(s, ActionKind.EnemyRush, "b")).reason == "mischief_pending",
                  "world mischief reservation");
            d.Tick();
            Check(d.Availability(ActionKind.EnemyRush) == "mischief_active", "world mischief lock");
        }

        s = Start();
        s.HP = 20;
        sim = new LocalSimulatorGateway();
        using (var d = new InteractionDispatcher(s, sim, new ReceiptJournal(100)))
        {
            d.Submit(Request(s, ActionKind.HealPod));
            d.Tick();
            Advance(s, 8, d);
            Check(s.HP >= 50, "AI reaches and collects healing pod");
        }
        s = Start(new Balance { foxSpeed = 0, attackInterval = 1000 });
        var stationary = s.AddEnemy(EnemyKind.Boar, new V(5, 0), false);
        stationary.speed = 0;
        stationary.cooldown = 1000;
        stationary.hp = stationary.maxHP = 1000;
        s.Execute(ActionKind.LaserMode);
        Advance(s, 2);
        Check(stationary.hp < 925, "laser applies damage over time");
        s = Start(new Balance { foxSpeed = 0, attackInterval = 1000 });
        stationary = s.AddEnemy(EnemyKind.Boar, new V(5, 0), false);
        stationary.speed = 0;
        stationary.cooldown = 1000;
        s.Execute(ActionKind.StarStrike);
        Advance(s, 2);
        Check(s.Kills >= 1, "star strike damages and kills target after tell");
        s = Start(new Balance { foxSpeed = 0 });
        s.Execute(ActionKind.ThornPatch);
        s.Fox = s.Hazards[0].pos;
        Advance(s, 1);
        Check(s.HP == 100, "thorn tell does not damage early");
        Advance(s, .5f);
        Check(s.HP == 94, "thorn applies damage after tell");
        s = Start();
        s.Execute(ActionKind.SprigSwarm);
        foreach (var enemy in s.Enemies)
            Check(V.Dist(enemy.pos, s.Fox) >= s.B.spawnSafeDistance && enemy.spawnTell > 0,
                  "viewer swarm safe portal");
        s = Start();
        s.HP = 60;
        s.AddEnemy(EnemyKind.Sprig, new V(8, 5), false);
        sim = new LocalSimulatorGateway();
        using (var d = new InteractionDispatcher(s, sim, new ReceiptJournal(100)))
        {
            for (int i = 0; i < 5; i++)
                Check(d.Submit(Request(s, (ActionKind)i, "session-" + i)).status == "queued",
                      "five request queue");
            Check(d.QueueCount == 5, "five-slot limit");
            d.Tick();
            Check(s.ExecutedActions == 1, "single dispatch");
            d.Tick();
            Check(s.ExecutedActions == 1, "same-step dispatch throttled");
            Advance(s, 5.2f, d);
            Check(d.QueueCount == 0, "queue drains");
        }
        string journal = Path.GetFullPath("artifacts/test-journal.tsv");
        if (File.Exists(journal))
            File.Delete(journal);
        var j = new ReceiptJournal(3, journal);
        for (int i = 0; i < 5; i++)
            j.Remember(new Receipt("r" + i, "executed"));
        j = new ReceiptJournal(3, journal);
        Check(j.Count == 3 && j.Find("r0") == null && j.Find("r4") != null, "persistent bounded journal");
        File.Delete(journal);
        var first = Start(null, 42);
        var second = Start(null, 42);
        Advance(first, 35);
        Advance(second, 35);
        Check(first.Fox.x == second.Fox.x && first.Fox.y == second.Fox.y && first.HP == second.HP &&
                  first.Kills == second.Kills,
              "seed determinism");
        Check(V.Dist(first.Fox, new V(0, -1)) > .25f, "autonomous movement");
    }
    static void Trials(int count)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var lines = new List<string> { "mode,seed,outcome,seconds,hp,kills,upgrades,actions" };
        int wins = 0, defeats = 0, timeouts = 0;
        long ticks = 0;
        int maxEnemies = 0, maxShots = 0, maxXP = 0;
        for (int mode = 0; mode < 4; mode++)
        {
            int runs = mode == 0 ? count : 10;
            for (int seed = 1; seed <= runs; seed++)
            {
                var s = Start(TrialBalance(), seed);
                var gateway = new LocalSimulatorGateway();
                using (var d = new InteractionDispatcher(s, gateway, new ReceiptJournal(100)))
                {
                    float nextAction = 1;
                    while (s.Phase != Phase.Results)
                    {
                        s.Step(Dt);
                        ticks++;
                        d.Tick();
                        maxEnemies = Math.Max(maxEnemies, s.RegularCount);
                        maxShots = Math.Max(maxShots, s.Shots.Count);
                        maxXP = Math.Max(maxXP, s.Pickups.FindAll(p => !p.heal).Count);
                        Check(s.RegularCount <= s.B.enemyCap && s.Shots.Count <= s.B.projectileCap,
                              "entity bounds");
                        Check(!float.IsNaN(s.Fox.x) && !float.IsNaN(s.Fox.y), "finite AI");
                        if (mode > 0 && s.Time >= nextAction)
                        {
                            nextAction = s.Time + 6;
                            for (int k = 0; k < 6; k++)
                            {
                                if (mode == 1 && k >= 3 || mode == 2 && k < 3)
                                    continue;
                                if (d.Availability((ActionKind)k) == null)
                                {
                                    gateway.Submit(s, (ActionKind)k, "test-session");
                                    break;
                                }
                            }
                        }
                    }
                    if (mode == 0)
                    {
                        if (s.Outcome == Outcome.Victory)
                            wins++;
                        else if (s.Outcome == Outcome.Defeat)
                            defeats++;
                        else
                            timeouts++;
                    }
                    lines.Add(string.Join(",", new object[] {
                        mode, seed, s.Outcome,
                        s.Time.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), s.HP,
                        s.Kills, s.Level, s.ExecutedActions
                    }));
                    string old = s.RunId;
                    Advance(s, 11, d);
                    Check(s.RunId != old, "every trial auto restarts");
                }
            }
        }
        File.WriteAllLines("artifacts/balance-trials.csv", lines);
        watch.Stop();
        string summary =
            "No-viewer seeds: " + count + "; victories=" + wins + "; defeats=" + defeats +
            "; timeouts=" + timeouts + "\nHelper/mischief/mixed: 10 seeds each\nFixed ticks: " + ticks +
            "; wall seconds: " + watch.Elapsed.TotalSeconds.ToString("F2") +
            "\nPeak regular enemies=" + maxEnemies + "; projectiles=" + maxShots + "; XP objects=" + maxXP +
            "\nThis is accelerated simulation, not a rendered endurance soak.\n";
        File.WriteAllText("artifacts/test-results.txt", summary);
        Console.WriteLine(summary);
    }
}
