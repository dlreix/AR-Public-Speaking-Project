using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRPublicSpeaking.AppShell.Flow;

namespace VRPublicSpeaking.AppShell.Results
{
    public class DashboardAdapter : MonoBehaviour
    {
        private const string DefaultDashboardSceneName = "Overview";

        [SerializeField] private MonoBehaviour dashboardController;
        [SerializeField] private GameObject dashboardRoot;
        [SerializeField] private string dashboardSceneName = DefaultDashboardSceneName;
        [SerializeField] private string dashboardLoadingMessage = "Opening dashboard...";
        [SerializeField] private string openMessage = "Open";
        [SerializeField] private string[] alternateOpenMessages =
        {
            "OpenDashboard",
            "ShowDashboard",
            "Show"
        };
        [SerializeField] private bool activateControllerGameObject = true;

        public bool IsAvailable => dashboardRoot != null ||
            HasControllerEntryPoint() ||
            CanOpenDashboardScene();

        public void OpenDashboard()
        {
            TryOpenDashboard();
        }

        public bool TryOpenDashboard()
        {
            bool opened = false;

            if (dashboardRoot != null)
            {
                dashboardRoot.SetActive(true);
                opened = true;
            }

            if (dashboardController != null)
            {
                if (activateControllerGameObject && !dashboardController.gameObject.activeSelf)
                {
                    dashboardController.gameObject.SetActive(true);
                    opened = true;
                }

                if (TryInvokeOpenMessage())
                {
                    opened = true;
                }
            }

            if (!opened)
            {
                opened = TryOpenDashboardScene();
            }

            if (!opened)
            {
                Debug.LogWarning("[DashboardAdapter] No dashboard integration has been wired yet.");
            }

            return opened;
        }

        private bool HasControllerEntryPoint()
        {
            return dashboardController != null &&
                (activateControllerGameObject || TryResolveOpenMethod(out _));
        }

        private bool TryInvokeOpenMessage()
        {
            if (!TryResolveOpenMethod(out MethodInfo method))
            {
                return false;
            }

            try
            {
                method.Invoke(dashboardController, null);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[DashboardAdapter] Failed to invoke '{method.Name}' on '{dashboardController.GetType().Name}'. {exception.Message}");
                return false;
            }
        }

        private bool TryResolveOpenMethod(out MethodInfo method)
        {
            method = null;
            if (dashboardController == null)
            {
                return false;
            }

            Type controllerType = dashboardController.GetType();
            foreach (string methodName in EnumerateOpenMessages())
            {
                method = controllerType.GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);

                if (method != null)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryOpenDashboardScene()
        {
            string sceneName = ResolveDashboardSceneName();
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"[DashboardAdapter] Dashboard scene '{sceneName}' is not in the build settings.");
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

        private bool CanOpenDashboardScene()
        {
            string sceneName = ResolveDashboardSceneName();
            return !string.IsNullOrWhiteSpace(sceneName) &&
                Application.CanStreamedLevelBeLoaded(sceneName);
        }

        private string ResolveDashboardSceneName()
        {
            return string.IsNullOrWhiteSpace(dashboardSceneName)
                ? DefaultDashboardSceneName
                : dashboardSceneName.Trim();
        }

        private IEnumerable<string> EnumerateOpenMessages()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            string primaryMessage = openMessage?.Trim();
            if (!string.IsNullOrWhiteSpace(primaryMessage) && seen.Add(primaryMessage))
            {
                yield return primaryMessage;
            }

            if (alternateOpenMessages == null)
            {
                yield break;
            }

            for (int index = 0; index < alternateOpenMessages.Length; index++)
            {
                string methodName = alternateOpenMessages[index]?.Trim();
                if (!string.IsNullOrWhiteSpace(methodName) && seen.Add(methodName))
                {
                    yield return methodName;
                }
            }
        }
    }
}
