#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Foxfire.Editor
{
public static class WardenBake
{
    const string Source = "Assets/Foxfire/Art/HollowrootWarden/";
    const string Model = Source + "Meshy_AI_Forest_Guardian_biped_Meshy_AI_Meshy_Merged_Animations.fbx";
    static readonly string[] Clips = { "Walking", "Running", "360_Power_Spin_Jump", "Charged_Ground_Slam", "Charged_Slash", "Dead" };
    const int Cell = 512, Frames = 32, Columns = 8;
    const int LocomotionCell = 256, LocomotionFrames = 16, LocomotionDirections = 8;
    [Serializable] sealed class Baseline { public PoseHash[] clips; }
    [Serializable] sealed class PoseHash { public string name, sha256; }

    public static void Validate()
    {
        // Root translation/rotation alone passed silhouette and unique-frame checks.
        // Pin the visually reviewed skeletal poses. Rebakes require explicit visual
        // review and baseline approval; Bake intentionally never updates this file.
        var baseline = JsonUtility.FromJson<Baseline>(File.ReadAllText("Assets/Foxfire/Editor/WardenAtlasBaseline.json"));
        if (baseline == null || baseline.clips == null || baseline.clips.Length != Clips.Length)
            throw new Exception("Warden reviewed-pose baseline is incomplete.");
        using (var sha = System.Security.Cryptography.SHA256.Create())
            foreach (var name in Clips)
            {
                var entry = baseline.clips.Single(c => c.name == name);
                var bytes = File.ReadAllBytes("Assets/Foxfire/Resources/HollowrootWarden/" + name + ".png");
                var hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                if (hash != entry.sha256)
                    throw new Exception("Unreviewed Warden poses: " + name + ". Review skeletal animation and native playback before approving a new atlas baseline.");
            }
        Debug.Log("FOXFIRE_WARDEN_POSE_BASELINE_SUCCESS clips=6");
    }
    [MenuItem("Foxfire/Bake Hollowroot Warden")]
    public static void Bake() { BakeClips(false); }

    [MenuItem("Foxfire/Bake Warden locomotion directions")]
    public static void BakeLocomotion() { BakeClips(true); }

    static void BakeClips(bool locomotionOnly)
    {
        Inspect();
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
        var facingRoot = new GameObject("Warden bake facing");
        var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
        instance.transform.SetParent(facingRoot.transform, false);
        var clips = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        var material = new Material(Shader.Find("Standard"));
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "Meshy_AI_Forest_Guardian_biped_texture_0.png");
        material.SetFloat("_Glossiness", .15f);
        foreach (var r in instance.GetComponentsInChildren<Renderer>()) r.sharedMaterial = material;
        // All samples render inside one editor update. Without this Unity reuses
        // cached skin matrices: root motion changes, but the limbs stay frozen.
        foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            skin.updateWhenOffscreen = true;
            skin.forceMatrixRecalculationPerRender = true;
        }
        var cam = new GameObject("Bake Camera").AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 2.5f;
        cam.transform.position = new Vector3(0, 5.8f, 8); cam.transform.LookAt(new Vector3(0, 1, 0));
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.clear;
        cam.nearClipPlane = .01f; cam.farClipPlane = 30;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.65f, .72f, .7f);
        var light = new GameObject("Key").AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.2f; light.color = new Color(1, .91f, .8f);
        light.transform.rotation = Quaternion.Euler(35, -35, 0);
        string destination = "Assets/Foxfire/Resources/HollowrootWarden";
        Directory.CreateDirectory(destination);
        var report = new System.Text.StringBuilder();
        report.AppendLine("Hollowroot Warden: attack/death 512px single-view atlases; Walking/Running 256px cells, 8 target-facing directions x 16 frames.\nCamera: fixed three-quarter view; locomotion model rotates toward each target direction. Full supplied clip durations; non-looping endpoints preserved.");
        var pivot = cam.WorldToViewportPoint(Vector3.zero);
        report.AppendLine("Feet pivot: " + pivot);
        foreach (string name in Clips)
        {
            var clip = clips.Single(c => c.name == name);
            bool locomotion = name == "Walking" || name == "Running";
            if (locomotionOnly && !locomotion)
            {
                report.AppendLine(name + ": preserved existing single-view atlas");
                continue;
            }
            int cell = locomotion ? LocomotionCell : Cell;
            int count = locomotion ? LocomotionFrames : Frames;
            int directions = locomotion ? LocomotionDirections : 1;
            int columns = locomotion ? LocomotionFrames : Columns;
            int rows = locomotion ? LocomotionDirections : Frames / Columns;
            var atlas = new Texture2D(cell * columns, cell * rows, TextureFormat.RGBA32, false);
            var rt = new RenderTexture(cell, cell, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var frame = new Texture2D(cell, cell, TextureFormat.RGBA32, false);
            for (int d = 0; d < directions; d++) for (int f = 0; f < count; f++)
            {
                facingRoot.transform.rotation = Quaternion.identity;
                clip.SampleAnimation(instance, clip.length * f / (locomotion ? count : count - 1));
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                // Camera is on +Z: +screen X is -world X, +screen Y is -world Z.
                facingRoot.transform.rotation = locomotion
                    ? Quaternion.Euler(0, -90 - d * 360f / directions, 0) : Quaternion.identity;
                cam.Render(); RenderTexture.active = rt;
                frame.ReadPixels(new Rect(0, 0, cell, cell), 0, 0); frame.Apply();
                var pixels = frame.GetPixels32(); int opaque = 0, edge = 0;
                for (int y = 0; y < cell; y++) for (int x = 0; x < cell; x++)
                    if (pixels[y * cell + x].a > 10) { opaque++; if (x < 2 || y < 2 || x >= cell - 2 || y >= cell - 2) edge++; }
                if (opaque < 100 || edge > 0) throw new Exception("Warden empty/clipped cell: " + name + " direction=" + d + " frame=" + f + " opaque=" + opaque + " edge=" + edge);
                int row = locomotion ? d : f / Columns;
                int column = locomotion ? f : f % Columns;
                atlas.SetPixels32(column * cell, (rows - 1 - row) * cell, cell, cell, pixels);
            }
            atlas.Apply(); File.WriteAllBytes(destination + "/" + name + ".png", atlas.EncodeToPNG());
            report.AppendLine(name + ": " + clip.length + " seconds; " + count * directions + " nonempty, unclipped frames");
            UnityEngine.Object.DestroyImmediate(atlas);
            RenderTexture.active = null; cam.targetTexture = null; rt.Release();
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(frame);
        }
        Directory.CreateDirectory("artifacts");
        File.WriteAllText("artifacts/warden-bake-report.txt", report.ToString());
        UnityEngine.Object.DestroyImmediate(material);
        UnityEngine.Object.DestroyImmediate(facingRoot);
        AssetDatabase.Refresh();
        foreach (string name in new[] { "Walking", "Running" })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(destination + "/" + name + ".png");
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        Debug.Log("FOXFIRE_WARDEN_BAKE_SUCCESS " + report);
    }
    public static void Inspect()
    {
        AssetDatabase.Refresh();
        var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;
        importer.SaveAndReimport();
        foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>())
            Debug.Log("WARDEN_CLIP " + clip.name + " duration=" + clip.length + " fps=" + clip.frameRate);
        foreach (var clip in importer.defaultClipAnimations)
            Debug.Log("WARDEN_TAKE " + clip.name + " first=" + clip.firstFrame + " last=" + clip.lastFrame);
        var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            Debug.Log("WARDEN_MESH " + renderer.name + " bounds=" + renderer.bounds + " material=" + renderer.sharedMaterial);
        UnityEngine.Object.DestroyImmediate(instance);
    }
}
}
#endif
