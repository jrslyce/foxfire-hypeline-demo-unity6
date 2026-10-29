using System;
using Foxfire;

class ActionExpansionTests
{
    const float Dt = 1f / 30;
    static int checks;
    static void Check(bool value, string reason)
    {
        checks++;
        if (!value) throw new Exception("FAIL: " + reason);
    }
    static Simulation Start(Balance balance = null)
    {
        var game = new Simulation(balance ?? new Balance(), 17);
        while (!game.Active) game.Step(Dt);
        return game;
    }
    static void Advance(Simulation game, float seconds)
    {
        for (int i = 0; i < (int)(seconds / Dt); i++) game.Step(Dt);
    }
    static void Main()
    {
        try
        {
            CatalogAndLegacyBalance();
            EffectsAndDamage();
            ForceWave();
            DispatcherSemantics();
            MiniBoss();
            Console.WriteLine("PASS action expansion: " + checks + " assertions");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            Environment.Exit(1);
        }
    }
    static void CatalogAndLegacyBalance()
    {
        string[] old = { "heal_pod", "laser_mode", "star_strike", "sprig_swarm",
                         "thorn_patch", "enemy_rush", "orange_champion", "teal_celebration" };
        Check(Actions.Count == 17, "expanded catalog count");
        for (int i = 0; i < old.Length; i++) Check(Actions.Ids[i] == old[i], "legacy action id " + i);
        Check(Actions.Ids[(int)ActionKind.EnemyGlow] == "enemy_glow" &&
              Actions.Ids[(int)ActionKind.RainbowShots] == "rainbow_shots", "new action keys");
        Check(Actions.Label(ActionKind.HealthCut) == "50% Health Cut", "health cut label");
        Check(Actions.Category(ActionKind.EnemyGlow) == "cosmetic" &&
              Actions.Category(ActionKind.MiniBoss) == "hinder" &&
              Actions.Category(ActionKind.ForceWave) == "help", "new action categories");
        var legacy = new Balance { actionCooldowns = new float[8] };
        Check(Actions.Cooldown(legacy, ActionKind.EnemyGlow) == 10 &&
              Actions.Cooldown(legacy, ActionKind.HealthCut) == 20 &&
              Actions.Cooldown(legacy, ActionKind.MiniBoss) == 30 &&
              Actions.Cooldown(legacy, ActionKind.MuddyFeet) == 15 &&
              Actions.Cooldown(legacy, ActionKind.ForceWave) == 12 &&
              Actions.Cooldown(legacy, ActionKind.RainbowShots) == 10, "legacy cooldown fallbacks");
    }
    static void EffectsAndDamage()
    {
        var game = Start(new Balance { foxSpeed = 4, attackInterval = 1000 });
        game.Fox = new V(2, -2);
        game.Execute(ActionKind.EnemyGlow);
        game.Execute(ActionKind.ReverseFacing);
        game.Execute(ActionKind.EnemySpin);
        game.Execute(ActionKind.PetalHeart);
        V heart = game.PetalHeartPosition;
        game.Execute(ActionKind.MuddyFeet);
        game.Execute(ActionKind.RainbowShots);
        Check(game.EnemyGlow == 8 && game.ReverseFacing == 8 && game.EnemySpin == 6 &&
              game.PetalHeart == 6 && game.MuddyFeet == 8 && game.RainbowShots == 5,
              "effect durations activate");
        Check(game.PetalHeartPosition.x == heart.x && game.PetalHeartPosition.y == heart.y,
              "petal heart position fixed on activation");
        game.Direction = new V(1, 0);
        float foxBeforeMove = game.Fox.x;
        game.Fox += new V(1, 0);
        Check(game.Fox.x > foxBeforeMove && game.PetalHeartPosition.x == heart.x &&
              game.PetalHeartPosition.y == heart.y, "petal heart stays at activation position");
        Check(Math.Abs(game.Speed - 2f) < .001f, "muddy feet halves fox speed");
        game.HP = 80;
        game.Execute(ActionKind.HealthCut);
        Check(Math.Abs(game.HP - 40) < .001f, "health cut uses current health");
        Advance(game, 5.1f);
        Check(game.RainbowShots == 0 && game.PetalHeart > .8f && game.MuddyFeet > 2,
              "effects use independent durations");
        Advance(game, 3);
        Check(game.EnemyGlow == 0 && game.ReverseFacing == 0 && game.EnemySpin == 0 &&
              game.PetalHeart == 0 && game.MuddyFeet == 0, "effects expire");
        game.Reset();
        Check(game.EnemyGlow == 0 && game.PetalHeart == 0 && game.ForceWavePulsesRemaining == 0,
              "reset clears new effects");

        game = Start(new Balance { foxSpeed = 0, attackInterval = 1000 });
        var target = game.AddEnemy(EnemyKind.Boar, new V(5, 0), false);
        target.speed = target.cooldown = 0;
        game.Execute(ActionKind.RainbowShots);
        game.Step(Dt);
        Check(game.Shots.Count > 0 && Math.Abs(game.Shots[0].damage - game.Damage * 2) < .001f,
              "rainbow doubles friendly bullet damage");
    }
    static void ForceWave()
    {
        var game = Start(new Balance { foxSpeed = 0, attackInterval = 1000, boltDamage = 0 });
        game.Fox = new V(0, 0);
        var enemy = game.AddEnemy(EnemyKind.Boar, new V(5, 0), false);
        enemy.speed = enemy.cooldown = 0;
        enemy.hp = enemy.maxHP = 1000;
        float hp = game.HP;
        float enemyHp = enemy.hp;
        game.Execute(ActionKind.ForceWave);
        Check(Math.Abs(enemy.pos.x - 7.5f) < .01f && game.ForceWavePulsesRemaining == 2,
              "force wave first immediate knockback");
        Advance(game, .5f);
        Check(game.ForceWavePulsesRemaining == 2, "force wave waits for second pulse");
        Advance(game, .2f);
        Check(game.ForceWavePulsesRemaining == 1, "force wave second pulse at .6 seconds");
        Advance(game, .6f);
        Check(game.ForceWavePulsesRemaining == 0 && game.ForceWavePulse > 0,
              "force wave third pulse at 1.2 seconds");
        Check(Math.Abs(game.HP - hp) < .001f && Math.Abs(enemy.hp - enemyHp) < .001f,
              "force wave deals no damage");
    }
    static void DispatcherSemantics()
    {
        var balance = new Balance { foxSpeed = 0, attackInterval = 1000 };
        balance.actionCooldowns[(int)ActionKind.EnemyGlow] = 0;
        var game = Start(balance);
        var gateway = new LocalSimulatorGateway();
        using (var dispatcher = new InteractionDispatcher(game, gateway, new ReceiptJournal(100)))
        {
            var glow = new ActionRequest { receiptId = "glow-once", sessionId = "viewer-a",
                actionId = Actions.Scoped(game.RunId, ActionKind.EnemyGlow) };
            Check(dispatcher.Submit(glow).status == "queued", "new action queues");
            dispatcher.Tick();
            Check(gateway.LastReceipt.status == "executed" && game.ExecutedActions == 1,
                  "new action executes");
            Check(dispatcher.Submit(glow).status == "executed" && game.ExecutedActions == 1,
                  "new action receipt is idempotent");
            var active = new ActionRequest { receiptId = "glow-active", sessionId = "viewer-b",
                actionId = Actions.Scoped(game.RunId, ActionKind.EnemyGlow) };
            Check(dispatcher.Submit(active).reason == "effect_active", "active cosmetic cannot refresh");

            var rainbow = new ActionRequest { receiptId = "rainbow-once", sessionId = "viewer-c",
                actionId = Actions.Scoped(game.RunId, ActionKind.RainbowShots) };
            Check(dispatcher.Submit(rainbow).status == "queued", "rainbow queues");
            AdvanceWithDispatcher(game, 1.1f, dispatcher);
            Check(dispatcher.Cooldown(ActionKind.RainbowShots) > 9.8f,
                  "rainbow starts ten-second cooldown");
            var rainbowAgain = new ActionRequest { receiptId = "rainbow-again", sessionId = "viewer-d",
                actionId = Actions.Scoped(game.RunId, ActionKind.RainbowShots) };
            Check(dispatcher.Submit(rainbowAgain).reason == "shared_cooldown",
                  "rainbow cooldown blocks refresh");
        }
    }
    static void AdvanceWithDispatcher(Simulation game, float seconds, InteractionDispatcher dispatcher)
    {
        for (int i = 0; i < (int)(seconds / Dt); i++)
        {
            game.Step(Dt);
            dispatcher.Tick();
        }
    }
    static void MiniBoss()
    {
        var game = Start(new Balance { bossAt = 1000, attackInterval = 1000 });
        game.Execute(ActionKind.MiniBoss);
        var mini = game.Enemies.Find(e => e.miniBoss);
        Check(mini != null && mini.kind == EnemyKind.MiniBoss && mini.maxHP == game.B.bossHP * .25f,
              "mini-boss marker and quarter end-boss HP");
        Check(game.Boss == null && game.Phase == Phase.Combat, "mini-boss does not start final phase");
        mini.hp = 0;
        game.Step(Dt);
        Check(game.Outcome == Outcome.None && game.Boss == null, "mini-boss death cannot win");
        var tight = new Balance { bossAt = 1000, enemyCap = 4, phaseCaps = new[] { 4, 4, 4, 4 }, attackInterval = 1000 };
        game = Start(tight);
        game.Execute(ActionKind.MiniBoss);
        while (game.EnemyCount < game.Cap)
            game.AddEnemy(EnemyKind.Sprig, new V(8, 5), false);
        Check(game.EnemyCount == 4 && game.Enemies.Count <= tight.enemyCap,
              "mini-boss shares bounded enemy capacity");
        game.Enemies.Find(e => e.miniBoss).hp = 0;
        game.Step(Dt);
        while (game.EnemyCount < game.Cap)
            game.AddEnemy(EnemyKind.Sprig, new V(8, 5), false);
        using (var dispatcher = new InteractionDispatcher(game, new LocalSimulatorGateway(), new ReceiptJournal(100)))
            Check(dispatcher.Availability(ActionKind.MiniBoss) == "enemy_capacity",
                  "mini-boss rejects a full shared capacity");
        game = Start(new Balance { bossAt = .01f, attackInterval = 1000, foxHP = 100000 });
        game.Step(Dt);
        Check(game.Phase == Phase.Boss && game.Boss != null, "real boss still spawns");
        game.Execute(ActionKind.MiniBoss);
        mini = game.Enemies.Find(e => e.miniBoss);
        Check(mini != null && game.Boss != mini, "mini-boss remains separate from final boss");
        mini.hp = 0;
        game.Step(Dt);
        Check(game.Outcome == Outcome.None && game.Boss != null, "mini-boss death preserves final boss");
        game.Boss.hp = 0;
        game.Step(Dt);
        Check(game.Outcome == Outcome.Victory, "final boss still controls victory");
    }
}
