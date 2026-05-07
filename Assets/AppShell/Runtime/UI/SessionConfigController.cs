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

        private void Awake()
        {
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

        }

        private void OnEnable()
        {
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
                    $"Difficulty: {snapshot.DifficultyLevel}  |  Audience: {snapshot.AudiencePreset}\n" +
                    $"Feedback: {snapshot.FeedbackLevel}\n" +
                    $"Systems: {BuildCompactSystemsSummary(snapshot)}" +
                    launchNote;
            }
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
    }
}
