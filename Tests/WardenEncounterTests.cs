using System;
using System.Collections.Generic;
using Foxfire;

class WardenEncounterTests
{
    const float Dt = 1f / 30;
    static int checks;

    static void Check(bool value, string message)
    {
        checks++;
        if (!value)
            throw new Exception("FAIL: " + message);
    }

    static Balance BossBalance()
    {
        return new Balance {
            introSeconds = 0,
            bossAt = .01f,
            maxSeconds = 240,
            foxHP = 100000,
            boltDamage = 0,
            foxSpeed = 0
        };
    }

    static Simulation StartBoss(Balance balance, int seed = 1)
    {
        var game = new Simulation(balance, seed);
        int guard = 0;
        while (game.Boss == null && guard++ < 20)
            game.Step(Dt);
        Check(game.Boss != null && game.Phase == Phase.Boss, "boss encounter starts");
        return game;
    }

    static void Advance(Simulation game, float seconds)
    {
        int steps = (int)Math.Ceiling(seconds / Dt);
        for (int i = 0; i < steps; i++)
            game.Step(Dt);
    }

    static void TriggerSummon(Simulation game)
    {
        var boss = game.Boss;
        boss.wardenMove = WardenMove.Summon;
        boss.wardenState = WardenState.Windup;
        boss.wardenStateClock = game.Clock - WardenTiming.Windup(WardenMove.Summon);
        game.Step(Dt);
    }

    static float DistanceToFox(Enemy boss, Simulation game)
    {
        return V.Dist(boss.pos, game.Fox);
    }

    static bool Same(Simulation a, Simulation b)
    {
        if (a.Phase != b.Phase || a.Outcome != b.Outcome || a.Time != b.Time ||
            a.Fox.x != b.Fox.x || a.Fox.y != b.Fox.y || a.HP != b.HP ||
            a.Enemies.Count != b.Enemies.Count || a.Shots.Count != b.Shots.Count ||
            a.Hazards.Count != b.Hazards.Count || a.Shockwaves.Count != b.Shockwaves.Count)
            return false;
        for (int i = 0; i < a.Enemies.Count; i++)
        {
            var x = a.Enemies[i];
            var y = b.Enemies[i];
            if (x.id != y.id || x.kind != y.kind || x.hp != y.hp || x.pos.x != y.pos.x ||
                x.pos.y != y.pos.y || x.attackCount != y.attackCount ||
                x.wardenState != y.wardenState || x.wardenMove != y.wardenMove ||
                x.wardenStage != y.wardenStage || x.wardenStateClock != y.wardenStateClock ||
                x.wardenImpactClock != y.wardenImpactClock)
                return false;
        }
        for (int i = 0; i < a.Shots.Count; i++)
        {
            var x = a.Shots[i];
            var y = b.Shots[i];
            if (x.hostile != y.hostile || x.damage != y.damage || x.pos.x != y.pos.x ||
                x.pos.y != y.pos.y || x.velocity.x != y.velocity.x || x.velocity.y != y.velocity.y)
                return false;
        }
        for (int i = 0; i < a.Shockwaves.Count; i++)
        {
            var x = a.Shockwaves[i];
            var y = b.Shockwaves[i];
            if (x.radius != y.radius || x.previousRadius != y.previousRadius || x.hit != y.hit ||
                x.gapAngle != y.gapAngle || x.gapHalfAngle != y.gapHalfAngle)
                return false;
        }
        return true;
    }

    static void DeterminismAndState()
    {
        var a = StartBoss(BossBalance(), 17);
        var b = StartBoss(BossBalance(), 17);
        bool sawChase = false, sawWindup = false, sawRecover = false;
        WardenState previous = a.Boss.wardenState;
        for (int i = 0; i < 2400; i++)
        {
            a.Step(Dt);
            b.Step(Dt);
            Check(Same(a, b), "same seed and input remain deterministic");
            var boss = a.Boss;
            if (boss == null)
                break;
            if (boss.wardenState == WardenState.Chase) sawChase = true;
            if (boss.wardenState == WardenState.Windup) sawWindup = true;
            if (boss.wardenState == WardenState.Recover) sawRecover = true;
            if (boss.wardenState != previous)
            {
                Check(Math.Abs(a.Clock - boss.wardenStateClock) < .0001,
                      "state clock is set at state entry");
                previous = boss.wardenState;
            }
            Check(boss.wardenStage >= 1 && boss.wardenStage <= 3, "warden stage is bounded");
            Check(a.Shockwaves.Count <= 6, "simultaneous shockwaves are capped");
        }
        Check(sawChase && sawWindup && sawRecover, "encounter alternates chase, windup and recovery");
    }

