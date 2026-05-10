using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;
using VRPublicSpeaking.AppShell.Results;

namespace VRPublicSpeaking.AppShell.UI
{
    public class EnvironmentSessionOverlayController : MonoBehaviour
    {
        [SerializeField] private AppRuntimeState runtimeState;
        [SerializeField] private MainController mainController;
        [SerializeField] private InSessionHudPresenter hudPresenter;
        [SerializeField] private WorldSpaceCanvasFollower overlayFollower;
        [SerializeField] private CanvasGroup dimmerCanvasGroup;
        [SerializeField] private AppPanelView pausePanel;
        [SerializeField] private AppPanelView resultsPanel;
        [SerializeField] private AppPanelView dashboardPanel;
        [SerializeField] private ResultsSummaryPresenter resultsSummaryPresenter;
        [SerializeField] private ResultsFlowController resultsFlowController;
        [SerializeField] private MainHubDashboardPresenter dashboardPresenter;
        [SerializeField] private RectTransform dashboardHost;
        [SerializeField] private GameObject dashboardContentRoot;
        [SerializeField] private TMP_Text pauseStatusLabel;
        [SerializeField] private TMP_Text dashboardScoreLabel;
        [SerializeField] private TMP_Text dashboardMetricsLabel;
        [SerializeField] private TMP_Text dashboardFocusLabel;
        [SerializeField] private TMP_Text dashboardRecommendationsLabel;
        [SerializeField] [Range(0f, 1f)] private float pauseDimAlpha = 0.42f;
        [SerializeField] [Range(0f, 1f)] private float resultsDimAlpha = 0.56f;
        [SerializeField] private string pauseStatusText =
            "Session paused. Timing, tracking, and scoring are safely on hold.";
        [SerializeField] private string pauseUnavailableStatusText =
            "Pause is available only while a live session is running.";
        [SerializeField] private bool applyVrReadabilityDefaults = true;
        [SerializeField] private Vector3 readableOverlayOffset = new Vector3(0f, 0.02f, 1.05f);
        [SerializeField] private float minimumVrOverlayTargetY = 1.62f;
        [SerializeField] private Vector3 raisedSceneOverlayOffset = new Vector3(0f, 0.20f, 1.05f);
        [SerializeField] private float raisedSceneMinimumVrOverlayTargetY = 1.92f;
        [SerializeField] private float pauseMinimumFontSize = 23f;
        [SerializeField] private float pauseMinimumButtonHeight = 94f;
        [SerializeField] private Vector2 pauseMinimumPanelSize = new Vector2(1120f, 800f);
        [SerializeField] private float resultsMinimumFontSize = 22f;
        [SerializeField] private float resultsMinimumButtonHeight = 92f;
        [SerializeField] private Vector2 resultsMinimumPanelSize = new Vector2(1180f, 820f);
        [SerializeField] private float dashboardMinimumButtonHeight = 92f;
        [SerializeField] private Vector2 dashboardMinimumPanelSize = new Vector2(1180f, 820f);
        [SerializeField] private float overlayDynamicPixelsPerUnit = 32f;
        [SerializeField] private bool useScreenSpaceOverlayWhenNoXrDevice = true;
        [SerializeField] private Vector2 desktopOverlayReferenceResolution = new Vector2(1540f, 1040f);
        [SerializeField] private float vrNavigationAxisThreshold = 0.55f;
        [SerializeField] private float vrNavigationRepeatDelay = 0.22f;
        [SerializeField] private float vrSelectedButtonScale = 1.08f;
        [SerializeField] private float vrCancelHoldSeconds = 0.6f;

        private MainController subscribedMainController;
        private int pausePanelShownFrame = -1;
        private int resultsPanelShownFrame = -1;
        private readonly List<RaycastResult> pointerRaycastResults = new List<RaycastResult>(16);
        private readonly List<Button> vrNavigationButtons = new List<Button>(12);
        private GraphicRaycaster overlayRaycaster;
        private EventSystem cachedEventSystem;
        private Canvas overlayCanvas;
        private Button vrFocusedButton;
        private Vector3 vrFocusedButtonBaseScale = Vector3.one;
        private bool readabilityDefaultsApplied;
        private bool desktopOverlayModeApplied;
        private bool legacyVrClickWasPressed;
        private bool vrCancelHoldConsumed;
        private float nextVrNavigationTime;
        private float vrNavigationFocusLockUntil;
        private float vrCancelHeldSeconds;
        private static readonly List<UnityEngine.XR.InputDevice> LegacyVrControllers =
            new List<UnityEngine.XR.InputDevice>();

        private void Awake()
        {
            AutoResolveIfNeeded();
            ResolveUiInputSupport();
        }

        private void Start()
        {
            ApplyClosedState();
        }

        private void OnEnable()
        {
            AutoResolveIfNeeded();
            AttachMainControllerEvents();
            hudPresenter?.Refresh();
        }

        private void OnDisable()
        {
            ClearVrFocusHighlight();
            DetachMainControllerEvents();
        }

        private void OnDestroy()
        {
            DetachMainControllerEvents();
        }

        private void Update()
        {
            ResolveUiInputSupport();

            if (Keyboard.current != null)
            {
                HandleDesktopOverlayShortcuts();
            }

            HandleDesktopOverlayPointerFallback();
            UpdateVrFocusHighlight();
            HandleVrOverlayNavigation();
            HandleVrOverlayCancel();
            HandleVrOverlayPointerFallback();
        }

        public void Configure(AppRuntimeState appRuntimeState, MainController controller)
        {
            runtimeState = appRuntimeState ?? AppRuntimeState.GetOrCreate();
            AutoResolveIfNeeded();

            if (controller != null)
            {
                mainController = controller;
            }

            AttachMainControllerEvents();
            ApplyClosedState();
        }

        public void HandleSessionStarted()
        {
            HideTransientPanels();
            overlayFollower?.SnapToTarget();
            hudPresenter?.Refresh();
        }

        public bool ShowResultsOverlay()
        {
            AutoResolveIfNeeded();

            if (resultsPanel == null || resultsSummaryPresenter == null)
            {
                Debug.LogWarning("[EnvironmentSessionOverlayController] Results overlay wiring is incomplete.");
                return false;
            }

            HidePausePanelInternal(updateRuntimeState: true);
            HideDashboardPanelInternal(updateRuntimeState: false);
            ApplyReadableOverlayDefaults(force: true);
            overlayFollower?.SnapToTarget();
            resultsSummaryPresenter.Refresh();
            BringPanelToFront(resultsPanel);
            resultsPanel.Show();
            FocusFirstButton(resultsPanel);
            ApplyDimmer(resultsDimAlpha);
            resultsPanelShownFrame = Time.frameCount;

            if (runtimeState != null)
            {
                runtimeState.SetPauseMenuVisible(false);
                runtimeState.SetResultsOverlayVisible(true);
            }

            hudPresenter?.Refresh();
            return true;
        }

        public bool ShowDashboardPanel()
        {
            AutoResolveIfNeeded();
            EnsureDashboardPanel();

            if (dashboardPanel == null)
            {
                Debug.LogWarning("[EnvironmentSessionOverlayController] VR dashboard panel could not be created.");
                return false;
            }

            HidePausePanelInternal(updateRuntimeState: true);
            HideResultsPanelInternal(updateRuntimeState: false);
            RefreshDashboardPanel();
            ApplyReadableOverlayDefaults(force: true);
            overlayFollower?.SnapToTarget();
            BringPanelToFront(dashboardPanel);
            dashboardPanel.Show();
            FocusFirstButton(dashboardPanel);
            ApplyDimmer(resultsDimAlpha);

            if (runtimeState != null)
            {
                runtimeState.SetPauseMenuVisible(false);
                runtimeState.SetResultsOverlayVisible(true);
            }

            hudPresenter?.Refresh();
            return true;
        }

        public void HideResultsOverlay()
        {
            HideResultsPanelInternal(updateRuntimeState: true);
            hudPresenter?.Refresh();
        }

