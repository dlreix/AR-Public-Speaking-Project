using TMPro;
using UnityEngine;
using System.Collections.Generic;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;
using VRPublicSpeaking.AppShell.Flow;

namespace VRPublicSpeaking.AppShell.UI
{
    public class ReadyPanelPresenter : MonoBehaviour
    {
        [SerializeField] private AppRuntimeState runtimeState;
        [SerializeField] private AppFlowManager appFlowManager;
        [SerializeField] private TMP_Text summaryLabel;
        [SerializeField] private TMP_Text warningLabel;

        private void OnEnable()
        {
            RefreshSummary();
        }

        public void RefreshSummary()
        {
            if (runtimeState == null)
            {
                runtimeState = AppRuntimeState.GetOrCreate();
            }

            if (summaryLabel == null)
            {
                return;
            }

            SessionConfig config = runtimeState != null ? runtimeState.GetSessionConfigCopy() : new SessionConfig();
            AppEnvironmentDefinition environmentDefinition = runtimeState != null ? runtimeState.SelectedEnvironment : null;
            string fallbackEnvironmentName = environmentDefinition != null
                ? environmentDefinition.DisplayName
                : "No environment selected";

            summaryLabel.text = config.BuildLaunchSummary(fallbackEnvironmentName);
            SetWarning(BuildWarningText(config, environmentDefinition));
        }

        public void SetWarning(string message)
        {
            if (warningLabel != null)
            {
                warningLabel.text = message ?? string.Empty;
            }
        }

        public void StartSession()
        {
            appFlowManager?.LaunchSession();
        }

        public void GoBack()
        {
            appFlowManager?.GoBack();
        }

        private static string BuildWarningText(SessionConfig config, AppEnvironmentDefinition environmentDefinition)
        {
            var warnings = new List<string>();

            if (environmentDefinition == null || !config.HasSelectedEnvironment)
            {
                warnings.Add("Select a launch-ready environment before starting the session.");
            }
            else if (!environmentDefinition.Available)
            {
                string reason = string.IsNullOrWhiteSpace(environmentDefinition.AvailabilityReason)
                    ? "The selected environment is currently unavailable."
                    : environmentDefinition.AvailabilityReason;
                warnings.Add(reason);
            }
            else if (environmentDefinition.IsMisconfigured)
            {
                warnings.Add("The selected environment is visible in the shell, but its scene wiring is incomplete.");
            }

            if (config.SessionDurationSeconds < SessionConfig.MinDurationSeconds ||
                config.SessionDurationSeconds > SessionConfig.MaxDurationSeconds)
            {
                warnings.Add(
                    $"Duration must stay between {SessionConfig.MinDurationSeconds / 60f:0} and {SessionConfig.MaxDurationSeconds / 60f:0} minutes.");
            }

            if (!config.HasAnyScoringEnabled)
            {
                warnings.Add("Gaze Scoring and Performance Scoring are both off. The session can still launch, but the results summary will be limited.");
            }

            if (config.EyeTrackingEnabled && !config.GazeScoringEnabled)
            {
                warnings.Add("Eye Tracking is enabled while Gaze Scoring is off. Tracking can still run, but gaze-based scoring will not contribute to the score.");
            }

            return string.Join("\n", warnings).Trim();
        }
    }
}
