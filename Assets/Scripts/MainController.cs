using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>
/// Main branch gaze controller with App Shell session, pause, and result routing hooks.
/// </summary>
public class MainController : MonoBehaviour
{
    [Header("Sistem Referansları")]
    [Tooltip("Main Camera üzerindeki EyeTrackingSystem scripti")]
    public EyeTrackingSystem eyeTracking;

    [Tooltip("Bu obje veya başka bir obje üzerindeki CircleEventSystem scripti")]
    public CircleEventSystem circleEvent;

    [Tooltip("Tek bakımlık hızlı nokta event sistemi (Özellik 1)")]
    public QuickGazeDotSystem quickGazeDot;

    [Tooltip("Hareketli takip noktası event sistemi (Özellik 2)")]
    public MovingGazeDotSystem movingGazeDot;

    [Tooltip("Tüm gaze event'leri arasında çakışmayı önleyen koordinatör")]
    public GazeEventCoordinator eventCoordinator;

    [Tooltip("GazeScoringSystem scripti (oturum sonu skoru için)")]
    public GazeScoringSystem gazeScoringSystem;

    [Tooltip("XR Origin içindeki kamera transformu (bilgi amaçlı referans; VR SDK tarafından yönetilir)")]
    public Transform playerHead;

    [Tooltip("VR kamerası (XR Origin > Camera Offset > Main Camera)")]
    public Camera mainCamera;

    [Header("UI Elemanları")]
    public Text statusText;
    public Text timerText;
    public Text stareWarningText;
    public Text headWarningText;
    public Text reviewInfoText;

    private bool sessionRunning;
    private bool sessionPaused;
    private bool debugMode;
    private bool inReview;
    private float sessionStartTime;
    private float sessionDuration;
    private float pausedDurationAccum;
    private float pauseStartedAt = -1f;
    private float lastFinalGazeScore = -1f;

    [Header("PC Mod (Fare + Klavye)")]
    [Tooltip("Fare hassasiyeti — sadece VR cihazı yokken kullanılır")]
    [Range(0.1f, 5f)]
    public float mouseSensitivity = 2f;

    [Tooltip("Oyun başlarken fare imlecini kilitle (PC modunda önerilir)")]
    public bool lockCursor = true;

    [Header("Shell Integration")]
    public bool allowRuntimeInput = true;
    public bool allowAutomaticReview = true;

    private float stareFadeTimer;
    private float headFadeTimer;

    private string currentHeadWarningMsg = "";
    private float currentHeadWarningAlpha = 0f;
    private string currentStareWarningMsg = "";
    private float currentStareWarningAlpha = 0f;

    private bool xrPrimaryWasPressed;
    private bool xrSecondaryWasPressed;
    private bool xrGripWasPressed;
    private float xrSecondaryPressStartedAt = -1f;
    private bool xrSecondaryLongPressHandled;

    private bool isVRMode;
    private float cameraPitch;
    private float cameraYaw;
    private const float VrPauseLongPressSeconds = 0.6f;

    public bool IsSessionRunning => sessionRunning;
    public bool IsSessionPaused => sessionPaused;
    public bool IsInReview => inReview;
    public bool IsVrMode => isVRMode;
    public float SessionDuration => sessionDuration;
    public float LiveSessionElapsedSeconds => sessionRunning ? GetCurrentElapsedSessionTime() : sessionDuration;
    public float LastFinalGazeScore => lastFinalGazeScore;

    public event Action SessionStarted;
    public event Action SessionPaused;
    public event Action SessionResumed;
    public event Action<float, float> SessionEnded;
    public event Action ReviewEntered;
    public event Action ReviewExited;

    public bool TryGetLiveWarningState(out string message, out float alpha)
    {
        message = string.Empty;
        alpha = 0f;

        if (!sessionRunning || sessionPaused)
        {
            return false;
        }

        if (currentHeadWarningAlpha > 0.01f)
        {
            message = currentHeadWarningMsg;
            alpha = currentHeadWarningAlpha;
            return true;
        }

        if (currentStareWarningAlpha > 0.01f)
        {
            message = currentStareWarningMsg;
            alpha = currentStareWarningAlpha;
            return true;
        }

        return false;
    }

