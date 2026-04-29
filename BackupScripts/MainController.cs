using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using System.Collections.Generic;

/// <summary>
/// Ana kontrolcü: UI yönetimi ve mod geçişleri.
/// VR: Kamera rotasyonu XR SDK tarafından otomatik yönetilir; bu script kameraya dokunmaz.
/// Hierarchy'de boş bir GameObject'e (örn. GameManager) atanır.
///
/// Kontroller:
///   VR  → Sağ/Sol controller:  A/X (primaryButton)   = oturum başlat/durdur/review kapat
///                               B/Y (secondaryButton) = debug modu (oturum sırasında)
///                               Grip                  = circle event (oturum sırasında)
///   PC  → Klavye:              R = oturum | D = debug | C = circle event
///          Fare:               Sol tık = circle event | Hareket = bakış yönü
/// </summary>
public class MainController : MonoBehaviour
{
    // ──────────────────────────────────────────────
    //  REFERANSLAR
    // ──────────────────────────────────────────────
    [Header("Sistem Referansları")]
    [Tooltip("Main Camera üzerindeki EyeTrackingSystem scripti")]
    public EyeTrackingSystem eyeTracking;

    [Tooltip("Bu obje veya başka bir obje üzerindeki CircleEventSystem scripti")]
    public CircleEventSystem circleEvent;

    [Tooltip("GazeScoringSystem scripti (oturum sonu skoru için)")]
    public GazeScoringSystem gazeScoringSystem;

    [Tooltip("XR Origin içindeki kamera transformu (bilgi amaçlı referans; VR SDK tarafından yönetilir)")]
    public Transform playerHead;

    [Tooltip("VR kamerası (XR Origin > Camera Offset > Main Camera)")]
    public Camera mainCamera;

    // ──────────────────────────────────────────────
    //  UI REFERANSLARI
    // ──────────────────────────────────────────────
    [Header("UI Elemanları")]
    public Text statusText;
    public Text timerText;
    public Text stareWarningText;
    public Text headWarningText;
    public Text reviewInfoText;

    // ──────────────────────────────────────────────
    //  ÖZEL DURUM DEĞİŞKENLERİ
    // ──────────────────────────────────────────────
    private bool sessionRunning;
    private bool debugMode;
    private bool inReview;
    private float sessionStartTime;
    private float sessionDuration;

    // ──────────────────────────────────────────────
    //  PC MOD AYARLARI
    // ──────────────────────────────────────────────
    [Header("PC Mod (Fare + Klavye)")]
    [Tooltip("Fare hassasiyeti — sadece VR cihazı yokken kullanılır")]
    [Range(0.1f, 5f)]
    public float mouseSensitivity = 2f;

    [Tooltip("Oyun başlarken fare imlecini kilitle (PC modunda önerilir)")]
    public bool lockCursor = true;

    // Uyarı fade zamanlayıcıları
    private float stareFadeTimer;
    private float headFadeTimer;

    // VR controller önceki kare durumu ("bu karede mi basıldı?" için)
    private bool xrPrimaryWasPressed;
    private bool xrSecondaryWasPressed;
    private bool xrGripWasPressed;

    // PC mod: fare bakış durumu
    private bool isVRMode;
    private float cameraPitch;
    private float cameraYaw;

    // ══════════════════════════════════════════════
    //  YAŞAM DÖNGÜSÜ
    // ══════════════════════════════════════════════

    void Start()
    {
        // VR cihazı var mı kontrol et
        List<InputDevice> headDevices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, headDevices);
        isVRMode = headDevices.Count > 0;

        if (!isVRMode && lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (playerHead != null)
        {
            Vector3 angles = playerHead.eulerAngles;
            cameraPitch = angles.x;
            cameraYaw   = angles.y;
        }

        Debug.Log("[MainController] Initialized. Mode: " + (isVRMode ? "VR" : "PC"));

        ClearAllUI();
    }

    void Update()
    {
        if (!isVRMode) HandleMouseLook();
        HandleModeInput();

        if (sessionRunning)
        {
            UpdateSessionUI();
            UpdateWarnings();
        }
    }

    // ══════════════════════════════════════════════
    //  MOD YÖNETİMİ
    // ══════════════════════════════════════════════

    /// <summary>PC modunda fareyle bakış yönünü döndürür.</summary>
    void HandleMouseLook()
    {
        if (playerHead == null) return;
        if (UnityEngine.InputSystem.Mouse.current == null) return;

        Vector2 delta = UnityEngine.InputSystem.Mouse.current.delta.ReadValue();
        cameraYaw   += delta.x * mouseSensitivity * 0.1f;
        cameraPitch -= delta.y * mouseSensitivity * 0.1f;
        cameraPitch  = Mathf.Clamp(cameraPitch, -80f, 80f);

        playerHead.rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
    }