    static void BalanceDefaultsAndLegacyFallback()
    {
        var defaults = new Balance();
        Check(defaults.bossHP > 0 && defaults.bossInterval > 0 && defaults.bossDamage > 0,
              "core defaults keep a valid Warden fallback");
        // An older serialized Balance leaves newly introduced fields at zero. The encounter
        // must use its documented defaults instead of producing a zero-duration/zero-speed boss.
        var legacy = BossBalance();
        legacy.wardenChaseDuration = legacy.wardenStage2ChaseDuration =
            legacy.wardenStage3ChaseDuration = legacy.wardenMoveSpeed =
            legacy.wardenStage2MoveSpeed = legacy.wardenStage3MoveSpeed = 0;
        legacy.wardenSlashDamage = legacy.wardenSlashRadius = legacy.wardenSlashHalfAngle =
            legacy.wardenSlamDamage = legacy.wardenSpinDamage = legacy.wardenProjectileSpeed =
            legacy.wardenProjectileGapHalfAngle = legacy.wardenRingSpeed =
            legacy.wardenRingMaxRadius = legacy.wardenRingHalfWidth = legacy.wardenRingDamage =
            legacy.wardenSummonTell = legacy.wardenSummonSafeDistance =
            legacy.wardenStage2Health = legacy.wardenStage3Health = 0;
        legacy.wardenProjectileCount = legacy.wardenSummonCount = legacy.wardenSummonCap =
            legacy.wardenShockwaveCap = 0;
        var game = StartBoss(legacy, 23);
        Advance(game, 8);
        Check(game.Boss != null && game.Boss.wardenStage >= 1,
              "legacy balance with zero Warden fields uses safe runtime fallbacks");

        legacy = BossBalance();
        legacy.wardenSummonCount = legacy.wardenSummonCap = 0;
        game = StartBoss(legacy, 29);
        TriggerSummon(game);
        Check(game.WardenMinionCount == 8,
              "legacy zero summon fields use the increased cast default");
        TriggerSummon(game);
        Check(game.WardenMinionCount == 16,
              "legacy zero summon cap preserves the increased active bound");
    }

    static void SummonCapacityAndGlobalBounds()
    {
        var game = StartBoss(BossBalance(), 31);
        int before = game.WardenMinionCount;
        TriggerSummon(game);
        Check(game.WardenMinionCount - before == 8,
              "an unconstrained summon cast adds exactly eight guardians");
        TriggerSummon(game);
        Check(game.WardenMinionCount == 16,
              "two unconstrained casts reach the sixteen guardian cap");
        TriggerSummon(game);
        Check(game.WardenMinionCount == 16,
              "additional casts cannot exceed the sixteen guardian cap");

        var constrained = BossBalance();
        constrained.enemyCap = 5;
        constrained.phaseCaps = new[] { 5, 5, 5, 5 };
        game = StartBoss(constrained, 37);
        TriggerSummon(game);
        Check(game.RegularCount <= game.Cap && game.WardenMinionCount <= 5,
              "global enemy capacity constrains summons without overflow");
        Check(game.PendingWardenSummons == 0,
              "constrained summon queue is resolved after the deferred flush");
    }

