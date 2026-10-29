using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
namespace Foxfire
{
// Only attached by the explicit development-player -warden-preview switch.
// This fixture is event/state driven so a slower or faster development player does not
// silently produce a false-positive, time-anchored boss capture.
public sealed class WardenPreview : MonoBehaviour
{
    public ArenaController arena;
    readonly HashSet<int> preBossEnemies = new HashSet<int>();
    readonly HashSet<int> observedMinions = new HashSet<int>();
    readonly HashSet<string> captured = new HashSet<string>();
    readonly List<string> captureOrder = new List<string>();
    readonly HashSet<string> motionFrames = new HashSet<string>();
    bool bossSeen, chase, slash, spin, slam, summon, minions, slamImpact, shockwaves;
    bool stage1, stage2, stage3, death, fade, deathRequested;
    int previewStage = 1;
    int maxActiveRings, summonStateFrames, stage1Frames, stage2Frames, stage3Frames;
    string directory, failure;
    static readonly string[] CaptureNames = {
        "chase", "slash", "spin", "slam", "slam-impact", "shockwaves", "summon", "minions",
        "stage-1", "stage-2", "stage-3", "death", "fade"
    };
    static readonly string[] RequiredBeforeDeath = {
        "chase", "slash", "spin", "slam", "slam-impact", "shockwaves", "summon", "minions",
        "stage-1", "stage-2", "stage-3"
    };

    IEnumerator Start()
    {
        directory = Path.Combine(Environment.GetEnvironmentVariable("FOXFIRE_STATE_DIR") ??
                                 Application.persistentDataPath, "warden-preview");
        Directory.CreateDirectory(directory);
        foreach (var name in CaptureNames)
            File.Delete(Path.Combine(directory, name + ".png"));
        for (var clip = WardenClip.Walking; clip <= WardenClip.Dead; clip++)
            for (int phase = 0; phase < 8; phase++)
                File.Delete(Path.Combine(directory, "motion-" + clip + "-" + phase + ".png"));
        File.Delete(Path.Combine(directory, "result.json"));

        // The in-memory balance gives the encounter enough simulated time to reach all three
        // health stages. ArenaController owns the isolated boss HP and zero player damage.
        double deadline = arena.Game.B.maxSeconds + 8;
        while (true)
        {
            // Record eight distinct clip phases from the actual world render, not just
            // state labels or a single attack still. Files are bounded to 6 x 8 frames.
            yield return new WaitForEndOfFrame();
            if (!CaptureMotion()) break;
            Observe();
            string milestone = NextMilestone();
            if (milestone != null)
            {
                yield return new WaitForEndOfFrame();
                if (!Capture(milestone))
                    break;
                continue;
            }

            PrepareNextHealthStage();

            if (!deathRequested && CoveredBeforeDeath())
            {
                var boss = arena.Game.Boss;
                if (boss == null)
                {
                    Fail("boss disappeared before the covered death capture");
                    break;
                }
                boss.hp = 0;
                deathRequested = true;
                continue;
            }

            if (CoveredAll())
                break;
            if (arena.Game.Clock >= deadline)
            {
                Fail("preview timed out before all required milestones");
                break;
            }
            yield return null;
        }

        bool success = failure == null && CoveredAll();
        WriteResult(success);
        Debug.Log((success ? "FOXFIRE_WARDEN_PREVIEW_SUCCESS " : "FOXFIRE_WARDEN_PREVIEW_FAILED ") + directory);
        Application.Quit(success ? 0 : 1);
    }

    void PrepareNextHealthStage()
    {
        var boss = arena.Game.Boss;
        if (boss == null || deathRequested || boss.wardenState != WardenState.Chase)
            return;
        if (previewStage == 1 && CanCaptureStage(1) && captured.Contains("stage-1") && MotionComplete(WardenClip.Walking))
        {
            // This is an isolated fixture-only health step, not player damage. Keeping the boss
            // comfortably above zero also makes an accidental early death impossible.
            boss.hp = boss.maxHP * .69f;
            previewStage = 2;
        }
        else if (previewStage == 2 && CanCaptureStage(2) && captured.Contains("stage-2"))
        {
            boss.hp = boss.maxHP * .34f;
            previewStage = 3;
        }
    }

