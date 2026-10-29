using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Foxfire
{
// Development-only deterministic presentation fixture for the viewer action set. It writes
// native Unity frames and a small manifest; it never connects to WebsiteGateway or a live bridge.
public sealed class ActionPreview : MonoBehaviour
{
    public ArenaController arena;
    static readonly string[] Actions = {
        "EnemyGlow", "ReverseFacing", "EnemySpin", "PetalHeart", "HealthCut", "MiniBoss",
        "MuddyFeet", "ForceWave", "RainbowShots"
    };
    static readonly ActionKind[] Kinds = {
        ActionKind.EnemyGlow, ActionKind.ReverseFacing, ActionKind.EnemySpin,
        ActionKind.PetalHeart, ActionKind.HealthCut, ActionKind.MiniBoss,
        ActionKind.MuddyFeet, ActionKind.ForceWave, ActionKind.RainbowShots
    };
    static Enemy SetupActors(Simulation game)
    {
        game.Enemies.Clear();
        var anchor = game.AddEnemy(EnemyKind.Boar, new V(-3.4f, 1.25f), false);
        anchor.hp = anchor.maxHP = 10000;
        var sprig = game.AddEnemy(EnemyKind.Sprig, new V(0, 1.55f), false);
        sprig.hp = sprig.maxHP = 10000;
        var wisp = game.AddEnemy(EnemyKind.Wisp, new V(3.4f, 1.25f), false);
        wisp.hp = wisp.maxHP = 10000;
        // The real final Warden remains present in every case; the MiniBoss action adds its
        // own quarter-health entity so the two boss paths are exercised together.
        var finalBoss = game.AddEnemy(EnemyKind.Warden, new V(4.3f, -1.15f), false);
        finalBoss.hp = finalBoss.maxHP = 10000;
        WardenEncounter.Begin(game, finalBoss);
        for (int i = 0; i < 5; i++)
            game.Shots.Add(new Shot { pos = new V(-2.2f + i * 1.1f, -.2f), velocity = new V(1.6f, 0),
                                      life = 2, hostile = false, damage = 0 });
        game.Fox = new V(0, -2.2f);
        game.Direction = new V(1, .2f);
        game.HP = game.MaxHP = 100000;
        game.Immune = 100000;
        return anchor;
    }

    IEnumerator Start()
    {
        if (arena == null) yield break;
        arena.enabled = false;
        string root = Environment.GetEnvironmentVariable("FOXFIRE_STATE_DIR") ?? Application.persistentDataPath;
        string directory = Path.Combine(root, "action-preview");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "motion.tsv"), "action\tbefore_x\tafter_x\n");
        File.WriteAllText(Path.Combine(directory, "force-wave.tsv"), "");
        var captured = new List<string>();
        var available = new List<string>();
        int frame = 0;
        int forceWavePulsesObserved = 0;
        float healthBefore = 0, healthAfter = 0;
        for (int actionIndex = 0; actionIndex < Actions.Length; actionIndex++)
        {
            string action = Actions[actionIndex];
            arena.Game.Reset();
            arena.Game.Step(1f / 30);
            Enemy motionAnchor = SetupActors(arena.Game);
            if (action == "HealthCut") healthBefore = arena.Game.HP;
            arena.Game.Execute(Kinds[actionIndex], "ActionPreview");
            if (action == "HealthCut") healthAfter = arena.Game.HP;
            available.Add(action);
            int previousPulses = arena.Game.ForceWavePulsesRemaining;
            int forcePulses = action == "ForceWave" ? 1 : 0;
            float motionBefore = motionAnchor.pos.x, motionAfter = motionBefore;
            for (int tick = 0; tick < 60; tick++)
            {
                arena.Game.Step(1f / 30);
                int remaining = arena.Game.ForceWavePulsesRemaining;
                if (action == "ForceWave" && remaining < previousPulses)
                    forcePulses += previousPulses - remaining;
                previousPulses = remaining;
                if (tick == 3) motionBefore = motionAnchor.pos.x;
                if (tick == 42) motionAfter = motionAnchor.pos.x;
                arena.RenderCombatPreview();
                yield return new WaitForEndOfFrame();
                bool capture = tick == 3 || tick == 42 ||
                               action == "ForceWave" && (tick == 20 || tick == 38);
                if (capture)
                {
                    string name = "frame-" + action.ToLowerInvariant() + "-" + tick.ToString("D2") + ".png";
                    var texture = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(directory, name), texture.EncodeToPNG());
                    Destroy(texture);
                    captured.Add(name);
                    frame++;
                }
            }
            if (action == "ReverseFacing" || action == "EnemySpin")
                File.AppendAllText(Path.Combine(directory, "motion.tsv"),
                    action + "\t" + motionBefore.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                    "\t" + motionAfter.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "\n");
            if (action == "ForceWave")
            {
                File.AppendAllText(Path.Combine(directory, "force-wave.tsv"),
                    "pulses=" + forcePulses + "\tremaining=" + arena.Game.ForceWavePulsesRemaining + "\n");
                forceWavePulsesObserved = forcePulses;
            }
        }
        bool passed = captured.Count == Actions.Length * 2 + 2 && available.Count == Actions.Length &&
                      healthAfter < healthBefore && forceWavePulsesObserved == 3;
        File.WriteAllText(Path.Combine(directory, "result.json"),
            "{\"frames\":" + frame + ",\"actions\":" + Actions.Length +
            ",\"available\":" + available.Count + ",\"health_cut_before\":" + healthBefore +
            ",\"health_cut_after\":" + healthAfter + ",\"force_wave_pulses\":" + forceWavePulsesObserved +
            ",\"isolated_fixture\":true,\"passed\":" +
            (passed ? "true" : "false") + "}");
        Debug.Log("FOXFIRE_ACTION_PREVIEW_" + (passed ? "SUCCESS" : "FAILED") + " " + directory);
        Application.Quit(passed ? 0 : 1);
    }
}
}
