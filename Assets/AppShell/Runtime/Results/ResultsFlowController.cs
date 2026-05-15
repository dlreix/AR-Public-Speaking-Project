using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;
using VRPublicSpeaking.AppShell.Flow;
using VRPublicSpeaking.AppShell.UI;

namespace VRPublicSpeaking.AppShell.Results
{
    public class ResultsFlowController : MonoBehaviour
    {
        [SerializeField] private AppRuntimeState runtimeState;
        [SerializeField] private TransitionManager transitionManager;
        [SerializeField] private MainHubDashboardPresenter dashboardPresenter;
        [SerializeField] private EnvironmentSessionOverlayController environmentSessionOverlayController;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private RectTransform dashboardHost;
        [SerializeField] private GameObject dashboardFirstRoot;
        [SerializeField] private bool buildDashboardFirstLayout = true;
        [SerializeField] private string retryLoadingMessage = "Retrying session...";
        [SerializeField] private string changeEnvironmentLoadingMessage = "Returning to environments...";
        [SerializeField] private string returnToHubLoadingMessage = "Returning to hub...";
        [SerializeField] private bool applyVrUsabilityDefaults = true;
        [SerializeField] private float minimumVrFontSize = 22f;
        [SerializeField] private float minimumVrButtonHeight = 92f;
        [SerializeField] private Vector2 minimumVrPanelSize = new Vector2(1180f, 820f);

        private bool routeInProgress;

        private void OnEnable()
        {
            routeInProgress = false;
            ApplyVrReadabilityDefaults();
            TryResolveRuntimeState();
            EnsureDashboardFirstLayout();
            dashboardPresenter?.RefreshDashboard();
            dashboardPresenter?.PanelView?.Show();
            SetStatus("Review the session dashboard, then choose a route.");
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
            EnsureDashboardFirstLayout();
            dashboardPresenter?.RefreshDashboard();
            dashboardPresenter?.PanelView?.Show();
            SetStatus("Dashboard is already open on this screen.");
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
            runtimeState?.SetPauseMenuVisible(false);
            runtimeState?.SetResultsOverlayVisible(false);
            environmentSessionOverlayController?.HideTransientPanels();
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
                statusLabel.text = BuildStatusMessage(message);
            }
        }

        private static string BuildStatusMessage(string message)
        {
            string baseMessage = message ?? string.Empty;
            if (Keyboard.current == null)
            {
                return $"{baseMessage}\nVR: thumbstick selects, trigger/A confirms.";
            }

            return $"{baseMessage}\nShortcuts: R Retry | C Room | H Hub\nVR: thumbstick selects, trigger/A confirms.";
        }

        private void ApplyVrReadabilityDefaults()
        {
            if (!applyVrUsabilityDefaults)
            {
                return;
            }

            AppPanelView panel = GetComponentInParent<AppPanelView>(true);
            Canvas canvas = GetComponentInParent<Canvas>(true);
            if (canvas != null)
            {
                VrUiUsabilityUtility.EnsureCanvasInputSupport(canvas.gameObject, canvas);
            }

            if (panel != null)
            {
                VrUiUsabilityUtility.ApplyReadablePanel(
                    panel,
                    Mathf.Max(20f, minimumVrFontSize),
                    Mathf.Max(84f, minimumVrButtonHeight),
                    minimumVrPanelSize);
            }
        }

