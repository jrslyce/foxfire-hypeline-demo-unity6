#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Foxfire.Editor
{
    public static class StartMenuBuilder
    {
        const string ScenePath = "Assets/Foxfire/Scenes/FoxfireArena.unity";
        static readonly Color Cream = new Color(1f, .906f, .745f), Teal = new Color(.5f, .843f, .776f);
        static Font font;
        static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }
        static Image Box(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            var image = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
        static Text Label(string name, Transform parent, float x, float y, float w, float h,
            string content, int size, Color color, bool bold = false, TextAnchor align = TextAnchor.UpperLeft)
        {
            var text = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Text>();
            text.font = font; text.text = content; text.fontSize = size; text.color = color;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.alignment = align; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
        [MenuItem("Foxfire/Create editable start menu")]
        public static void CreateMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Exit Play mode before creating the menu."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (Object.FindObjectOfType<StartMenuView>(true) != null) { Debug.Log("Editable start menu already exists."); return; }
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = new GameObject("Start Menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(StartMenuView));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = Camera.main != null ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            var view = root.GetComponent<StartMenuView>(); view.menuGroup = root.GetComponent<CanvasGroup>();
            var panel = Box("Menu Panel", root.transform, 76, 72, 1128, 576, new Color(.016f,.075f,.068f,.96f)).transform;
            Box("Top Border", panel, 0, 0, 1128, 2, new Color(.72f,.58f,.35f,.8f));
            Box("Bottom Border", panel, 0, 574, 1128, 2, new Color(.72f,.58f,.35f,.55f));
            Box("Left Border", panel, 0, 0, 2, 576, new Color(.72f,.58f,.35f,.55f));
            Box("Right Border", panel, 1126, 0, 2, 576, new Color(.72f,.58f,.35f,.55f));
            Box("Portrait Divider", panel, 748, 0, 1, 574, new Color(.55f,.70f,.57f,.22f));
            Box("Title Accent", panel, 40, 49, 35, 3, new Color(0,.745f,.745f));
            Label("Eyebrow", panel, 89, 39, 570, 24, "THE FOREST NEEDS ITS GUARDIAN", 14, Teal, true);
            Label("Title", panel, 34, 88, 680, 78, "FOXFIRE ARENA", 59, Cream, true);
            view.subtitle = Label("Subtitle", panel, 40, 175, 650, 30, "An enchanted forest. One brave fox.", 19, new Color(.88f,.906f,.85f));
            Label("Objective", panel, 40, 215, 650, 28, "Survive the clearing and face the Hollowroot Warden.", 15, new Color(.66f,.74f,.69f));
            Box("Controls Divider", panel, 40, 279, 660, 1, new Color(.6f,.75f,.65f,.22f));
            Label("Controls Heading", panel, 40, 305, 200, 22, "HOW TO PLAY", 14, Teal, true);
            Box("Movement Key Background", panel, 40, 346, 91, 39, new Color(.12f,.20f,.17f));
            Label("Movement Keys", panel, 40, 346, 91, 39, "W A S D", 13, Cream, true, TextAnchor.MiddleCenter);
            Label("Movement Description", panel, 146, 352, 255, 26, "Move  /  Arrow keys", 19, new Color(.88f,.906f,.85f));
            Box("Attack Key Background", panel, 40, 400, 91, 39, new Color(.12f,.20f,.17f));
            Label("Attack Key", panel, 40, 400, 91, 39, "AUTO", 13, Cream, true, TextAnchor.MiddleCenter);
            Label("Attack Description", panel, 146, 406, 350, 26, "Attacks fire automatically", 19, new Color(.88f,.906f,.85f));
            var buttonImage = Box("Play Button", panel, 40, 478, 310, 60, new Color(.04f,.46f,.40f));
            buttonImage.raycastTarget = true;
            var button = buttonImage.gameObject.AddComponent<Button>(); button.targetGraphic = buttonImage;
            view.actionLabel = Label("Button Label", button.transform, 0, 0, 310, 60, "ENTER THE FOREST", 21, Color.white, true, TextAnchor.MiddleCenter);
            UnityEventTools.AddPersistentListener(button.onClick, view.OnPlayClicked);
            view.pauseHint = Label("Pause Hint", panel, 377, 499, 290, 25, "ESC to pause in game", 15, new Color(.66f,.74f,.69f));
            Label("Portrait Heading", panel, 784, 50, 300, 22, "GUARDIAN OF THE CLEARING", 14, Teal, true);
            var portrait = Rect("Fox Portrait", panel, 757, 124, 330, 330).gameObject.AddComponent<RawImage>();
            portrait.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Foxfire/Resources/FoxHero/FoxWalk.png");
            portrait.uvRect = new Rect(0, 7f/32f, 1f/16f, 1f/32f); portrait.raycastTarget = false;
            Box("Portrait Caption Divider", panel, 787, 464, 277, 1, new Color(.55f,.70f,.57f,.3f));
            Label("Portrait Caption", panel, 787, 483, 280, 25, "A little courage goes a long way.", 15, new Color(.66f,.74f,.69f));
            if (Object.FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            Debug.Log("Editable start menu saved in " + ScenePath);
        }
    }
}
#endif