    static void HealthStagesAndStationaryAttacks()
    {
        var game = StartBoss(BossBalance());
        var boss = game.Boss;
        Check(boss.wardenStage == 1, "full health starts at stage one");
        boss.hp = boss.maxHP * .70f;
        game.Step(Dt);
        Check(boss.wardenStage == 2, "70 percent health enters stage two");
        boss.hp = boss.maxHP * .35f;
        game.Step(Dt);
        Check(boss.wardenStage == 3, "35 percent health enters stage three");

        game = StartBoss(BossBalance());
        boss = game.Boss;
        V initial = boss.pos;
        bool movedDuringChase = false;
        float initialDistance = DistanceToFox(boss, game);
        for (int i = 0; i < 120 && boss.wardenState == WardenState.Chase; i++)
        {
            game.Step(Dt);
            movedDuringChase |= V.Dist(initial, boss.pos) > .001f;
        }
        Check(movedDuringChase && DistanceToFox(boss, game) < initialDistance,
              "warden moves toward the fox during chase");

        while (boss.wardenState != WardenState.Windup)
            game.Step(Dt);
        WardenMove move = boss.wardenMove;
        V windupPosition = boss.pos;
        boss.hp = boss.maxHP * .34f;
        int windupTicks = 0;
        while (boss.wardenState == WardenState.Windup && windupTicks++ < 120)
        {
            game.Step(Dt);
            Check(V.Dist(windupPosition, boss.pos) < .0001f, "boss holds position during windup");
            Check(boss.wardenMove == move, "stage change does not interrupt attack");
        }
        Check(boss.wardenState == WardenState.Recover, "impact enters recovery");
        V recoveryPosition = boss.pos;
        int recoveryTicks = 0;
        while (boss.wardenState == WardenState.Recover && recoveryTicks++ < 120)
        {
            game.Step(Dt);
            Check(V.Dist(recoveryPosition, boss.pos) < .0001f, "boss holds position during recovery");
        }
    }

    static void ImpactsAndPatterns()
    {
        var balance = BossBalance();
        var game = StartBoss(balance, 8);
        var seen = new HashSet<WardenMove>();
        int previousAttack = 0;
        bool sawSummon = false;
        int guard = 0;
        while (game.Time < 90 && guard++ < 3000)
        {
            game.Step(Dt);
            var boss = game.Boss;
            if (boss == null)
                break;
            Check(boss.attackCount >= previousAttack && boss.attackCount <= previousAttack + 1,
                  "one attack impact is emitted per simulation step");
            if (boss.attackCount != previousAttack)
            {
                Check(boss.attackCount == previousAttack + 1, "attack count increments once per impact");
                Check(Math.Abs(game.Clock - boss.attackClock) < .0001,
                      "attack clock records the impact time");
                Check(Math.Abs(game.Clock - boss.wardenImpactClock) < .0001,
                      "warden impact clock records the impact time");
                seen.Add(boss.wardenMove);
                sawSummon |= boss.wardenMove == WardenMove.Summon;
                previousAttack = boss.attackCount;
            }
            Check(game.Shots.Count <= balance.projectileCap, "bullet wave obeys projectile cap");
            Check(game.Shockwaves.Count <= 6, "ring pattern obeys ring cap");
        }
        Check(seen.Contains(WardenMove.Slash) && seen.Contains(WardenMove.Slam) &&
              seen.Contains(WardenMove.Spin), "all three attack moves are exercised");
        Check(sawSummon, "summon is a distinct impact move");

        game = StartBoss(BossBalance(), 3);
        game.Shots.Clear();
        game.Shockwaves.Clear();
        int before = game.Shots.Count;
        int oldAttacks = game.Boss.attackCount;
        bool sawSlam = false;
        bool patternFound = false;
        while (game.Time < 90 && !sawSlam)
        {
            game.Step(Dt);
            sawSlam = game.Boss.attackCount > oldAttacks && game.Boss.wardenMove == WardenMove.Slam &&
                      game.Boss.wardenState == WardenState.Recover;
            if (sawSlam)
                patternFound = game.Shots.Count > before || game.Shockwaves.Count > 0;
        }
        Check(sawSlam, "explicit Slam impact is reached");
        Check(patternFound,
              "ground pound creates a visible projectile or shockwave pattern");
        bool defaultRange = false;
        foreach (var wave in game.Shockwaves)
            defaultRange |= wave.maxRadius == 30;
        Check(defaultRange, "default ground pound ring reaches twice the former distance");

        var custom = BossBalance();
        custom.wardenRingMaxRadius = 22;
        game = StartBoss(custom, 8);
        game.Boss.wardenState = WardenState.Windup;
        game.Boss.wardenMove = WardenMove.Slam;
        game.Boss.wardenStateClock = game.Clock - WardenTiming.Windup(WardenMove.Slam);
        game.Step(Dt);
        Check(game.Shockwaves.Count > 0 && game.Shockwaves[0].maxRadius == 22,
              "custom ground pound ring range remains respected");
    }

