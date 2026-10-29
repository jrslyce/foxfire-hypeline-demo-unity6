using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace Foxfire
{
// Development-only, isolated scripted acceptance capture, never a live gateway.
public sealed class CombatPreview : MonoBehaviour
{
    public ArenaController arena;
    string caption = "LOCAL COMBAT FIXTURE — scripted setup, real simulation/render";
    readonly System.Text.StringBuilder evidence = new System.Text.StringBuilder();
    readonly System.Collections.Generic.HashSet<string> executed = new System.Collections.Generic.HashSet<string>();
    void Submit(ActionKind kind, string name, string id)
    {
        var receipt = arena.Dispatcher.Submit(new ActionRequest {
            receiptId = id, sessionId = id, displayName = name,
            actionId = Actions.Scoped(arena.Game.RunId, kind)
        });
        evidence.AppendLine(id + " submitted=" + receipt.status);
    }
    IEnumerator Start()
    {
        if (!Debug.isDebugBuild) { enabled = false; yield break; }
        string directory = Path.Combine(Environment.GetEnvironmentVariable("FOXFIRE_STATE_DIR") ?? Application.persistentDataPath, "combat-preview");
        Directory.CreateDirectory(directory);
        var game = arena.Game;
        arena.Dispatcher.Received += r => {
            evidence.AppendLine(r.id + " status=" + r.status + " reason=" + r.reason);
            if (r.status == "executed") executed.Add(r.id);
        };
        game.Step(1f / 30);
        game.Enemies.Clear(); game.Shots.Clear();
        game.Fox = new V(0, 0);
        game.Enemies.Add(new Enemy { id = 2001, kind = EnemyKind.Boar, pos = new V(-6, 0), hp = 1000, maxHP = 1000, speed = game.B.boarSpeed, radius = .5f });
        game.Enemies.Add(new Enemy { id = 2002, kind = EnemyKind.Sprig, pos = new V(5, 2), hp = 1000, maxHP = 1000, speed = game.B.sprigSpeed, radius = .35f });
        game.Enemies.Add(new Enemy { id = 2003, kind = EnemyKind.Wisp, pos = new V(2, -4), hp = 1000, maxHP = 1000, speed = game.B.wispSpeed, radius = .4f });
        int captures = 0, hostilePeak = 0;
        float healBefore = 0, healedHP = 0, thornBefore = 0, thornAfter = 0;
        bool boarCharge = false, mushroomCharge = false;
        for (int tick = 0; tick <= 600; tick++)
        {
            if (tick == 240)
            {
                evidence.AppendLine("combat HP=" + game.HP);
                game.Enemies.Clear(); game.Shots.Clear(); game.Hazards.Clear(); game.Pickups.Clear();
                game.Fox = new V(0, 0); game.HP = 55; game.Immune = 0;
                healBefore = game.HP;
                caption = "LOCAL FIXTURE — Healing Pod through the real dispatcher";
                Submit(ActionKind.HealPod, "LadyBird", "preview-heal");
            }
            if (tick == 360)
            {
                healedHP = game.HP;
                game.Fox = new V(0, 0); game.Direction = new V(1, 0);
                game.Enemies.Add(new Enemy { id = 2004, kind = EnemyKind.Boar, pos = new V(-3, 1), hp = 1000, maxHP = 1000, cooldown = 100, shotCooldown = 100, radius = .5f });
                caption = "LOCAL FIXTURE — Star Strike and Bramble sender attribution";
                Submit(ActionKind.StarStrike, "BBQ-fan", "preview-star");
                Submit(ActionKind.ThornPatch, "RainyStreet", "preview-thorn");
            }
            if (tick >= 435 && tick <= 510)
            {
                // Explicit collision fixture: place the fox in the live bramble to measure real damage.
                foreach (var hazard in game.Hazards)
                    if (hazard.viewer && !hazard.star && hazard.tell <= 0)
                    {
                        game.Fox = hazard.pos;
                        if (thornBefore == 0) thornBefore = game.HP;
                        caption = "LOCAL COLLISION FIXTURE — fox held in bramble; real HP damage";
                    }
            }
            game.Step(1f / 30);
            arena.Dispatcher.Tick();
            if (tick == 510) thornAfter = game.HP;
            int hostile = 0;
            foreach (var shot in game.Shots) if (shot.hostile) hostile++;
            hostilePeak = Math.Max(hostilePeak, hostile);
            foreach (var enemy in game.Enemies)
                if (enemy.charge > 0) { if (enemy.kind == EnemyKind.Boar) boarCharge = true; else mushroomCharge = true; }
            arena.RenderCombatPreview();
            yield return new WaitForEndOfFrame();
            if (tick % 4 == 0)
            {
                var capture = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(directory, "frame-" + captures.ToString("D3") + ".png"), capture.EncodeToPNG());
                Destroy(capture); captures++;
            }
        }
        evidence.AppendLine("frames=" + captures + " hostilePeak=" + hostilePeak + " boarCharge=" + boarCharge + " mushroomCharge=" + mushroomCharge);
        evidence.AppendLine("heal before=" + healBefore + " after=" + healedHP);
        evidence.AppendLine("bramble before=" + thornBefore + " after=" + thornAfter);
        bool passed = hostilePeak > 0 && boarCharge && mushroomCharge && healedHP > healBefore && thornAfter < thornBefore &&
            executed.Contains("preview-heal") && executed.Contains("preview-star") && executed.Contains("preview-thorn");
        evidence.AppendLine("passed=" + passed + " isolated_fixture=true");
        File.WriteAllText(Path.Combine(directory, "result.txt"), evidence.ToString());
        Debug.Log("FOXFIRE_COMBAT_PREVIEW_" + (passed ? "SUCCESS" : "FAILED") + " " + directory);
        Application.Quit(passed ? 0 : 1);
    }
    void OnGUI()
    {
        GUI.matrix = Matrix4x4.identity;
        GUI.Box(new Rect(12, Screen.height - 35, Screen.width - 24, 27), caption);
    }
}
}
