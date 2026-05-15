using UnityEngine;
using UnityEngine.SceneManagement;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;
using VRPublicSpeaking.AppShell.Flow;

namespace VRPublicSpeaking.AppShell.Results
{
    public class DashboardAdapter : MonoBehaviour
    {
        private const string DefaultMainHubSceneName = "MainHubScene";

        [SerializeField] private string mainHubSceneName = DefaultMainHubSceneName;
        [SerializeField] private string dashboardLoadingMessage = "Opening dashboard...";

        public bool IsAvailable => HasHubFlowInScene() || CanOpenMainHubScene();

        public void OpenDashboard()
        {
            TryOpenDashboard();
        }

        public bool TryOpenDashboard()
        {
            if (TryOpenDashboardInCurrentHub())
            {
                return true;
            }

            AppRuntimeState runtimeState = AppRuntimeState.GetOrCreate();
            if (runtimeState == null)
            {
                Debug.LogWarning("[DashboardAdapter] Runtime state is unavailable, so the dashboard cannot open.");
                return false;
            }

            string sceneName = ResolveMainHubSceneName(runtimeState);
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("[DashboardAdapter] Main hub scene name is empty, so the dashboard cannot open.");
                return false;
            }

            runtimeState.RequestHubPanel(AppPanelType.Dashboard);

            if (SceneManager.GetActiveScene().name == sceneName)
            {
                return TryOpenDashboardInCurrentHub();
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"[DashboardAdapter] Main hub scene '{sceneName}' is not in the build settings.");
                return false;
            }

            TransitionManager transitionManager = TransitionManager.Instance;
            if (transitionManager != null)
            {
                transitionManager.LoadScene(sceneName, dashboardLoadingMessage);
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }

            return true;
        }

        private bool TryOpenDashboardInCurrentHub()
        {
            AppFlowManager flowManager = FindFirstObjectByType<AppFlowManager>(FindObjectsInactive.Include);
            if (flowManager == null)
            {
                return false;
            }

            flowManager.OpenDashboardPanel();
            return true;
        }

        private static bool HasHubFlowInScene()
        {
            return FindFirstObjectByType<AppFlowManager>(FindObjectsInactive.Include) != null;
        }

        private bool CanOpenMainHubScene()
        {
            AppRuntimeState runtimeState = AppRuntimeState.GetOrCreate();
            string sceneName = ResolveMainHubSceneName(runtimeState);
            return !string.IsNullOrWhiteSpace(sceneName) &&
                (SceneManager.GetActiveScene().name == sceneName || Application.CanStreamedLevelBeLoaded(sceneName));
        }

        private string ResolveMainHubSceneName(AppRuntimeState runtimeState)
        {
            if (runtimeState != null && !string.IsNullOrWhiteSpace(runtimeState.MainHubSceneName))
            {
                return runtimeState.MainHubSceneName.Trim();
            }

            return string.IsNullOrWhiteSpace(mainHubSceneName)
                ? DefaultMainHubSceneName
                : mainHubSceneName.Trim();
        }
    }
}
