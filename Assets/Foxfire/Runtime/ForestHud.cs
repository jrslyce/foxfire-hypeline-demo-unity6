using System;
using UnityEngine;

namespace Foxfire
{
// Presentation only: values, visibility and announcement lifetimes come from Simulation.
// The blank atlas is shared by all panels; labels and bars are always drawn live.
internal sealed partial class ForestHud : IDisposable
{
    readonly Texture2D atlas;
    readonly Texture2D heart, sparkle;
    readonly GUIStyle heading, stat, bossLabel, kicker, viewerName, body;
    readonly GUIContent content = new GUIContent();
    static readonly Color Ivory = PlaceholderArt.Hex("f4e8d0");
    static readonly Color Teal = PlaceholderArt.Hex("00c6b6");
    static readonly Color Plum = PlaceholderArt.Hex("a86fa5");
    // Atlas pixel rectangles use top-left coordinates, converted only at draw time.
    static readonly Rect PanelSource = new Rect(26, 125, 1484, 273);
    static readonly Rect FoxSource = new Rect(570, 535, 394, 394);

    public ForestHud(Font font)
    {
        atlas = Resources.Load<Texture2D>("ForestHud/forest-hud");
        if (atlas == null) Debug.LogError("Foxfire HUD atlas missing: ForestHud/forest-hud");
        heading = Style(font, 22, Ivory, FontStyle.Bold);
        stat = Style(font, 18, Ivory);
        bossLabel = Style(font, 13, Ivory, FontStyle.Bold, TextAnchor.MiddleCenter);
        kicker = Style(font, 10, Teal, FontStyle.Bold);
        viewerName = Style(font, 23, Color.white, FontStyle.Bold);
        body = Style(font, 16, Ivory);
        body.wordWrap = true;
        CreateScreenStyles(font);
        heart = Icon(true);
        sparkle = Icon(false);
    }

    static GUIStyle Style(Font font, int size, Color color, FontStyle weight = FontStyle.Normal,
                          TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        return new GUIStyle { font = font, fontSize = size, fontStyle = weight, alignment = alignment,
            richText = false, clipping = TextClipping.Clip, normal = { textColor = color } };
    }

    public void Draw(Simulation game, string level, string hp, bool simulation)
    {
        Color previous = GUI.color;
        GUI.color = Color.white;
        Panel(new Rect(421, 20, 438, 94));
        if (atlas != null) Region(new Rect(382, 26, 80, 80), FoxSource);
        Label(new Rect(475, 29, 354, 29), "FOXFIRE CLEARING", heading);
        Label(new Rect(475, 58, 116, 26), level, stat);
        GUI.color = Teal;
        GUI.DrawTexture(new Rect(695, 64, 17, 16), heart);
        GUI.color = Color.white;
        stat.alignment = TextAnchor.MiddleRight;
        Fit(new Rect(718, 58, 109, 26), hp, stat, 18, 12);
        stat.alignment = TextAnchor.MiddleLeft;
        Meter(new Rect(475, 88, 352, 12), game.MaxHP > 0 ? game.HP / game.MaxHP : 0, Teal);

        var boss = game.Boss;
        if (boss != null && boss.hp > 0)
        {
            Panel(new Rect(444, 122, 392, 49), new Color(.86f, .77f, .91f, 1));
            Label(new Rect(475, 127, 330, 20), "HOLLOWROOT WARDEN", bossLabel);
            Meter(new Rect(475, 149, 330, 10), boss.maxHP > 0 ? boss.hp / boss.maxHP : 0, Plum);
        }

        for (int i = 0; i < game.ViewerAnnouncements.Count && i < 3; i++)
        {
            var announcement = game.ViewerAnnouncements[i];
            float age = (float)(game.Clock - announcement.createdAt);
            float slide = 1 - Mathf.Clamp01(age / .35f);
            // Preserve main's compact 210 x 53 card footprint and right/top anchor.
            Matrix4x4 hudMatrix = GUI.matrix;
            GUI.matrix = hudMatrix * Matrix4x4.TRS(new Vector3(1054, 116 + i * 58, 0),
                Quaternion.identity, new Vector3(.5f, .5f, 1));
            float x = 460 * slide * slide * slide;
            float y = 0;
            Panel(new Rect(x, y, 420, 106));
            Color accent = announcement.IsJoin ? Ivory : Teal;
            GUI.color = accent;
            GUI.DrawTexture(new Rect(x + 21, y + 34, 33, 33), sparkle);
            GUI.color = Color.white;
            kicker.normal.textColor = accent;
            kicker.fontSize = 11;
            Label(new Rect(x + 65, y + 12, 327, 16), announcement.IsJoin ? "VIEWER" :
                simulation ? "SIMULATED VIEWER ACTION" : "LIVE VIEWER ACTION", kicker);
            Fit(new Rect(x + 65, y + 29, 327, 36), announcement.viewerName, viewerName, 30, 13);
            body.fontSize = 20;
            Label(new Rect(x + 65, y + 70, 327, 28), announcement.IsJoin ? "has entered the game!" :
                "activated " + announcement.actionName, body);
            GUI.matrix = hudMatrix;
        }
        GUI.color = previous;
    }