    void Start()
    {
        // Eski (Legacy) Canvas objesi sahnede kalmışsa ve App Shell arayüzü ile çakışıyorsa onu otomatik yok et.
        if (statusText != null)
        {
            Canvas legacyCanvas = statusText.GetComponentInParent<Canvas>();
            if (legacyCanvas != null && legacyCanvas.name == "Canvas")
            {
                Debug.Log("[MainController] Auto-destroying legacy Canvas to prevent App Shell UI overlap.");
                Destroy(legacyCanvas.gameObject);
            }
        }

        List<InputDevice> headDevices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, headDevices);
        isVRMode = headDevices.Count > 0;

        if (!isVRMode && lockCursor)
        {
            ApplyDesktopCursorState(true);
        }

        if (playerHead != null)
        {
            Vector3 angles = playerHead.eulerAngles;
            cameraPitch = angles.x;
            cameraYaw = angles.y;
        }

        Debug.Log("[MainController] Initialized. Mode: " + (isVRMode ? "VR" : "PC"));
        ClearAllUI();
    }

    void Update()
    {
        if (!isVRMode) HandleMouseLook();
        HandleModeInput();

        if (sessionRunning && !sessionPaused)
        {
            UpdateSessionUI();
            UpdateWarnings();
        }
    }

    void HandleMouseLook()
    {
        if (playerHead == null) return;
        if (UnityEngine.InputSystem.Mouse.current == null) return;

        Vector2 delta = UnityEngine.InputSystem.Mouse.current.delta.ReadValue();
        cameraYaw += delta.x * mouseSensitivity * 0.1f;
        cameraPitch -= delta.y * mouseSensitivity * 0.1f;
        cameraPitch = Mathf.Clamp(cameraPitch, -80f, 80f);

        playerHead.rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
    }

    void HandleModeInput()
    {
        if (!allowRuntimeInput)
            return;

        bool xrPrimary = GetXRButton(CommonUsages.primaryButton);
        bool xrSecondary = GetXRButton(CommonUsages.secondaryButton);
        bool xrGrip = GetXRButton(CommonUsages.gripButton);

        bool mouseLeftClick = !isVRMode
                              && UnityEngine.InputSystem.Mouse.current != null
                              && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;

        bool keyboardPrimaryPressed = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame;
        bool keyboardDebugPressed = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.dKey.wasPressedThisFrame;
        bool keyboardCirclePressed = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.cKey.wasPressedThisFrame;
        bool keyboardPausePressed = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;

        bool vrPauseTogglePressed = false;
        bool vrDebugPressed = false;

        if (xrSecondary && !xrSecondaryWasPressed)
        {
            xrSecondaryPressStartedAt = Time.unscaledTime;
            xrSecondaryLongPressHandled = false;
        }

        if (xrSecondary &&
            !xrSecondaryLongPressHandled &&
            xrSecondaryPressStartedAt >= 0f &&
            sessionRunning &&
            Time.unscaledTime - xrSecondaryPressStartedAt >= VrPauseLongPressSeconds)
        {
            xrSecondaryLongPressHandled = true;
            vrPauseTogglePressed = true;
        }

        if (!xrSecondary && xrSecondaryWasPressed)
        {
            if (!xrSecondaryLongPressHandled && sessionRunning && !sessionPaused)
            {
                vrDebugPressed = true;
            }

            xrSecondaryPressStartedAt = -1f;
            xrSecondaryLongPressHandled = false;
        }

        bool rPressed = keyboardPrimaryPressed || (xrPrimary && !xrPrimaryWasPressed);
        bool dPressed = keyboardDebugPressed || vrDebugPressed;
        bool cPressed = keyboardCirclePressed || (xrGrip && !xrGripWasPressed) || mouseLeftClick;
        bool pausePressed = keyboardPausePressed || vrPauseTogglePressed;

        xrPrimaryWasPressed = xrPrimary;
        xrSecondaryWasPressed = xrSecondary;
        xrGripWasPressed = xrGrip;

        if (pausePressed && sessionRunning)
        {
            TogglePauseFromShell();
            return;
        }

        if (sessionPaused)
        {
            return;
        }

        if (rPressed)
        {
            if (inReview) ExitReview();
            else if (!sessionRunning) StartSession();
            else StopSession();
        }

        if (dPressed && sessionRunning) ToggleDebug();

        if (cPressed && sessionRunning)
        {
            Debug.Log("[MainController] Circle event toggled.");
            if (circleEvent != null)
            {
                circleEvent.ToggleEvent();
            }
        }
    }

    static bool GetXRButton(InputFeatureUsage<bool> usage)
    {
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, devices);
        foreach (InputDevice device in devices)
        {
            if (device.TryGetFeatureValue(usage, out bool value) && value) return true;
        }

        return false;
    }

    string ControlHint(string vrHint, string pcHint) => isVRMode ? vrHint : pcHint;

    void StartSession()
    {
        if (inReview)
        {
            ExitReview();
        }

        sessionRunning = true;
        sessionPaused = false;
        inReview = false;
        debugMode = false;
        sessionStartTime = Time.time;
        sessionDuration = 0f;
        pausedDurationAccum = 0f;
        pauseStartedAt = -1f;
        lastFinalGazeScore = -1f;

        if (eyeTracking != null)
        {
            eyeTracking.Activate();
            eyeTracking.SetPaused(TrackingPauseSource.PauseMenu, false);
        }

        NotifyEventSystemsSessionStarted();
        ApplyDesktopCursorState(true);

        UpdateActiveStatusText();
        SetActive(statusText, true);
        SetActive(timerText, true);
        SetActive(reviewInfoText, false);

        Debug.Log("[MainController] *** Session Started ***");
        SessionStarted?.Invoke();
    }

    void StopSession()
    {
        sessionDuration = GetCurrentElapsedSessionTime();
        sessionRunning = false;
        sessionPaused = false;
        pauseStartedAt = -1f;
        pausedDurationAccum = 0f;
        debugMode = false;

        NotifyEventSystemsSessionEnded();

        if (eyeTracking != null)
        {
            eyeTracking.SetDebugVisible(false);
            eyeTracking.SetPaused(TrackingPauseSource.PauseMenu, false);
            eyeTracking.Deactivate();
        }

        lastFinalGazeScore = gazeScoringSystem != null
            ? gazeScoringSystem.FinalizeSession()
            : -1f;

        if (lastFinalGazeScore >= 0f)
            Debug.Log(string.Format("[MainController] *** Session Ended *** Duration: {0:F1}s | Gaze Score: {1:F1}/100", sessionDuration, lastFinalGazeScore));
        else
            Debug.Log(string.Format("[MainController] *** Session Ended *** Duration: {0:F1}s | (GazeScoringSystem not assigned)", sessionDuration));

        ClearSessionUI();
        ApplyDesktopCursorState(false);
        SessionEnded?.Invoke(sessionDuration, lastFinalGazeScore);

        if (allowAutomaticReview)
            EnterReview();
    }

    void NotifyEventSystemsSessionStarted()
    {
        if (quickGazeDot != null) quickGazeDot.OnSessionStarted();
        if (movingGazeDot != null) movingGazeDot.OnSessionStarted();
    }

    void NotifyEventSystemsSessionEnded()
    {
        if (circleEvent != null) circleEvent.ForceStop();
        if (quickGazeDot != null) quickGazeDot.OnSessionEnded();
        if (movingGazeDot != null) movingGazeDot.OnSessionEnded();
        if (eventCoordinator != null) eventCoordinator.StopAll();
    }

    void ToggleDebug()
    {
        debugMode = !debugMode;
        Debug.Log("[MainController] Debug mode: " + (debugMode ? "ON" : "OFF"));
        if (eyeTracking != null)
            eyeTracking.SetDebugVisible(debugMode);

        UpdateActiveStatusText();
    }

    void EnterReview()
    {
        if (eyeTracking == null)
            return;

        inReview = true;
        eyeTracking.EnterReviewMode();

        string closeKey = isVRMode ? "A" : "R";
        string info = string.Format(
            "ISI HARİTASI SONUCU\nSüre: {0:F1}s | Bakış noktası: {1}\n" +
            "Yeşil = az bakılan | Kırmızı = çok bakılan\n" +
            "Etrafına bakarak incele | {2}: kapat",
            sessionDuration, eyeTracking.TotalPoints, closeKey);

        SetText(reviewInfoText, info);
        SetActive(reviewInfoText, true);
        SetActive(statusText, false);
        ReviewEntered?.Invoke();
    }

    void ExitReview()
    {
        inReview = false;
        if (eyeTracking != null)
            eyeTracking.ExitReviewMode();

        SetActive(reviewInfoText, false);
        ReviewExited?.Invoke();
    }

    public void StartSessionFromShell()
    {
        if (!sessionRunning)
            StartSession();
    }

    public void StopSessionFromShell()
    {
        if (sessionRunning)
            StopSession();
    }

    public void AbortSessionFromShell()
    {
        if (!sessionRunning && !sessionPaused && !inReview)
            return;

        sessionDuration = 0f;
        sessionRunning = false;
        sessionPaused = false;
        inReview = false;
        debugMode = false;
        pauseStartedAt = -1f;
        pausedDurationAccum = 0f;
        lastFinalGazeScore = -1f;

        NotifyEventSystemsSessionEnded();

        if (eyeTracking != null)
        {
            eyeTracking.SetDebugVisible(false);
            eyeTracking.SetPaused(TrackingPauseSource.PauseMenu, false);
            eyeTracking.ExitReviewMode();
            eyeTracking.Deactivate();
        }

        ClearSessionUI();
        SetActive(reviewInfoText, false);
        ApplyDesktopCursorState(false);
    }

    public void ExitReviewFromShell()
    {
        if (inReview)
            ExitReview();
    }

    public void SetShellInputEnabled(bool enabled)
    {
        allowRuntimeInput = enabled;
    }

    public void SetAutomaticReviewEnabled(bool enabled)
    {
        allowAutomaticReview = enabled;
    }

    public void PauseSessionFromShell()
    {
        if (!sessionRunning || sessionPaused)
            return;

        sessionPaused = true;
        pauseStartedAt = Time.time;
        debugMode = false;

        if (eyeTracking != null)
        {
            eyeTracking.SetDebugVisible(false);
            eyeTracking.SetPaused(TrackingPauseSource.PauseMenu, true);
        }

        ApplyDesktopCursorState(false);
        SetActive(stareWarningText, false);
        SetActive(headWarningText, false);
        SessionPaused?.Invoke();
    }

    public void ResumeSessionFromShell()
    {
        if (!sessionRunning || !sessionPaused)
            return;

        pausedDurationAccum += Mathf.Max(0f, Time.time - pauseStartedAt);
        pauseStartedAt = -1f;
        sessionPaused = false;

        if (eyeTracking != null)
            eyeTracking.SetPaused(TrackingPauseSource.PauseMenu, false);

        ApplyDesktopCursorState(true);
        UpdateActiveStatusText();
        SessionResumed?.Invoke();
    }

    public void TogglePauseFromShell()
    {
        if (sessionPaused)
            ResumeSessionFromShell();
        else
            PauseSessionFromShell();
    }

    void UpdateSessionUI()
    {
        float elapsed = GetCurrentElapsedSessionTime();
        SetText(timerText, string.Format("{0:F1}s", elapsed));
    }

    void UpdateWarnings()
    {
        if (eyeTracking == null)
            return;

        UpdateVirtualWarning(
            eyeTracking.IsStareWarning,
            "Çok uzun süredir aynı yere bakıyorsun!",
            ref stareFadeTimer, 1f, ref currentStareWarningMsg, ref currentStareWarningAlpha, stareWarningText);

        UpdateVirtualWarning(
            eyeTracking.IsHeadWarning,
            "Kafanı çok hızlı çeviriyorsun!\nDaha sakin ve akıcı hareketler yap.",
            ref headFadeTimer, 1.5f, ref currentHeadWarningMsg, ref currentHeadWarningAlpha, headWarningText);
    }

    void UpdateVirtualWarning(bool isActive, string message, ref float fadeTimer, float fadeDelay, ref string currentMsg, ref float currentAlpha, Text legacyText)
    {
        if (isActive)
        {
            currentMsg = message;
            currentAlpha = 1f;
            fadeTimer = 0f;
            if (legacyText != null)
            {
                legacyText.gameObject.SetActive(true);
                legacyText.text = message;
                Color c = legacyText.color;
                c.a = 1f;
                legacyText.color = c;
            }
        }
        else if (currentAlpha > 0f)
        {
            fadeTimer += Time.deltaTime;
            if (fadeTimer > fadeDelay)
            {
                currentAlpha -= Time.deltaTime;
                if (currentAlpha <= 0f)
                {
                    currentAlpha = 0f;
                    currentMsg = "";
                }
                
                if (legacyText != null && legacyText.gameObject.activeSelf)
                {
                    Color c = legacyText.color;
                    c.a = currentAlpha;
                    legacyText.color = c;
                    if (currentAlpha <= 0f)
                        legacyText.gameObject.SetActive(false);
                }
            }
        }
    }

    void ClearAllUI()
    {
        SetText(statusText, "");
        SetText(timerText, "");
        SetText(stareWarningText, "");
        SetText(headWarningText, "");
        SetText(reviewInfoText, "");
        SetActive(stareWarningText, false);
        SetActive(headWarningText, false);
        SetActive(reviewInfoText, false);
    }

    void ClearSessionUI()
    {
        SetText(statusText, "");
        SetText(timerText, "");
        SetText(stareWarningText, "");
        SetText(headWarningText, "");
        SetActive(stareWarningText, false);
        SetActive(headWarningText, false);
    }

    static void SetText(Text t, string s)
    {
        if (t != null) t.text = s;
    }

    static void SetActive(Text t, bool active)
    {
        if (t != null) t.gameObject.SetActive(active);
    }



    float GetCurrentElapsedSessionTime()
    {
        if (!sessionRunning)
            return sessionDuration;

        float pauseOffset = pausedDurationAccum;
        if (sessionPaused && pauseStartedAt >= 0f)
            pauseOffset += Mathf.Max(0f, Time.time - pauseStartedAt);

        return Mathf.Max(0f, Time.time - sessionStartTime - pauseOffset);
    }

    void UpdateActiveStatusText()
    {
        SetText(statusText, debugMode
            ? ControlHint(
                "KAYIT YAPILIYOR [DEBUG] (A: durdur | B: pause için basılı tut | Grip: circle)",
                "KAYIT YAPILIYOR [DEBUG] (R: durdur | D: debug kapat | Esc: pause | C/Sol-Tık: circle)")
            : ControlHint(
                "KAYIT YAPILIYOR (A: durdur | B: kısa bas debug | B: basılı tut pause | Grip: circle)",
                "KAYIT YAPILIYOR (R: durdur | D: debug | Esc: pause | C/Sol-Tık: circle event)"));
    }

    void ApplyDesktopCursorState(bool locked)
    {
        if (!lockCursor)
            return;

        bool hasDesktopInput =
            UnityEngine.InputSystem.Keyboard.current != null ||
            UnityEngine.InputSystem.Mouse.current != null;

        if (!hasDesktopInput)
            return;

        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
