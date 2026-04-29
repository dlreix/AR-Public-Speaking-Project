using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;

namespace VRPublicSpeaking.AppShell.UI
{
    public class SessionConfigController : MonoBehaviour
    {
        [SerializeField] private AppRuntimeState runtimeState;

        [Header("Duration")]
        [SerializeField] private Slider durationSlider;
        [SerializeField] private TMP_Text durationValueLabel;
        [SerializeField] private bool durationSliderUsesMinutes = true;

        [Header("Dropdowns")]
        [SerializeField] private TMP_Dropdown difficultyDropdown;
        [SerializeField] private TMP_Dropdown audienceDropdown;
        [SerializeField] private TMP_Dropdown feedbackDropdown;

        [Header("Feature Toggles")]
        [SerializeField] private Toggle eyeTrackingToggle;
        [SerializeField] private Toggle gazeScoringToggle;
        [SerializeField] private Toggle performanceScoringToggle;
        [SerializeField] private Toggle voiceAnalysisToggle;
        [SerializeField] private Toggle postureAnalysisToggle;

        [Header("Preview")]
        [SerializeField] private TMP_Text summaryPreviewLabel;
        [SerializeField] private Button recommendedDemoSetupButton;
        [SerializeField] private TMP_Text recommendedDemoSetupLabel;
        [SerializeField] private bool createRecommendedDemoSetupButton = true;

        private bool recommendedPresetApplied;

        private void Awake()
        {
            EnsureRecommendedDemoSetupButton();

            if (durationSlider != null)
            {
                durationSlider.onValueChanged.AddListener(_ => HandleConfigInputChanged());
            }

            if (difficultyDropdown != null)
            {
                difficultyDropdown.onValueChanged.AddListener(_ => HandleConfigInputChanged());
            }

            if (audienceDropdown != null)
            {
                audienceDropdown.onValueChanged.AddListener(_ => HandleConfigInputChanged());
            }

            if (feedbackDropdown != null)
            {
                feedbackDropdown.onValueChanged.AddListener(_ => HandleConfigInputChanged());
            }

            AddToggleListener(eyeTrackingToggle);
            AddToggleListener(gazeScoringToggle);
            AddToggleListener(performanceScoringToggle);
            AddToggleListener(voiceAnalysisToggle);
            AddToggleListener(postureAnalysisToggle);

            if (recommendedDemoSetupButton != null)
            {
                recommendedDemoSetupButton.onClick.AddListener(ApplyRecommendedDemoSetup);
            }
        }

        private void OnEnable()
        {
            EnsureRecommendedDemoSetupButton();
            LoadFromRuntime();
        }

        public SessionConfig BuildConfigSnapshot()
        {
            if (runtimeState == null)
            {
                runtimeState = AppRuntimeState.GetOrCreate();
            }

            SessionConfig config = runtimeState != null ? runtimeState.GetSessionConfigCopy() : new SessionConfig();

            if (durationSlider != null)
            {
                config.SessionDurationSeconds = durationSliderUsesMinutes
                    ? durationSlider.value * 60f
                    : durationSlider.value;
            }

            if (difficultyDropdown != null)
            {
                config.DifficultyLevel = (SessionDifficulty)Mathf.Clamp(
                    difficultyDropdown.value,
                    0,
                    System.Enum.GetValues(typeof(SessionDifficulty)).Length - 1);
            }

            if (audienceDropdown != null)
            {
                config.AudiencePreset = (AudiencePreset)Mathf.Clamp(
                    audienceDropdown.value,
                    0,
                    System.Enum.GetValues(typeof(AudiencePreset)).Length - 1);
            }

            if (feedbackDropdown != null)
            {
                config.FeedbackLevel = (FeedbackLevel)Mathf.Clamp(
                    feedbackDropdown.value,
                    0,
                    System.Enum.GetValues(typeof(FeedbackLevel)).Length - 1);
            }

            if (eyeTrackingToggle != null)
            {
                config.EyeTrackingEnabled = eyeTrackingToggle.isOn;
            }

            if (gazeScoringToggle != null)
            {
                config.GazeScoringEnabled = gazeScoringToggle.isOn;
            }

            if (performanceScoringToggle != null)
            {
                config.PerformanceScoringEnabled = performanceScoringToggle.isOn;
            }

            if (voiceAnalysisToggle != null)
            {
                config.VoiceAnalysisEnabled = voiceAnalysisToggle.isOn;
            }

            if (postureAnalysisToggle != null)
            {
                config.PostureAnalysisEnabled = postureAnalysisToggle.isOn;
            }

            config.Normalize();
            return config;
        }