        public void TogglePauseMenu()
        {
            if (runtimeState != null && runtimeState.CurrentRuntimeState.ResultsOverlayVisible)
            {
                return;
            }

            if (runtimeState != null && runtimeState.CurrentRuntimeState.PauseMenuVisible)
            {
                ClosePauseMenu();
                return;
            }

            OpenPauseMenu();
        }

        public void OpenPauseMenu()
        {
            AutoResolveIfNeeded();

            if (runtimeState != null && runtimeState.CurrentRuntimeState.ResultsOverlayVisible)
            {
                return;
            }

            if (mainController == null || !mainController.IsSessionRunning)
            {
                UpdatePauseStatus(pauseUnavailableStatusText);
                return;
            }

            if (mainController.IsSessionPaused)
            {
                ShowPausePanelInternal();
                return;
            }

            mainController.PauseSessionFromShell();
        }

        public void ClosePauseMenu()
        {
            if (mainController != null && mainController.IsSessionPaused)
            {
                mainController.ResumeSessionFromShell();
            }

            HidePausePanelInternal(updateRuntimeState: true);
            runtimeState?.MarkSessionResumed();
            hudPresenter?.Refresh();
        }

        public void ResumeSession()
        {
            ClosePauseMenu();
        }

        public void RestartSession()
        {
            if (resultsFlowController == null)
            {
                UpdatePauseStatus("Restart route is unavailable because results flow is not wired.");
                return;
            }

            HideTransientPanels();

            if (mainController != null)
            {
                mainController.AbortSessionFromShell();
            }

            runtimeState?.MarkSessionCancelled();
            resultsFlowController.RetryLastSession();
        }

        public void EndSession()
        {
            if (mainController == null || !mainController.IsSessionRunning)
            {
                UpdatePauseStatus("End Session is unavailable because no active session is running.");
                return;
            }

            HidePausePanelInternal(updateRuntimeState: true);
            runtimeState?.MarkSessionResumed();
            mainController.StopSessionFromShell();
        }

        public void ReturnToHub()
        {
            if (resultsFlowController == null)
            {
                UpdatePauseStatus("Return route is unavailable because results flow is not wired.");
                return;
            }

            HideTransientPanels();

            if (mainController != null)
            {
                mainController.AbortSessionFromShell();
            }

            runtimeState?.MarkSessionCancelled();
            resultsFlowController.ReturnToHub();
        }

        public void HideTransientPanels()
        {
            HidePausePanelInternal(updateRuntimeState: true);
            HideResultsPanelInternal(updateRuntimeState: true);
            HideDashboardPanelInternal(updateRuntimeState: true);
            ApplyDimmer(0f);
            hudPresenter?.Refresh();
        }

        private void AutoResolveIfNeeded()
        {
            runtimeState ??= AppRuntimeState.GetOrCreate();
            mainController ??= FindFirstObjectByType<MainController>(FindObjectsInactive.Include);
            hudPresenter ??= GetComponentInChildren<InSessionHudPresenter>(true);
            overlayFollower ??= GetComponent<WorldSpaceCanvasFollower>();
            resultsSummaryPresenter ??= GetComponentInChildren<ResultsSummaryPresenter>(true);
            resultsFlowController ??= GetComponentInChildren<ResultsFlowController>(true);

            if (pausePanel == null || resultsPanel == null || dashboardPanel == null)
            {
                AppPanelView[] panels = GetComponentsInChildren<AppPanelView>(true);
                for (int index = 0; index < panels.Length; index++)
                {
                    if (panels[index] == null)
                    {
                        continue;
                    }

                    if (pausePanel == null && panels[index].PanelType == AppPanelType.PauseOverlay)
                    {
                        pausePanel = panels[index];
                    }
                    else if (resultsPanel == null && panels[index].PanelType == AppPanelType.ResultsSummary)
                    {
                        resultsPanel = panels[index];
                    }
                    else if (dashboardPanel == null && panels[index].gameObject.name == "VrDashboardOverlayPanel")
                    {
                        dashboardPanel = panels[index];
                    }
                }
            }

            if (dimmerCanvasGroup == null)
            {
                Transform dimmer = transform.Find("Dimmer");
                if (dimmer != null)
                {
                    dimmerCanvasGroup = dimmer.GetComponent<CanvasGroup>();
                }
            }

            ApplyReadableOverlayDefaults();
        }

        private void AttachMainControllerEvents()
        {
            if (subscribedMainController == mainController)
            {
                return;
            }

            DetachMainControllerEvents();

            if (mainController == null)
            {
                return;
            }

            subscribedMainController = mainController;
            subscribedMainController.SessionStarted += HandleMainControllerSessionStarted;
            subscribedMainController.SessionPaused += HandleMainControllerSessionPaused;
            subscribedMainController.SessionResumed += HandleMainControllerSessionResumed;
        }

        private void DetachMainControllerEvents()
        {
            if (subscribedMainController == null)
            {
                return;
            }

            subscribedMainController.SessionStarted -= HandleMainControllerSessionStarted;
            subscribedMainController.SessionPaused -= HandleMainControllerSessionPaused;
            subscribedMainController.SessionResumed -= HandleMainControllerSessionResumed;
            subscribedMainController = null;
        }

        private void HandleMainControllerSessionStarted()
        {
            HandleSessionStarted();
        }

        private void HandleMainControllerSessionPaused()
        {
            runtimeState?.MarkSessionPaused();
            ShowPausePanelInternal();
        }

        private void HandleMainControllerSessionResumed()
        {
            runtimeState?.MarkSessionResumed();
            HidePausePanelInternal(updateRuntimeState: true);
            hudPresenter?.Refresh();
        }

        private void ShowPausePanelInternal()
        {
            HideResultsPanelInternal(updateRuntimeState: true);
            HideDashboardPanelInternal(updateRuntimeState: true);

            if (pausePanel == null)
            {
                return;
            }

            UpdatePauseStatus(pauseStatusText);
            ApplyReadableOverlayDefaults(force: true);
            EnsurePauseButtonWiring();
            overlayFollower?.SnapToTarget();
            BringPanelToFront(pausePanel);
            pausePanel.Show();
            FocusFirstButton(pausePanel);
            ApplyDimmer(pauseDimAlpha);
            pausePanelShownFrame = Time.frameCount;
            runtimeState?.SetPauseMenuVisible(true);
            runtimeState?.SetResultsOverlayVisible(false);
            hudPresenter?.Refresh();
        }

        private void HidePausePanelInternal(bool updateRuntimeState)
        {
            pausePanel?.Hide();
            pausePanelShownFrame = -1;

            if (updateRuntimeState)
            {
                runtimeState?.SetPauseMenuVisible(false);
            }

            if ((runtimeState == null || !runtimeState.CurrentRuntimeState.ResultsOverlayVisible) &&
                !IsDashboardPanelVisible())
            {
                ApplyDimmer(0f);
            }
        }

        private void HideResultsPanelInternal(bool updateRuntimeState)
        {
            resultsPanel?.Hide();
            resultsPanelShownFrame = -1;

            if (updateRuntimeState)
            {
                runtimeState?.SetResultsOverlayVisible(false);
            }

            if ((runtimeState == null || !runtimeState.CurrentRuntimeState.PauseMenuVisible) &&
                !IsDashboardPanelVisible())
            {
                ApplyDimmer(0f);
            }
        }

        private void HideDashboardPanelInternal(bool updateRuntimeState)
        {
            dashboardPanel?.Hide();

            if (updateRuntimeState)
            {
                runtimeState?.SetResultsOverlayVisible(false);
            }

            if (runtimeState == null || !runtimeState.CurrentRuntimeState.PauseMenuVisible)
            {
                ApplyDimmer(0f);
            }
        }

        private void ApplyClosedState()
        {
            HidePausePanelInternal(updateRuntimeState: true);
            HideResultsPanelInternal(updateRuntimeState: true);
            HideDashboardPanelInternal(updateRuntimeState: true);
            ApplyDimmer(0f);
            hudPresenter?.Refresh();
        }

