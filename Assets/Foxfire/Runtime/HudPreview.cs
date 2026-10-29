using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Foxfire
{
// Opt-in development fixture: isolated local dispatcher plus actual rendered game frames.
// It cannot select WebsiteGateway or publish a live stream.
public sealed class HudPreview : MonoBehaviour
{
    public ArenaController arena;
    string directory;
    readonly System.Text.StringBuilder evidence = new System.Text.StringBuilder();
    bool executed;

    IEnumerator Capture(string name)
    {
        arena.RenderCombatPreview();
        yield return new WaitForEndOfFrame();
        var frame = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(directory, name + ".png"), frame.EncodeToPNG());
        evidence.AppendLine(name + " resolution=" + frame.width + "x" + frame.height +
            " hp=" + arena.Game.HP + " boss=" + (arena.Game.Boss != null) +
            " announcements=" + arena.Game.ViewerAnnouncements.Count);
        Destroy(frame);
    }

    IEnumerator Start()
    {
        if (!Debug.isDebugBuild) { enabled = false; yield break; }
        directory = Path.Combine(Environment.GetEnvironmentVariable("FOXFIRE_STATE_DIR") ??
            Application.persistentDataPath, "hud-preview");
        Directory.CreateDirectory(directory);
        var game = arena.Game;
        arena.Dispatcher.Received += receipt => {
            evidence.AppendLine(receipt.id + " status=" + receipt.status);
            if (receipt.id == "hud-celebration" && receipt.status == "executed") executed = true;
        };
        game.Step(1f / 30);
        game.Level = 6; game.HP = 84; game.MaxHP = 100;
        game.Fox = new V(0, 0);
        game.Enemies.Clear(); game.Shots.Clear();
        yield return Capture("01-normal");
        arena.Dispatcher.Submit(new ActionRequest { receiptId = "hud-celebration",
            sessionId = "hud-ladybird", displayName = "LadyBird",
            actionId = Actions.Scoped(game.RunId, ActionKind.TealCelebration) });
        for (int i = 0; i < 45; i++)
        {
            game.Step(1f / 30); arena.Dispatcher.Tick();
            yield return Capture("action-" + i.ToString("D3"));
        }
        // Explicit layout fixture: Warden health and 25-character name stress the same live renderer.
        game.Enemies.Add(new Enemy { id = 9001, kind = EnemyKind.Warden, pos = new V(2, 2),
            hp = 600, maxHP = 1000, radius = 1, cooldown = 100 });
        game.AnnounceViewer("Muxy#1", null);
        game.AnnounceViewer("WWWWWWWWWWWWWWWWWWWWWWWWW", "Orange Champion");
        for (int i = 0; i < 15; i++) game.Step(1f / 30);
        game.HP = 1;
        yield return Capture("02-boss-low-health-stacked");
        game.HP = 0;
        yield return Capture("03-empty-health");
        game.HP = game.MaxHP;
        yield return Capture("04-full-health");
        Screen.SetResolution(1024, 768, false);
        yield return null; yield return null;
        yield return Capture("05-four-three");
        Screen.SetResolution(1920, 1080, false);
        yield return null; yield return null;
        yield return Capture("06-full-hd");
        Screen.SetResolution(1280, 720, false);
        yield return null; yield return null;
        game.Enemies.Clear();
        for (int i = 0; i < 255; i++) game.Step(1f / 30);
        yield return Capture("07-expired");
        bool passed = executed && game.ViewerAnnouncements.Count == 0 && game.Boss == null;
        yield return ReportScenarios();
        passed &= reportPassed;
        evidence.AppendLine("passed=" + passed + " isolated_local_fixture=true");
        File.WriteAllText(Path.Combine(directory, "result.txt"), evidence.ToString());
        Debug.Log("FOXFIRE_HUD_PREVIEW_" + (passed ? "SUCCESS" : "FAILED"));
        Application.Quit(passed ? 0 : 1);
    }

    bool reportPassed;
    IEnumerator ReportScenarios()
    {
        var game = arena.Game;
        game.B.introSeconds = 5;
        game.Reset();
        yield return Capture("08-intro");
        game.B.introSeconds = 0;
        game.Step(1f / 30);
        game.GrantUpgrade();
        yield return Capture("09-level-up");
        game.GrantUpgrade(); game.GrantUpgrade();
        yield return Capture("10-stacked-upgrades");

        // Scripted local layout fixture; requests still pass through the actual dispatcher.
        game.B.phaseCaps = new[] { 30, 30, 30, 30 };
        game.B.spawnRates = new float[4];
        game.B.actionCooldowns = new float[Actions.Count];
        game.B.sessionCooldown = 0; game.B.dispatchInterval = 0;
        game.HP = 60; game.Immune = 10000;
        game.Enemies.Add(new Enemy { id = 3001, kind = EnemyKind.Boar, pos = new V(6, 3),
            hp = 10000, maxHP = 10000, speed = 0, cooldown = 1000, shotCooldown = 1000, radius = .5f });
        string[] names = { "LadyBird", "Muxy#1", "BBQ-fan", "RainyStreet" };
        for (int i = 0; i < Actions.Count; i++)
        {
            arena.Dispatcher.Submit(new ActionRequest { receiptId = "report-action-" + i,
                sessionId = "report-viewer-" + i, displayName = names[i % names.Length],
                actionId = Actions.Scoped(game.RunId, (ActionKind)i) });
            game.Step(1f / 30); arena.Dispatcher.Tick();
        }
        for (int i = 0; i < 16; i++)
        {
            game.Celebration = 0;
            string name = i == 0 ? "WWWWWWWWWWWWWWWWWWWWWWWWW" : "ForestFriend" + i.ToString("D2");
            arena.Dispatcher.Submit(new ActionRequest { receiptId = "report-thanks-" + i,
                sessionId = "report-thanks-" + i, displayName = name,
                actionId = Actions.Scoped(game.RunId, ActionKind.TealCelebration) });
            game.Step(1f / 30); arena.Dispatcher.Tick();
        }
        // Expired requests count as sent, without pretending they ran.
        arena.Dispatcher.Submit(new ActionRequest { receiptId = "report-expired", sessionId = "report-expired",
            displayName = "LadyBird", actionId = Actions.Scoped(game.RunId, ActionKind.LaserMode), expiresAtUnixMs = 1 });
        game.HP = 0; game.Step(1f / 30); arena.Dispatcher.Tick();
        var report = game.RunInteractions;
        reportPassed = game.Outcome == Outcome.Defeat && report.Sealed && report.TotalSent == 25 &&
            report.TotalPlayed > 0 && report.TotalPlayed < report.TotalSent && report.ViewerCount == 20;
        evidence.AppendLine("report sent=" + report.TotalSent + " played=" + report.TotalPlayed +
            " viewers=" + report.ViewerCount + " pages=" + game.ThankYouPages);
        for (int i = 0; i < Actions.Count; i++)
            evidence.AppendLine(Actions.Label((ActionKind)i) + " sent=" + report.SentCount((ActionKind)i) +
                " played=" + report.PlayedCount((ActionKind)i));
        yield return Capture("11-death-report-page-1");
        for (int i = 0; i < 190; i++) game.Step(1f / 30);
        yield return Capture("12-death-report-page-2");
        Screen.SetResolution(1024, 768, false);
        yield return null; yield return null;
        yield return Capture("13-report-four-three");
        Screen.SetResolution(1920, 1080, false);
        yield return null; yield return null;
        yield return Capture("14-report-full-hd");
        Screen.SetResolution(1280, 720, false);
        yield return null; yield return null;

        // Finish real simulation phases for the other result variants, including zero participation.
        game.Reset(); game.Step(1f / 30);
        game.B.bossAt = .01f; game.Step(1f / 30);
        if (game.Boss != null) game.Boss.hp = 0;
        for (int i = 0; i < 120 && game.Active; i++) game.Step(1f / 30);
        reportPassed &= game.Outcome == Outcome.Victory && game.RunInteractions.TotalSent == 0;
        yield return Capture("15-victory-empty-report");
        game.B.bossAt = 100000; game.B.maxSeconds = .01f;
        game.Reset(); game.Step(1f / 30); game.Step(1f / 30);
        reportPassed &= game.Outcome == Outcome.Timeout;
        yield return Capture("16-timeout-report");
        evidence.AppendLine("reportFixturesPassed=" + reportPassed);
    }
}
}
