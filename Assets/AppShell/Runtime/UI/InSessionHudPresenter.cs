using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;

namespace VRPublicSpeaking.AppShell.UI
{
    public class InSessionHudPresenter : MonoBehaviour
    {
        [SerializeField] private AppRuntimeState runtimeState;
        [SerializeField] private MainController mainController;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text timerLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private RectTransform warningRoot;
        [SerializeField] private CanvasGroup warningCanvasGroup;
        [SerializeField] private WorldSpaceCanvasFollower warningFollower;
        [SerializeField] private TMP_Text warningLabel;
        [SerializeField] private bool hideWhenSessionInactive = true;
        [SerializeField] private string inactiveStatusText = "Session idle";
        [SerializeField] private Color warningTextColor = new Color(0.98f, 0.74f, 0.39f, 1f);
        [SerializeField] private Color warningBackgroundColor = new Color(0.08f, 0.11f, 0.15f, 0.92f);
        [SerializeField] private Vector3 warningFollowOffset = new Vector3(0f, -0.12f, 0.92f);
        [SerializeField] private Vector2 desktopWarningAnchoredPosition = new Vector2(0f, -135f);
        [SerializeField] private Vector2 desktopWarningSize = new Vector2(540f, 90f);
        [SerializeField] private float desktopWarningFontSize = 16f;

        private bool warningVisible;

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            EnsureHudLayout();
        }

        private void Update()
        {
            if (runtimeState == null)
            {
                runtimeState = AppRuntimeState.GetOrCreate();
            }

            if (mainController == null)
            {
                mainController = FindFirstObjectByType<MainController>(FindObjectsInactive.Include);
            }

            Refresh();
        }

        public void Refresh()
        {
            if (runtimeState == null)
            {
                return;
            }

            SessionRuntimeState runtime = runtimeState.CurrentRuntimeState;
            SessionConfig config = runtimeState.CurrentSessionConfig;
            bool isActive = runtime != null && runtime.SessionRunning;
            bool hideForOverlay = runtime != null && (runtime.PauseMenuVisible || runtime.ResultsOverlayVisible);
            float targetDurationSeconds = config?.SessionDurationSeconds ?? SessionConfig.DefaultDurationSeconds;
            float elapsedSeconds = isActive
                ? Mathf.Max(0f, targetDurationSeconds - runtime.TimeRemainingSeconds)
                : 0f;

            SetVisible((!hideWhenSessionInactive || isActive) && !hideForOverlay);

            if (timerLabel != null)
            {
                timerLabel.text = isActive
                    ? FormatStopwatchTime(elapsedSeconds)
                    : FormatStopwatchTime(0f);
            }

            if (statusLabel != null)
            {
                statusLabel.text = isActive
                    ? $"{(runtime != null && runtime.SessionPaused ? "Paused" : "Target")}: {FormatStopwatchTime(targetDurationSeconds)} | {GetPracticeModeLabel(config?.PracticeMode ?? PracticeMode.GuidedPractice)}"
                    : inactiveStatusText;
            }

            RefreshLiveWarning(isActive && !hideForOverlay);
        }

        private void SetVisible(bool isVisible)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = isVisible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void RefreshLiveWarning(bool canShow)
        {
            EnsureHudLayout();

            if (warningLabel == null || warningRoot == null)
            {
                return;
            }

            if (!canShow || mainController == null || !mainController.TryGetLiveWarningState(out string warningMessage, out float warningAlpha))
            {
                SetWarningVisible(false);
                return;
            }

            SetWarningVisible(true);
            warningLabel.text = warningMessage;

            Color color = warningTextColor;
            color.a = Mathf.Clamp01(Mathf.Max(0.35f, warningAlpha));
            warningLabel.color = color;

            if (warningCanvasGroup != null)
            {
                warningCanvasGroup.alpha = Mathf.Clamp01(Mathf.Max(0.45f, warningAlpha));
                warningCanvasGroup.interactable = false;
                warningCanvasGroup.blocksRaycasts = false;
            }
        }

        private void EnsureHudLayout()
        {
            RectTransform hudRect = transform as RectTransform;
            if (hudRect != null)
            {
                hudRect.sizeDelta = new Vector2(Mathf.Max(hudRect.sizeDelta.x, 460f), 140f);
            }

            if (timerLabel != null)
            {
                ConfigureHudLabel(timerLabel, new Vector2(420f, 60f), new Vector2(0f, 18f));
            }

            if (statusLabel != null)
            {
                ConfigureHudLabel(statusLabel, new Vector2(420f, 60f), new Vector2(0f, -24f));
                statusLabel.fontSize = 18f;
                statusLabel.color = statusLabel.color.a > 0f ? statusLabel.color : new Color(0.70f, 0.78f, 0.90f, 1f);
            }

            EnsureWarningPanel();
        }

