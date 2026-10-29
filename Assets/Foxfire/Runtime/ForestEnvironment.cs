using System;
using System.Collections.Generic;
using UnityEngine;

namespace Foxfire
{
// Retained, non-colliding scenery kept below actor sorting orders. Its private clock is
// deliberately independent of the deterministic simulation and viewer action RNG.
public sealed class ForestEnvironment
{
    readonly List<SpriteRenderer> retained = new List<SpriteRenderer>();
    readonly List<Vector2> eyeAnchors = new List<Vector2>();
    readonly System.Random layoutRandom = new System.Random(4711);
    readonly System.Random random = new System.Random();
    SpriteRenderer eyeRenderer;
    float eyeTimer;
    float eyeNextGap;
    float eyeVisibleFor;
    float eyeOpacity;
    bool eyesVisible;

    ForestEnvironment(Func<string, Sprite> sprite, Transform parent)
    {
        Build(sprite, parent);
        ScheduleGap();
    }

    public static ForestEnvironment Create(Func<string, Sprite> sprite, Transform parent)
    {
        return new ForestEnvironment(sprite, parent);
    }

    SpriteRenderer Add(Func<string, Sprite> sprite, string key, Vector2 position, Vector2 scale,
                       Color color, int order, float rotation, Transform parent)
    {
        var go = new GameObject("Forest · " + key);
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.transform.rotation = Quaternion.Euler(0, 0, rotation);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite(key);
        renderer.color = color;
        renderer.sortingOrder = order;
        retained.Add(renderer);
        return renderer;
    }

    float Next(float min, float max)
    {
        return min + (float)layoutRandom.NextDouble() * (max - min);
    }

    float NextEye(float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    void Build(Func<string, Sprite> sprite, Transform parent)
    {
        // Several staggered bands make the clearing smaller while leaving a broad, readable
        // stage. The innermost band is still entirely below actors and effects.
        for (int band = 0; band < 4; band++)
        {
            int count = 46 - band * 4;
            for (int i = 0; i < count; i++)
            {
                bool horizontal = (i + band) % 2 == 0;
                float t = Next(-15.5f, 15.5f);
                float edge = 8.5f - band * .9f;
                float side = 15.5f - band * 1.3f;
                Vector2 p = horizontal
                    ? new Vector2(t, (i + band) % 4 < 2 ? Next(edge - .35f, edge + .35f) : Next(-edge - .35f, -edge + .35f))
                    : new Vector2((i + band) % 4 < 2 ? Next(-side - .35f, -side + .35f) : Next(side - .35f, side + .35f), Next(-8.3f, 8.3f));
                float size = Next(1.25f, 1.8f) * (1f - band * .07f);
                Add(sprite, "tree", p, new Vector2(size * Next(.86f, 1.1f), size),
                    band == 0 ? new Color(.88f, .96f, .92f) : band == 1 ? new Color(.82f, .94f, .88f) : new Color(.76f, .9f, .84f), -850 + band * 8,
                    Next(-5, 5), parent);
                Vector2 anchor = p + new Vector2(0, Next(.2f, .65f));
                // Stay inside the broadcast camera and away from its compact top-center HUD.
                if (Mathf.Abs(anchor.x) <= 15.2f && Mathf.Abs(anchor.y) <= 8.3f &&
                    !(anchor.y > 6.3f && Mathf.Abs(anchor.x) < 4.5f))
                    eyeAnchors.Add(anchor);
            }
        }

        // Polygonal debris is sparse and hugs the same perimeter, keeping the play space clear.
        for (int i = 0; i < 16; i++)
        {
            bool horizontal = i % 2 == 0;
            float t = Next(-10.2f, 10.2f);
            Vector2 p = horizontal
                ? new Vector2(t, i % 4 < 2 ? Next(6.1f, 6.8f) : Next(-6.8f, -6.1f))
                : new Vector2(i % 4 < 2 ? Next(-10.5f, -9.8f) : Next(9.8f, 10.5f), t * .62f);
            if (i % 3 == 0)
                Add(sprite, "log", p, new Vector2(Next(.95f, 1.45f), Next(.55f, .8f)),
                    new Color(.92f, .86f, .78f), -730, Next(-35, 35), parent);
            else
                Add(sprite, "rock", p, new Vector2(Next(.4f, .75f), Next(.35f, .65f)),
                    i % 2 == 0 ? new Color(.9f, .98f, .94f) : new Color(.78f, .9f, .87f), -735, Next(-12, 12), parent);
        }

        eyeRenderer = Add(sprite, "eyes", new Vector2(0, 0), new Vector2(.25f, .25f),
            Color.white, -600, 0, parent);
        eyeRenderer.enabled = false;
    }

    void ScheduleGap()
    {
        eyeTimer = 0;
        eyeNextGap = NextEye(18f, 40f);
        eyeVisibleFor = NextEye(2f, 4f);
        eyesVisible = false;
        eyeRenderer.enabled = false;
    }

    public void Tick(float deltaTime)
    {
        eyeTimer += Mathf.Max(0, deltaTime);
        if (!eyesVisible && eyeTimer >= eyeNextGap)
        {
            eyesVisible = true;
            eyeTimer = 0;
            Vector2 anchor = eyeAnchors[random.Next(eyeAnchors.Count)];
            eyeRenderer.transform.position = new Vector3(anchor.x, anchor.y, 0);
            eyeRenderer.transform.localScale = new Vector3(NextEye(.33f, .4f), NextEye(.33f, .4f), 1);
            eyeOpacity = NextEye(.9f, 1f);
            eyeRenderer.enabled = true;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log("FOXFIRE_FOREST_EYES time=" + Time.unscaledTime.ToString("F2") +
                      " duration=" + eyeVisibleFor.ToString("F2") + " position=" + anchor);
#endif
        }
        else if (eyesVisible && eyeTimer >= eyeVisibleFor)
            ScheduleGap();
        if (eyesVisible)
        {
            float fade = Mathf.Min(Mathf.Clamp01(eyeTimer / .35f),
                                   Mathf.Clamp01((eyeVisibleFor - eyeTimer) / .45f));
            eyeRenderer.color = new Color(1f, .91f, .62f, eyeOpacity * fade);
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < retained.Count; i++)
            if (retained[i] != null)
                UnityEngine.Object.Destroy(retained[i].gameObject);
        retained.Clear();
    }
}
}