        public void PushCurrentUIToRuntime()
        {
            SessionConfig config = BuildConfigSnapshot();
            runtimeState?.ApplySessionConfig(config);
        }

        public void LoadFromRuntime()
        {
            if (runtimeState == null)
            {
                runtimeState = AppRuntimeState.GetOrCreate();
            }

            SessionConfig config = runtimeState != null ? runtimeState.GetSessionConfigCopy() : new SessionConfig();

            if (durationSlider != null)
            {
                float sliderValue = durationSliderUsesMinutes
                    ? config.SessionDurationSeconds / 60f
                    : config.SessionDurationSeconds;
                sliderValue = Mathf.Clamp(sliderValue, durationSlider.minValue, durationSlider.maxValue);
                durationSlider.SetValueWithoutNotify(sliderValue);
            }

            if (difficultyDropdown != null)
            {
                difficultyDropdown.SetValueWithoutNotify((int)config.DifficultyLevel);
            }

            if (audienceDropdown != null)
            {
                audienceDropdown.SetValueWithoutNotify((int)config.AudiencePreset);
            }

            if (feedbackDropdown != null)
            {
                feedbackDropdown.SetValueWithoutNotify((int)config.FeedbackLevel);
            }

            SetToggleWithoutNotify(eyeTrackingToggle, config.EyeTrackingEnabled);
            SetToggleWithoutNotify(gazeScoringToggle, config.GazeScoringEnabled);
            SetToggleWithoutNotify(performanceScoringToggle, config.PerformanceScoringEnabled);
            SetToggleWithoutNotify(voiceAnalysisToggle, config.VoiceAnalysisEnabled);
            SetToggleWithoutNotify(postureAnalysisToggle, config.PostureAnalysisEnabled);

            RefreshSummaryPreview();
        }

        public void RefreshSummaryPreview()
        {
            SessionConfig snapshot = BuildConfigSnapshot();

            if (durationValueLabel != null)
            {
                durationValueLabel.text = snapshot.GetDurationDisplay();
            }

            if (summaryPreviewLabel != null)
            {
                string environmentName = !string.IsNullOrWhiteSpace(snapshot.SelectedEnvironmentName)
                    ? snapshot.SelectedEnvironmentName
                    : "Choose an environment";
                string launchNote = !snapshot.HasAnyScoringEnabled
                    ? "\nNote: core scoring is off, so results will be limited."
                    : string.Empty;

                summaryPreviewLabel.text =
                    $"Room: {environmentName}\n" +
                    $"Mode: {snapshot.PracticeMode}  |  {snapshot.GetDurationDisplay()}\n" +
                    $"Context: {snapshot.DifficultyLevel} / {snapshot.AudiencePreset}\n" +
                    $"Feedback: {snapshot.FeedbackLevel}\n" +
                    $"Systems: {BuildCompactSystemsSummary(snapshot)}" +
                    (recommendedPresetApplied ? "\nPreset: Demo-ready setup applied." : string.Empty) +
                    launchNote;
            }
        }

        public void ApplyRecommendedDemoSetup()
        {
            recommendedPresetApplied = true;

            if (durationSlider != null)
            {
                float recommendedDuration = durationSliderUsesMinutes ? 3f : 180f;
                durationSlider.SetValueWithoutNotify(Mathf.Clamp(
                    recommendedDuration,
                    durationSlider.minValue,
                    durationSlider.maxValue));
            }

            if (difficultyDropdown != null)
            {
                difficultyDropdown.SetValueWithoutNotify((int)SessionDifficulty.Normal);
            }

            if (audienceDropdown != null)
            {
                audienceDropdown.SetValueWithoutNotify((int)AudiencePreset.Neutral);
            }

            if (feedbackDropdown != null)
            {
                feedbackDropdown.SetValueWithoutNotify((int)FeedbackLevel.Standard);
            }

            SetToggleWithoutNotify(eyeTrackingToggle, true);
            SetToggleWithoutNotify(gazeScoringToggle, true);
            SetToggleWithoutNotify(performanceScoringToggle, true);
            SetToggleWithoutNotify(voiceAnalysisToggle, false);
            SetToggleWithoutNotify(postureAnalysisToggle, false);

            PushCurrentUIToRuntime();
            RefreshSummaryPreview();
        }

