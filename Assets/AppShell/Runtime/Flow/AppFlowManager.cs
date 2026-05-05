using UnityEngine;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;
using VRPublicSpeaking.AppShell.Integration;
using VRPublicSpeaking.AppShell.Results;
using VRPublicSpeaking.AppShell.UI;
using Unity.XR.CoreUtils;

namespace VRPublicSpeaking.AppShell.Flow
{
    public class AppFlowManager : MonoBehaviour
    {
        [SerializeField] private AppRuntimeState runtimeState;
        [SerializeField] private UIStateController uiStateController;
        [SerializeField] private EnvironmentSelectionController environmentSelectionController;
        [SerializeField] private SessionConfigController sessionConfigController;
        [SerializeField] private ReadyPanelPresenter readyPanelPresenter;
        [SerializeField] private ProgressPanelPresenter progressPanelPresenter;
        [SerializeField] private SettingsPanelPresenter settingsPanelPresenter;
        [SerializeField] private ResultsSummaryPresenter resultsSummaryPresenter;
        [SerializeField] private SessionLaunchController sessionLaunchController;
        [SerializeField] private ShellSceneRigController shellSceneRigController;
        [SerializeField] private Canvas worldSpaceHubCanvas;
        [SerializeField] private Vector3 fixedCanvasOffset = new Vector3(0f, -0.12f, 2.1f);
        [SerializeField] private bool keepMenuFixedToView = true;
        [SerializeField] private float menuFollowPositionSpeed = 14f;
        [SerializeField] private float menuFollowRotationSpeed = 14f;
        [SerializeField] private bool keepHubRigStationary = true;
        [SerializeField] private float hubCameraYOffset = 1.36f;
        [SerializeField] private string mainHubSceneName = "MainHubScene";
        [SerializeField] private string resultsSceneName = string.Empty;
        [SerializeField] private bool useWalkableTutorialHub = true;
        [SerializeField] private bool installTutorialPanels = true;

        private MainHubTutorialController tutorialController;

        private void Start()
        {
            if (runtimeState == null)
            {
                runtimeState = AppRuntimeState.GetOrCreate();
            }

            runtimeState?.ConfigureNavigation(mainHubSceneName, resultsSceneName);

            if (sessionConfigController != null)
            {
                sessionConfigController.LoadFromRuntime();
            }

            if (readyPanelPresenter != null)
            {
                readyPanelPresenter.RefreshSummary();
            }

            OpenRequestedStartupPanel();
            ConfigureShellSceneRigController();
            ConfigureTutorialHub();
        }

        public void OpenLoginPanel()
        {
            uiStateController?.ShowPanel(AppPanelType.Login);
        }

        public void OpenHomePanel()
        {
            uiStateController?.ShowPanel(AppPanelType.Home);
        }

        public void OpenPracticeModePanel()
        {
            uiStateController?.ShowPanel(AppPanelType.PracticeMode);
        }

        public void OpenEnvironmentSelectionPanel()
        {
            environmentSelectionController?.Refresh();
            uiStateController?.ShowPanel(AppPanelType.EnvironmentSelection);
        }

        public void OpenSessionSetupPanel()
        {
            sessionConfigController?.LoadFromRuntime();
            uiStateController?.ShowPanel(AppPanelType.SessionSetup);
        }

        public void OpenReadyPanel()
        {
            sessionConfigController?.PushCurrentUIToRuntime();
            readyPanelPresenter?.RefreshSummary();
            uiStateController?.ShowPanel(AppPanelType.Ready);
        }

        public void OpenProgressPanel()
        {
            progressPanelPresenter?.Refresh();
            uiStateController?.ShowPanel(AppPanelType.Progress);
        }

        public void OpenSettingsPanel()
        {
            settingsPanelPresenter?.Refresh();
            uiStateController?.ShowPanel(AppPanelType.Settings);
        }

        public void OpenResultsPanel()
        {
            resultsSummaryPresenter?.Refresh();
            uiStateController?.ShowPanel(AppPanelType.ResultsSummary);
        }

