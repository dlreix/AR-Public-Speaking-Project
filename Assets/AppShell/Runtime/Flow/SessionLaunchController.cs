using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;
using VRPublicSpeaking.AppShell.UI;

namespace VRPublicSpeaking.AppShell.Flow
{
    public class SessionLaunchController : MonoBehaviour
    {
        [SerializeField] private AppRuntimeState runtimeState;
        [SerializeField] private EnvironmentSelectionController environmentSelectionController;
        [SerializeField] private SessionConfigController sessionConfigController;
        [SerializeField] private TransitionManager transitionManager;
        [SerializeField] private TMP_Text validationLabel;

        public bool LaunchSelectedSession()
        {
            if (runtimeState == null)
            {
                runtimeState = AppRuntimeState.GetOrCreate();
            }

            SessionConfig config = sessionConfigController != null
                ? sessionConfigController.BuildConfigSnapshot()
                : (runtimeState != null ? runtimeState.GetSessionConfigCopy() : new SessionConfig());
            config.Normalize();
            runtimeState?.ApplySessionConfig(config);

            if (!TryResolveEnvironment(out AppEnvironmentDefinition environmentDefinition))
            {
                SetValidationMessage("Select an available environment before launching a session.");
                return false;
            }

            if (!ValidateConfiguration(environmentDefinition, config, out string validationMessage))
            {
                SetValidationMessage(validationMessage);
                return false;
            }

            SetValidationMessage(string.Empty);
            runtimeState?.PrepareSessionLaunch(environmentDefinition, config);
            transitionManager ??= TransitionManager.Instance;

            if (transitionManager != null)
            {
                transitionManager.LoadScene(
                    environmentDefinition.SceneName,
                    $"Loading {environmentDefinition.DisplayName}...");
            }
            else
            {
                SceneManager.LoadScene(environmentDefinition.SceneName);
            }

            return true;
        }

        private bool TryResolveEnvironment(out AppEnvironmentDefinition environmentDefinition)
        {
            if (environmentSelectionController != null &&
                environmentSelectionController.TryGetSelectedEnvironment(out environmentDefinition))
            {
                return true;
            }

            environmentDefinition = runtimeState != null ? runtimeState.SelectedEnvironment : null;
            return environmentDefinition != null && environmentDefinition.Available && environmentDefinition.IsConfigured;
        }

        private static bool ValidateConfiguration(
            AppEnvironmentDefinition environmentDefinition,
            SessionConfig config,
            out string validationMessage)
        {
            if (environmentDefinition == null)
            {
                validationMessage = "Environment is missing.";
                return false;
            }

            if (!environmentDefinition.Available || !environmentDefinition.IsConfigured)
            {
                if (!environmentDefinition.Available)
                {
                    validationMessage = string.IsNullOrWhiteSpace(environmentDefinition.AvailabilityReason)
                        ? "Selected environment is currently unavailable."
                        : environmentDefinition.AvailabilityReason;
                }
                else
                {
                    validationMessage = "Selected environment is visible in the shell, but its scene wiring is incomplete.";
                }
                return false;
            }

            if (config == null)
            {
                validationMessage = "Session configuration is missing.";
                return false;
            }

            config.Normalize();

            if (config.SessionDurationSeconds < SessionConfig.MinDurationSeconds ||
                config.SessionDurationSeconds > SessionConfig.MaxDurationSeconds)
            {
                validationMessage =
                    $"Session duration must stay between {SessionConfig.MinDurationSeconds / 60f:0} and {SessionConfig.MaxDurationSeconds / 60f:0} minutes.";
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(environmentDefinition.SceneName))
            {
                validationMessage = $"Scene '{environmentDefinition.SceneName}' is not in the build settings.";
                return false;
            }

            validationMessage = string.Empty;
            return true;
        }

        private void SetValidationMessage(string message)
        {
            if (validationLabel != null)
            {
                validationLabel.text = message ?? string.Empty;
            }
        }
    }
}
