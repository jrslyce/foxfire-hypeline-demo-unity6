using System;
using System.Collections.Generic;
using UnityEngine;

namespace Foxfire
{
// Shared baked frames use the existing sprite pool and depth sorting. No per-enemy meshes.
public sealed class EnemyModelVisuals : IDisposable
{
    public const int Directions = 16, Frames = 16, Cell = 256;
    public const float FloatAmplitude = .12f, FloatAngularSpeed = 2.2f;
    public static readonly string[] Names = { "AmethystBoar", "Emberboar", "Shroomling" };
    [Serializable] public sealed class BakeInfo { public float pivotY, duration; }
    sealed class Pose { public V position; public double clock, cycle; public float angle; }
    readonly Sprite[][] sprites = new Sprite[3][];
    readonly float[] durations = new float[3];
    // Reference identity prevents recycled simulation ids inheriting old poses.
    readonly Dictionary<Enemy, Pose> poses = new Dictionary<Enemy, Pose>();
    readonly HashSet<Enemy> seen = new HashSet<Enemy>();
    readonly List<Enemy> stale = new List<Enemy>();
    public bool Available => sprites[0] != null && sprites[1] != null && sprites[2] != null;

    public EnemyModelVisuals()
    {
        for (int n = 0; n < Names.Length; n++)
        {
            var atlas = Resources.Load<Texture2D>("EnemyFrames/" + Names[n]);
            var metadata = Resources.Load<TextAsset>("EnemyFrames/" + Names[n]);
            if (atlas == null || metadata == null)
            { Debug.LogWarning("Enemy atlas missing; using fallback: " + Names[n]); continue; }
            int count = n == 2 ? 1 : Frames;
            if (atlas.width != count * Cell || atlas.height != Directions * Cell)
                throw new InvalidOperationException("Invalid enemy atlas dimensions: " + Names[n]);
            var info = JsonUtility.FromJson<BakeInfo>(metadata.text);
            if (info == null || info.duration <= 0 || float.IsNaN(info.duration) ||
                info.pivotY <= 0 || info.pivotY >= 1)
                throw new InvalidOperationException("Invalid enemy bake metadata: " + Names[n]);
            durations[n] = info.duration;
            sprites[n] = new Sprite[Directions * count];
            for (int d = 0; d < Directions; d++) for (int f = 0; f < count; f++)
                sprites[n][d * count + f] = Sprite.Create(atlas,
                    new Rect(f * Cell, (Directions - 1 - d) * Cell, Cell, Cell),
                    new Vector2(.5f, info.pivotY), Cell / 3.6f, 0, SpriteMeshType.FullRect);
        }
        if (Available) Debug.Log("FOXFIRE_ENEMY_FRAMES_READY models=3 directions=16 boarFrames=16 floatAmplitude=0.12");
    }

    public Sprite Sample(Enemy enemy, double clock, V target, out V position)
    {
        return Sample(enemy, clock, target, false, out position);
    }

    // ReverseFacing is presentation-only. It mirrors the sampled travel direction while the
    // simulation keeps its original heading for movement, targeting, and collision checks.
    public Sprite Sample(Enemy enemy, double clock, V target, bool reverseFacing, out V position)
    {
        position = enemy.pos + new V(0, -.35f);
        int n = enemy.kind == EnemyKind.Boar ? (enemy.champion ? 1 : 0) :
            enemy.kind == EnemyKind.Sprig || enemy.kind == EnemyKind.Wisp ? 2 : -1;
        if (n < 0 || sprites[n] == null) return null;
        if (!poses.TryGetValue(enemy, out var pose))
        {
            V initial = target - enemy.pos;
            pose = new Pose { position = enemy.pos, clock = clock,
                angle = Mathf.Atan2(initial.y, initial.x) * Mathf.Rad2Deg, cycle = (enemy.id * .137) % 1 };
            poses.Add(enemy, pose);
        }
        double elapsed = Math.Max(0, clock - pose.clock);
        V delta = enemy.pos - pose.position;
        bool moving = delta.x * delta.x + delta.y * delta.y > .00000001f;
        if (elapsed > 0 && enemy.dissolve <= 0)
        {
            V heading = enemy.tell > 0 || enemy.charge > 0 ? enemy.dir : delta;
            if (reverseFacing)
                heading = heading * -1;
            if (moving || enemy.tell > 0)
                pose.angle = Mathf.MoveTowardsAngle(pose.angle, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg, (float)elapsed * 360);
            if (moving && enemy.tell <= 0)
                pose.cycle = (pose.cycle + elapsed * (enemy.charge > 0 ? 1.8 : 1) / durations[n]) % 1;
        }
        pose.clock = clock; pose.position = enemy.pos;
        int direction = Mathf.RoundToInt(Mathf.Repeat(pose.angle, 360) / (360f / Directions)) % Directions;
        bool floating = n == 2;
        int count = floating ? 1 : Frames;
        if (floating) position.y += .25f + FloatOffset(clock, enemy.id);
        return sprites[n][direction * count + Math.Min(count - 1, (int)(pose.cycle * count))];
    }

    public static float FloatOffset(double clock, int id) => Mathf.Sin((float)(clock * FloatAngularSpeed + id * .73)) * FloatAmplitude;
    public void EndFrame(IReadOnlyList<Enemy> enemies)
    {
        seen.Clear(); foreach (var enemy in enemies) seen.Add(enemy);
        stale.Clear(); foreach (var pair in poses) if (!seen.Contains(pair.Key)) stale.Add(pair.Key);
        foreach (var enemy in stale) poses.Remove(enemy);
    }
    public void Dispose()
    {
        foreach (var model in sprites) if (model != null) foreach (var sprite in model)
            if (Application.isPlaying) UnityEngine.Object.Destroy(sprite); else UnityEngine.Object.DestroyImmediate(sprite);
        poses.Clear(); seen.Clear(); stale.Clear();
    }
}
}