        private void EnsureWarningPanel()
        {
            Transform warningParent = transform.parent != null ? transform.parent : transform;

            if (warningRoot == null)
            {
                Transform existingRoot = warningParent.Find("LiveWarningPanel");
                warningRoot = existingRoot as RectTransform;
            }

            if (warningRoot == null)
            {
                GameObject warningRootObject = new GameObject("LiveWarningPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                warningRootObject.transform.SetParent(warningParent, false);
                warningRoot = warningRootObject.GetComponent<RectTransform>();
            }

            bool useScreenSpacePlacement = IsUnderScreenSpaceCanvas();
            Vector2 warningSize = useScreenSpacePlacement ? desktopWarningSize : new Vector2(420f, 78f);

            warningRoot.anchorMin = new Vector2(0.5f, 0.5f);
            warningRoot.anchorMax = new Vector2(0.5f, 0.5f);
            warningRoot.pivot = new Vector2(0.5f, 0.5f);
            warningRoot.sizeDelta = warningSize;
            warningRoot.anchoredPosition = useScreenSpacePlacement ? desktopWarningAnchoredPosition : Vector2.zero;
            warningRoot.localRotation = Quaternion.identity;
            warningRoot.localScale = Vector3.one;
            warningRoot.SetAsLastSibling();

            if (warningCanvasGroup == null)
            {
                warningCanvasGroup = warningRoot.GetComponent<CanvasGroup>();
            }
            if (warningCanvasGroup == null)
            {
                warningCanvasGroup = warningRoot.gameObject.AddComponent<CanvasGroup>();
            }

            warningCanvasGroup.interactable = false;
            warningCanvasGroup.blocksRaycasts = false;

            Image background = warningRoot.GetComponent<Image>();
            if (background == null)
            {
                background = warningRoot.gameObject.AddComponent<Image>();
            }

            background.color = warningBackgroundColor;
            background.raycastTarget = false;

            if (warningFollower == null)
            {
                warningFollower = warningRoot.GetComponent<WorldSpaceCanvasFollower>();
            }
            if (warningFollower == null)
            {
                warningFollower = warningRoot.gameObject.AddComponent<WorldSpaceCanvasFollower>();
            }

            warningFollower.enabled = !useScreenSpacePlacement;
            if (!useScreenSpacePlacement)
            {
                warningFollower.Configure(null, warningFollowOffset, false, true, 14f, 16f);
                warningFollower.SetFollowContinuously(true);
            }

            if (warningLabel != null && warningLabel.transform.parent == transform)
            {
                warningLabel.transform.SetParent(warningRoot, false);
            }

            if (warningLabel == null)
            {
                Transform existingLabel = warningRoot.Find("WarningLabel");
                if (existingLabel != null)
                {
                    warningLabel = existingLabel.GetComponent<TMP_Text>();
                }
            }

            if (warningLabel == null)
            {
                GameObject labelObject = new GameObject("WarningLabel", typeof(RectTransform));
                labelObject.transform.SetParent(warningRoot, false);
                warningLabel = labelObject.AddComponent<TextMeshProUGUI>();
            }

            ConfigureHudLabel(
                warningLabel,
                useScreenSpacePlacement ? new Vector2(500f, 66f) : new Vector2(380f, 58f),
                Vector2.zero);
            warningLabel.fontSize = useScreenSpacePlacement ? desktopWarningFontSize : 13f;
            warningLabel.alignment = TextAlignmentOptions.Center;
            warningLabel.textWrappingMode = TextWrappingModes.Normal;
            warningLabel.overflowMode = TextOverflowModes.Ellipsis;
            warningLabel.color = warningTextColor;
            warningLabel.raycastTarget = false;

            SetWarningVisible(warningVisible);
        }

        private void SetWarningVisible(bool isVisible)
        {
            if (warningRoot == null)
            {
                warningVisible = false;
                return;
            }

            if (isVisible && !warningVisible)
            {
                warningRoot.gameObject.SetActive(true);
                if (warningFollower != null && warningFollower.enabled)
                {
                    warningFollower.SnapToTarget();
                }
            }
            else if (!isVisible)
            {
                if (warningLabel != null)
                {
                    warningLabel.text = string.Empty;
                }

                warningRoot.gameObject.SetActive(false);
            }

            warningVisible = isVisible;
        }

        private bool IsUnderScreenSpaceCanvas()
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                return false;
            }

            Canvas rootCanvas = parentCanvas.rootCanvas != null ? parentCanvas.rootCanvas : parentCanvas;
            return rootCanvas.renderMode != RenderMode.WorldSpace;
        }

        private static void ConfigureHudLabel(TMP_Text label, Vector2 size, Vector2 anchoredPosition)
        {
            if (label == null)
            {
                return;
            }

            if (label.transform is RectTransform rectTransform)
            {
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.sizeDelta = size;
                rectTransform.anchoredPosition = anchoredPosition;
            }
        }

        private static string FormatStopwatchTime(float seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int minutes = totalSeconds / 60;
            int remainingSeconds = totalSeconds % 60;
            return $"{minutes:00}:{remainingSeconds:00}";
        }

        private static string GetPracticeModeLabel(PracticeMode practiceMode)
        {
            return practiceMode switch
            {
                PracticeMode.GuidedPractice => "Guided Practice",
                PracticeMode.FreePractice => "Free Practice",
                PracticeMode.EvaluationMode => "Evaluation Mode",
                PracticeMode.ChallengeMode => "Challenge Mode",
                _ => "Practice"
            };
        }
    }
}