        private void EnsureDashboardFirstLayout()
        {
            if (!buildDashboardFirstLayout)
            {
                return;
            }

            AppPanelView panelView = GetComponent<AppPanelView>() ?? GetComponentInParent<AppPanelView>(true);
            RectTransform panelRoot = panelView != null
                ? panelView.transform as RectTransform
                : transform as RectTransform;

            if (panelRoot == null)
            {
                return;
            }

            if (dashboardFirstRoot == null)
            {
                Transform existingRoot = panelRoot.Find("ResultsDashboardFirstRoot");
                dashboardFirstRoot = existingRoot != null ? existingRoot.gameObject : null;
            }

            if (dashboardFirstRoot == null)
            {
                HideLegacyResultChildren(panelRoot);
                BuildDashboardFirstLayout(panelRoot);
            }
            else
            {
                HideLegacyResultChildren(panelRoot);
                dashboardFirstRoot.SetActive(true);
                if (dashboardHost == null)
                {
                    Transform existingHost = dashboardFirstRoot.transform.Find("ResultsDashboardHost");
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

        private void HideLegacyResultChildren(RectTransform panelRoot)
        {
            for (int index = 0; index < panelRoot.childCount; index++)
            {
                Transform child = panelRoot.GetChild(index);
                if (child != null && child.gameObject != dashboardFirstRoot)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private void BuildDashboardFirstLayout(RectTransform panelRoot)
        {
            dashboardFirstRoot = new GameObject(
                "ResultsDashboardFirstRoot",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            dashboardFirstRoot.transform.SetParent(panelRoot, false);

            RectTransform rootRect = dashboardFirstRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            LayoutElement rootLayoutElement = dashboardFirstRoot.GetComponent<LayoutElement>();
            rootLayoutElement.flexibleWidth = 1f;
            rootLayoutElement.flexibleHeight = 1f;
            rootLayoutElement.minHeight = 720f;
            rootLayoutElement.preferredHeight = 780f;

            HorizontalLayoutGroup rootLayout = dashboardFirstRoot.GetComponent<HorizontalLayoutGroup>();
            rootLayout.padding = new RectOffset(26, 26, 24, 24);
            rootLayout.spacing = 18f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = true;

            GameObject dashboardHostObject = new GameObject(
                "ResultsDashboardHost",
                typeof(RectTransform),
                typeof(LayoutElement));
            dashboardHostObject.transform.SetParent(dashboardFirstRoot.transform, false);
            dashboardHost = dashboardHostObject.GetComponent<RectTransform>();
            LayoutElement dashboardLayout = dashboardHostObject.GetComponent<LayoutElement>();
            dashboardLayout.flexibleWidth = 1f;
            dashboardLayout.flexibleHeight = 1f;
            dashboardLayout.minWidth = 920f;

            dashboardPresenter = dashboardHostObject.AddComponent<MainHubDashboardPresenter>();

            GameObject actionRail = new GameObject(
                "ResultsActionRail",
                typeof(RectTransform),
                typeof(Image),
                typeof(VerticalLayoutGroup),
                typeof(LayoutElement));
            actionRail.transform.SetParent(dashboardFirstRoot.transform, false);

            Image railImage = actionRail.GetComponent<Image>();
            railImage.color = new Color(0.055f, 0.085f, 0.125f, 0.94f);
            railImage.raycastTarget = true;

            Outline railOutline = actionRail.AddComponent<Outline>();
            railOutline.effectColor = new Color(0.12f, 0.78f, 0.96f, 0.28f);
            railOutline.effectDistance = new Vector2(1f, -1f);

            LayoutElement railLayout = actionRail.GetComponent<LayoutElement>();
            railLayout.minWidth = 260f;
            railLayout.preferredWidth = 286f;
            railLayout.flexibleHeight = 1f;

            VerticalLayoutGroup railGroup = actionRail.GetComponent<VerticalLayoutGroup>();
            railGroup.padding = new RectOffset(22, 22, 22, 22);
            railGroup.spacing = 14f;
            railGroup.childControlWidth = true;
            railGroup.childControlHeight = false;
            railGroup.childForceExpandWidth = true;
            railGroup.childForceExpandHeight = false;

            CreateRailText(actionRail.transform, "RailBadge", "SESSION COMPLETE", 16f, FontStyles.Bold, new Color(0.12f, 0.78f, 0.96f, 1f), 26f);
            CreateRailText(actionRail.transform, "RailTitle", "Next Step", 30f, FontStyles.Bold, new Color(0.92f, 0.97f, 1f, 1f), 42f);
            CreateRailText(actionRail.transform, "RailSubtitle", "Dashboard stays open while you pick the next route.", 17f, FontStyles.Normal, new Color(0.58f, 0.68f, 0.78f, 1f), 64f);
            CreateRailButton(actionRail.transform, "RetryButton", "Retry", RetryLastSession, new Color(0.21f, 0.63f, 0.96f, 1f));
            CreateRailButton(actionRail.transform, "ChangeEnvironmentButton", "Change Room", ChangeEnvironment, new Color(0.11f, 0.19f, 0.27f, 0.96f));
            CreateRailButton(actionRail.transform, "ReturnToHubButton", "Return To Hub", ReturnToHub, new Color(0.16f, 0.16f, 0.22f, 0.96f));

            GameObject spacer = new GameObject("RailSpacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(actionRail.transform, false);
            LayoutElement spacerLayout = spacer.GetComponent<LayoutElement>();
            spacerLayout.flexibleHeight = 1f;

            statusLabel = CreateRailText(
                actionRail.transform,
                "RouteStatusLabel",
                string.Empty,
                16f,
                FontStyles.Italic,
                new Color(0.58f, 0.68f, 0.78f, 1f),
                150f);
        }

        private static TMP_Text CreateRailText(
            Transform parent,
            string name,
            string text,
            float fontSize,
            FontStyles style,
            Color color,
            float height)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);
            LayoutElement layout = textObject.GetComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;

            TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateRailButton(
            Transform parent,
            string name,
            string label,
            UnityEngine.Events.UnityAction action,
            Color color)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.minHeight = 76f;
            layout.preferredHeight = 76f;

            Image image = buttonObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = new Color(0.24f, 0.42f, 0.55f, 1f);
            colors.selectedColor = new Color(0.24f, 0.42f, 0.55f, 1f);
            colors.pressedColor = new Color(0.35f, 0.62f, 0.78f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            TextMeshProUGUI text = CreateRailText(
                buttonObject.transform,
                "Label",
                label,
                21f,
                FontStyles.Bold,
                Color.white,
                76f) as TextMeshProUGUI;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 14f;
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return button;
        }
    }
}
