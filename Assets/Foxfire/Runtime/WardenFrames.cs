using System;
using UnityEngine;
namespace Foxfire
{
public sealed class WardenFrames : IDisposable
{
    public const int Cell = 512, Count = 32, Columns = 8;
    const int LocomotionCell = 256, LocomotionCount = 16, LocomotionDirections = 8;
    readonly Sprite[][] frames = new Sprite[6][];
    static readonly string[] Names = { "Walking", "Running", "360_Power_Spin_Jump", "Charged_Ground_Slam", "Charged_Slash", "Dead" };
    public bool Available { get; private set; }
    public WardenFrames()
    {
        for (int c = 0; c < Names.Length; c++)
        {
            var atlas = Resources.Load<Texture2D>("HollowrootWarden/" + Names[c]);
            if (atlas == null) { Debug.LogWarning("Hollowroot Warden atlas missing: " + Names[c]); return; }
            if (atlas.width != 4096 || atlas.height != 2048) throw new InvalidOperationException("Invalid Warden atlas size: " + Names[c]);
            bool locomotion = c < 2;
            int cell = locomotion ? LocomotionCell : Cell;
            int count = locomotion ? LocomotionCount : Count;
            int directions = locomotion ? LocomotionDirections : 1;
            frames[c] = new Sprite[directions * count];
            for (int d = 0; d < directions; d++) for (int f = 0; f < count; f++)
            {
                int row = locomotion ? d : f / Columns;
                int column = locomotion ? f : f % Columns;
                frames[c][d * count + f] = Sprite.Create(atlas,
                    new Rect(column * cell, (locomotion ? (LocomotionDirections - 1 - row) : (3 - row)) * cell, cell, cell),
                    new Vector2(.5f, .3285014f), cell / 4f, 0, SpriteMeshType.FullRect);
                frames[c][d * count + f].name = Names[c] + "_" + d + "_" + f;
            }
        }
        Available = true;
        Debug.Log("FOXFIRE_WARDEN_READY clips=6 frames=384 locomotionDirections=8");
    }
    public Sprite Sample(WardenClip clip, float seconds)
    {
        return Sample(clip, seconds, new V(1, 0));
    }

    public Sprite Sample(WardenClip clip, float seconds, V direction)
    {
        if (!Available) return null;
        if (clip == WardenClip.Idle) return frames[0][0];
        bool locomotion = clip == WardenClip.Walking || clip == WardenClip.Running;
        bool loop = locomotion;
        float phase = Mathf.Max(0, seconds) / WardenAnimation.Duration(clip);
        int count = locomotion ? LocomotionCount : Count;
        int frame = loop ? Mathf.FloorToInt((phase % 1) * count) : Mathf.Min(count - 1, Mathf.FloorToInt(phase * (count - 1)));
        int directionIndex = locomotion ? WardenFacing.DirectionIndex(direction) : 0;
        return frames[(int)clip - 1][directionIndex * count + frame];
    }
    public void Dispose()
    {
        foreach (var clip in frames) if (clip != null) foreach (var sprite in clip)
            if (Application.isPlaying) UnityEngine.Object.Destroy(sprite); else UnityEngine.Object.DestroyImmediate(sprite);
    }
}
}
