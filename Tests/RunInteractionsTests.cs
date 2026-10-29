using System;
using Foxfire;

class RunInteractionsTests
{
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    static ActionRequest Request(Simulation game, string id, string name, ActionKind kind = ActionKind.TealCelebration)
    {
        return new ActionRequest { receiptId = id, sessionId = id, displayName = name,
            actionId = Actions.Scoped(game.RunId, kind) };
    }
    static void Main()
    {
        var game = new Simulation(new Balance { introSeconds = 0, bossAt = 100000,
            maxSeconds = 100001, resultsSeconds = 1, phaseCaps = new[] { 0, 0, 0, 0 } });
        game.Step(1f / 30);
        using (var dispatcher = new InteractionDispatcher(game, new LocalSimulatorGateway(), new ReceiptJournal(1000)))
        {
            var action = Request(game, "one", "BBQ-fan");
            Check(dispatcher.Submit(action).status == "queued", "accepted request");
            dispatcher.Submit(action);
            Check(game.RunInteractions.TotalSent == 1, "queued replay counted once");
            action.displayName = "mutated";
            dispatcher.Tick(); dispatcher.Submit(action);
            Check(game.RunInteractions.TotalSent == 1 && game.RunInteractions.TotalPlayed == 1, "terminal replay counted once");
            Check(game.RunInteractions.ViewerAt(0) == "BBQ-fan", "snapshot contributor before caller mutation");
            var rejected = Request(game, "two", "LadyBird");
            Check(dispatcher.Submit(rejected).status == "rejected", "cooldown rejected");
            dispatcher.Submit(rejected);
            Check(game.RunInteractions.TotalSent == 2 && game.RunInteractions.TotalPlayed == 1, "rejection counted once but never played");
            var expired = Request(game, "three", "Muxy#1", ActionKind.LaserMode);
            expired.expiresAtUnixMs = 1;
            dispatcher.Submit(expired); dispatcher.Submit(expired);
            Check(game.RunInteractions.SentCount(ActionKind.LaserMode) == 1 && game.RunInteractions.PlayedCount(ActionKind.LaserMode) == 0, "fresh expired action tracked once");
            dispatcher.Submit(new ActionRequest { receiptId = "join", sessionId = "join", eventKind = "join", displayName = "RainyStreet" });
            var invalid = Request(game, "unknown", "Unknown"); invalid.actionId = "unknown";
            dispatcher.Submit(invalid);
            var stale = Request(game, "stale", "Stale"); stale.actionId = "fox_old_teal_celebration";
            dispatcher.Submit(stale);
            Check(game.RunInteractions.TotalSent == 3 && game.RunInteractions.ViewerCount == 3, "joins and unscoped requests excluded");
            var pending = Request(game, "pending", "RainyStreet", ActionKind.LaserMode);
            dispatcher.Submit(pending);
            game.HP = 0; game.Step(1f / 30); dispatcher.Tick();
            var final = game.RunInteractions;
            Check(final.Sealed && final.TotalSent == 4 && final.TotalPlayed == 1, "death freezes pending as not played");
            dispatcher.Submit(Request(game, "late", "LateViewer"));
            Check(final.TotalSent == 4 && final.ViewerCount == 4, "results-time request cannot change report");
            Check(final.ViewerAt(0) == "BBQ-fan" && final.ViewerAt(1) == "LadyBird" && final.ViewerAt(2) == "Muxy#1" && final.ViewerAt(3) == "RainyStreet", "alphabetic names and exact punctuation");
            game.Reset();
            Check(game.RunInteractions.TotalSent == 0 && game.RunInteractions.ViewerCount == 0 && final.TotalSent == 4, "next run resets without altering finished report");
            game.Step(1f / 30);
            for (int i = 0; i < 25; i++)
            {
                var r = Request(game, "viewer-" + i, "Viewer" + i);
                r.expiresAtUnixMs = 1; dispatcher.Submit(r);
            }
            Check(game.ThankYouPages == 3 && game.ResultsDuration >= 15, "all contributors receive a readable page");
            game.HP = 0; game.Step(1f / 30);
            int run = game.RunNumber;
            for (int i = 0; i < 301; i++) game.Step(1f / 30);
            Check(game.RunNumber == run && game.Phase == Phase.Results, "hold results for the last thank-you page");
            for (int i = 0; i < 160; i++) game.Step(1f / 30);
            Check(game.RunNumber == run + 1 && game.RunInteractions.TotalSent == 0, "automatic next run after all pages");
        }
        Console.WriteLine("PASS run report: sent/played, replay, rejection, expiry, sender snapshot, joins, stale requests, death freeze, reset and complete thank-you pagination");
    }
}
