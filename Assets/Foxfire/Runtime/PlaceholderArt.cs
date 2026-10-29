using System.Collections.Generic;
using UnityEngine;
namespace Foxfire
{
// Original, replaceable geometric art. One shared texture per silhouette; sprites are pooled by the view.
public static class PlaceholderArt
{
    static Color32 Clear = new Color32(0, 0, 0, 0);
    static Color Orange = Hex("ee8844"), Cream = Hex("ffe7be"), Teal = Hex("00bebe"), Plum = Hex("8d3e91"),
                 Dark = Hex("172c2c");
    public static Color Hex(string s)
    {
        Color c;
        ColorUtility.TryParseHtmlString("#" + s, out c);
        return c;
    }
    public static Sprite Make(string kind)
    {
        const int n = 128;
        var pixels = new Color32[n * n];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Clear;
        System.Action<float, float, float, float, Color> ellipse = (cx, cy, rx, ry, c) =>
        {
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    if (Mathf.Pow((x - cx) / rx, 2) + Mathf.Pow((y - cy) / ry, 2) <= 1)
                        pixels[y * n + x] = c;
        };
        System.Action<Vector2[], Color> poly = (points, c) =>
        {
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool inside = false;
                    for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                    {
                        var a = points[i];
                        var b = points[j];
                        if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)
                            inside = !inside;
                    }
                    if (inside)
                        pixels[y * n + x] = c;
                }
        };
        if (kind == "fox")
        {
            ellipse(81, 58, 36, 23, Hex("b95d32"));
            poly(P(69, 42, 108, 50, 124, 89, 108, 109, 88, 96, 85, 74), Orange);
            poly(P(100, 74, 124, 89, 108, 109, 88, 96), Cream);
            ellipse(48, 45, 23, 27, Orange);
            ellipse(42, 33, 16, 15, Cream);
            ellipse(32, 17, 10, 8, Dark);
            ellipse(60, 17, 10, 8, Dark);
            poly(P(20, 66, 17, 113, 42, 92, 59, 98, 82, 112, 77, 66, 51, 45), Orange);
            poly(P(23, 80, 23, 103, 39, 90), Hex("5b3036"));
            poly(P(62, 91, 77, 103, 74, 79), Hex("5b3036"));
            poly(P(20, 69, 43, 65, 51, 49, 35, 54), Cream);
            poly(P(77, 69, 58, 65, 51, 49, 66, 54), Cream);
            ellipse(35, 73, 5, 6, Dark);
            ellipse(65, 73, 5, 6, Dark);
            ellipse(36, 75, 1.6f, 2, Cream);
            ellipse(66, 75, 1.6f, 2, Cream);
            poly(P(46, 56, 56, 56, 51, 51), Dark);
            poly(P(50, 93, 45, 83, 50, 77, 56, 83), Teal);
        }
        else if (kind == "sprig")
        {
            poly(P(29, 44, 11, 77, 37, 70, 48, 112, 64, 86, 89, 111, 91, 75, 116, 65, 97, 47, 87, 23, 42, 22),
                 Hex("4f325c"));
            ellipse(64, 54, 36, 30, Plum);
            poly(P(37, 27, 25, 12, 49, 22), Dark);
            poly(P(78, 24, 106, 13, 91, 34), Dark);
            ellipse(49, 60, 7, 8, Cream);
            ellipse(82, 60, 7, 8, Cream);
            ellipse(51, 59, 3, 5, Dark);
            ellipse(80, 59, 3, 5, Dark);
            poly(P(62, 51, 70, 51, 66, 44), Teal);
            poly(P(45, 90, 54, 116, 68, 102, 61, 87), Hex("7c6681"));
        }
        else if (kind == "boar" || kind == "orange_boar")
        {
            bool champion = kind == "orange_boar";
            ellipse(68, 60, 48, 32, champion ? Hex("da651e") : Hex("493451"));
            poly(P(34, 74, 44, 108, 58, 85, 70, 116, 81, 83, 99, 106, 104, 73), champion ? Hex("ffba55") : Plum);
            ellipse(38, 43, 28, 26, champion ? Hex("ff963f") : Hex("795378"));
            ellipse(22, 40, 14, 12, champion ? Hex("ffc17d") : Hex("a27c99"));
            ellipse(36, 59, 5, 5, Cream);
            poly(P(36, 24, 15, 27, 24, 48), Cream);
            poly(P(48, 24, 68, 23, 59, 44), Cream);
            ellipse(60, 22, 9, 9, Dark);
            ellipse(96, 27, 9, 9, Dark);
        }
        else if (kind == "wisp")
        {
            ellipse(64, 62, 40, 43, new Color(.55f, .24f, .57f, .22f));
            poly(P(35, 38, 23, 63, 35, 88, 59, 103, 89, 93, 107, 61, 89, 32, 77, 17, 61, 36, 47, 18), Plum);
            ellipse(64, 60, 24, 22, Hex("cb8dc5"));
            ellipse(53, 62, 4, 6, Dark);
            ellipse(76, 62, 4, 6, Dark);
        }
        else if (kind == "boss")
        {
            poly(P(13, 14, 34, 55, 21, 88, 7, 102, 28, 98, 37, 120, 45, 90, 57, 78, 65, 112, 80, 97, 91, 121,
                   93, 87, 120, 108, 113, 80, 95, 62, 119, 13, 84, 23, 60, 13, 43, 22),
                 Hex("493f45"));
            ellipse(66, 55, 31, 40, Hex("715a52"));
            poly(P(49, 78, 61, 93, 78, 78, 88, 59, 72, 30, 55, 36, 44, 59), Dark);
            poly(P(60, 78, 77, 60, 66, 39, 53, 60), Teal);
            ellipse(48, 80, 6, 4, Cream);
            ellipse(83, 80, 6, 4, Cream);
            poly(P(15, 17, 38, 40, 47, 26), Hex("9a7361"));
        }
        else if (kind == "tree")
        {
            poly(P(52, 0, 60, 70, 74, 70, 82, 0), Hex("283b3a"));
            ellipse(64, 74, 56, 44, Hex("1e3c39"));
            ellipse(49, 93, 35, 28, Hex("284a43"));
            ellipse(31, 63, 28, 26, Hex("23433c"));
            ellipse(91, 67, 29, 29, Hex("183633"));
        }
        else if (kind == "rock")
        {
            poly(P(16, 36, 34, 14, 76, 10, 108, 37, 92, 91, 56, 112, 22, 91), Hex("385253"));
            poly(P(16, 36, 34, 14, 76, 10, 64, 48), Hex("273d40"));
            poly(P(22, 91, 56, 112, 92, 91, 64, 62), Hex("52706b"));
            poly(P(64, 62, 92, 91, 108, 37, 76, 10), Hex("3f5f5a"));
        }
        else if (kind == "log")
        {
            // Snapped limbs and uneven ends read as a fallen tree at broadcast scale.
            poly(P(42, 62, 48, 102, 39, 114, 53, 107, 58, 67), Hex("5b453e"));
            poly(P(72, 48, 80, 15, 100, 8, 85, 22, 86, 48), Hex("5b453e"));
            poly(P(12, 35, 24, 20, 105, 34, 116, 54, 102, 82, 21, 69, 29, 56, 10, 51, 24, 42), Hex("5b453e"));
            poly(P(24, 20, 105, 34, 94, 53, 20, 42), Hex("81604d"));
            poly(P(26, 58, 52, 65, 81, 60, 95, 67, 80, 74, 50, 76, 29, 68), Hex("35574a"));
            poly(P(32, 39, 80, 45, 86, 49, 40, 45), Hex("423833"));
            ellipse(105, 56, 13, 22, Hex("b18768"));
            ellipse(105, 56, 7, 13, Hex("5b453e"));
        }
        else if (kind == "eyes")
        {
            ellipse(39, 64, 15, 19, Hex("ffe7be"));
            ellipse(89, 64, 15, 19, Hex("ffe7be"));
            ellipse(39, 65, 6, 9, Hex("00bebe"));
            ellipse(89, 65, 6, 9, Hex("00bebe"));
        }
        else if (kind == "shard")
        {
            poly(P(64, 112, 95, 65, 64, 17, 33, 65), Teal);
            poly(P(64, 112, 64, 17, 33, 65), Hex("77d8c6"));
        }
        else if (kind == "pod")
        {
            ellipse(64, 60, 40, 42, Teal);
            ellipse(64, 62, 31, 33, Hex("1a5552"));
            poly(P(56, 87, 73, 87, 73, 70, 90, 70, 90, 54, 73, 54, 73, 37, 56, 37, 56, 54, 39, 54, 39, 70, 56,
                   70),
                 Cream);
        }
        else if (kind == "ring")
        {
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(64, 64));
                    if (d < 62 && d > 57)
                        pixels[y * n + x] = Color.white;
                }
        }
        else if (kind == "diamond")
        {
            poly(P(64, 126, 126, 64, 64, 2, 2, 64), Color.white);
        }
        else if (kind == "square")
        {
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
        }
        else
            ellipse(64, 64, 62, 62, Color.white);
        var texture = new Texture2D(n, n, TextureFormat.RGBA32, false);
        texture.name = kind + "_placeholder";
        texture.SetPixels32(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 64);
    }
    static Vector2[] P(params float[] a)
    {
        var result = new Vector2[a.Length / 2];
        for (int i = 0; i < result.Length; i++)
            result[i] = new Vector2(a[i * 2], a[i * 2 + 1]);
        return result;
    }
}
}
