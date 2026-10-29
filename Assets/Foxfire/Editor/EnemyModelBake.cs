using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Foxfire.Editor
{
public static class EnemyModelBake
{
    public const string Source = "Assets/Foxfire/Art/EnemyModels/";
    public const string Destination = "Assets/Foxfire/Resources/EnemyFrames";
    const int Cell = EnemyModelVisuals.Cell, Directions = EnemyModelVisuals.Directions;
    public static void InspectWalking()
    {
        AssetDatabase.Refresh();
        foreach (string name in EnemyModelVisuals.Names.Take(2))
        {
            string path = Directory.GetFiles(Source + name, "*Walking_withSkin.fbx").Single();
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            var bindings = AnimationUtility.GetCurveBindings(clip);
            Debug.Log("GAIT_SOURCE " + name + " clip=" + clip.name + " duration=" + clip.length + " bindings=" + bindings.Length);
            foreach (var binding in bindings.Take(12)) Debug.Log("GAIT_BINDING " + binding.path + " " + binding.propertyName);
            var bones = model.GetComponentsInChildren<Transform>();
            clip.SampleAnimation(model, 0);
            var positions = bones.Select(b => b.localPosition).ToArray();
            var rotations = bones.Select(b => b.localRotation).ToArray();
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            var initial = new Vector3[skins.Length][];
            for (int s = 0; s < skins.Length; s++)
            {
                var mesh = new Mesh(); skins[s].BakeMesh(mesh); initial[s] = mesh.vertices;
                Debug.Log("GAIT_SKIN " + skins[s].name + " bones=" + skins[s].bones.Length + " vertices=" + mesh.vertexCount);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            clip.SampleAnimation(model, clip.length * .25f);
            for (int b = 0; b < bones.Length; b++)
            {
                float rotation = Quaternion.Angle(rotations[b], bones[b].localRotation);
                float position = Vector3.Distance(positions[b], bones[b].localPosition);
                if (rotation > .01f || position > .0001f) Debug.Log("GAIT_BONE " + bones[b].name + " degrees=" + rotation + " travel=" + position);
            }
            for (int s = 0; s < skins.Length; s++)
            {
                var mesh = new Mesh(); skins[s].BakeMesh(mesh); var vertices = mesh.vertices;
                float max = 0;
                for (int v = 0; v < vertices.Length; v++) max = Mathf.Max(max, Vector3.Distance(initial[s][v], vertices[v]));
                Debug.Log("GAIT_DEFORMATION " + name + " max=" + max);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            Debug.Log("GAIT_RENDERERS " + name + " skinned=" + skins.Length + " static=" + model.GetComponentsInChildren<MeshRenderer>().Length);
            UnityEngine.Object.DestroyImmediate(model);
        }
    }
    [MenuItem("Foxfire/Bake enemy models")]
    public static void Bake()
    {
        AssetDatabase.Refresh();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        Directory.CreateDirectory(Destination);
        var report = new System.Text.StringBuilder();
        foreach (string name in EnemyModelVisuals.Names) BakeModel(name, report);
        Directory.CreateDirectory("artifacts");
        File.WriteAllText("artifacts/enemy-bake-report.txt", report.ToString());
        AssetDatabase.Refresh();
        Validate();
        Debug.Log("FOXFIRE_ENEMY_BAKE_SUCCESS " + report);
    }
    static void BakeModel(string name, System.Text.StringBuilder report)
    {
        string folder = Source + name;
        string modelPath = Directory.GetFiles(folder, name == "Shroomling" ? "*.fbx" : "*Walking_withSkin.fbx").Single();
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = name != "Shroomling";
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.SaveAndReimport();
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
        var parent = new GameObject("Normalized model"); model.transform.SetParent(parent.transform, false);
        // Static FBX roots may carry the source coordinate-system correction (e.g. Z-up).
        // Preserve it while stripping animation root translation, or the shroomling lies sideways.
        var rootRotation = model.transform.localRotation;
        var rootScale = model.transform.localScale;
        var rootPosition = model.transform.localPosition;
        var clip = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__") && c.length > 0);
        if (name != "Shroomling" && clip == null) throw new Exception("Missing walking animation: " + name);
        string texturePath = Directory.GetFiles(folder, name == "Shroomling" ? "*_texture.png" : "*_texture_0.png").Single();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null) throw new Exception("Missing albedo: " + name);
        var material = new Material(Shader.Find("Standard")); material.mainTexture = texture;
        material.SetFloat("_Glossiness", .15f);
        var renderers = model.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
        // All samples render within one editor update. Otherwise Unity can reuse the
        // first skinning matrices while the bones advance, producing a frozen gait atlas.
        foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            skin.updateWhenOffscreen = true;
            skin.forceMatrixRecalculationPerRender = true;
        }
        if (clip != null) clip.SampleAnimation(model, 0);
        var bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        float scale = 1.5f / bounds.size.y;
        var offset = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
        var cam = new GameObject("Bake camera").AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 1.8f;
        cam.transform.position = new Vector3(0, 4.65f, 8); cam.transform.LookAt(new Vector3(0, .65f, 0));
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.clear;
        cam.nearClipPlane = .01f; cam.farClipPlane = 30;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.65f, .72f, .7f);
        var light = new GameObject("Key").AddComponent<Light>(); light.type = LightType.Directional;
        light.intensity = 1.2f; light.color = new Color(1, .91f, .8f); light.transform.rotation = Quaternion.Euler(35, -35, 0);
        var rt = new RenderTexture(Cell, Cell, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        var frame = new Texture2D(Cell, Cell, TextureFormat.RGBA32, false);
        int count = clip != null ? EnemyModelVisuals.Frames : 1;
        var atlas = new Texture2D(Cell * count, Cell * Directions, TextureFormat.RGBA32, false);
        Color32[] firstPose = null;
        int firstArea = 0;
        float silhouetteMotion = 0;
        for (int d = 0; d < Directions; d++) for (int f = 0; f < count; f++)
        {
            parent.transform.rotation = Quaternion.identity; parent.transform.localScale = Vector3.one;
            if (clip != null) clip.SampleAnimation(model, clip.length * f / count);
            model.transform.localRotation = rootRotation;
            model.transform.localScale = rootScale;
            model.transform.localPosition = rootPosition + offset;
            parent.transform.localScale = Vector3.one * scale;
            parent.transform.rotation = Quaternion.Euler(0, -90 - d * 360f / Directions, 0);
            cam.Render(); RenderTexture.active = rt;
            frame.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0); frame.Apply();
            var pixels = frame.GetPixels32(); int opaque = 0, edge = 0;
            for (int y = 0; y < Cell; y++) for (int x = 0; x < Cell; x++)
                if (pixels[y * Cell + x].a > 10) { opaque++; if (x < 2 || y < 2 || x >= Cell - 2 || y >= Cell - 2) edge++; }
            if (opaque < 100 || edge > 0) throw new Exception("Empty/clipped enemy frame " + name + " dir=" + d + " frame=" + f + " opaque=" + opaque + " edge=" + edge);
            if (count > 1 && (d == 0 || d == Directions / 2))
            {
                if (f == 0)
                {
                    firstPose = pixels;
                    firstArea = pixels.Count(p => p.a > 128);
                    silhouetteMotion = 0;
                }
                else
                {
                    int changed = 0;
                    for (int p = 0; p < pixels.Length; p++)
                        if ((pixels[p].a > 128) != (firstPose[p].a > 128)) changed++;
                    silhouetteMotion = Mathf.Max(silhouetteMotion, (float)changed / firstArea);
                }
                if (f == count - 1)
                {
                    if (silhouetteMotion <= .18f)
                        throw new Exception("Near-frozen walking silhouette: " + name + " dir=" + d + " motion=" + silhouetteMotion);
                    report.AppendLine(name + " side=" + d + " silhouetteMotion=" + silhouetteMotion);
                }
            }
            atlas.SetPixels32(f * Cell, (Directions - 1 - d) * Cell, Cell, Cell, pixels);
        }
        atlas.Apply(); File.WriteAllBytes(Destination + "/" + name + ".png", atlas.EncodeToPNG());
        var info = new EnemyModelVisuals.BakeInfo { pivotY = cam.WorldToViewportPoint(Vector3.zero).y, duration = clip != null ? clip.length : 1 };
        File.WriteAllText(Destination + "/" + name + ".json", JsonUtility.ToJson(info, true));
        report.AppendLine(name + " frames=" + count * Directions + " duration=" + info.duration + " pivot=" + info.pivotY + " all nonempty and unclipped");
        RenderTexture.active = null; cam.targetTexture = null; rt.Release();
        foreach (var obj in new UnityEngine.Object[] { parent, cam.gameObject, light.gameObject, rt, frame, atlas, material }) UnityEngine.Object.DestroyImmediate(obj);
    }
    public static void Validate()
    {
        using (var frames = new EnemyModelVisuals())
        {
            if (!frames.Available) throw new Exception("Enemy atlases are required for release.");
            for (int n = 0; n < 4; n++)
            {
                var e = new Enemy { id = n, kind = n == 3 ? EnemyKind.Sprig : n == 2 ? EnemyKind.Wisp : EnemyKind.Boar, champion = n == 1 };
                V p;
                var first = frames.Sample(e, 0, new V(1, 0), out p);
                if (first == null || frames.Sample(e, 0, new V(0, 1), out p) != first) throw new Exception("Pose hold failed");
                if (frames.Sample(e, 1, new V(0, 1), out p) != first) throw new Exception("Stationary pose changed");
                e.dissolve = 1;
                if (frames.Sample(e, 2, new V(0, 1), out p) != first) throw new Exception("Dissolve reverted to placeholder");
                if (n < 2)
                {
                    e.dissolve = 0; e.pos = new V(1, 0);
                    var walking = frames.Sample(e, 2.125, new V(1, 0), out p);
                    if (walking == first) throw new Exception("Walking did not advance");
                    e.pos = new V(2, 0);
                    if (frames.Sample(e, 3.125, new V(1, 0), out p) != walking) throw new Exception("Gait did not loop");
                    e.tell = 1; e.dir = new V(1, 0); e.pos = new V(3, 0);
                    if (frames.Sample(e, 3.375, e.dir, out p) != walking) throw new Exception("Charge tell did not hold pose");
                    e.tell = 0; e.charge = 1; e.pos = new V(4, 0);
                    if (frames.Sample(e, 3.5, e.dir, out p) == walking) throw new Exception("Charge gait did not advance");
                }
            }
            if (Mathf.Abs(EnemyModelVisuals.FloatOffset(0, 0)) > .001f ||
                Mathf.Abs(EnemyModelVisuals.FloatOffset(Math.PI / 4.4, 0) - .12f) > .001f)
                throw new Exception("Float amplitude/period regression");
        }
        Debug.Log("FOXFIRE_ENEMY_VALIDATION_SUCCESS models=3 kinds=Boar,Champion,Sprig,Wisp poseHold=true dissolve=true float=true walkLoop=true charge=true");
    }
}
}