        private void ResolveUiInputSupport()
        {
            if (overlayCanvas == null)
            {
                overlayCanvas = GetComponent<Canvas>();
            }

            ApplyDesktopPreviewCanvasMode();
            VrUiUsabilityUtility.EnsureCanvasInputSupport(gameObject, overlayCanvas);

            if (overlayRaycaster == null)
            {
                overlayRaycaster = GetComponent<GraphicRaycaster>();
            }

            SyncOverlayEventCamera();

            if (cachedEventSystem == null)
            {
                cachedEventSystem = EventSystem.current;
                if (cachedEventSystem == null)
                {
                    cachedEventSystem = FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
                }
            }
        }

        private void ApplyDimmer(float alpha)
        {
            if (dimmerCanvasGroup == null)
            {
                return;
            }

            dimmerCanvasGroup.alpha = Mathf.Clamp01(alpha);
            // The dimmer is visual-only. If it blocks raycasts, world-space overlay
            // buttons behind it stop receiving both mouse and tracked-device clicks.
            dimmerCanvasGroup.interactable = false;
            dimmerCanvasGroup.blocksRaycasts = false;
        }

        private static void BringPanelToFront(AppPanelView panel)
        {
            if (panel != null)
            {
                panel.transform.SetAsLastSibling();
            }
        }

        private void UpdatePauseStatus(string message)
        {
            if (pauseStatusLabel != null)
            {
                pauseStatusLabel.text = BuildPauseStatusMessage(message);
            }
        }

        private static string BuildPauseStatusMessage(string message)
        {
            string baseMessage = message ?? string.Empty;
            if (Keyboard.current == null)
            {
                return $"{baseMessage}\n\nOpen pause in VR: press controller Menu once, or hold B/Y (secondary) for 0.6s during a live session.\nIn this panel: thumbstick selects, trigger/A confirms, hold B/Y or Menu to go back.";
            }

            return $"{baseMessage}\n\nPC: Esc opens pause. Enter/1 Resume  R/2 Restart  E/3 End  H/4 Hub\nVR: Menu opens pause, or hold B/Y for 0.6s. Thumbstick selects, trigger/A confirms.";
        }

        private void EnsureDashboardPanel()
        {
            bool createdPanel = false;
            if (dashboardPanel == null)
            {
                Transform existingPanel = transform.Find("VrDashboardOverlayPanel");
                if (existingPanel != null)
                {
                    dashboardPanel = existingPanel.GetComponent<AppPanelView>();
                    if (dashboardPanel == null)
                    {
                        dashboardPanel = existingPanel.gameObject.AddComponent<AppPanelView>();
                    }
                }
                else
                {
                    GameObject panelRoot = new GameObject(
                        "VrDashboardOverlayPanel",
                        typeof(RectTransform),
                        typeof(CanvasGroup),
                        typeof(Image),
                        typeof(AppPanelView));
                    panelRoot.transform.SetParent(transform, false);
                    dashboardPanel = panelRoot.GetComponent<AppPanelView>();
                    createdPanel = true;
                }
            }

            if (dashboardPanel == null)
            {
                return;
            }

            dashboardPanel.SetPanelType(AppPanelType.Dashboard);

            RectTransform panelRect = dashboardPanel.transform as RectTransform;
            ConfigureCenteredRect(panelRect, new Vector2(1260f, 830f), new Vector2(0f, -6f));

            Image panelBackground = dashboardPanel.GetComponent<Image>();
            if (panelBackground == null)
            {
                panelBackground = dashboardPanel.gameObject.AddComponent<Image>();
            }
            panelBackground.color = new Color(0.035f, 0.055f, 0.085f, 0.96f);
            panelBackground.raycastTarget = true;

            Outline outline = dashboardPanel.GetComponent<Outline>();
            if (outline == null)
            {
                outline = dashboardPanel.gameObject.AddComponent<Outline>();
            }
            outline.effectColor = new Color(0.12f, 0.78f, 0.96f, 0.28f);
            outline.effectDistance = new Vector2(1f, -1f);

            VerticalLayoutGroup oldVerticalLayout = dashboardPanel.GetComponent<VerticalLayoutGroup>();
            if (oldVerticalLayout != null)
            {
                oldVerticalLayout.enabled = false;
            }

            EnsureFullDashboardOverlayContent();
            VrUiUsabilityUtility.ApplyReadablePanel(
                dashboardPanel,
                16f,
                Mathf.Max(70f, dashboardMinimumButtonHeight * 0.75f),
                dashboardMinimumPanelSize);
            if (createdPanel)
            {
                dashboardPanel.gameObject.SetActive(false);
            }
        }

        private void EnsureFullDashboardOverlayContent()
        {
            if (dashboardPanel == null)
            {
                return;
            }

            if (dashboardContentRoot == null)
            {
                Transform existingRoot = dashboardPanel.transform.Find("VrDashboardFullRoot");
                dashboardContentRoot = existingRoot != null ? existingRoot.gameObject : null;
            }

            if (dashboardContentRoot == null)
            {
                HideDashboardLegacyChildren();
                BuildFullDashboardOverlayContent();
            }
            else
            {
                HideDashboardLegacyChildren();
                dashboardContentRoot.SetActive(true);
                if (dashboardHost == null)
                {
                    Transform existingHost = dashboardContentRoot.transform.Find("VrDashboardHost");
                    dashboardHost = existingHost as RectTransform;
                }
            }

            if (dashboardPresenter == null && dashboardHost != null)
            {
                dashboardPresenter = dashboardHost.GetComponent<MainHubDashboardPresenter>();
                if (dashboardPresenter == null)
                {
                    dashboardPresenter = dashboardHost.gameObject.AddComponent<MainHubDashboardPresenter>();
                }
            }

            if (dashboardPresenter != null && dashboardHost != null)
            {
                dashboardPresenter.ConfigureEmbedded(dashboardHost, runtimeState, includeBackButton: false, includeFooter: true);
            }
        }