    static Simulation CombatOnly()
    {
        var b = new Balance { introSeconds = 0, bossAt = 1000, maxSeconds = 10,
                              foxHP = 100, foxSpeed = 0, boltDamage = 0 };
        var game = new Simulation(b);
        while (!game.Active)
            game.Step(Dt);
        return game;
    }

    static Shockwave Ring(float gapAngle)
    {
        return new Shockwave { pos = new V(0, 0), radius = 0, previousRadius = 0,
            speed = 3, maxRadius = 5, halfWidth = .35f, damage = 17,
            gapAngle = gapAngle, gapHalfAngle = .25f };
    }

    static void RingCollisionAndSummonSafety()
    {
        var game = CombatOnly();
        game.Fox = new V(2, 0);
        var safeGap = Ring(0);
        game.Shockwaves.Add(safeGap);
        for (int i = 0; i < 60; i++)
        {
            game.Immune = 0;
            game.Step(Dt);
        }
        Check(game.HP == 100, "shockwave gap is a viable escape route");
        Check(!safeGap.hit && safeGap.radius > 2.7f, "gap test covers the entire ring crossing");

        game = CombatOnly();
        game.Fox = new V(2, 0);
        var ring = Ring((float)Math.PI);
        game.Shockwaves.Add(ring);
        for (int i = 0; i < 60; i++)
        {
            game.Immune = 0;
            game.Step(Dt);
        }
        Check(game.HP == 83, "swept shockwave hits once when it crosses the fox");
        Check(ring.hit, "ring records its collision");

        game = CombatOnly();
        game.Fox = new V(5, 0);
        ring = Ring((float)Math.PI);
        ring.speed = 100;
        ring.maxRadius = 15;
        game.Shockwaves.Add(ring);
        game.Step(.1f);
        Check(game.HP == 83 && ring.radius > 9,
              "fast ring crossing between ticks cannot tunnel through fox");

        game = CombatOnly();
        game.Fox = new V(10, 0);
        ring = Ring((float)Math.PI);
        ring.pos = new V(-10, 0); // 20 units apart, both inside the playable arena.
        ring.maxRadius = 30;
        ring.speed = 10;
        game.Shockwaves.Add(ring);
        for (int i = 0; i < 100; i++)
        {
            game.Immune = 0;
            game.Step(Dt);
        }
        Check(ring.hit && game.HP == 83,
              "ground pound ring reaches and damages beyond the former 15 unit range");

        var balance = BossBalance();
        balance.enemyCap = 4;
        balance.phaseCaps = new[] { 4, 4, 4, 4 };
        game = StartBoss(balance, 19);
        game.Boss.hp = game.Boss.maxHP * .34f;
        bool sawSpawnTell = false;
        for (int i = 0; i < 3000; i++)
        {
            game.Step(Dt);
            Check(game.RegularCount <= 4, "summons obey global enemy cap");
            foreach (var enemy in game.Enemies)
            {
                if (enemy.kind != EnemyKind.Warden && enemy.spawnTell > 0)
                {
                    sawSpawnTell = true;
                    Check(V.Dist(enemy.pos, game.Fox) >= balance.wardenSummonSafeDistance,
                          "summons spawn outside fox safe distance");
                }
            }
            if (sawSpawnTell && game.Boss.attackCount > 6)
                break;
        }
        Check(sawSpawnTell, "summons use a readable portal tell");
    }

