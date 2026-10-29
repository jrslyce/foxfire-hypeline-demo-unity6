using System;
using UnityEngine;

namespace Foxfire
{
    // Meshy walking cycle baked at 32 angles. Running changes playback speed only.
    public sealed class FoxHeroFrames : IDisposable
    {
        public const int FramesPerDirection = 16;
        public const int DirectionCount = FoxFacing.DirectionCount;
        public const int CellSize = 256;
        public const float RunPlaybackSpeed = 1.7f;
        readonly Sprite[] frames;
        readonly FoxFacing facing = new FoxFacing();
        double cycle;
        public bool Available => frames != null;

        public FoxHeroFrames()
        {
            var atlas = Resources.Load<Texture2D>("FoxHero/FoxWalk");
            if (atlas == null) { Debug.LogWarning("Fox hero atlas missing; using fallback art."); return; }
            if (atlas.width != FramesPerDirection * CellSize || atlas.height != DirectionCount * CellSize)
                throw new InvalidOperationException("Fox hero atlas must be 4096x8192; check import max size.");
            frames = new Sprite[FramesPerDirection * DirectionCount];
            for (int d = 0; d < DirectionCount; d++)
                for (int f = 0; f < FramesPerDirection; f++)
                {
                    var sprite = Sprite.Create(atlas, new Rect(f * CellSize, (DirectionCount - 1 - d) * CellSize, CellSize, CellSize),
                        new Vector2(.5f, .24336293f), 128, 0, SpriteMeshType.FullRect);
                    sprite.name = "Fox_" + d + "_" + f;
                    frames[d * FramesPerDirection + f] = sprite;
                }
            Debug.Log("FOXFIRE_MESHY_FOX_READY directions=32 frames=16 runSpeed=1.7");
        }

        public Sprite Sample(double elapsed, V heading, bool moving)
        {
            if (!Available) return null;
            if (moving)
            {
                cycle = (cycle + Math.Max(0, elapsed) * RunPlaybackSpeed) % 1;
            }
            // Hold the sampled pose between simulation ticks and when movement stops.
            int frame = (int)(cycle * FramesPerDirection);
            int direction = facing.Sample(elapsed, heading.x, heading.y, moving);
            return frames[direction * FramesPerDirection + frame];
        }

        public static int DirectionIndex(float x, float y) =>
            FoxFacing.DirectionIndex(Math.Atan2(y, x));

        public void Dispose()
        {
            if (frames == null) return;
            foreach (var sprite in frames)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(sprite);
                else UnityEngine.Object.DestroyImmediate(sprite);
            }
            // The atlas is a shared Resources asset and is not owned by these runtime sprites.
        }
    }
}