    void Observe()
    {
        var boss = arena.Game.Boss;
        if (boss != null && !bossSeen)
        {
            bossSeen = true;
            foreach (var enemy in arena.Game.Enemies)
                if (enemy.kind != EnemyKind.Warden)
                    preBossEnemies.Add(enemy.id);
        }
        if (boss != null)
        {
            if (boss.wardenState == WardenState.Chase)
                chase = true;
            if (boss.wardenState == WardenState.Windup || boss.wardenState == WardenState.Recover)
            {
                if (boss.wardenMove == WardenMove.Slash) slash = true;
                if (boss.wardenMove == WardenMove.Spin) spin = true;
                if (boss.wardenMove == WardenMove.Slam) slam = true;
                if (boss.wardenMove == WardenMove.Summon)
                {
                    summon = true;
                    summonStateFrames++;
                }
                if (boss.wardenMove == WardenMove.Slam && arena.Game.Shockwaves.Count > 0)
                    slamImpact = true;
            }
            switch (boss.wardenStage)
            {
                case 1: stage1 = true; stage1Frames++; break;
                case 2: stage2 = true; stage2Frames++; break;
                case 3: stage3 = true; stage3Frames++; break;
            }
        }
        if (arena.Game.Shockwaves.Count > 0)
        {
            shockwaves = true;
            maxActiveRings = Math.Max(maxActiveRings, arena.Game.Shockwaves.Count);
        }
        if (bossSeen)
        {
            foreach (var enemy in arena.Game.Enemies)
            {
                if (enemy.kind == EnemyKind.Warden || preBossEnemies.Contains(enemy.id))
                    continue;
                observedMinions.Add(enemy.id);
                if (enemy.wardenSummoned && enemy.hp > 0 && enemy.spawnTell <= 0)
                    minions = true;
            }
        }
        if (arena.Game.Boss == null && arena.WardenPresentation.Clip == WardenClip.Dead &&
            arena.WardenPresentation.Visible)
            death = true;
        if (death && !arena.WardenPresentation.Visible)
            fade = true;
    }

    bool CoveredBeforeDeath()
    {
        for (var clip = WardenClip.Walking; clip <= WardenClip.Slash; clip++)
            if (!MotionComplete(clip)) return false;
        if (!(chase && slash && spin && slam && summon && slamImpact && shockwaves &&
              stage1 && stage2 && stage3 && minions))
            return false;
        foreach (var name in RequiredBeforeDeath)
            if (!captured.Contains(name))
                return false;
        return true;
    }

    bool CoveredAll()
    {
        return CoveredBeforeDeath() && death && fade && MotionComplete(WardenClip.Dead);
    }

    bool MotionComplete(WardenClip clip)
    {
        for (int phase = 0; phase < 8; phase++)
            if (!motionFrames.Contains("motion-" + clip + "-" + phase)) return false;
        return true;
    }

    bool CaptureMotion()
    {
        var presentation = arena.WardenPresentation;
        if (!presentation.Visible || presentation.Clip == WardenClip.Idle) return true;
        float phase = presentation.Seconds / WardenAnimation.Duration(presentation.Clip);
        if (presentation.Clip == WardenClip.Walking || presentation.Clip == WardenClip.Running)
            phase %= 1;
        int bin = Mathf.Clamp(Mathf.FloorToInt(phase * 8), 0, 7);
        string name = "motion-" + presentation.Clip + "-" + bin;
        if (motionFrames.Contains(name)) return true;
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        if (texture == null) { Fail("failed to capture " + name); return false; }
        File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
        Destroy(texture);
        motionFrames.Add(name);
        return true;
    }

    string NextMilestone()
    {
        if (chase && CanCaptureChase() && !captured.Contains("chase")) return "chase";
        if (slash && CanCaptureMove(WardenMove.Slash) && !captured.Contains("slash")) return "slash";
        if (spin && CanCaptureMove(WardenMove.Spin) && !captured.Contains("spin")) return "spin";
        if (slam && CanCaptureMove(WardenMove.Slam) && !captured.Contains("slam")) return "slam";
        if (slamImpact && HasMatureShockwave() && !captured.Contains("slam-impact")) return "slam-impact";
        if (shockwaves && HasMatureShockwave() && !captured.Contains("shockwaves")) return "shockwaves";
        if (summon && CanCaptureMove(WardenMove.Summon) && !captured.Contains("summon")) return "summon";
        if (minions && !captured.Contains("minions")) return "minions";
        if (stage1 && CanCaptureStage(1) && !captured.Contains("stage-1")) return "stage-1";
        if (stage2 && CanCaptureStage(2) && !captured.Contains("stage-2")) return "stage-2";
        if (stage3 && CanCaptureStage(3) && !captured.Contains("stage-3")) return "stage-3";
        if (death && CanCaptureDeath() && !captured.Contains("death")) return "death";
        if (fade && !captured.Contains("fade")) return "fade";
        return null;
    }

