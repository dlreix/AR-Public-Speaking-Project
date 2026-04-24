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

        private MainController subscribedMainController;
        private int pausePanelShownFrame = -1;
        private int resultsPanelShownFrame = -1;
        private readonly List<RaycastResult> pointerRaycastResults = new List<RaycastResult>(16);
        private GraphicRaycaster overlayRaycaster;
        private EventSystem cachedEventSystem;
        private Canvas overlayCanvas;

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

            if (overlayRaycaster == null)
            {
                overlayRaycaster = GetComponent<GraphicRaycaster>();
            }

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

            bool isVisible = alpha > 0.001f;
            dimmerCanvasGroup.alpha = Mathf.Clamp01(alpha);
            dimmerCanvasGroup.interactable = isVisible;
            dimmerCanvasGroup.blocksRaycasts = isVisible;
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
                return baseMessage;
            }

            return $"{baseMessage}\n\nKeys: [Enter/1] Resume  [R/2] Restart  [E/3] End  [H/4] Hub";
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

            TryInvokeButtonByRectHit(activePanel, screenPosition);
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

        private void TryInvokeButtonByRectHit(AppPanelView activePanel, Vector2 screenPosition)
        {
            if (activePanel == null)
            {
                return;
            }

            Camera eventCamera = overlayCanvas != null ? overlayCanvas.worldCamera : null;
            Button[] buttons = activePanel.GetComponentsInChildren<Button>(true);
            for (int index = buttons.Length - 1; index >= 0; index--)
            {
                Button button = buttons[index];
                if (button == null || !button.IsActive() || !button.IsInteractable())
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

                cachedEventSystem?.SetSelectedGameObject(button.gameObject);
                button.onClick.Invoke();
                return;
            }
        }
    }
}
