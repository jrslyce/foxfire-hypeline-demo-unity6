using System;
using Foxfire;

class EnemyCombatTests
{
    const float Dt = 1f / 30f;

    static void Check(bool value, string reason)
    {
        if (!value)
            throw new Exception(reason);
    }

    static Balance QuietBalance()
    {
        return new Balance {
            bossAt = 9999,
            maxSeconds = 9999,
            attackInterval = 999,
            spawnRates = new[] { .01f, .01f, .01f, .01f },
            projectileCap = 64,
            immunity = 0,
            chargeDuration = 1.75f,
            chargeTell = 1.2f,
            chargeCooldown = 2,
            dispatchInterval = 0,
            starTell = .2f,
            rootTell = .2f,
            thornDuration = 6
        };
    }

    static Simulation Start(Balance balance)
    {
        var game = new Simulation(balance);
        while (!game.Active)
            game.Step(Dt);
        return game;
    }

    static void Advance(Simulation game, float seconds)
    {
        int steps = (int)Math.Ceiling(seconds / Dt);
        for (int i = 0; i < steps; i++)
            game.Step(Dt);
    }

    static void Advance(Simulation game, InteractionDispatcher dispatcher, float seconds)
    {
        int steps = (int)Math.Ceiling(seconds / Dt);
        for (int i = 0; i < steps; i++)
        {
            game.Step(Dt);
            dispatcher.Tick();
        }
    }

    static int HostileShots(Simulation game)
    {
        int count = 0;
        foreach (var shot in game.Shots)
            if (shot.hostile)
                count++;
        return count;
    }

    static bool SameDirection(V a, V b)
    {
        return a.Length > .9f && b.Length > .9f && V.Dot(a.Unit, b.Unit) > .98f;
    }

    static ActionRequest Request(Simulation game, string receiptId, ActionKind kind, string name)
    {
        return new ActionRequest {
            receiptId = receiptId,
            sessionId = "enemy-combat-" + receiptId,
            actionId = Actions.Scoped(game.RunId, kind),
            displayName = name,
            eventKind = "action"
        };
    }

    static int FirstFanSize(EnemyKind kind, int projectileCap)
    {
        var balance = QuietBalance();
        balance.projectileCap = projectileCap;
        var game = Start(balance);
        game.Fox = new V(0, 0);
        var enemy = game.AddEnemy(kind, kind == EnemyKind.Boar ? new V(6, 0) : new V(5, 0), false);
        enemy.speed = 0;
        enemy.cooldown = 0;
        enemy.shotTell = 0;
        enemy.shotCooldown = 0;
        int before = HostileShots(game);
        for (int i = 0; i < 240; i++)
        {
            game.Step(.05f);
            int current = HostileShots(game);
            if (current > before)
                return current - before;
        }
        return 0;
    }

    static void BulletFansAndCaps()
    {
        foreach (var kind in new[] { EnemyKind.Boar, EnemyKind.Sprig, EnemyKind.Wisp })
        {
            int fan = FirstFanSize(kind, 64);
            Check(fan >= 3, kind + " emits a real bullet fan");

            int capped = FirstFanSize(kind, 3);
            Check(capped <= 3, kind + " fan respects projectile cap");
            Check(capped == 3, kind + " fan fills the cap rather than silently dropping its pattern");
        }
    }