        private void HideDashboardLegacyChildren()
        {
            if (dashboardPanel == null)
            {
                return;
            }

            Transform panelTransform = dashboardPanel.transform;
            for (int index = 0; index < panelTransform.childCount; index++)
            {
                Transform child = panelTransform.GetChild(index);
                if (child != null && child.gameObject != dashboardContentRoot)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private void BuildFullDashboardOverlayContent()
        {
            dashboardContentRoot = new GameObject(
                "VrDashboardFullRoot",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            dashboardContentRoot.transform.SetParent(dashboardPanel.transform, false);

            RectTransform rootRect = dashboardContentRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = new Vector2(26f, 24f);
            rootRect.offsetMax = new Vector2(-26f, -24f);

            LayoutElement rootLayoutElement = dashboardContentRoot.GetComponent<LayoutElement>();
            rootLayoutElement.flexibleWidth = 1f;
            rootLayoutElement.flexibleHeight = 1f;

            HorizontalLayoutGroup rootLayout = dashboardContentRoot.GetComponent<HorizontalLayoutGroup>();
            rootLayout.spacing = 18f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = true;

            GameObject dashboardHostObject = new GameObject(
                "VrDashboardHost",
                typeof(RectTransform),
                typeof(LayoutElement));
            dashboardHostObject.transform.SetParent(dashboardContentRoot.transform, false);
            dashboardHost = dashboardHostObject.GetComponent<RectTransform>();

            LayoutElement dashboardLayout = dashboardHostObject.GetComponent<LayoutElement>();
            dashboardLayout.flexibleWidth = 1f;
            dashboardLayout.flexibleHeight = 1f;
            dashboardLayout.minWidth = 900f;

            dashboardPresenter = dashboardHostObject.AddComponent<MainHubDashboardPresenter>();

            GameObject actionRail = new GameObject(
                "VrDashboardActionRail",
                typeof(RectTransform),
                typeof(Image),
                typeof(VerticalLayoutGroup),
                typeof(LayoutElement));
            actionRail.transform.SetParent(dashboardContentRoot.transform, false);

            Image railImage = actionRail.GetComponent<Image>();
            railImage.color = new Color(0.055f, 0.085f, 0.125f, 0.94f);
            railImage.raycastTarget = true;

            Outline railOutline = actionRail.AddComponent<Outline>();
            railOutline.effectColor = new Color(0.12f, 0.78f, 0.96f, 0.28f);
            railOutline.effectDistance = new Vector2(1f, -1f);

            LayoutElement railLayout = actionRail.GetComponent<LayoutElement>();
            railLayout.minWidth = 250f;
            railLayout.preferredWidth = 270f;
            railLayout.flexibleHeight = 1f;

            VerticalLayoutGroup railGroup = actionRail.GetComponent<VerticalLayoutGroup>();
            railGroup.padding = new RectOffset(22, 22, 22, 22);
            railGroup.spacing = 14f;
            railGroup.childControlWidth = true;
            railGroup.childControlHeight = false;
            railGroup.childForceExpandWidth = true;
            railGroup.childForceExpandHeight = false;

            CreateDashboardText(actionRail.transform, "DashboardRouteBadge", "SESSION COMPLETE", 16f, FontStyles.Bold, TextAlignmentOptions.Left, new Color(0.12f, 0.78f, 0.96f, 1f), 26f);
            CreateDashboardText(actionRail.transform, "DashboardRouteTitle", "Next Step", 30f, FontStyles.Bold, TextAlignmentOptions.Left, new Color(0.92f, 0.97f, 1f, 1f), 42f);
            CreateDashboardText(actionRail.transform, "DashboardRouteLead", "Tabs, Coach, History, Refresh, and Reset stay available here.", 17f, FontStyles.Normal, TextAlignmentOptions.Left, new Color(0.58f, 0.68f, 0.78f, 1f), 86f);
            CreateDashboardButton(actionRail.transform, "DashboardRetryButton", "Retry", () => resultsFlowController?.RetryLastSession());
            CreateDashboardButton(actionRail.transform, "DashboardChangeButton", "Change Room", () => resultsFlowController?.ChangeEnvironment());
            CreateDashboardButton(actionRail.transform, "DashboardHubButton", "Return To Hub", () => resultsFlowController?.ReturnToHub());

            GameObject spacer = new GameObject("DashboardRailSpacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(actionRail.transform, false);
            spacer.GetComponent<LayoutElement>().flexibleHeight = 1f;
        }

        private void RefreshDashboardPanel()
        {
            if (dashboardPanel == null)
            {
                return;
            }

            runtimeState ??= AppRuntimeState.GetOrCreate();
            if (dashboardPresenter != null)
            {
                dashboardPresenter.RefreshDashboard();
                dashboardPresenter.PanelView?.Show();
                return;
            }

            SessionResultSummary summary = runtimeState != null
                ? runtimeState.GetLastSessionResultCopy()
                : new SessionResultSummary();

            if (dashboardScoreLabel != null)
            {
                string scoreText = summary.HasOverallScore
                    ? $"{summary.TotalScore:0.0}"
                    : "Score pending";
                string bandText = string.IsNullOrWhiteSpace(summary.PerformanceBand)
                    ? "Latest run"
                    : summary.PerformanceBand;
                dashboardScoreLabel.text = $"{scoreText}\n<size=22>{bandText}</size>";
            }

            if (dashboardMetricsLabel != null)
            {
                dashboardMetricsLabel.text =
                    $"Eye {FormatDashboardMetric(summary.HasEyeContactScore, summary.EyeContactScore)}     " +
                    $"Speech {FormatDashboardMetric(summary.HasSpeechPaceScore, summary.SpeechPaceScore)}     " +
                    $"Posture {FormatDashboardMetric(summary.HasPostureScore, summary.PostureScore)}";
            }

            if (dashboardFocusLabel != null)
            {
                dashboardFocusLabel.text = BuildDashboardFocusText(summary);
            }

            if (dashboardRecommendationsLabel != null)
            {
                dashboardRecommendationsLabel.text = BuildDashboardRecommendationsText(summary);
            }
        }

        private static TMP_Text CreateDashboardText(
            Transform parent,
            string name,
            string text,
            float fontSize,
            FontStyles fontStyle,
            TextAlignmentOptions alignment,
            Color color,
            float preferredHeight)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);
            TMP_Text label = textObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.alignment = alignment;
            label.color = color;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            LayoutElement layoutElement = textObject.GetComponent<LayoutElement>();
            layoutElement.minHeight = preferredHeight;
            layoutElement.preferredHeight = preferredHeight;
            return label;
        }

        private static Button CreateDashboardButton(
            Transform parent,
            string name,
            string label,
            UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.11f, 0.19f, 0.27f, 0.96f);
            image.raycastTarget = true;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.RemoveAllListeners();
            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.11f, 0.19f, 0.27f, 0.96f);
            colors.highlightedColor = new Color(0.24f, 0.42f, 0.55f, 1f);
            colors.selectedColor = new Color(0.24f, 0.42f, 0.55f, 1f);
            colors.pressedColor = new Color(0.35f, 0.62f, 0.78f, 1f);
            button.colors = colors;

            LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.minHeight = 82f;
            layoutElement.preferredHeight = 82f;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 8f);
            labelRect.offsetMax = new Vector2(-12f, -8f);

            TMP_Text labelText = labelObject.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 22f;
            labelText.fontStyle = FontStyles.Bold;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = Color.white;
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
            labelText.overflowMode = TextOverflowModes.Ellipsis;
            labelText.raycastTarget = false;

            return button;
        }