    void Label(Rect rect, string text, GUIStyle style)
    {
        content.text = text;
        GUI.Label(rect, content, style);
    }

    void Fit(Rect rect, string text, GUIStyle style, int size, int minimum)
    {
        content.text = text;
        style.fontSize = size;
        while (style.fontSize > minimum && style.CalcSize(content).x > rect.width) style.fontSize--;
        GUI.Label(rect, content, style);
    }

    void Panel(Rect rect) { Panel(rect, Color.white); }
    void Panel(Rect rect, Color tint)
    {
        if (atlas == null)
        {
            GUI.color = new Color(.02f, .07f, .065f, .97f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            return;
        }
        GUI.color = tint;
        // Slice ornament separately so short boss panels and taller cards keep their corners.
        const float sourceEdge = 120;
        float edge = Mathf.Min(28, rect.height / 2);
        for (int row = 0; row < 3; row++)
        for (int col = 0; col < 3; col++)
        {
            float sx = col == 0 ? 0 : col == 1 ? sourceEdge : PanelSource.width - sourceEdge;
            float sy = row == 0 ? 0 : row == 1 ? sourceEdge : PanelSource.height - sourceEdge;
            float sw = col == 1 ? PanelSource.width - sourceEdge * 2 : sourceEdge;
            float sh = row == 1 ? PanelSource.height - sourceEdge * 2 : sourceEdge;
            float dx = col == 0 ? 0 : col == 1 ? edge : rect.width - edge;
            float dy = row == 0 ? 0 : row == 1 ? edge : rect.height - edge;
            float dw = col == 1 ? rect.width - edge * 2 : edge;
            float dh = row == 1 ? rect.height - edge * 2 : edge;
            Region(new Rect(rect.x + dx, rect.y + dy, dw, dh),
                new Rect(PanelSource.x + sx, PanelSource.y + sy, sw, sh));
        }
        GUI.color = Color.white;
    }

    void Region(Rect destination, Rect source)
    {
        GUI.DrawTextureWithTexCoords(destination, atlas, new Rect(source.x / atlas.width,
            1 - source.yMax / atlas.height, source.width / atlas.width, source.height / atlas.height));
    }

    static void Rounded(Rect rect, Color color, float radius)
    {
        if (rect.width <= 0 || rect.height <= 0) return;
        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, radius);
    }

    static void Meter(Rect rect, float ratio, Color fill)
    {
        Rounded(rect, new Color(.005f, .012f, .012f, 1), 4);
        var inside = new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4);
        Rounded(inside, new Color(.055f, .09f, .085f, 1), 2);
        inside.width *= Mathf.Clamp01(float.IsNaN(ratio) ? 0 : ratio);
        Rounded(inside, fill, 2);
        inside.height = 2;
        Rounded(inside, new Color(1, 1, 1, .17f), 1);
    }

    static Texture2D Icon(bool isHeart)
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
            name = isHeart ? "HUD heart" : "HUD sparkle", filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int covered = 0;
            for (int sy = 0; sy < 4; sy++)
            for (int sx = 0; sx < 4; sx++)
            {
                float px = ((x + (sx + .5f) / 4) / size * 2 - 1) * 1.2f;
                float py = ((y + (sy + .5f) / 4) / size * 2 - 1) * 1.2f;
                float v = px * px + py * py - 1;
                bool inside = isHeart ? v * v * v - px * px * py * py * py <= 0 :
                    Mathf.Sqrt(Mathf.Abs(px)) + Mathf.Sqrt(Mathf.Abs(py)) < 1;
                if (inside) covered++;
            }
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(covered * 255 / 16));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    public void Dispose()
    {
        UnityEngine.Object.Destroy(heart);
        UnityEngine.Object.Destroy(sparkle);
    }
}
}
