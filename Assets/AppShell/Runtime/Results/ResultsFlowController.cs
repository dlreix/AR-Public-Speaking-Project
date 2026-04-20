using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;
using VRPublicSpeaking.AppShell.Flow;

namespace VRPublicSpeaking.AppShell.Results
{
    public class ResultsFlowController : MonoBehaviour
    {
        [SerializeField] private AppRuntimeState runtimeState;
        [SerializeField] private TransitionManager transitionManager;
        [SerializeField] private DashboardAdapter dashboardAdapter;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private string retryLoadingMessage = "Retrying session...";
        [SerializeField] private string changeEnvironmentLoadingMessage = "Returning to environments...";
        [SerializeField] private string returnToHubLoadingMessage = "Returning to hub...";

        private bool routeInProgress;

        private void OnEnable()
        {
            routeInProgress = false;
            SetStatus("Retry keeps the current setup. Change Environment returns to room selection.");
        }

        public void RetryLastSession()
        {
            if (!TryResolveRuntimeState())
            {
                SetStatus("Runtime state is unavailable, so the session cannot be retried yet.");
                return;
            }

            SessionConfig config = runtimeState.GetSessionConfigCopy();
            AppEnvironmentDefinition environmentDefinition = runtimeState.SelectedEnvironment;

            if (environmentDefinition == null)
            {
                SetStatus("Retry is unavailable because no environment is selected.");
                return;
            }

            if (!environmentDefinition.Available)
            {
                SetStatus(string.IsNullOrWhiteSpace(environmentDefinition.AvailabilityReason)
                    ? $"'{environmentDefinition.DisplayName}' is currently unavailable."
                    : environmentDefinition.AvailabilityReason);
                return;
            }

            if (!environmentDefinition.IsConfigured)
            {
                SetStatus(
                    $"'{environmentDefinition.DisplayName}' is visible in the shell, but its scene wiring is incomplete.");
                return;
            }

            if (!ValidateSceneTarget(
                    environmentDefinition.SceneName,
                    "Retry is blocked because the selected environment scene is not in the build settings."))
            {
                return;
            }

            config.Normalize();
            runtimeState.PrepareSessionLaunch(environmentDefinition, config);
            BeginRoute(
                environmentDefinition.SceneName,
                $"Retrying {environmentDefinition.DisplayName}...",
                retryLoadingMessage);
        }

        public void ChangeEnvironment()
        {
            if (!TryResolveRuntimeState())
            {
                SetStatus("Main hub state is unavailable, so environment selection cannot be reopened yet.");
                return;
            }

            if (!ValidateSceneTarget(
                    runtimeState.MainHubSceneName,
                    "Main hub scene is not available in build settings, so the environment picker cannot open yet."))
            {
                return;
            }

            runtimeState.RequestHubPanel(AppPanelType.EnvironmentSelection);
            BeginRoute(
                runtimeState.MainHubSceneName,
                "Returning to environments...",
                changeEnvironmentLoadingMessage);
        }

        public void ReturnToHub()
        {
            if (!TryResolveRuntimeState())
            {
                SetStatus("Main hub state is unavailable, so the shell cannot return home yet.");
                return;
            }

            if (!ValidateSceneTarget(
                    runtimeState.MainHubSceneName,
                    "Main hub scene is not available in build settings, so the shell cannot return home yet."))
            {
                return;
            }

            runtimeState.RequestHubPanel(AppPanelType.Home);
            BeginRoute(
                runtimeState.MainHubSceneName,
                "Returning to hub...",
                returnToHubLoadingMessage);
        }

        public void OpenDashboard()
        {
            if (dashboardAdapter != null && dashboardAdapter.TryOpenDashboard())
            {
                SetStatus("Dashboard entry opened.");
                return;
            }

            SetStatus("Dashboard is not connected yet. The results summary remains available.");
        }

        private bool TryResolveRuntimeState()
        {
            runtimeState ??= AppRuntimeState.GetOrCreate();
            return runtimeState != null;
        }

        private bool ValidateSceneTarget(string sceneName, string missingSceneMessage)
        {
            if (routeInProgress)
            {
                SetStatus("A transition is already in progress.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                SetStatus(missingSceneMessage);
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SetStatus(missingSceneMessage);
                Debug.LogWarning($"[ResultsFlowController] Scene '{sceneName}' is not in the build settings.");
                return false;
            }

            return true;
        }

        private void BeginRoute(string sceneName, string statusMessage, string loadingMessage)
        {
            routeInProgress = true;
            SetStatus(statusMessage);
            transitionManager ??= TransitionManager.Instance;

            if (transitionManager != null)
            {
                transitionManager.LoadScene(sceneName, loadingMessage);
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message ?? string.Empty;
            }
        }
    }
}
