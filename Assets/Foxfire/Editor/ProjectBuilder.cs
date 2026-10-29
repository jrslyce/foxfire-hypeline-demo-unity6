#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Foxfire.Editor
{
public static class ProjectBuilder
{
    const string Scene = "Assets/Foxfire/Scenes/FoxfireArena.unity";
    [MenuItem("Foxfire/Prepare project")]
    public static void Prepare()
    {
        Directory.CreateDirectory("Assets/Foxfire/Resources");
        Directory.CreateDirectory("Assets/Foxfire/Scenes");
        Directory.CreateDirectory("Assets/Foxfire/Prefabs");
        var balance =
            AssetDatabase.LoadAssetAtPath<BalanceAsset>("Assets/Foxfire/Resources/DefaultBalance.asset");
        if (balance == null)
        {
            balance = ScriptableObject.CreateInstance<BalanceAsset>();
            AssetDatabase.CreateAsset(balance, "Assets/Foxfire/Resources/DefaultBalance.asset");
        }
        if (AssetDatabase.LoadAssetAtPath<BalanceAsset>("Assets/Foxfire/Resources/GddBaseline.asset") == null)
        {
            var original = ScriptableObject.CreateInstance<BalanceAsset>();
            AssetDatabase.CreateAsset(original, "Assets/Foxfire/Resources/GddBaseline.asset");
        }
        // The scene contains the hand-editable menu. Builds must never recreate it.
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene) == null)
            throw new InvalidOperationException("The Foxfire scene is missing: " + Scene);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true) };
        PlayerSettings.companyName = "Muxy";
        PlayerSettings.productName = "Foxfire Arena";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.runInBackground = true;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        // Local loopback bridge QA is restricted to development players; releases use HTTPS.
        PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,
                                           ScriptingImplementation.Mono2x);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("artifacts");
        File.WriteAllText("artifacts/player-balance.json", JsonUtility.ToJson(balance.values, true));
        Debug.Log("FOXFIRE_PREPARED");
    }
    [MenuItem("Foxfire/Build Mac player")]
    public static void BuildMac()
    {
        Prepare();
        Build(BuildTarget.StandaloneOSX, "Builds/Mac/Foxfire Arena.app");
    }
    public static void BuildMacDevelopment()
    {
        Prepare();
        Build(BuildTarget.StandaloneOSX, "Builds/MacDevelopment/Foxfire Arena.app", BuildOptions.Development);
    }
    public static void BuildEnemyReleaseCandidates()
    {
        BuildMacDevelopment();
        BuildLinuxDevelopment();
        BuildLinux();
    }
    public static void BuildLinux()
    {
        Prepare();
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
            throw new InvalidOperationException(
                "Install Linux Build Support (Mono) for the pinned editor first.");
        Build(BuildTarget.StandaloneLinux64, "Builds/Linux/FoxfireArena.x86_64");
    }
    public static void BuildLinuxDevelopment()
    {
        Prepare();
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
            throw new InvalidOperationException("Install Linux Build Support (Mono) for the pinned editor first.");
        Build(BuildTarget.StandaloneLinux64, "Builds/LinuxDevelopment/FoxfireArena.x86_64", BuildOptions.Development);
    }
    public static void VerifyFoxHero()
    {
        using (var fox = new FoxHeroFrames())
        {
            if (!fox.Available) throw new Exception("Fox hero atlas is missing from the release.");
            var moving = fox.Sample(.1, new V(1, 0), true);
            if (moving != fox.Sample(0, new V(1, 0), false) ||
                moving != fox.Sample(.1, new V(0, 1), false))
                throw new Exception("Fox pose must hold between ticks and when stopped.");
            for (int d = 0; d < FoxHeroFrames.DirectionCount; d++)
            {
                double a = d * 2 * Math.PI / FoxHeroFrames.DirectionCount;
                var sprite = fox.Sample(.5, new V((float)Math.Cos(a), (float)Math.Sin(a)), true);
                if (sprite.rect.y != (FoxHeroFrames.DirectionCount - 1 - d) * FoxHeroFrames.CellSize)
                    throw new Exception("Fox atlas facing row mismatch at direction " + d);
            }
        }
        Debug.Log("FOXFIRE_FOX_VALIDATION_SUCCESS directions=32 poseHold=true");
    }
    static void Build(BuildTarget target, string path, BuildOptions options = BuildOptions.None)
    {
        VerifyFoxHero();
        EnemyModelBake.Validate();
        WardenBake.Validate();
        using (var warden = new WardenFrames())
        {
            if (!warden.Available) throw new Exception("Warden atlases missing from release.");
            foreach (WardenClip clip in Enum.GetValues(typeof(WardenClip)))
                if (warden.Sample(clip, 0) == null || warden.Sample(clip, WardenAnimation.Duration(clip)) == null)
                    throw new Exception("Warden animation missing: " + clip);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report = BuildPipeline.BuildPlayer(
            new BuildPlayerOptions { scenes = new[] { Scene }, locationPathName = path, target = target,
                                     options = options });
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Build failed: " + report.summary.result);
        Debug.Log("FOXFIRE_BUILD_SUCCESS " + path);
    }
}
}
#endif