    static void LongerBoarChargeAndTrackingLock()
    {
        var balance = QuietBalance();
        var game = Start(balance);
        game.Fox = new V(0, 0);
        var boar = game.AddEnemy(EnemyKind.Boar, new V(6, 0), false);
        boar.speed = 0;
        boar.cooldown = 0;
        boar.shotTell = 0;
        boar.shotCooldown = 100;

        game.Step(.1f);
        Check(boar.tell > .9f, "boar exposes its charge tell before charging");
        V initial = boar.dir;

        game.Fox = new V(0, 4);
        Advance(game, .6f);
        V tracked = boar.dir;
        Check(!SameDirection(initial, tracked), "boar tracks the fox during the early tell");
        Check(SameDirection(tracked, (game.Fox - boar.pos).Unit), "boar tell points at the tracked fox");

        while (boar.tell > .25f)
            game.Step(.05f);
        V locked = boar.dir;
        game.Fox = new V(0, -4);
        game.Step(.05f);
        Check(SameDirection(locked, boar.dir), "boar locks its charge direction for the final quarter-second tell");

        while (boar.charge <= 0)
            game.Step(.05f);
        V chargeStart = boar.pos;
        Advance(game, 1.1f);
        Check(boar.charge > 0, "boar charge still has time remaining after 1.1 seconds");
        Check(V.Dist(chargeStart, boar.pos) > 1.5f, "longer boar charge materially travels");
        Check(new Balance().chargeDuration > 1, "default boar charge duration is extended");
    }

    static void MushroomLunges()
    {
        foreach (var kind in new[] { EnemyKind.Sprig, EnemyKind.Wisp })
        {
            var game = Start(QuietBalance());
            game.B.foxSpeed = 0;
            game.Fox = new V(0, 0);
            var enemy = game.AddEnemy(kind, kind == EnemyKind.Wisp ? new V(3.5f, 0) : new V(5, 0), false);
            enemy.shotTell = 0;
            enemy.shotCooldown = 100;
            enemy.cooldown = 0;
            float normalStep = enemy.speed * .05f;
            float maxStep = 0;
            float maxDistanceIncrease = 0;
            V previous = enemy.pos;
            float previousDistance = V.Dist(enemy.pos, game.Fox);
            for (int i = 0; i < 120; i++)
            {
                game.Step(.05f);
                float step = V.Dist(previous, enemy.pos);
                if (step > maxStep)
                    maxStep = step;
                float distanceIncrease = V.Dist(enemy.pos, game.Fox) - previousDistance;
                if (distanceIncrease > maxDistanceIncrease)
                    maxDistanceIncrease = distanceIncrease;
                previous = enemy.pos;
                previousDistance = V.Dist(enemy.pos, game.Fox);
            }
            Check(maxStep > normalStep * 1.5f + .02f, kind + " performs an observable lunge rather than only walking");
            if (kind == EnemyKind.Wisp)
                Check(maxDistanceIncrease > normalStep * 1.5f + .02f, "wisp lunge moves away from the fox only when crowded");
        }
    }

    static void ContactAndOrbitDuringShotTell()
    {
        var balance = QuietBalance();
        balance.foxSpeed = 0;
        balance.immunity = 0;
        var game = Start(balance);
        game.Fox = new V(0, 0);
        game.Direction = new V();
        game.Upgrades[5] = 1;
        var enemy = game.AddEnemy(EnemyKind.Boar, new V(0, 0), false);
        enemy.speed = 0;
        enemy.cooldown = 100;
        enemy.shotTell = .5f;
        enemy.shotCooldown = 100;
        enemy.hp = enemy.maxHP = 100;
        float hp = game.HP;
        float enemyHP = enemy.hp;

        game.Step(.05f);

        Check(game.HP < hp, "contact damage still applies during an enemy shot tell");
        Check(enemy.hp < enemyHP, "orbiting spark damage still applies during an enemy shot tell");
    }

