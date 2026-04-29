using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VRPublicSpeaking.AppShell.Data
{
    [Serializable]
    public class SessionConfig
    {
        public const float DefaultDurationSeconds = 300f;
        public const float MinDurationSeconds = 60f;
        public const float MaxDurationSeconds = 900f;

        [SerializeField] private PracticeMode practiceMode = PracticeMode.GuidedPractice;
        [SerializeField] private string selectedEnvironmentId = string.Empty;
        [SerializeField] private string selectedEnvironmentName = string.Empty;
        [SerializeField] private string selectedEnvironmentSceneName = string.Empty;
        [SerializeField] private string selectedSpawnPointName = string.Empty;
        [SerializeField] private float sessionDurationSeconds = DefaultDurationSeconds;
        [SerializeField] private SessionDifficulty difficultyLevel = SessionDifficulty.Normal;
        [SerializeField] private AudiencePreset audiencePreset = AudiencePreset.Neutral;
        [SerializeField] private bool eyeTrackingEnabled = true;
        [SerializeField] private bool gazeScoringEnabled = true;
        [SerializeField] private bool performanceScoringEnabled = true;
        [SerializeField] private bool voiceAnalysisEnabled = true;
        [SerializeField] private bool postureAnalysisEnabled = false;
        [SerializeField] private FeedbackLevel feedbackLevel = FeedbackLevel.Standard;
        [SerializeField] private bool autoStartOnSceneLoad = true;

        public PracticeMode PracticeMode
        {
            get => practiceMode;
            set => practiceMode = value;
        }

        public string SelectedEnvironmentId
        {
            get => selectedEnvironmentId;
            set => selectedEnvironmentId = value ?? string.Empty;
        }

        public string SelectedEnvironmentName
        {
            get => selectedEnvironmentName;
            set => selectedEnvironmentName = value ?? string.Empty;
        }

        public string SelectedEnvironmentSceneName
        {
            get => selectedEnvironmentSceneName;
            set => selectedEnvironmentSceneName = value ?? string.Empty;
        }

        public string SelectedSpawnPointName
        {
            get => selectedSpawnPointName;
            set => selectedSpawnPointName = value ?? string.Empty;
        }

        public float SessionDurationSeconds
        {
            get => sessionDurationSeconds;
            set => sessionDurationSeconds = Mathf.Clamp(value, MinDurationSeconds, MaxDurationSeconds);
        }

        public SessionDifficulty DifficultyLevel
        {
            get => difficultyLevel;
            set => difficultyLevel = value;
        }

        public AudiencePreset AudiencePreset
        {
            get => audiencePreset;
            set => audiencePreset = value;
        }

        public bool EyeTrackingEnabled
        {
            get => eyeTrackingEnabled;
            set => eyeTrackingEnabled = value;
        }

        public bool GazeScoringEnabled
        {
            get => gazeScoringEnabled;
            set => gazeScoringEnabled = value;
        }

        public bool PerformanceScoringEnabled
        {
            get => performanceScoringEnabled;
            set => performanceScoringEnabled = value;
        }

        public bool VoiceAnalysisEnabled
        {
            get => voiceAnalysisEnabled;
            set => voiceAnalysisEnabled = value;
        }

        public bool PostureAnalysisEnabled
        {
            get => postureAnalysisEnabled;
            set => postureAnalysisEnabled = value;
        }

        public FeedbackLevel FeedbackLevel
        {
            get => feedbackLevel;
            set => feedbackLevel = value;
        }

        public bool AutoStartOnSceneLoad
        {
            get => autoStartOnSceneLoad;
            set => autoStartOnSceneLoad = value;
        }

        public bool HasSelectedEnvironment => !string.IsNullOrWhiteSpace(SelectedEnvironmentSceneName);
        public bool HasAnyScoringEnabled => gazeScoringEnabled || performanceScoringEnabled;

        public void ApplyEnvironment(AppEnvironmentDefinition environmentDefinition)
        {
            if (environmentDefinition == null)
            {
                SelectedEnvironmentId = string.Empty;
                SelectedEnvironmentName = string.Empty;
                SelectedEnvironmentSceneName = string.Empty;
                SelectedSpawnPointName = string.Empty;
                return;
            }

            SelectedEnvironmentId = environmentDefinition.Id;
            SelectedEnvironmentName = environmentDefinition.DisplayName;
            SelectedEnvironmentSceneName = environmentDefinition.SceneName;
            SelectedSpawnPointName = environmentDefinition.SpawnPointName;
        }

        public SessionConfig Clone()
        {
            return (SessionConfig)MemberwiseClone();
        }

        public void Normalize()
        {
            sessionDurationSeconds = Mathf.Clamp(
                sessionDurationSeconds <= 0f ? DefaultDurationSeconds : sessionDurationSeconds,
                MinDurationSeconds,
                MaxDurationSeconds);
        }

        public string GetDurationDisplay()
        {
            float minutes = SessionDurationSeconds / 60f;
            return minutes >= 1f ? $"{minutes:0.#} min" : $"{SessionDurationSeconds:0} sec";
        }

        public string GetEnabledSystemsSummary()
        {
            var enabledSystems = new List<string>();

            if (EyeTrackingEnabled)
            {
                enabledSystems.Add("Eye Tracking");
            }

            if (GazeScoringEnabled)
            {
                enabledSystems.Add("Gaze Scoring");
            }

            if (PerformanceScoringEnabled)
            {
                enabledSystems.Add("Performance Scoring");
            }

            if (VoiceAnalysisEnabled)
            {
                enabledSystems.Add("Voice Analysis");
            }

            if (PostureAnalysisEnabled)
            {
                enabledSystems.Add("Posture Analysis");
            }

            return enabledSystems.Count > 0
                ? string.Join(", ", enabledSystems)
                : "No analysis systems enabled";
        }

        public string BuildLaunchSummary(string fallbackEnvironmentName = "No environment selected")
        {
            string environmentName = HasSelectedEnvironment && !string.IsNullOrWhiteSpace(SelectedEnvironmentName)
                ? SelectedEnvironmentName
                : fallbackEnvironmentName;

            var summary = new StringBuilder();
            summary.AppendLine($"Mode: {PracticeMode}");
            summary.AppendLine($"Environment: {environmentName}");
            summary.AppendLine($"Duration: {GetDurationDisplay()}");
            summary.AppendLine($"Difficulty: {DifficultyLevel}");
            summary.AppendLine($"Audience: {AudiencePreset}");
            summary.AppendLine($"Feedback: {FeedbackLevel}");
            summary.AppendLine($"Systems: {GetEnabledSystemsSummary()}");
            return summary.ToString().TrimEnd();
        }

        public void ResetToDefaults()
        {
            practiceMode = PracticeMode.GuidedPractice;
            selectedEnvironmentId = string.Empty;
            selectedEnvironmentName = string.Empty;
            selectedEnvironmentSceneName = string.Empty;
            selectedSpawnPointName = string.Empty;
            sessionDurationSeconds = DefaultDurationSeconds;
            difficultyLevel = SessionDifficulty.Normal;
            audiencePreset = AudiencePreset.Neutral;
            eyeTrackingEnabled = true;
            gazeScoringEnabled = true;
            performanceScoringEnabled = true;
            voiceAnalysisEnabled = true;
            postureAnalysisEnabled = false;
            feedbackLevel = FeedbackLevel.Standard;
            autoStartOnSceneLoad = true;
        }
    }
}