    void HandleModeInput()
    {
        // VR controller anlık durumu
        bool xrPrimary   = GetXRButton(CommonUsages.primaryButton);   // A / X
        bool xrSecondary = GetXRButton(CommonUsages.secondaryButton); // B / Y
        bool xrGrip      = GetXRButton(CommonUsages.gripButton);      // Grip

        // Fare sol tık (PC modunda circle event için)
        bool mouseLeftClick = !isVRMode
                              && UnityEngine.InputSystem.Mouse.current != null
                              && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;

        // "Bu karede basıldı mı?" — klavye veya VR controller
        bool rPressed = (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame)
                        || (xrPrimary   && !xrPrimaryWasPressed);
        bool dPressed = (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.dKey.wasPressedThisFrame)
                        || (xrSecondary && !xrSecondaryWasPressed);
        bool cPressed = (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.cKey.wasPressedThisFrame)
                        || (xrGrip      && !xrGripWasPressed)
                        || mouseLeftClick;

        // Önceki kare durumunu sakla
        xrPrimaryWasPressed   = xrPrimary;
        xrSecondaryWasPressed = xrSecondary;
        xrGripWasPressed      = xrGrip;

        // R / A: Oturum başlat / durdur / review kapat
        if (rPressed)
        {
            if (inReview)              ExitReview();
            else if (!sessionRunning)  StartSession();
            else                       StopSession();
        }

        // D / B: Debug modu (sadece oturum sırasında)
        if (dPressed && sessionRunning) ToggleDebug();

        // C / Grip: Circle event (sadece oturum sırasında)
        if (cPressed && sessionRunning)
        {
            Debug.Log("[MainController] Circle event toggled.");
            circleEvent.ToggleEvent();
        }
    }

    /// <summary>Herhangi bir VR controller'da belirtilen düğmeye basılıp basılmadığını döner.</summary>
    static bool GetXRButton(InputFeatureUsage<bool> usage)
    {
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, devices);
        foreach (InputDevice device in devices)
            if (device.TryGetFeatureValue(usage, out bool value) && value) return true;
        return false;
    }

    string ControlHint(string vrHint, string pcHint) => isVRMode ? vrHint : pcHint;

    void StartSession()
    {
        sessionRunning = true;
        inReview = false;
        debugMode = false;
        sessionStartTime = Time.time;

        eyeTracking.Activate();
        SetText(statusText, ControlHint(
            "KAYIT YAPILIYOR (A: durdur | B: debug | Grip: circle event)",
            "KAYIT YAPILIYOR (R: durdur | D: debug | C/Sol-Tık: circle event)"));
        SetActive(statusText, true);
        SetActive(timerText, true);
        SetActive(reviewInfoText, false);

        Debug.Log("[MainController] *** Session Started ***");
    }

    void StopSession()
    {
        sessionRunning = false;
        sessionDuration = Time.time - sessionStartTime;

        eyeTracking.Deactivate();
        circleEvent.ForceStop();

        float finalScore = gazeScoringSystem != null
            ? gazeScoringSystem.FinalizeSession()
            : -1f;

        if (finalScore >= 0f)
            Debug.Log(string.Format("[MainController] *** Session Ended *** Duration: {0:F1}s | Gaze Score: {1:F1}/100", sessionDuration, finalScore));
        else
            Debug.Log(string.Format("[MainController] *** Session Ended *** Duration: {0:F1}s | (GazeScoringSystem not assigned)", sessionDuration));

        ClearSessionUI();
        EnterReview();
    }

    void ToggleDebug()
    {
        debugMode = !debugMode;
        Debug.Log("[MainController] Debug mode: " + (debugMode ? "ON" : "OFF"));
        eyeTracking.SetDebugVisible(debugMode);

        SetText(statusText, debugMode
            ? ControlHint(
                "KAYIT YAPILIYOR [DEBUG] (A: durdur | B: debug kapat | Grip: circle)",
                "KAYIT YAPILIYOR [DEBUG] (R: durdur | D: debug kapat | C/Sol-Tık: circle)")
            : ControlHint(
                "KAYIT YAPILIYOR (A: durdur | B: debug | Grip: circle event)",
                "KAYIT YAPILIYOR (R: durdur | D: debug | C/Sol-Tık: circle event)"));
    }

    void EnterReview()
    {
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
    }

    void ExitReview()
    {
        inReview = false;
        eyeTracking.ExitReviewMode();
        SetActive(reviewInfoText, false);
    }

    // ══════════════════════════════════════════════
    //  UYARI SİSTEMİ
    // ══════════════════════════════════════════════

    void UpdateSessionUI()
    {
        float elapsed = Time.time - sessionStartTime;
        SetText(timerText, string.Format("{0:F1}s", elapsed));
    }

    void UpdateWarnings()
    {
        UpdateFadingWarning(
            stareWarningText,
            eyeTracking.IsStareWarning,
            "Çok uzun süredir aynı yere bakıyorsun!",
            ref stareFadeTimer, 1f);

        UpdateFadingWarning(
            headWarningText,
            eyeTracking.IsHeadWarning,
            "Kafanı çok hızlı çeviriyorsun!\nDaha sakin ve akıcı hareketler yap.",
            ref headFadeTimer, 1.5f);
    }

    void UpdateFadingWarning(Text textElement, bool isActive, string message,
                              ref float fadeTimer, float fadeDelay)
    {
        if (textElement == null) return;

        if (isActive)
        {
            textElement.gameObject.SetActive(true);
            textElement.text = message;
            Color c = textElement.color;
            c.a = 1f;
            textElement.color = c;
            fadeTimer = 0f;
        }
        else if (textElement.gameObject.activeSelf)
        {
            fadeTimer += Time.deltaTime;
            if (fadeTimer > fadeDelay)
            {
                Color c = textElement.color;
                c.a -= Time.deltaTime;
                textElement.color = c;
                if (c.a <= 0f)
                    textElement.gameObject.SetActive(false);
            }
        }
    }

    // ══════════════════════════════════════════════
    //  YARDIMCI METODLAR
    // ══════════════════════════════════════════════

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
}