        public void SelectPracticeMode(PracticeMode practiceMode)
        {
            if (runtimeState == null)
            {
                runtimeState = AppRuntimeState.GetOrCreate();
            }

            SessionConfig config = runtimeState != null ? runtimeState.GetSessionConfigCopy() : new SessionConfig();
            config.PracticeMode = practiceMode;
            runtimeState?.ApplySessionConfig(config);

            OpenEnvironmentSelectionPanel();
        }

        public void ContinueFromEnvironmentSelection()
        {
            environmentSelectionController?.ConfirmSelection();
            OpenSessionSetupPanel();
        }

        public void ContinueFromSessionSetup()
        {
            OpenReadyPanel();
        }

        public void LaunchSession()
        {
            sessionLaunchController?.LaunchSelectedSession();
        }

        public void GoBack()
        {
            uiStateController?.GoBack();
        }

        public void QuitApplication()
        {
#if UNITY_EDITOR
            Debug.Log("[AppFlowManager] Exit requested from HomePanel.");
#else
            Application.Quit();
#endif
        }

        private void OpenRequestedStartupPanel()
        {
            if (DataManager.Instance != null && DataManager.Instance.currentUser == "DefaultUser")
            {
                OpenLoginPanel(); // DEÐÝÞTÝ: Eskiden OpenHomePanel idi, artýk ilk Login açýlacak
                return;
            }

            AppPanelType requestedPanel = runtimeState.CurrentRuntimeState.RequestedHubPanel;
            runtimeState.ResetRequestedHubPanel();

            switch (requestedPanel)
            {
                case AppPanelType.ResultsSummary:
                    OpenResultsPanel();
                    break;

                case AppPanelType.EnvironmentSelection:
                    OpenEnvironmentSelectionPanel();
                    break;

                case AppPanelType.SessionSetup:
                    OpenSessionSetupPanel();
                    break;

                case AppPanelType.Ready:
                    OpenReadyPanel();
                    break;

                case AppPanelType.Progress:
                    OpenProgressPanel();
                    break;

                case AppPanelType.Settings:
                    OpenSettingsPanel();
                    break;

                case AppPanelType.Home:
                    OpenHomePanel(); // Baþka bir ekrandan özel olarak Home'a dönmek istenirse
                    break;

                default:
                    OpenLoginPanel(); // DEÐÝÞTÝ: Eskiden OpenHomePanel idi, artýk varsayýlanýmýz Login
                    break;
            }
        }

        private void ConfigureShellSceneRigController()
        {
            Canvas canvas = ResolveHubCanvas();
            if (shellSceneRigController == null)
            {
                shellSceneRigController = GetComponent<ShellSceneRigController>();
            }

            if (shellSceneRigController == null)
            {
                shellSceneRigController = gameObject.AddComponent<ShellSceneRigController>();
            }

            bool keepCanvasInView = useWalkableTutorialHub ? false : keepMenuFixedToView;
            bool keepRigLocked = useWalkableTutorialHub ? false : keepHubRigStationary;

            shellSceneRigController.Configure(
                canvas,
                "MainHubBackdrop",
                keepCanvasInView,
                fixedCanvasOffset,
                menuFollowPositionSpeed,
                menuFollowRotationSpeed,
                keepRigLocked,
                hubCameraYOffset,
                XROrigin.TrackingOriginMode.Device);
            shellSceneRigController.InitializeNow();
        }

        private void ConfigureTutorialHub()
        {
            if (!useWalkableTutorialHub || !installTutorialPanels)
            {
                return;
            }

            Canvas canvas = ResolveHubCanvas();
            if (tutorialController == null)
            {
                tutorialController = GetComponent<MainHubTutorialController>();
            }

            if (tutorialController == null)
            {
                tutorialController = gameObject.AddComponent<MainHubTutorialController>();
            }

            tutorialController.Configure(canvas);
        }

        private Canvas ResolveHubCanvas()
        {
            if (worldSpaceHubCanvas != null)
            {
                return worldSpaceHubCanvas;
            }

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < canvases.Length; index++)
            {
                Canvas candidate = canvases[index];
                if (candidate == null || candidate.renderMode != RenderMode.WorldSpace)
                {
                    continue;
                }

                if (candidate.name == "WorldSpaceHubCanvas")
                {
                    worldSpaceHubCanvas = candidate;
                    return worldSpaceHubCanvas;
                }
            }

            return null;
        }
    }
}