        private static void SetToggleWithoutNotify(Toggle toggle, bool value)
        {
            if (toggle != null)
            {
                toggle.SetIsOnWithoutNotify(value);
            }
        }

        private void AddToggleListener(Toggle toggle)
        {
            if (toggle != null)
            {
                toggle.onValueChanged.AddListener(_ => HandleConfigInputChanged());
            }
        }

        private void HandleConfigInputChanged()
        {
            recommendedPresetApplied = false;
            RefreshSummaryPreview();
        }

        private static string BuildCompactSystemsSummary(SessionConfig snapshot)
        {
            if (snapshot == null)
            {
                return "None";
            }

            System.Collections.Generic.List<string> enabledSystems = new System.Collections.Generic.List<string>();

            if (snapshot.EyeTrackingEnabled)
            {
                enabledSystems.Add("Eye");
            }

            if (snapshot.GazeScoringEnabled)
            {
                enabledSystems.Add("Gaze");
            }

            if (snapshot.PerformanceScoringEnabled)
            {
                enabledSystems.Add("Performance");
            }

            if (snapshot.VoiceAnalysisEnabled)
            {
                enabledSystems.Add("Voice");
            }

            if (snapshot.PostureAnalysisEnabled)
            {
                enabledSystems.Add("Posture");
            }

            if (enabledSystems.Count == 0)
            {
                return "None";
            }

            if (enabledSystems.Count <= 3)
            {
                return string.Join(", ", enabledSystems);
            }

            return string.Join(", ", enabledSystems.GetRange(0, 3)) + $" +{enabledSystems.Count - 3}";
        }

        private void EnsureRecommendedDemoSetupButton()
        {
            if (!createRecommendedDemoSetupButton || recommendedDemoSetupButton != null)
            {
                return;
            }

            Transform parent = summaryPreviewLabel != null && summaryPreviewLabel.transform.parent != null
                ? summaryPreviewLabel.transform.parent
                : transform;

            Transform existing = parent.Find("RecommendedDemoSetupButton");
            GameObject buttonObject = existing != null
                ? existing.gameObject
                : new GameObject("RecommendedDemoSetupButton", typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0f);
            rectTransform.anchoredPosition = new Vector2(0f, 10f);
            rectTransform.sizeDelta = new Vector2(0f, 42f);

            Image background = buttonObject.GetComponent<Image>();
            if (background == null)
            {
                background = buttonObject.AddComponent<Image>();
            }

            background.color = new Color(0.21f, 0.63f, 0.96f, 1f);

            recommendedDemoSetupButton = buttonObject.GetComponent<Button>();
            if (recommendedDemoSetupButton == null)
            {
                recommendedDemoSetupButton = buttonObject.AddComponent<Button>();
            }

            recommendedDemoSetupButton.targetGraphic = background;

            LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = buttonObject.AddComponent<LayoutElement>();
            }

            layoutElement.preferredHeight = 42f;
            layoutElement.minHeight = 38f;

            Transform labelTransform = buttonObject.transform.Find("Label");
            GameObject labelObject = labelTransform != null
                ? labelTransform.gameObject
                : new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(14f, 0f);
            labelRect.offsetMax = new Vector2(-14f, 0f);

            recommendedDemoSetupLabel = labelObject.GetComponent<TMP_Text>();
            if (recommendedDemoSetupLabel == null)
            {
                recommendedDemoSetupLabel = labelObject.AddComponent<TextMeshProUGUI>();
            }

            recommendedDemoSetupLabel.text = "Use Recommended Demo Setup";
            recommendedDemoSetupLabel.fontSize = 17f;
            recommendedDemoSetupLabel.fontStyle = FontStyles.Bold;
            recommendedDemoSetupLabel.alignment = TextAlignmentOptions.Center;
            recommendedDemoSetupLabel.color = Color.white;
            recommendedDemoSetupLabel.raycastTarget = false;

            ColorBlock colors = recommendedDemoSetupButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.96f, 0.99f, 1f, 1f);
            colors.pressedColor = new Color(0.78f, 0.90f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            recommendedDemoSetupButton.colors = colors;
        }
    }
}