        private static void ConfigureCenteredRect(RectTransform rectTransform, Vector2 size, Vector2 anchoredPosition)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
        }

        private static string FormatDashboardMetric(bool hasValue, float value)
        {
            return hasValue ? $"{value:0}" : "--";
        }

        private static string BuildDashboardFocusText(SessionResultSummary summary)
        {
            if (summary == null)
            {
                return "Focus area: session data pending.";
            }

            if (!string.IsNullOrWhiteSpace(summary.StrongestArea) && !string.IsNullOrWhiteSpace(summary.WeakestArea))
            {
                return $"Strongest: {summary.StrongestArea}     Focus: {summary.WeakestArea}";
            }

            if (!string.IsNullOrWhiteSpace(summary.WeakestArea))
            {
                return $"Focus: {summary.WeakestArea}";
            }

            return "Focus area will appear after scoring completes.";
        }

        private static string BuildDashboardRecommendationsText(SessionResultSummary summary)
        {
            if (summary == null || summary.Recommendations == null || summary.Recommendations.Count == 0)
            {
                return "No recommendations yet. Complete a scored session to populate this panel.";
            }

            int count = Mathf.Min(3, summary.Recommendations.Count);
            var builder = new System.Text.StringBuilder();
            for (int index = 0; index < count; index++)
            {
                if (index > 0)
                {
                    builder.AppendLine();
                }

                builder.Append("- ");
                builder.Append(summary.Recommendations[index]);
            }

            return builder.ToString();
        }

        private void ApplyReadableOverlayDefaults(bool force = false)
        {
            if (!applyVrReadabilityDefaults || (readabilityDefaultsApplied && !force))
            {
                return;
            }

            if (overlayFollower != null)
            {
                overlayFollower.SetOffset(ResolveReadableOverlayOffset());
                overlayFollower.SetMinimumTargetY(ResolveMinimumOverlayTargetY());
            }

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.dynamicPixelsPerUnit = Mathf.Max(scaler.dynamicPixelsPerUnit, overlayDynamicPixelsPerUnit);
            }

            VrUiUsabilityUtility.EnsureCanvasInputSupport(gameObject, overlayCanvas != null ? overlayCanvas : GetComponent<Canvas>());
            VrUiUsabilityUtility.ApplyReadablePanel(
                pausePanel,
                Mathf.Max(pauseMinimumFontSize, 23f),
                Mathf.Max(pauseMinimumButtonHeight, 94f),
                pauseMinimumPanelSize);
            VrUiUsabilityUtility.ApplyReadablePanel(
                resultsPanel,
                Mathf.Max(resultsMinimumFontSize, 22f),
                Mathf.Max(resultsMinimumButtonHeight, 92f),
                resultsMinimumPanelSize);
            VrUiUsabilityUtility.ApplyReadablePanel(
                dashboardPanel,
                16f,
                Mathf.Max(70f, dashboardMinimumButtonHeight * 0.75f),
                dashboardMinimumPanelSize);
            ApplyPanelReadability(pausePanel, Mathf.Max(pauseMinimumFontSize, 23f), Mathf.Max(pauseMinimumButtonHeight, 94f));
            ApplyPausePanelCopy(pausePanel);
            ApplyPanelReadability(resultsPanel, Mathf.Max(resultsMinimumFontSize, 22f), Mathf.Max(resultsMinimumButtonHeight, 92f));
            ApplyResultsPanelCleanup(resultsPanel);
            readabilityDefaultsApplied = true;
        }

        private Vector3 ResolveReadableOverlayOffset()
        {
            return UsesRaisedVrPlacementForActiveScene()
                ? raisedSceneOverlayOffset
                : readableOverlayOffset;
        }

        private float ResolveMinimumOverlayTargetY()
        {
            return UsesRaisedVrPlacementForActiveScene()
                ? Mathf.Max(minimumVrOverlayTargetY, raisedSceneMinimumVrOverlayTargetY)
                : minimumVrOverlayTargetY;
        }

        private static bool UsesRaisedVrPlacementForActiveScene()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            return sceneName.Contains("Conference") || sceneName.Contains("Meeting");
        }

        private static void ApplyResultsPanelCleanup(AppPanelView panel)
        {
            if (panel == null)
            {
                return;
            }

            HideTextObject(panel, "RetryInfo");
            HideTextObject(panel, "ChangeEnvironmentInfo");
            HideTextObject(panel, "DashboardInfo");

            SetTextObject(panel, "ResultsActionLead", "Pick the next route.");
            SetTextObject(panel, "ResultsLead", "The full dashboard opens by default after a completed session.");

            SetPreferredHeight(panel, "ScoreValue", 82f);
            SetPreferredHeight(panel, "SummaryValue", 82f);
            SetPreferredHeight(panel, "MetricsCard", 150f);
            SetPreferredHeight(panel, "NotesCard", 150f);
            SetPreferredHeight(panel, "RouteStatusLabel", 92f);
        }

        private static void ApplyPausePanelCopy(AppPanelView panel)
        {
            if (panel == null)
            {
                return;
            }

            SetTextObject(panel, "PauseActionLead", "VR: press Menu to pause/resume, or hold B/Y for 0.6s.\nThen use thumbstick + trigger/A.");
            SetSummaryStrip(panel, "PauseRuleC", "VR INPUT", "Menu opens pause. Hold B/Y (secondary) for 0.6s as a backup.");
            SetPreferredHeight(panel, "PauseActionLead", 104f);
            SetPreferredHeight(panel, "PauseStatusLabel", 148f);
        }

        private void EnsurePauseButtonWiring()
        {
            WirePanelButton(pausePanel, "ResumeButton", ResumeSession);
            WirePanelButton(pausePanel, "RestartButton", RestartSession);
            WirePanelButton(pausePanel, "EndButton", EndSession);
            WirePanelButton(pausePanel, "HubButton", ReturnToHub);
        }

        private static void WirePanelButton(
            AppPanelView panel,
            string buttonName,
            UnityEngine.Events.UnityAction action)
        {
            Button button = FindPanelButton(panel, buttonName);
            if (button == null || action == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private static void HideTextObject(AppPanelView panel, string name)
        {
            TMP_Text text = FindPanelText(panel, name);
            if (text != null)
            {
                text.gameObject.SetActive(false);
            }
        }

        private static void SetTextObject(AppPanelView panel, string name, string value)
        {
            TMP_Text text = FindPanelText(panel, name);
            if (text != null)
            {
                text.text = value;
            }
        }

        private static void SetSummaryStrip(AppPanelView panel, string stripName, string title, string value)
        {
            Transform strip = FindPanelTransform(panel, stripName);
            if (strip == null)
            {
                return;
            }

            TMP_Text[] texts = strip.GetComponentsInChildren<TMP_Text>(true);
            for (int index = 0; index < texts.Length; index++)
            {
                TMP_Text text = texts[index];
                if (text == null)
                {
                    continue;
                }

                if (text.gameObject.name == "StripTitle")
                {
                    text.text = title;
                }
                else if (text.gameObject.name == "StripValue")
                {
                    text.text = value;
                }
            }
        }

        private static TMP_Text FindPanelText(AppPanelView panel, string name)
        {
            if (panel == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            TMP_Text[] textElements = panel.GetComponentsInChildren<TMP_Text>(true);
            for (int index = 0; index < textElements.Length; index++)
            {
                TMP_Text textElement = textElements[index];
                if (textElement != null && textElement.gameObject.name == name)
                {
                    return textElement;
                }
            }

            return null;
        }

        private static Button FindPanelButton(AppPanelView panel, string name)
        {
            if (panel == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            Button[] buttons = panel.GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                Button button = buttons[index];
                if (button != null && button.gameObject.name == name)
                {
                    return button;
                }
            }

            return null;
        }

        private static Transform FindPanelTransform(AppPanelView panel, string name)
        {
            if (panel == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return FindDescendantByName(panel.transform, name);
        }

        private static Transform FindDescendantByName(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            for (int index = 0; index < root.childCount; index++)
            {
                Transform child = root.GetChild(index);
                if (child == null)
                {
                    continue;
                }

                if (child.name == name)
                {
                    return child;
                }

                Transform descendant = FindDescendantByName(child, name);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }

        private static void SetPreferredHeight(AppPanelView panel, string objectName, float height)
        {
            if (panel == null || string.IsNullOrWhiteSpace(objectName))
            {
                return;
            }

            Transform[] children = panel.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < children.Length; index++)
            {
                Transform child = children[index];
                if (child == null || child.gameObject.name != objectName)
                {
                    continue;
                }

                LayoutElement layoutElement = child.GetComponent<LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = child.gameObject.AddComponent<LayoutElement>();
                }

                layoutElement.minHeight = Mathf.Max(layoutElement.minHeight, height);
                layoutElement.preferredHeight = Mathf.Max(layoutElement.preferredHeight, height);
                return;
            }
        }

        private void ApplyDesktopPreviewCanvasMode()
        {
            if (!useScreenSpaceOverlayWhenNoXrDevice ||
                desktopOverlayModeApplied ||
                overlayCanvas == null ||
                IsXrDeviceActive())
            {
                return;
            }

            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.worldCamera = null;

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            if (overlayFollower != null)
            {
                overlayFollower.enabled = false;
            }

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = desktopOverlayReferenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                scaler.referencePixelsPerUnit = 100f;
                scaler.dynamicPixelsPerUnit = Mathf.Max(scaler.dynamicPixelsPerUnit, 24f);
            }

            desktopOverlayModeApplied = true;
        }

        private static bool IsXrDeviceActive()
        {
            return UnityEngine.XR.XRSettings.isDeviceActive || HasVrController();
        }

        private static void ApplyPanelScale(AppPanelView panel, float targetScale)
        {
            if (panel == null || targetScale <= 0f)
            {
                return;
            }

            Vector3 currentScale = panel.transform.localScale;
            float largestAxis = Mathf.Max(currentScale.x, currentScale.y, currentScale.z);
            if (largestAxis >= targetScale)
            {
                return;
            }

            panel.transform.localScale = Vector3.one * targetScale;
        }

        private static void ApplyPanelReadability(AppPanelView panel, float minimumFontSize, float minimumButtonHeight)
        {
            if (panel == null)
            {
                return;
            }

            TMP_Text[] textElements = panel.GetComponentsInChildren<TMP_Text>(true);
            for (int index = 0; index < textElements.Length; index++)
            {
                TMP_Text textElement = textElements[index];
                if (textElement == null)
                {
                    continue;
                }

                string textObjectName = textElement.gameObject.name;
                if (textObjectName == "ResumeInfo" ||
                    textObjectName == "RestartInfo" ||
                    textObjectName == "EndInfo")
                {
                    textElement.gameObject.SetActive(false);
                    continue;
                }

                if (textElement.fontSize >= minimumFontSize)
                {
                    continue;
                }

                textElement.fontSize = minimumFontSize;
            }

            Button[] buttons = panel.GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                Button button = buttons[index];
                if (button == null)
                {
                    continue;
                }

                LayoutElement layoutElement = button.GetComponent<LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = button.gameObject.AddComponent<LayoutElement>();
                }

                layoutElement.minHeight = Mathf.Max(layoutElement.minHeight, minimumButtonHeight);
                layoutElement.preferredHeight = Mathf.Max(layoutElement.preferredHeight, minimumButtonHeight);
            }
        }

        private void HandleDesktopOverlayShortcuts()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            var keyboard = Keyboard.current;
            bool pauseVisible =
                (runtimeState != null && runtimeState.CurrentRuntimeState.PauseMenuVisible) ||
                (mainController != null && mainController.IsSessionPaused);
            bool resultsVisible =
                runtimeState != null && runtimeState.CurrentRuntimeState.ResultsOverlayVisible;

            if (pauseVisible)
            {
                if (Time.frameCount == pausePanelShownFrame)
                {
                    return;
                }

                if (keyboard.enterKey.wasPressedThisFrame || WasShortcutPressed(keyboard.digit1Key, keyboard.numpad1Key))
                {
                    ResumeSession();
                }
                else if (keyboard.rKey.wasPressedThisFrame || WasShortcutPressed(keyboard.digit2Key, keyboard.numpad2Key))
                {
                    RestartSession();
                }
                else if (keyboard.eKey.wasPressedThisFrame || WasShortcutPressed(keyboard.digit3Key, keyboard.numpad3Key))
                {
                    EndSession();
                }
                else if (keyboard.hKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame || WasShortcutPressed(keyboard.digit4Key, keyboard.numpad4Key))
                {
                    ReturnToHub();
                }
            }
            else if (resultsVisible)
            {
                if (Time.frameCount == resultsPanelShownFrame)
                {
                    return;
                }

                if (keyboard.enterKey.wasPressedThisFrame || keyboard.rKey.wasPressedThisFrame || WasShortcutPressed(keyboard.digit1Key, keyboard.numpad1Key))
                {
                    resultsFlowController?.RetryLastSession();
                }
                else if (keyboard.cKey.wasPressedThisFrame || WasShortcutPressed(keyboard.digit2Key, keyboard.numpad2Key))
                {
                    resultsFlowController?.ChangeEnvironment();
                }
                else if (keyboard.hKey.wasPressedThisFrame ||
                         keyboard.backspaceKey.wasPressedThisFrame ||
                         WasShortcutPressed(keyboard.digit3Key, keyboard.numpad3Key) ||
                         WasShortcutPressed(keyboard.digit4Key, keyboard.numpad4Key))
                {
                    resultsFlowController?.ReturnToHub();
                }
            }
        }

        private static bool WasShortcutPressed(KeyControl primary, KeyControl secondary = null)
        {
            bool primaryPressed = primary != null && primary.wasPressedThisFrame;
            bool secondaryPressed = secondary != null && secondary.wasPressedThisFrame;
            return primaryPressed || secondaryPressed;
        }

        private void HandleDesktopOverlayPointerFallback()
        {
            AppPanelView activePanel = GetActiveInteractivePanel();
            if (activePanel == null)
            {
                return;
            }

            if (!TryGetDesktopClickPosition(out Vector2 screenPosition))
            {
                return;
            }

            if (TryInvokeButtonByGraphicRaycast(activePanel, screenPosition))
            {
                return;
            }

            TryInvokeButtonByRectHit(activePanel, screenPosition);
        }

        private void HandleVrOverlayPointerFallback()
        {
            AppPanelView activePanel = GetActiveInteractivePanel();
            if (activePanel == null || !WasVrOverlayClickPressedThisFrame())
            {
                return;
            }

            if (IsClickablePanelButton(activePanel, vrFocusedButton))
            {
                InvokeOverlayButton(vrFocusedButton);
                return;
            }

            Camera eventCamera = ResolveOverlayEventCamera();
            Vector2 screenCenter = GetOverlayScreenCenter(eventCamera);

            if (TryInvokeButtonByGraphicRaycast(activePanel, screenCenter) ||
                TryInvokeButtonByRectHit(activePanel, screenCenter))
            {
                return;
            }

            FocusFirstButton(activePanel);
        }

        private void UpdateVrFocusHighlight()
        {
            if (!HasVrController())
            {
                ClearVrFocusHighlight();
                return;
            }

            AppPanelView activePanel = GetActiveInteractivePanel();
            Camera eventCamera = ResolveOverlayEventCamera();
            if (activePanel == null)
            {
                ClearVrFocusHighlight();
                return;
            }

            if (IsClickablePanelButton(activePanel, vrFocusedButton) &&
                (activePanel == pausePanel ||
                 activePanel == dashboardPanel ||
                 activePanel == resultsPanel ||
                 Time.unscaledTime < vrNavigationFocusLockUntil))
            {
                return;
            }

            Vector2 screenCenter = GetOverlayScreenCenter(eventCamera);

            if (TryFindButtonByRectHit(activePanel, screenCenter, eventCamera, out Button focusedButton))
            {
                SetVrFocusedButton(focusedButton);
                return;
            }

            if (IsClickablePanelButton(activePanel, vrFocusedButton))
            {
                return;
            }

            FocusFirstButton(activePanel);
        }

        private void HandleVrOverlayNavigation()
        {
            AppPanelView activePanel = GetActiveInteractivePanel();
            if (activePanel == null || !HasVrController())
            {
                nextVrNavigationTime = 0f;
                return;
            }

            if (!TryReadVrNavigationDirection(out Vector2 direction))
            {
                nextVrNavigationTime = 0f;
                return;
            }

            if (Time.unscaledTime < nextVrNavigationTime)
            {
                return;
            }

            MoveVrFocus(activePanel, direction);
            vrNavigationFocusLockUntil = Time.unscaledTime + 1.25f;
            nextVrNavigationTime = Time.unscaledTime + Mathf.Max(0.08f, vrNavigationRepeatDelay);
        }

        private void HandleVrOverlayCancel()
        {
            AppPanelView activePanel = GetActiveInteractivePanel();
            if (activePanel == null || !HasVrController())
            {
                ResetVrCancelHold();
                return;
            }

            if (!IsVrCancelButtonPressed())
            {
                ResetVrCancelHold();
                return;
            }

            vrCancelHeldSeconds += Time.unscaledDeltaTime;
            if (vrCancelHoldConsumed || vrCancelHeldSeconds < Mathf.Max(0.1f, vrCancelHoldSeconds))
            {
                return;
            }

            vrCancelHoldConsumed = true;
            if (activePanel == pausePanel)
            {
                ResumeSession();
            }
            else if (activePanel == dashboardPanel)
            {
                resultsFlowController?.ReturnToHub();
            }
            else if (activePanel == resultsPanel)
            {
                resultsFlowController?.ReturnToHub();
            }
        }

        private void ResetVrCancelHold()
        {
            vrCancelHeldSeconds = 0f;
            vrCancelHoldConsumed = false;
        }

        private void MoveVrFocus(AppPanelView activePanel, Vector2 direction)
        {
            CollectClickablePanelButtons(activePanel, vrNavigationButtons);
            if (vrNavigationButtons.Count == 0)
            {
                ClearVrFocusHighlight();
                return;
            }

            int currentIndex = vrNavigationButtons.IndexOf(vrFocusedButton);
            if (currentIndex < 0 || !IsClickablePanelButton(activePanel, vrFocusedButton))
            {
                SetVrFocusedButton(vrNavigationButtons[0]);
                return;
            }

            if (TryFindDirectionalButton(activePanel, vrFocusedButton, direction, vrNavigationButtons, out Button directionalButton))
            {
                SetVrFocusedButton(directionalButton);
                return;
            }

            int step = direction.x > 0f || direction.y < 0f ? 1 : -1;
            int nextIndex = (currentIndex + step + vrNavigationButtons.Count) % vrNavigationButtons.Count;
            SetVrFocusedButton(vrNavigationButtons[nextIndex]);
        }

        private void FocusFirstButton(AppPanelView activePanel)
        {
            CollectClickablePanelButtons(activePanel, vrNavigationButtons);
            if (vrNavigationButtons.Count > 0)
            {
                SetVrFocusedButton(vrNavigationButtons[0]);
            }
            else
            {
                ClearVrFocusHighlight();
            }
        }

        private void CollectClickablePanelButtons(AppPanelView activePanel, List<Button> buttons)
        {
            buttons.Clear();
            if (activePanel == null)
            {
                return;
            }

            Button[] panelButtons = activePanel.GetComponentsInChildren<Button>(true);
            for (int index = 0; index < panelButtons.Length; index++)
            {
                Button button = panelButtons[index];
                if (IsClickablePanelButton(activePanel, button))
                {
                    buttons.Add(button);
                }
            }
        }

        private static bool TryFindDirectionalButton(
            AppPanelView activePanel,
            Button currentButton,
            Vector2 direction,
            List<Button> buttons,
            out Button targetButton)
        {
            targetButton = null;
            if (activePanel == null || currentButton == null || buttons == null || buttons.Count == 0)
            {
                return false;
            }

            Vector2 normalizedDirection = direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Vector2.down;
            Vector2 perpendicular = new Vector2(-normalizedDirection.y, normalizedDirection.x);
            Vector2 currentCenter = ProjectButtonCenterOnPanel(activePanel.transform, currentButton);
            float bestScore = float.PositiveInfinity;

            for (int index = 0; index < buttons.Count; index++)
            {
                Button candidate = buttons[index];
                if (candidate == null || candidate == currentButton)
                {
                    continue;
                }

                Vector2 delta = ProjectButtonCenterOnPanel(activePanel.transform, candidate) - currentCenter;
                float forwardDistance = Vector2.Dot(delta, normalizedDirection);
                if (forwardDistance <= 0.01f)
                {
                    continue;
                }

                float lateralDistance = Mathf.Abs(Vector2.Dot(delta, perpendicular));
                float score = lateralDistance * 1.8f + forwardDistance * 0.05f;
                if (score < bestScore)
                {
                    bestScore = score;
                    targetButton = candidate;
                }
            }

            return targetButton != null;
        }

        private static Vector2 ProjectButtonCenterOnPanel(Transform panelTransform, Button button)
        {
            RectTransform rectTransform = button != null ? button.transform as RectTransform : null;
            if (rectTransform == null || panelTransform == null)
            {
                return Vector2.zero;
            }

            Vector3 center = GetRectWorldCenter(rectTransform);
            return new Vector2(
                Vector3.Dot(center, panelTransform.right),
                Vector3.Dot(center, panelTransform.up));
        }

        private static Vector3 GetRectWorldCenter(RectTransform rectTransform)
        {
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f;
        }

        private static Vector2 GetOverlayScreenCenter(Camera eventCamera)
        {
            float screenWidth = eventCamera != null && eventCamera.pixelWidth > 0 ? eventCamera.pixelWidth : Screen.width;
            float screenHeight = eventCamera != null && eventCamera.pixelHeight > 0 ? eventCamera.pixelHeight : Screen.height;
            return new Vector2(screenWidth * 0.5f, screenHeight * 0.5f);
        }

        private void SetVrFocusedButton(Button focusedButton)
        {
            if (focusedButton == null || focusedButton == vrFocusedButton)
            {
                return;
            }

            ClearVrFocusHighlight();
            vrFocusedButton = focusedButton;
            vrFocusedButtonBaseScale = focusedButton.transform.localScale;
            focusedButton.transform.localScale = vrFocusedButtonBaseScale * Mathf.Max(1.01f, vrSelectedButtonScale);
            cachedEventSystem?.SetSelectedGameObject(focusedButton.gameObject);
        }

        private void ClearVrFocusHighlight()
        {
            if (vrFocusedButton != null)
            {
                vrFocusedButton.transform.localScale = vrFocusedButtonBaseScale;
                if (cachedEventSystem != null && cachedEventSystem.currentSelectedGameObject == vrFocusedButton.gameObject)
                {
                    cachedEventSystem.SetSelectedGameObject(null);
                }
            }

            vrFocusedButton = null;
            vrFocusedButtonBaseScale = Vector3.one;
        }

        private bool WasVrOverlayClickPressedThisFrame()
        {
            bool inputSystemPressed =
                WasControllerButtonPressedThisFrame(UnityEngine.InputSystem.XR.XRController.leftHand, "triggerPressed") ||
                WasControllerButtonPressedThisFrame(UnityEngine.InputSystem.XR.XRController.rightHand, "triggerPressed") ||
                WasControllerButtonPressedThisFrame(UnityEngine.InputSystem.XR.XRController.leftHand, "primaryButton") ||
                WasControllerButtonPressedThisFrame(UnityEngine.InputSystem.XR.XRController.rightHand, "primaryButton");

            bool legacyPressed =
                IsLegacyControllerButtonPressed(UnityEngine.XR.CommonUsages.triggerButton) ||
                IsLegacyControllerButtonPressed(UnityEngine.XR.CommonUsages.primaryButton);
            bool legacyPressedThisFrame = legacyPressed && !legacyVrClickWasPressed;
            legacyVrClickWasPressed = legacyPressed;

            return inputSystemPressed || legacyPressedThisFrame;
        }

        private static bool HasVrController()
        {
            return UnityEngine.InputSystem.XR.XRController.leftHand != null ||
                UnityEngine.InputSystem.XR.XRController.rightHand != null ||
                HasLegacyVrController();
        }

        private static bool WasControllerButtonPressedThisFrame(
            UnityEngine.InputSystem.XR.XRController controller,
            string controlName)
        {
            if (controller == null || string.IsNullOrWhiteSpace(controlName))
            {
                return false;
            }

            ButtonControl control = controller.TryGetChildControl<ButtonControl>(controlName);
            return control != null && control.wasPressedThisFrame;
        }

        private bool TryReadVrNavigationDirection(out Vector2 direction)
        {
            direction = Vector2.zero;

            Vector2 input =
                ReadControllerAxis(UnityEngine.InputSystem.XR.XRController.leftHand, "primary2DAxis");
            if (input.sqrMagnitude < vrNavigationAxisThreshold * vrNavigationAxisThreshold)
            {
                input = ReadControllerAxis(UnityEngine.InputSystem.XR.XRController.rightHand, "primary2DAxis");
            }

            if (input.sqrMagnitude < vrNavigationAxisThreshold * vrNavigationAxisThreshold)
            {
                input = ReadLegacyControllerAxis(UnityEngine.XR.CommonUsages.primary2DAxis);
            }

            if (input.sqrMagnitude < vrNavigationAxisThreshold * vrNavigationAxisThreshold)
            {
                return false;
            }

            direction = Mathf.Abs(input.x) > Mathf.Abs(input.y)
                ? new Vector2(Mathf.Sign(input.x), 0f)
                : new Vector2(0f, Mathf.Sign(input.y));
            return direction.sqrMagnitude > 0.5f;
        }

        private static Vector2 ReadControllerAxis(
            UnityEngine.InputSystem.XR.XRController controller,
            string controlName)
        {
            if (controller == null || string.IsNullOrWhiteSpace(controlName))
            {
                return Vector2.zero;
            }

            Vector2Control control = controller.TryGetChildControl<Vector2Control>(controlName);
            return control != null ? control.ReadValue() : Vector2.zero;
        }

        private static Vector2 ReadLegacyControllerAxis(InputFeatureUsage<Vector2> usage)
        {
            RefreshLegacyVrControllers();
            for (int index = 0; index < LegacyVrControllers.Count; index++)
            {
                UnityEngine.XR.InputDevice device = LegacyVrControllers[index];
                if (device.isValid &&
                    device.TryGetFeatureValue(usage, out Vector2 value) &&
                    value.sqrMagnitude > 0.001f)
                {
                    return value;
                }
            }

            return Vector2.zero;
        }

        private static bool IsVrCancelButtonPressed()
        {
            return
                IsControllerButtonPressed(UnityEngine.InputSystem.XR.XRController.leftHand, "secondaryButton") ||
                IsControllerButtonPressed(UnityEngine.InputSystem.XR.XRController.rightHand, "secondaryButton") ||
                IsControllerButtonPressed(UnityEngine.InputSystem.XR.XRController.leftHand, "menuButton") ||
                IsControllerButtonPressed(UnityEngine.InputSystem.XR.XRController.rightHand, "menuButton") ||
                IsLegacyControllerButtonPressed(UnityEngine.XR.CommonUsages.secondaryButton) ||
                IsLegacyControllerButtonPressed(new InputFeatureUsage<bool>("menuButton"));
        }

        private static bool IsControllerButtonPressed(
            UnityEngine.InputSystem.XR.XRController controller,
            string controlName)
        {
            if (controller == null || string.IsNullOrWhiteSpace(controlName))
            {
                return false;
            }

            ButtonControl control = controller.TryGetChildControl<ButtonControl>(controlName);
            return control != null && control.isPressed;
        }

        private static bool HasLegacyVrController()
        {
            RefreshLegacyVrControllers();
            for (int index = 0; index < LegacyVrControllers.Count; index++)
            {
                if (LegacyVrControllers[index].isValid)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsLegacyControllerButtonPressed(InputFeatureUsage<bool> usage)
        {
            RefreshLegacyVrControllers();
            for (int index = 0; index < LegacyVrControllers.Count; index++)
            {
                UnityEngine.XR.InputDevice device = LegacyVrControllers[index];
                if (device.isValid &&
                    device.TryGetFeatureValue(usage, out bool value) &&
                    value)
                {
                    return true;
                }
            }

            return false;
        }

        private static void RefreshLegacyVrControllers()
        {
            if (LegacyVrControllers.Count > 0 && HasValidLegacyController())
            {
                return;
            }

            LegacyVrControllers.Clear();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Controller,
                LegacyVrControllers);
        }

        private static bool HasValidLegacyController()
        {
            for (int index = 0; index < LegacyVrControllers.Count; index++)
            {
                if (LegacyVrControllers[index].isValid)
                {
                    return true;
                }
            }

            return false;
        }

        private AppPanelView GetActiveInteractivePanel()
        {
            if (pausePanel != null && pausePanel.gameObject.activeInHierarchy)
            {
                return pausePanel;
            }

            if (dashboardPanel != null && dashboardPanel.gameObject.activeInHierarchy)
            {
                return dashboardPanel;
            }

            if (resultsPanel != null && resultsPanel.gameObject.activeInHierarchy)
            {
                return resultsPanel;
            }

            return null;
        }

        private bool IsDashboardPanelVisible()
        {
            return dashboardPanel != null && dashboardPanel.gameObject.activeInHierarchy;
        }

        private bool TryGetDesktopClickPosition(out Vector2 screenPosition)
        {
            screenPosition = default;

            bool pressedThisFrame = false;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                screenPosition = Mouse.current.position.ReadValue();
                pressedThisFrame = true;
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.mousePresent && Input.GetMouseButtonDown(0))
            {
                screenPosition = Input.mousePosition;
                pressedThisFrame = true;
            }
#endif

            return pressedThisFrame;
        }

        private bool TryInvokeButtonByGraphicRaycast(AppPanelView activePanel, Vector2 screenPosition)
        {
            if (activePanel == null || overlayRaycaster == null || cachedEventSystem == null)
            {
                return false;
            }

            var pointerEventData = new PointerEventData(cachedEventSystem)
            {
                position = screenPosition,
                button = PointerEventData.InputButton.Left
            };

            pointerRaycastResults.Clear();
            overlayRaycaster.Raycast(pointerEventData, pointerRaycastResults);

            for (int index = 0; index < pointerRaycastResults.Count; index++)
            {
                RaycastResult result = pointerRaycastResults[index];
                Button button = result.gameObject != null
                    ? result.gameObject.GetComponentInParent<Button>()
                    : null;

                if (!IsClickablePanelButton(activePanel, button))
                {
                    continue;
                }

                InvokeOverlayButton(button);
                return true;
            }

            return false;
        }

        private bool TryInvokeButtonByRectHit(AppPanelView activePanel, Vector2 screenPosition)
        {
            if (activePanel == null)
            {
                return false;
            }

            Camera eventCamera = ResolveOverlayEventCamera();
            if (TryFindButtonByRectHit(activePanel, screenPosition, eventCamera, out Button hitButton))
            {
                InvokeOverlayButton(hitButton);
                return true;
            }

            return false;
        }

        private static bool TryFindButtonByRectHit(
            AppPanelView activePanel,
            Vector2 screenPosition,
            Camera eventCamera,
            out Button hitButton)
        {
            hitButton = null;
            if (activePanel == null)
            {
                return false;
            }

            Button[] buttons = activePanel.GetComponentsInChildren<Button>(true);
            for (int index = buttons.Length - 1; index >= 0; index--)
            {
                Button button = buttons[index];
                if (!IsClickablePanelButton(activePanel, button))
                {
                    continue;
                }

                RectTransform rectTransform = button.transform as RectTransform;
                if (rectTransform == null)
                {
                    continue;
                }

                if (!RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera))
                {
                    continue;
                }

                hitButton = button;
                return true;
            }

            return false;
        }

        private Camera ResolveOverlayEventCamera()
        {
            if (overlayCanvas == null)
            {
                return Camera.main ?? FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
            }

            if (overlayCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            Camera preferredCamera = ResolvePreferredOverlayCamera();
            if (preferredCamera != null)
            {
                if (overlayCanvas.worldCamera != preferredCamera)
                {
                    overlayCanvas.worldCamera = preferredCamera;
                }

                return preferredCamera;
            }

            return overlayCanvas.worldCamera;
        }

        private void SyncOverlayEventCamera()
        {
            if (overlayCanvas == null || overlayCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return;
            }

            Camera preferredCamera = ResolvePreferredOverlayCamera();
            if (preferredCamera != null && overlayCanvas.worldCamera != preferredCamera)
            {
                overlayCanvas.worldCamera = preferredCamera;
            }
        }

        private Camera ResolvePreferredOverlayCamera()
        {
            Camera mainCamera = Camera.main;
            if (IsUsableEventCamera(mainCamera))
            {
                return mainCamera;
            }

            Camera bestAlignedCamera = null;
            float bestScore = float.NegativeInfinity;
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera candidate = cameras[index];
                if (!IsUsableEventCamera(candidate))
                {
                    continue;
                }

                Vector3 toOverlay = transform.position - candidate.transform.position;
                float distance = toOverlay.magnitude;
                if (distance < 0.001f)
                {
                    continue;
                }

                float alignment = Vector3.Dot(candidate.transform.forward, toOverlay / distance);
                float targetTexturePenalty = candidate.targetTexture != null ? 0.35f : 0f;
                float score = alignment - targetTexturePenalty - (distance * 0.001f);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestAlignedCamera = candidate;
                }
            }

            if (bestAlignedCamera != null)
            {
                return bestAlignedCamera;
            }

            return IsUsableEventCamera(overlayCanvas != null ? overlayCanvas.worldCamera : null)
                ? overlayCanvas.worldCamera
                : null;
        }

        private static bool IsUsableEventCamera(Camera camera)
        {
            return camera != null && camera.isActiveAndEnabled && camera.gameObject.activeInHierarchy;
        }

        private static bool IsClickablePanelButton(AppPanelView activePanel, Button button)
        {
            return activePanel != null &&
                button != null &&
                button.transform.IsChildOf(activePanel.transform) &&
                button.IsActive() &&
                button.interactable;
        }

        private void InvokeOverlayButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            if (TryInvokePausePanelButton(button))
            {
                return;
            }

            cachedEventSystem?.SetSelectedGameObject(button.gameObject);
            button.onClick.Invoke();
        }

        private bool TryInvokePausePanelButton(Button button)
        {
            if (pausePanel == null ||
                button == null ||
                !button.transform.IsChildOf(pausePanel.transform))
            {
                return false;
            }

            switch (button.gameObject.name)
            {
                case "ResumeButton":
                    ResumeSession();
                    return true;
                case "RestartButton":
                    RestartSession();
                    return true;
                case "EndButton":
                    EndSession();
                    return true;
                case "HubButton":
                    ReturnToHub();
                    return true;
                default:
                    return false;
            }
        }
    }
}
