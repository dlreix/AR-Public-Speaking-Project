using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
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
        [SerializeField] private ResultsSummaryPresenter resultsSummaryPresenter;
        [SerializeField] private ResultsFlowController resultsFlowController;
        [SerializeField] private TMP_Text pauseStatusLabel;
        [SerializeField] [Range(0f, 1f)] private float pauseDimAlpha = 0.42f;
        [SerializeField] [Range(0f, 1f)] private float resultsDimAlpha = 0.56f;
        [SerializeField] private string pauseStatusText =
            "Session paused. Timing, tracking, and scoring are safely on hold.";
        [SerializeField] private string pauseUnavailableStatusText =
            "Pause is available only while a live session is running.";
        [SerializeField] private bool applyVrReadabilityDefaults = true;
        [SerializeField] private Vector3 readableOverlayOffset = new Vector3(0f, -0.10f, 1.08f);
        [SerializeField] private float pauseMinimumFontSize = 22f;
        [SerializeField] private float pauseMinimumButtonHeight = 72f;
        [SerializeField] private float pausePanelReadableScale = 1.18f;
        [SerializeField] private bool useScreenSpaceOverlayWhenNoXrDevice = true;
        [SerializeField] private Vector2 desktopOverlayReferenceResolution = new Vector2(1540f, 1040f);

        private MainController subscribedMainController;
        private int pausePanelShownFrame = -1;
        private int resultsPanelShownFrame = -1;
        private readonly List<RaycastResult> pointerRaycastResults = new List<RaycastResult>(16);
        private GraphicRaycaster overlayRaycaster;
        private EventSystem cachedEventSystem;
        private Canvas overlayCanvas;
        private Button vrFocusedButton;
        private Vector3 vrFocusedButtonBaseScale = Vector3.one;
        private bool readabilityDefaultsApplied;
        private bool desktopOverlayModeApplied;

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
            overlayFollower?.SnapToTarget();
            resultsSummaryPresenter.Refresh();
            BringPanelToFront(resultsPanel);
            resultsPanel.Show();
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
                return;
            }

            HidePausePanelInternal(updateRuntimeState: true);
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

            if (pausePanel == null || resultsPanel == null)
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

            if (pausePanel == null)
            {
                return;
            }

            UpdatePauseStatus(pauseStatusText);
            overlayFollower?.SnapToTarget();
            BringPanelToFront(pausePanel);
            pausePanel.Show();
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

            if (runtimeState == null || !runtimeState.CurrentRuntimeState.ResultsOverlayVisible)
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

            if (runtimeState == null || !runtimeState.CurrentRuntimeState.PauseMenuVisible)
            {
                ApplyDimmer(0f);
            }
        }

        private void ApplyClosedState()
        {
            HidePausePanelInternal(updateRuntimeState: true);
            HideResultsPanelInternal(updateRuntimeState: true);
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
                return $"{baseMessage}\n\nVR: look at a button, press trigger/A. Hold secondary for 0.6s to close.";
            }

            return $"{baseMessage}\n\nPC: Enter/1 Resume  R/2 Restart  E/3 End  H/4 Hub\nVR: look at a button, press trigger/A.";
        }

        private void ApplyReadableOverlayDefaults()
        {
            if (!applyVrReadabilityDefaults || readabilityDefaultsApplied)
            {
                return;
            }

            if (overlayFollower != null)
            {
                overlayFollower.SetOffset(readableOverlayOffset);
            }

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.dynamicPixelsPerUnit = Mathf.Max(scaler.dynamicPixelsPerUnit, 24f);
            }

            ApplyPanelReadability(pausePanel, pauseMinimumFontSize, pauseMinimumButtonHeight);
            ApplyPanelScale(pausePanel, pausePanelReadableScale);
            ApplyPanelReadability(resultsPanel, 20f, 66f);
            ApplyResultsPanelCleanup(resultsPanel);
            readabilityDefaultsApplied = true;
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
            SetTextObject(panel, "ResultsLead", "Latest run recap. Dashboard can replace this panel later.");

            SetPreferredHeight(panel, "ScoreValue", 82f);
            SetPreferredHeight(panel, "SummaryValue", 82f);
            SetPreferredHeight(panel, "MetricsCard", 150f);
            SetPreferredHeight(panel, "NotesCard", 150f);
            SetPreferredHeight(panel, "RouteStatusLabel", 92f);
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
                    continue;
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
                else if (keyboard.dKey.wasPressedThisFrame || WasShortcutPressed(keyboard.digit3Key, keyboard.numpad3Key))
                {
                    resultsFlowController?.OpenDashboard();
                }
                else if (keyboard.hKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame || WasShortcutPressed(keyboard.digit4Key, keyboard.numpad4Key))
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
            if (eventCamera == null)
            {
                return;
            }

            float screenWidth = eventCamera.pixelWidth > 0 ? eventCamera.pixelWidth : Screen.width;
            float screenHeight = eventCamera.pixelHeight > 0 ? eventCamera.pixelHeight : Screen.height;
            Vector2 screenCenter = new Vector2(screenWidth * 0.5f, screenHeight * 0.5f);
            TryInvokeButtonByRectHit(activePanel, screenCenter);
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
            if (activePanel == null || eventCamera == null)
            {
                ClearVrFocusHighlight();
                return;
            }

            float screenWidth = eventCamera.pixelWidth > 0 ? eventCamera.pixelWidth : Screen.width;
            float screenHeight = eventCamera.pixelHeight > 0 ? eventCamera.pixelHeight : Screen.height;
            Vector2 screenCenter = new Vector2(screenWidth * 0.5f, screenHeight * 0.5f);

            if (!TryFindButtonByRectHit(activePanel, screenCenter, eventCamera, out Button focusedButton))
            {
                ClearVrFocusHighlight();
                return;
            }

            if (focusedButton == vrFocusedButton)
            {
                return;
            }

            ClearVrFocusHighlight();
            vrFocusedButton = focusedButton;
            vrFocusedButtonBaseScale = focusedButton.transform.localScale;
            focusedButton.transform.localScale = vrFocusedButtonBaseScale * 1.055f;
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

        private static bool WasVrOverlayClickPressedThisFrame()
        {
            return WasControllerButtonPressedThisFrame(UnityEngine.InputSystem.XR.XRController.leftHand, "triggerPressed") ||
                WasControllerButtonPressedThisFrame(UnityEngine.InputSystem.XR.XRController.rightHand, "triggerPressed") ||
                WasControllerButtonPressedThisFrame(UnityEngine.InputSystem.XR.XRController.leftHand, "primaryButton") ||
                WasControllerButtonPressedThisFrame(UnityEngine.InputSystem.XR.XRController.rightHand, "primaryButton");
        }

        private static bool HasVrController()
        {
            return UnityEngine.InputSystem.XR.XRController.leftHand != null ||
                UnityEngine.InputSystem.XR.XRController.rightHand != null;
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

        private AppPanelView GetActiveInteractivePanel()
        {
            if (pausePanel != null && pausePanel.gameObject.activeInHierarchy)
            {
                return pausePanel;
            }

            if (resultsPanel != null && resultsPanel.gameObject.activeInHierarchy)
            {
                return resultsPanel;
            }

            return null;
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

            if (Input.mousePresent && Input.GetMouseButtonDown(0))
            {
                screenPosition = Input.mousePosition;
                pressedThisFrame = true;
            }

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

        private void TryInvokeButtonByRectHit(AppPanelView activePanel, Vector2 screenPosition)
        {
            if (activePanel == null)
            {
                return;
            }

            Camera eventCamera = ResolveOverlayEventCamera();
            if (TryFindButtonByRectHit(activePanel, screenPosition, eventCamera, out Button hitButton))
            {
                InvokeOverlayButton(hitButton);
            }
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
                button.IsInteractable();
        }

        private void InvokeOverlayButton(Button button)
        {
            cachedEventSystem?.SetSelectedGameObject(button.gameObject);
            button.onClick.Invoke();
        }
    }
}
