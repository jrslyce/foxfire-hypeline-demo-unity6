using UnityEngine;
using UnityEngine.UI;

namespace Foxfire
{
    // References point to normal, editable Canvas objects in the scene.
    public sealed class StartMenuView : MonoBehaviour
    {
        public CanvasGroup menuGroup;
        public Text subtitle;
        public Text actionLabel;
        public Text pauseHint;
        [TextArea] public string pausedSubtitle = "Your adventure is paused.";
        public string resumeLabel = "RESUME ADVENTURE";
        public string resumeHint = "ESC to resume";
        public string victorySubtitle = "The clearing is safe once more.";
        public string defeatSubtitle = "The forest awaits your return.";
        ArenaController arena;
        GameObject aiButton;
        string initialSubtitle, initialAction, initialHint;
        bool captured;

        void CaptureDefaults()
        {
            if (captured) return;
            initialSubtitle = subtitle.text;
            initialAction = actionLabel.text;
            initialHint = pauseHint.text;
            var playButton = actionLabel.GetComponentInParent<Button>();
            if (playButton != null)
            {
                aiButton = Instantiate(playButton.gameObject, playButton.transform.parent);
                aiButton.name = "AI Mode Button";
                var rect = (RectTransform)aiButton.transform;
                rect.anchoredPosition += new Vector2(337, 0);
                if (pauseHint.transform.parent == playButton.transform.parent)
                    pauseHint.rectTransform.anchoredPosition = ((RectTransform)playButton.transform).anchoredPosition + new Vector2(0, -66);
                var button = aiButton.GetComponent<Button>();
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(OnAiClicked);
                aiButton.GetComponentInChildren<Text>().text = "AI MODE";
            }
            captured = true;
        }
        public void SetState(ArenaController controller, bool visible, bool paused, Outcome outcome)
        {
            CaptureDefaults();
            arena = controller;
            menuGroup.alpha = visible ? 1 : 0;
            menuGroup.interactable = visible;
            menuGroup.blocksRaycasts = visible;
            if (aiButton != null)
            {
                aiButton.SetActive(visible);
                aiButton.GetComponentInChildren<Text>().text = paused && controller != null && controller.Game != null && !controller.Game.PlayerControlled ? "TAKE CONTROL" : "AI MODE";
            }
            if (!visible) return;
            subtitle.text = paused ? pausedSubtitle : outcome == Outcome.Victory ? victorySubtitle :
                outcome != Outcome.None ? defeatSubtitle : initialSubtitle;
            actionLabel.text = paused ? resumeLabel : initialAction;
            pauseHint.text = paused ? resumeHint : initialHint;
            pauseHint.gameObject.SetActive(paused);
        }
        public void OnPlayClicked()
        {
            if (arena != null) arena.StartOrResume();
        }
        public void OnAiClicked()
        {
            if (arena != null) arena.StartAiMode();
        }
    }
}
