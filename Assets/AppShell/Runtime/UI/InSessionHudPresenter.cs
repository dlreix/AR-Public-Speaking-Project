using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        [SerializeField] private TMP_Text liveScoreLabel;
        [SerializeField] private WorldSpaceCanvasFollower hudFollower;
        [SerializeField] private RectTransform warningRoot;
        [SerializeField] private CanvasGroup warningCanvasGroup;
        [SerializeField] private WorldSpaceCanvasFollower warningFollower;
        [SerializeField] private TMP_Text warningLabel;
        [SerializeField] private GazeScoringSystem gazeScoringSystem;
        [SerializeField] private bool hideWhenSessionInactive = true;
        [SerializeField] private bool showLiveWarnings;
        [SerializeField] private bool showLiveScore = true;
        [SerializeField] private string inactiveStatusText = "Session idle";
        [SerializeField] private Color warningTextColor = new Color(0.98f, 0.74f, 0.39f, 1f);
        [SerializeField] private Color warningBackgroundColor = new Color(0.08f, 0.11f, 0.15f, 0.92f);
        [SerializeField] private Vector3 hudFollowOffset = new Vector3(0f, -0.48f, 0.98f);
        [SerializeField] private Vector3 warningFollowOffset = new Vector3(0f, -0.12f, 0.92f);
        [SerializeField] private Vector3 raisedSceneHudFollowOffset = new Vector3(0f, -0.26f, 0.98f);
        [SerializeField] private Vector3 raisedSceneWarningFollowOffset = new Vector3(0f, 0.04f, 0.92f);
        [SerializeField] private Vector2 vrHudSize = new Vector2(280f, 96f);
        [SerializeField] private Vector2 desktopHudSize = new Vector2(500f, 160f);
        [SerializeField] private Vector2 desktopWarningAnchoredPosition = new Vector2(0f, -135f);
        [SerializeField] private Vector2 desktopWarningSize = new Vector2(540f, 90f);
        [SerializeField] private float minimumHudTargetY = 1.55f;
        [SerializeField] private float raisedSceneMinimumHudTargetY = 1.82f;
        [SerializeField] private float vrTimerFontSize = 28f;
        [SerializeField] private float vrScoreFontSize = 14f;
        [SerializeField] private float vrStatusFontSize = 11f;
        [SerializeField] private float desktopTimerFontSize = 40f;
        [SerializeField] private float desktopScoreFontSize = 16f;
        [SerializeField] private float desktopWarningFontSize = 16f;

        private bool warningVisible;
        private bool hudVisible;

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

            if (gazeScoringSystem == null)
            {
                gazeScoringSystem = FindFirstObjectByType<GazeScoringSystem>(FindObjectsInactive.Include);
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
                bool isScreenSpace = IsUnderScreenSpaceCanvas();
                statusLabel.text = isActive
                    ? BuildStatusText(runtime, config, targetDurationSeconds, isScreenSpace)
                    : inactiveStatusText;
            }

            if (liveScoreLabel != null)
            {
                liveScoreLabel.gameObject.SetActive(showLiveScore);
                liveScoreLabel.text = BuildLiveScoreText(isActive);
            }

            RefreshLiveWarning(showLiveWarnings && isActive && !hideForOverlay);
        }

        private void SetVisible(bool isVisible)
        {
            if (canvasGroup == null)
            {
                return;
            }

            if (isVisible && !hudVisible && hudFollower != null && hudFollower.enabled)
            {
                hudFollower.SnapToTarget();
            }

            canvasGroup.alpha = isVisible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            hudVisible = isVisible;
        }

        private void RefreshLiveWarning(bool canShow)
        {
            EnsureHudLayout();

            if (!showLiveWarnings)
            {
                SetWarningVisible(false);
                return;
            }

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
            bool useScreenSpacePlacement = IsUnderScreenSpaceCanvas();
            RectTransform hudRect = transform as RectTransform;
            if (hudRect != null)
            {
                hudRect.anchorMin = new Vector2(0.5f, 0.5f);
                hudRect.anchorMax = new Vector2(0.5f, 0.5f);
                hudRect.pivot = new Vector2(0.5f, 0.5f);
                hudRect.sizeDelta = useScreenSpacePlacement ? desktopHudSize : vrHudSize;
                if (useScreenSpacePlacement)
                {
                    hudRect.anchoredPosition = new Vector2(0f, -340f);
                }
            }

            if (timerLabel != null)
            {
                ConfigureHudLabel(
                    timerLabel,
                    useScreenSpacePlacement ? new Vector2(460f, 62f) : new Vector2(250f, 40f),
                    useScreenSpacePlacement ? new Vector2(0f, 30f) : new Vector2(0f, 24f));
                timerLabel.fontSize = useScreenSpacePlacement ? desktopTimerFontSize : vrTimerFontSize;
                timerLabel.alignment = TextAlignmentOptions.Center;
                timerLabel.overflowMode = TextOverflowModes.Ellipsis;
                timerLabel.raycastTarget = false;
            }

            EnsureLiveScoreLabel();

            if (liveScoreLabel != null)
            {
                ConfigureHudLabel(
                    liveScoreLabel,
                    useScreenSpacePlacement ? new Vector2(460f, 34f) : new Vector2(250f, 22f),
                    useScreenSpacePlacement ? new Vector2(0f, -18f) : new Vector2(0f, -8f));
                liveScoreLabel.fontSize = useScreenSpacePlacement ? desktopScoreFontSize : vrScoreFontSize;
                liveScoreLabel.alignment = TextAlignmentOptions.Center;
                liveScoreLabel.textWrappingMode = TextWrappingModes.NoWrap;
                liveScoreLabel.overflowMode = TextOverflowModes.Ellipsis;
                liveScoreLabel.color = liveScoreLabel.color.a > 0f ? liveScoreLabel.color : new Color(0.98f, 0.84f, 0.42f, 1f);
                liveScoreLabel.raycastTarget = false;
            }

            if (statusLabel != null)
            {
                ConfigureHudLabel(
                    statusLabel,
                    useScreenSpacePlacement ? new Vector2(460f, 54f) : new Vector2(260f, 28f),
                    useScreenSpacePlacement ? new Vector2(0f, -56f) : new Vector2(0f, -34f));
                statusLabel.fontSize = useScreenSpacePlacement ? 18f : vrStatusFontSize;
                statusLabel.alignment = TextAlignmentOptions.Center;
                statusLabel.textWrappingMode = useScreenSpacePlacement ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                statusLabel.overflowMode = TextOverflowModes.Ellipsis;
                statusLabel.color = statusLabel.color.a > 0f ? statusLabel.color : new Color(0.70f, 0.78f, 0.90f, 1f);
                statusLabel.raycastTarget = false;
            }

            Image background = GetComponent<Image>();
            if (background != null)
            {
                background.raycastTarget = false;
            }

            EnsureHudFollower(useScreenSpacePlacement);
            EnsureWarningPanel();
        }

        private void EnsureLiveScoreLabel()
        {
            if (liveScoreLabel == null)
            {
                Transform existingLabel = transform.Find("LiveScoreLabel");
                if (existingLabel != null)
                {
                    liveScoreLabel = existingLabel.GetComponent<TMP_Text>();
                }
            }

            if (liveScoreLabel != null)
            {
                return;
            }

            GameObject labelObject = new GameObject("LiveScoreLabel", typeof(RectTransform));
            labelObject.transform.SetParent(transform, false);
            liveScoreLabel = labelObject.AddComponent<TextMeshProUGUI>();
            liveScoreLabel.text = "Score --";
        }

        private void EnsureHudFollower(bool useScreenSpacePlacement)
        {
            if (hudFollower == null)
            {
                hudFollower = GetComponent<WorldSpaceCanvasFollower>();
            }

            if (useScreenSpacePlacement)
            {
                if (hudFollower != null)
                {
                    hudFollower.enabled = false;
                }

                transform.localScale = Vector3.one;
                return;
            }

            if (hudFollower == null)
            {
                hudFollower = gameObject.AddComponent<WorldSpaceCanvasFollower>();
            }

            hudFollower.enabled = true;
            hudFollower.Configure(null, ResolveHudFollowOffset(), false, true, 16f, 18f);
            hudFollower.SetMinimumTargetY(ResolveMinimumHudTargetY());
            hudFollower.SetFollowContinuously(true);
        }

        private void EnsureWarningPanel()
        {
            Transform warningParent = transform.parent != null ? transform.parent : transform;

            if (warningRoot == null)
            {
                Transform existingRoot = warningParent.Find("LiveWarningPanel");
                warningRoot = existingRoot as RectTransform;
            }

            if (!showLiveWarnings)
            {
                SetWarningVisible(false);
                if (warningFollower != null)
                {
                    warningFollower.enabled = false;
                }

                return;
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
                warningFollower.Configure(null, ResolveWarningFollowOffset(), false, true, 14f, 16f);
                warningFollower.SetMinimumTargetY(ResolveMinimumHudTargetY());
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

        private Vector3 ResolveHudFollowOffset()
        {
            return UsesRaisedVrPlacementForActiveScene()
                ? raisedSceneHudFollowOffset
                : hudFollowOffset;
        }

        private Vector3 ResolveWarningFollowOffset()
        {
            return UsesRaisedVrPlacementForActiveScene()
                ? raisedSceneWarningFollowOffset
                : warningFollowOffset;
        }

        private float ResolveMinimumHudTargetY()
        {
            return UsesRaisedVrPlacementForActiveScene()
                ? Mathf.Max(minimumHudTargetY, raisedSceneMinimumHudTargetY)
                : minimumHudTargetY;
        }

        private static bool UsesRaisedVrPlacementForActiveScene()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            return sceneName.Contains("Conference") || sceneName.Contains("Meeting");
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

        private string BuildLiveScoreText(bool isActive)
        {
            if (!showLiveScore)
            {
                return string.Empty;
            }

            if (!isActive)
            {
                return "Score --";
            }

            if (gazeScoringSystem == null)
            {
                gazeScoringSystem = FindFirstObjectByType<GazeScoringSystem>(FindObjectsInactive.Include);
            }

            return gazeScoringSystem != null
                ? $"Score {gazeScoringSystem.GazeScore:0}"
                : "Score --";
        }

        private static string BuildStatusText(
            SessionRuntimeState runtime,
            SessionConfig config,
            float targetDurationSeconds,
            bool isScreenSpace)
        {
            string pausePrefix = runtime != null && runtime.SessionPaused ? "Paused" : "Target";
            if (!isScreenSpace)
            {
                return $"{pausePrefix} {FormatStopwatchTime(targetDurationSeconds)} | Menu/B hold: pause";
            }

            return $"{pausePrefix}: {FormatStopwatchTime(targetDurationSeconds)} | Esc pause | {GetPracticeModeLabel(config?.PracticeMode ?? PracticeMode.GuidedPractice)}";
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
