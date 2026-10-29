#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MuxyGateway;
using Foxfire;

public static class FoxfireHypelineSetup
{
    [MenuItem("Foxfire/Set up Hypeline prefabs")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Exit Play mode first."); return; }
        var menuView = Object.FindObjectOfType<StartMenuView>(true);
        if (menuView == null) { Debug.LogError("Open the FoxfireArena scene first."); return; }
        var manager = Object.FindObjectOfType<MuxyGatewayManager>(true);
        bool createdManager = manager == null;
        if (manager == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.muxy.gateway/Prefabs/MuxyGatewayManager.prefab");
            if (prefab == null) { Debug.LogError("Muxy Hypeline package or manager prefab is missing."); return; }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Add Gateway manager");
            manager = instance.GetComponent<MuxyGatewayManager>();
        }
        // Configure the game catalog in the manager Inspector, without altering SDK source.
        var field = typeof(MuxyGatewayManager).GetField("GameActions", BindingFlags.Instance | BindingFlags.NonPublic);
        var existingActions = (List<GameAction>)field.GetValue(manager);
        var actions = existingActions != null ? new List<GameAction>(existingActions) : new List<GameAction>();
        for (int i = 0; i < Actions.Count; i++)
        {
            var kind = (ActionKind)i;
            if (actions.Exists(action => action.ID == Actions.Ids[i])) continue;
            actions.Add(new GameAction { ID = Actions.Ids[i], Name = Actions.Label(kind),
                Description = Actions.Description(kind),
                Category = Actions.Category(kind) == "help" ? GameActionCategory.Help :
                    Actions.Category(kind) == "hinder" ? GameActionCategory.Hinder : GameActionCategory.Neutral,
                State = GameActionState.Unavailable, Impact = 1, Count = GameAction.InfiniteCount,
                OnGameActionUsed = new MuxyGatewayGameActionUsedEvent() });
        }
        Undo.RecordObject(manager, "Configure Foxfire viewer actions");
        field.SetValue(manager, actions);
        var metadata = new SerializedObject(manager);
        if (createdManager)
        {
            metadata.FindProperty("GameMetadata").FindPropertyRelative("Name").stringValue = "Foxfire Arena";
            metadata.FindProperty("GameMetadata").FindPropertyRelative("Theme").stringValue = "#00bebe";
        }
        metadata.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
        var hypelineMenu = Object.FindObjectOfType<MuxyHypelineMenu>(true);
        if (hypelineMenu == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.muxy.gateway/Prefabs/MuxyHypelineMenu.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Add Hypeline menu");
            instance.transform.SetParent(menuView.transform, false);
            instance.transform.localScale = Vector3.one * .82f;
            hypelineMenu = instance.GetComponent<MuxyHypelineMenu>();
        }
        Undo.RecordObject(hypelineMenu, "Connect Hypeline menu");
        hypelineMenu.GatewayManager = manager;
        hypelineMenu.GameDescription = "Viewers can help the fox, summon enemies, and trigger magical effects.";
        PrefabUtility.RecordPrefabInstancePropertyModifications(hypelineMenu);
        var alert = Object.FindObjectOfType<MuxyHypelineInteractionAlert>(true);
        if (alert == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.muxy.gateway/Prefabs/MuxyHypelineInteractionAlert.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Add Hypeline interaction alert");
            alert = instance.GetComponent<MuxyHypelineInteractionAlert>();
        }
        Undo.RecordObject(alert, "Connect Hypeline alert");
        alert.GatewayManager = manager;
        PrefabUtility.RecordPrefabInstancePropertyModifications(alert);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Selection.activeGameObject = manager.gameObject;
        Debug.Log("Hypeline prefabs and Foxfire actions configured. Set the linked Game ID in the manager.");
    }
}
#endif