    static void DispatcherHealAndNamedImpacts()
    {
        var balance = QuietBalance();
        balance.foxSpeed = 3.6f;
        var game = Start(balance);
        game.HP = 55;
        var gateway = new LocalSimulatorGateway();
        using (var dispatcher = new InteractionDispatcher(game, gateway, new ReceiptJournal(100)))
        {
            var heal = Request(game, "heal", ActionKind.HealPod, "Helper_One");
            Check(dispatcher.Submit(heal).status == "queued", "heal request queues through dispatcher");
            dispatcher.Tick();
            Check(game.ExecutedActions == 1 && game.Pickups.Exists(p => p.heal), "heal action executes and creates a pickup");
            Advance(game, dispatcher, 4);
            Check(game.HP > 55, "fox pursues and collects the healing pickup");

            game.HP = 55;
            game.Fox = new V(0, 0);
            game.Direction = new V();
            var enemy = game.AddEnemy(EnemyKind.Boar, new V(4, 0), false);
            enemy.speed = 0;
            enemy.cooldown = 999;
            var star = Request(game, "star", ActionKind.StarStrike, "<Star_User>\n");
            Check(dispatcher.Submit(star).status == "queued", "named star request queues through dispatcher");
            dispatcher.Tick();
            var starHazard = game.Hazards.Find(h => h.star);
            Check(starHazard != null && starHazard.viewerName == "Star_User", "star hazard retains sanitized viewer name");
            int actions = game.ExecutedActions;
            star.displayName = "Changed_Name";
            star.expiresAtUnixMs = 1;
            Check(dispatcher.Submit(star).status == "executed" && game.ExecutedActions == actions &&
                  game.Hazards.FindAll(h => h.star).Count == 1, "replayed star receipt is idempotent and does not duplicate impact");
            Advance(game, .35f);
            Check(enemy.hp <= 0 || !game.Enemies.Contains(enemy), "star impact harms the controlled enemy");
            Check(game.Bursts.Exists(b => b.viewerName == "Star_User"), "star impact burst retains viewer name");

            game.HP = 55;
            game.Fox = new V(0, 0);
            game.Direction = new V(1, 0);
            var thorn = Request(game, "thorn", ActionKind.ThornPatch, "<Bramble-User>\n");
            Check(dispatcher.Submit(thorn).status == "queued", "named bramble request queues through dispatcher");
            dispatcher.Tick();
            var patch = game.Hazards.Find(h => h.viewer);
            Check(patch != null && patch.viewerName == "Bramble-User", "bramble hazard retains sanitized viewer name");
            game.Fox = patch.pos;
            game.Direction = new V();
            Advance(game, .35f);
            Check(game.HP < 55, "controlled bramble collision harms fox HP");
        }
    }

    static void LegacyTuningFallbacks()
    {
        var balance = new Balance {
            boarShotTell = 0, boarShotCooldown = 0, boarProjectileSpeed = 0,
            boarProjectileSpread = 0, boarProjectileCount = 0,
            sprigShotTell = 0, sprigShotCooldown = 0, sprigProjectileSpeed = 0,
            sprigProjectileSpread = 0, sprigProjectileCount = 0,
            wispShotTell = 0, wispShotCooldown = 0, wispProjectileSpread = 0, wispProjectileCount = 0,
            mushroomLungeTell = 0, mushroomLungeSpeed = 0, mushroomLungeDuration = 0,
            mushroomLungeCooldown = 0, enemyProjectileDamageScale = 0
        };
        foreach (var kind in new[] { EnemyKind.Boar, EnemyKind.Sprig, EnemyKind.Wisp })
            Check(EnemyCombat.ShotTell(balance, kind) > 0 && EnemyCombat.ShotCooldown(balance, kind) > 0 &&
                EnemyCombat.ProjectileCount(balance, kind) == 3 && EnemyCombat.ProjectileSpeed(balance, kind) > 0 &&
                EnemyCombat.ProjectileSpread(balance, kind) > 0, "legacy ranged tuning remains playable: " + kind);
        Check(EnemyCombat.LungeTell(balance) > 0 && EnemyCombat.LungeSpeed(balance) > 0 &&
            EnemyCombat.LungeDuration(balance) > 0 && EnemyCombat.LungeCooldown(balance) > 0 &&
            EnemyCombat.ProjectileDamageScale(balance) > 0, "legacy lunge and damage tuning remain playable");
    }

    public static void Main()
    {
        BulletFansAndCaps();
        LongerBoarChargeAndTrackingLock();
        MushroomLunges();
        ContactAndOrbitDuringShotTell();
        DispatcherHealAndNamedImpacts();
        LegacyTuningFallbacks();
        Console.WriteLine("PASS enemy fans, caps, charges, tracking lock, mushroom lunges, dispatcher healing, named impacts and idempotency");
    }
}