    static void IntegrationRegressions()
    {
        foreach (var field in typeof(Balance).GetFields())
            if (field.Name.StartsWith("warden", StringComparison.Ordinal))
                Check(Attribute.IsDefined(field, typeof(System.Runtime.Serialization.OptionalFieldAttribute)),
                      "new balance field is optional in legacy JSON: " + field.Name);

        var balance = BossBalance();
        var game = StartBoss(balance);
        var boss = game.Boss;
        boss.hp = boss.maxHP * .34f;
        boss.wardenState = WardenState.Windup;
        boss.wardenMove = WardenMove.Slam;
        boss.wardenStateClock = game.Clock - WardenTiming.Windup(WardenMove.Slam);
        game.Step(Dt);
        Check(game.Shockwaves.Count == 2, "stage three slam emits two shock rings");
        Check(game.Shockwaves[0].gapAngle == game.Shockwaves[1].gapAngle,
              "both stage three rings preserve the telegraphed escape gap");
        Check(game.Shockwaves[0].speed > game.Shockwaves[1].speed,
              "secondary ring separates from primary ring");
        while (game.Shockwaves.Count < 6)
            game.Shockwaves.Add(Ring(0));
        boss.wardenState = WardenState.Windup;
        boss.wardenStateClock = game.Clock - WardenTiming.Windup(WardenMove.Slam);
        game.Step(Dt);
        Check(game.Shockwaves.Count == 6, "slam refuses rings beyond the cap");

        var minion = game.AddEnemy(EnemyKind.Boar, new V(10, 5), false);
        minion.wardenSummoned = true;
        game.Shots.Add(new Shot { hostile = true, life = 10 });
        balance.maxSeconds = game.Time + Dt * .5f;
        game.Step(Dt);
        Check(game.Outcome == Outcome.Timeout && game.Shockwaves.Count == 0 &&
              game.WardenMinionCount == 0 && game.PendingWardenSummons == 0,
              "timeout clears encounter rings and minions");
        foreach (var shot in game.Shots)
            Check(!shot.hostile, "timeout clears hostile shots");
    }

    static void DeathAndResetCleanup()
    {
        var game = StartBoss(BossBalance());
        game.Shots.Clear();
        game.Shots.Add(new Shot { hostile = true, life = 10 });
        game.Hazards.Add(new Hazard { pos = game.Fox, radius = 2, life = 10, damage = 1 });
        game.Shockwaves.Add(Ring((float)Math.PI));
        var summoned = game.AddEnemy(EnemyKind.Boar, new V(8, 5), false);
        summoned.wardenSummoned = true;
        summoned.spawnTell = 0;
        game.Boss.hp = 0;
        game.Step(Dt);
        Check(game.Outcome == Outcome.Victory, "boss death awards victory");
        Check(game.Boss == null, "boss is removed after death");
        Check(game.Shockwaves.Count == 0, "boss death clears shockwaves");
        bool hostileShot = false;
        foreach (var shot in game.Shots)
            hostileShot |= shot.hostile;
        Check(!hostileShot, "boss death clears hostile projectiles");
        bool liveSummoned = false;
        foreach (var enemy in game.Enemies)
            liveSummoned |= enemy.wardenSummoned && enemy.hp > 0;
        Check(!liveSummoned, "boss removal cleans live summoned enemies, not only pending summons");

        game.Reset();
        Check(game.Boss == null && game.Shots.Count == 0 && game.Hazards.Count == 0 &&
              game.Shockwaves.Count == 0 && game.Enemies.Count == 0,
              "new run clears encounter collections");
    }