    bool CanCaptureChase()
    {
        var boss = arena.Game.Boss;
        return boss != null && boss.wardenState == WardenState.Chase &&
               arena.Game.Clock - boss.wardenStateClock >= .4;
    }

    bool CanCaptureStage(int stage)
    {
        var boss = arena.Game.Boss;
        return boss != null && boss.wardenStage == stage && boss.wardenState == WardenState.Chase &&
               arena.Game.Clock - boss.wardenStateClock >= .6;
    }

    bool CanCaptureMove(WardenMove move)
    {
        var boss = arena.Game.Boss;
        return boss != null && boss.wardenState == WardenState.Windup && boss.wardenMove == move &&
               arena.Game.Clock - boss.wardenStateClock >= WardenTiming.Windup(move) * .5f;
    }

    bool HasMatureShockwave()
    {
        foreach (var wave in arena.Game.Shockwaves)
            if (wave.radius >= 2.5f)
                return true;
        return false;
    }

    bool CanCaptureDeath()
    {
        return arena.Game.Boss == null && arena.WardenPresentation.Clip == WardenClip.Dead &&
               arena.WardenPresentation.Visible && arena.WardenPresentation.Seconds > 1;
    }

    bool Capture(string name)
    {
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        if (texture == null)
        {
            Fail("failed to capture " + name);
            return false;
        }
        File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
        Destroy(texture);
        captured.Add(name);
        captureOrder.Add(name);
        return true;
    }

    void Fail(string reason)
    {
        if (failure == null)
            failure = reason;
    }

    static string Json(string value)
    {
        return value == null ? "null" : "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    string CaptureOrderJson()
    {
        var json = new StringBuilder("[");
        for (int i = 0; i < captureOrder.Count; i++)
        {
            if (i > 0) json.Append(',');
            json.Append(Json(captureOrder[i]));
        }
        return json.Append(']').ToString();
    }

    void WriteResult(bool success)
    {
        var json = new StringBuilder();
        json.Append("{\"schemaVersion\":2,\"success\":").Append(success ? "true" : "false");
        json.Append(",\"motionFrames\":").Append(motionFrames.Count);
        json.Append(",\"completed\":").Append(CoveredAll() ? "true" : "false");
        json.Append(",\"failure\":").Append(Json(failure));
        json.Append(",\"source\":\"Unity development player render\"");
        json.Append(",\"frames\":").Append(captureOrder.Count);
        json.Append(",\"captureOrder\":").Append(CaptureOrderJson());
        json.Append(",\"chase\":").Append(chase ? "true" : "false");
        json.Append(",\"slash\":").Append(slash ? "true" : "false");
        json.Append(",\"spin\":").Append(spin ? "true" : "false");
        json.Append(",\"slam\":").Append(slam ? "true" : "false");
        json.Append(",\"slamImpact\":").Append(slamImpact ? "true" : "false");
        json.Append(",\"shockwaves\":").Append(shockwaves ? "true" : "false");
        json.Append(",\"summons\":").Append(summon ? "true" : "false");
        json.Append(",\"minions\":").Append(minions ? "true" : "false");
        json.Append(",\"stage1\":").Append(stage1 ? "true" : "false");
        json.Append(",\"stage2\":").Append(stage2 ? "true" : "false");
        json.Append(",\"stage3\":").Append(stage3 ? "true" : "false");
        json.Append(",\"death\":").Append(death ? "true" : "false");
        json.Append(",\"fade\":").Append(fade ? "true" : "false");
        json.Append(",\"maxActiveRings\":").Append(maxActiveRings);
        json.Append(",\"minionsObserved\":").Append(observedMinions.Count);
        json.Append(",\"summonStateFrames\":").Append(summonStateFrames);
        json.Append(",\"stage1Frames\":").Append(stage1Frames);
        json.Append(",\"stage2Frames\":").Append(stage2Frames);
        json.Append(",\"stage3Frames\":").Append(stage3Frames);
        json.Append(",\"outcome\":").Append(Json(arena.Game.Outcome.ToString()));
        json.Append(",\"simulationSeconds\":").Append(arena.Game.Time.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        json.Append('}');
        File.WriteAllText(Path.Combine(directory, "result.json"), json.ToString());
    }
}
}
