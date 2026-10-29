using System;
using Foxfire;

class ChampionTests
{
    const float Dt = 1f / 30;
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    static Simulation Start(Balance balance = null)
    {
        var game = new Simulation(balance ?? new Balance());
        while (!game.Active) game.Step(Dt);
        return game;
    }
    static ActionRequest Request(Simulation game, string id, string name = "MuxyVisitor")
    {
        return new ActionRequest { receiptId = id, sessionId = "viewer-" + id,
            actionId = Actions.Scoped(game.RunId, ActionKind.OrangeChampion), displayName = name };
    }
    static void Main()
    {
        var game = Start();
        using (var dispatcher = new InteractionDispatcher(game, new LocalSimulatorGateway(), new ReceiptJournal(100)))
        {
            var request = Request(game, "champion", "Verified_Viewer42");
            Check(dispatcher.Submit(request).status == "queued", "champion queues");
            request.displayName = "changed_after_submission";
            dispatcher.Tick();
            var enemy = game.Enemies.Find(e => e.champion);
            Check(enemy != null && enemy.kind == EnemyKind.Boar, "champion boar spawned");
            Check(enemy.hp == game.B.boarHP * 3 && enemy.maxHP == enemy.hp, "champion has triple HP");
            Check(enemy.radius > .55f, "champion larger than regular boar");
            Check(enemy.spawnTell > 0 && V.Dist(enemy.pos, game.Fox) >= game.B.spawnSafeDistance, "safe visible portal");
            Check(enemy.displayName == "Verified_Viewer42", "name copied into immutable queued envelope");
            Check(dispatcher.Cooldown(ActionKind.OrangeChampion) == 5, "shared champion cooldown");
            request.expiresAtUnixMs = 1;
            Check(dispatcher.Submit(request).status == "executed" && game.ExecutedActions == 1,
                "completed duplicate remains executed even when replay has expired");
            enemy.pos = game.Fox; enemy.spawnTell = 0; enemy.speed = 0; enemy.cooldown = 100;
            game.Step(Dt);
            Check(Math.Abs(game.HP - (game.MaxHP - game.B.boarDamage * 1.25f)) < .01f, "champion stronger contact damage");
            enemy.hp = 0; game.Step(Dt);
            Check(!game.Enemies.Exists(e => e.champion), "dead champion removed");
            game.Reset();
            Check(!game.Enemies.Exists(e => e.champion) && dispatcher.Cooldown(ActionKind.OrangeChampion) == 0,
                "run reset clears champion and cooldown");
        }
        game = Start();
        game.Execute(ActionKind.OrangeChampion, null);
        using (var dispatcher = new InteractionDispatcher(game, new LocalSimulatorGateway(), new ReceiptJournal(100)))
        {
            Check(game.Enemies.Find(e => e.champion).displayName == "MuxyVisitor", "guest name fallback");
            Check(dispatcher.Availability(ActionKind.OrangeChampion) == "champion_active", "one active champion");
            game.HP = 20;
            Check(dispatcher.Availability(ActionKind.OrangeChampion) == "guardian_protected", "low HP protection");
        }
        game = Start(new Balance { bossAt = .01f }); game.Step(Dt);
        using (var dispatcher = new InteractionDispatcher(game, new LocalSimulatorGateway(), new ReceiptJournal(100)))
            Check(dispatcher.Availability(ActionKind.OrangeChampion) == null, "champion available in boss");
        game = Start();
        while (game.RegularCount < game.Cap) game.AddEnemy(EnemyKind.Sprig, new V(8, 5), false);
        using (var dispatcher = new InteractionDispatcher(game, new LocalSimulatorGateway(), new ReceiptJournal(100)))
            Check(dispatcher.Availability(ActionKind.OrangeChampion) == "enemy_capacity", "champion respects enemy cap");
        game = Start();
        using (var dispatcher = new InteractionDispatcher(game, new LocalSimulatorGateway(), new ReceiptJournal(100)))
        {
            var expired = Request(game, "expired"); expired.expiresAtUnixMs = 1;
            Check(dispatcher.Submit(expired).reason == "request_expired" && game.ExecutedActions == 0,
                "expired website request cannot execute");
            var future = Request(game, "future"); future.expiresAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 100;
            Check(dispatcher.Submit(future).status == "queued", "unexpired request queues");
            System.Threading.Thread.Sleep(150);
            dispatcher.Tick();
            Check(dispatcher.Journal.Find("future").reason == "request_expired" && game.ExecutedActions == 0,
                "request expiring in queue cannot execute");
        }
        Check(Actions.ViewerName("<>\0") == "MuxyVisitor", "unsafe empty name fallback");
        Check(Actions.ViewerName(new string('a', 300)).Length == 25, "bounded nameplate");
        Check(Actions.ViewerName("user\nname") == "username", "no control characters in nameplate");
        Check(Actions.Cooldown(new Balance { actionCooldowns = new float[6] }, ActionKind.OrangeChampion) == 5,
            "legacy six-action balance supported");
        Console.WriteLine("PASS champion identity, stronger stats, portal, limits, replay, expiry, reset and legacy balance tests");
    }
}