    static void RetainEnemiesAndViewerActions()
    {
        var balance = BossBalance();
        balance.bossAt = 1;
        var game = new Simulation(balance, 29);
        game.Step(Dt);
        var sprig = game.AddEnemy(EnemyKind.Sprig, new V(-8, 4), false);
        var boar = game.AddEnemy(EnemyKind.Boar, new V(8, -4), false);
        game.Execute(ActionKind.OrangeChampion, "LadyBird");
        var champion = game.Enemies.Find(e => e.champion);
        var survivors = new[] { sprig, boar, champion };
        foreach (var enemy in survivors) enemy.hp = enemy.maxHP = 10000;
        Advance(game, 3);
        Check(game.Phase == Phase.Boss, "retention fixture reaches Warden phase");
        foreach (var enemy in survivors)
            Check(game.Enemies.Contains(enemy) && enemy.hp > 0 && enemy.dissolve == 0,
                  "pre-boss enemy survives beyond former dissolve window: " + enemy.kind);
        Check(champion.displayName == "LadyBird" && champion.champion,
              "retained champion keeps viewer identity");
        int kills = game.Kills;
        sprig.hp = 0;
        game.Step(Dt);
        Check(game.Kills == kills + 1 && game.Pickups.Exists(p => !p.heal && p.xp > 0),
              "retained regular enemies still award kills and XP during boss");
        var summon = game.AddEnemy(EnemyKind.Sprig, new V(-10, 5), false);
        summon.wardenSummoned = true;
        summon.hp = 0;
        game.Step(Dt);
        Check(game.Kills == kills + 1, "Warden summons remain non-farmable");

        // Test each catalog entry independently so active effects do not mask phase eligibility.
        for (int i = 0; i < Actions.Count; i++)
        {
            var kind = (ActionKind)i;
            game = StartBoss(BossBalance());
            game.HP = game.MaxHP * .7f;
            game.AddEnemy(EnemyKind.Boar, new V(8, -4), false);
            using (var dispatcher = new InteractionDispatcher(game, new LocalSimulatorGateway(), new ReceiptJournal(100)))
            {
                Check(dispatcher.Availability(kind) == null, "boss offers action " + kind);
                game.HP = game.MaxHP * .2f;
                game.B.maxSeconds = game.Time + 5;
                Check(dispatcher.Availability(kind) == null, "boss offers action at low HP near deadline " + kind);
                var request = new ActionRequest { receiptId = "boss-action-" + i, sessionId = "viewer-" + i,
                    actionId = Actions.Scoped(game.RunId, kind), displayName = "LadyBird" };
                Check(dispatcher.Submit(request).status == "queued", "boss accepts " + kind);
                dispatcher.Tick();
                Check(dispatcher.Journal.Find(request.receiptId).status == "executed" && game.ExecutedActions == 1,
                      "boss executes " + kind);
                Check(dispatcher.Submit(request).status == "executed" && game.ExecutedActions == 1,
                      "boss action replay is idempotent " + kind);
                Check(dispatcher.Cooldown(kind) > 0, "boss preserves shared cooldown " + kind);
            }
        }
        foreach (var kind in new[] { ActionKind.SprigSwarm, ActionKind.EnemyRush, ActionKind.OrangeChampion })
        {
            balance = BossBalance();
            balance.bossAt = .05f;
            game = new Simulation(balance, 31);
            game.Step(Dt);
            game.AddEnemy(EnemyKind.Sprig, new V(-8, 4), false);
            using (var dispatcher = new InteractionDispatcher(game, new LocalSimulatorGateway(), new ReceiptJournal(100)))
            {
                var request = new ActionRequest { receiptId = "cross-phase-" + kind, sessionId = "viewer",
                    actionId = Actions.Scoped(game.RunId, kind), displayName = "BBQ-fan" };
                Check(dispatcher.Submit(request).status == "queued", "pre-boss action queues " + kind);
                Advance(game, .1f);
                Check(game.Phase == Phase.Boss, "queued action crosses boss boundary");
                dispatcher.Tick();
                Check(dispatcher.Journal.Find(request.receiptId).status == "executed",
                      "boss arrival does not reject accepted action " + kind);
            }
        }
    }

    public static void Main()
    {
        try
        {
            RetainEnemiesAndViewerActions();
            DeterminismAndState();
            BalanceDefaultsAndLegacyFallback();
            SummonCapacityAndGlobalBounds();
            HealthStagesAndStationaryAttacks();
            ImpactsAndPatterns();
            RingCollisionAndSummonSafety();
            DeathAndResetCleanup();
            IntegrationRegressions();
            Console.WriteLine("PASS Warden encounter: " + checks + " assertions");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            Environment.Exit(1);
        }
    }
}
