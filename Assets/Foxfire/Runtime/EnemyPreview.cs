using System.Collections;
using System.IO;
using UnityEngine;
namespace Foxfire
{
// Explicit development-only render fixture. It cannot connect to a live bridge.
public sealed class EnemyPreview : MonoBehaviour
{
    public ArenaController arena;
    IEnumerator Start()
    {
        arena.enabled = false;
        string directory = Path.Combine(System.Environment.GetEnvironmentVariable("FOXFIRE_STATE_DIR") ?? Application.persistentDataPath, "enemy-preview");
        Directory.CreateDirectory(directory);
        var enemies = new[] {
            new Enemy { id = 1001, kind = EnemyKind.Boar, hp = 100, maxHP = 100 },
            new Enemy { id = 1002, kind = EnemyKind.Boar, champion = true, hp = 100, maxHP = 100 },
            new Enemy { id = 1003, kind = EnemyKind.Sprig, hp = 100, maxHP = 100 },
            new Enemy { id = 1004, kind = EnemyKind.Wisp, hp = 100, maxHP = 100 },
            new Enemy { id = 1005, kind = EnemyKind.Sprig, hp = 100, maxHP = 100 },
            new Enemy { id = 1006, kind = EnemyKind.Sprig, hp = 0, maxHP = 100 },
            new Enemy { id = 1007, kind = EnemyKind.Wisp, hp = 100, maxHP = 100 },
            new Enemy { id = 1008, kind = EnemyKind.Warden, hp = 100, maxHP = 100 }
        };
        int captures = 0;
        int rushCaptures = 0, expiredRushCaptures = 0;
        for (int tick = 0; tick <= 240; tick++)
        {
            arena.Game.Step(1f / 30);
            // Hold the rush state through the first half so the deterministic preview also
            // captures the speed boost indicator on model and fallback presentations.
            arena.Game.Rush = tick < 180 ? 8 : 0;
            arena.Game.Enemies.Clear();
            float time = tick / 30f;
            for (int n = 0; n < enemies.Length; n++)
            {
                // Two rows keep the rushing model and fallback cases visible. The final three
                // fixtures prove dead, portal-tell, and Warden exclusions from rush bolts.
                enemies[n].dir = new V(1, 0);
                enemies[n].pos = new V(-6f + (n % 4) * 4f + time * .25f, n < 4 ? 1.5f : -2f);
                enemies[n].charge = n < 2 && tick >= 90 && tick < 135 ? 1 : 0;
                enemies[n].tell = n < 2 && tick >= 135 && tick < 165 ? 1 : 0;
                enemies[n].spawnTell = n == 6 && tick < 180 ? 1 : 0;
                enemies[n].dissolve = tick >= 195 ? Mathf.Max(.001f, (240 - tick) / 30f) : 0;
                arena.Game.Enemies.Add(enemies[n]);
            }
            arena.RenderEnemyPreview();
            yield return new WaitForEndOfFrame();
            // Four simulation ticks are approximately 0.133 seconds at the 30 Hz preview rate;
            // the old 15-tick cadence could alias the walking cycle.
            if (tick % 4 == 0)
            {
                var capture = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(directory, "frame-" + captures.ToString("D2") + ".png"), capture.EncodeToPNG());
                Destroy(capture); captures++;
                if (tick < 180) rushCaptures++; else expiredRushCaptures++;
            }
        }
        File.WriteAllText(Path.Combine(directory, "result.json"), "{\"frames\":" + captures +
            ",\"source\":\"Unity development player render\",\"isolated_fixture\":true,\"rush_visual\":true" +
            ",\"rush_frames\":" + rushCaptures + ",\"rush_expired_frames\":" + expiredRushCaptures +
            ",\"dead_fixture_id\":1006,\"spawn_tell_fixture_id\":1007,\"warden_fixture_id\":1008}");
        Debug.Log("FOXFIRE_ENEMY_PREVIEW_SUCCESS " + directory);
        Application.Quit();
    }
}
}
