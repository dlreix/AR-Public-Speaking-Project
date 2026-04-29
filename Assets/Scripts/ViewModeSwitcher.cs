using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
using UnityEngine.XR;
using XrInputDevice = UnityEngine.XR.InputDevice;

public class ViewModeSwitcher : MonoBehaviour
{
    [Header("Camera Wiring")]
    public Camera presenterCamera;
    public Camera[] audienceCameras;
    public PlayerController playerController;

    [Header("Desktop Shortcuts")]
    [SerializeField] private bool blockDuringXrRuntime = true;
    [SerializeField] private bool logViewChanges = true;

    [Header("VR Self View")]
    [SerializeField] private bool enableVrSelfView = true;
    [SerializeField] private Vector3 vrMonitorLocalPosition = new Vector3(0.34f, -0.08f, 0.82f);
    [SerializeField] private Vector3 vrMonitorLocalEulerAngles = new Vector3(-6f, -18f, 0f);
    [SerializeField] private Vector2 vrMonitorSize = new Vector2(420f, 250f);
    [SerializeField] private float vrMonitorScale = 0.001f;
    [SerializeField] private int vrRenderTextureWidth = 1280;
    [SerializeField] private int vrRenderTextureHeight = 720;
    [SerializeField] private bool showVrMonitorHint = true;

    private int activeAudienceIndex = -1;
    private bool vrMonitorVisible;
    private bool leftThumbstickWasPressed;
    private bool rightThumbstickWasPressed;
    private readonly List<XrInputDevice> leftHandDevices = new List<XrInputDevice>(2);
    private readonly List<XrInputDevice> rightHandDevices = new List<XrInputDevice>(2);

    private RenderTexture vrMonitorRenderTexture;
    private Canvas vrMonitorCanvas;
    private RawImage vrMonitorFeedImage;
    private TMP_Text vrMonitorLabel;

    void Start()
    {
        ResolveReferencesIfNeeded();
        ActivatePresenterMode(logChange: false);
    }

    void Update()
    {
        ResolveReferencesIfNeeded();

        bool xrRuntimeActive = IsXrDisplayActive();

        if (Keyboard.current == null)
        {
            HandleVrSelfViewShortcuts(xrRuntimeActive);
            return;
        }

        if (!xrRuntimeActive || !blockDuringXrRuntime)
        {
            if (WasShortcutPressed(Keyboard.current.digit5Key, Keyboard.current.numpad5Key, Keyboard.current.f5Key))
            {
                ActivatePresenterMode();
            }

            if (WasShortcutPressed(Keyboard.current.digit6Key, Keyboard.current.numpad6Key, Keyboard.current.f6Key))
            {
                ActivateAudienceCamera(0);
            }

            if (WasShortcutPressed(Keyboard.current.digit7Key, Keyboard.current.numpad7Key, Keyboard.current.f7Key))
            {
                ActivateAudienceCamera(1);
            }

            if (WasShortcutPressed(Keyboard.current.digit8Key, Keyboard.current.numpad8Key, Keyboard.current.f8Key))
            {
                ActivateAudienceCamera(2);
            }
        }

        if (enableVrSelfView)
        {
            if (Keyboard.current.vKey.wasPressedThisFrame)
            {
                ToggleVrSelfViewMonitor();
            }

            if (Keyboard.current.nKey.wasPressedThisFrame)
            {
                CycleVrSelfViewCamera(+1);
            }

            if (Keyboard.current.mKey.wasPressedThisFrame)
            {
                CycleVrSelfViewCamera(-1);
            }
        }

        HandleVrSelfViewShortcuts(xrRuntimeActive);
    }

    void OnDisable()
    {
        HideVrSelfViewMonitor();
    }

    public void ActivatePresenterMode()
    {
        ActivatePresenterMode(logChange: true);
    }

    void ActivatePresenterMode(bool logChange)
    {
        ResolveReferencesIfNeeded();

        if (IsXrDisplayActive())
        {
            HideVrSelfViewMonitor();
            UpdateManagedCameraTags(presenterCamera);

            if (logChange && logViewChanges)
                Debug.Log("Presenter Mode Active");

            return;
        }

        if (presenterCamera != null)
            presenterCamera.enabled = true;

        if (audienceCameras != null)
        {
            foreach (Camera cam in audienceCameras)
            {
                if (cam != null)
                    cam.enabled = false;
            }
        }

        activeAudienceIndex = -1;
        UpdateManagedCameraTags(presenterCamera);

        if (playerController != null)
        {
            playerController.movementEnabled = true;
            playerController.lookEnabled = true;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (logChange && logViewChanges)
            Debug.Log("Presenter Mode Active");
    }

    public void ActivateAudienceCamera(int index)
    {
        ResolveReferencesIfNeeded();

        if (IsXrDisplayActive())
        {
            ShowVrSelfViewCamera(index);
            return;
        }

        if (presenterCamera != null)
            presenterCamera.enabled = false;

        if (audienceCameras != null)
        {
            for (int i = 0; i < audienceCameras.Length; i++)
            {
                if (audienceCameras[i] != null)
                    audienceCameras[i].enabled = (i == index);
            }
        }

        activeAudienceIndex = index;
        Camera activeAudienceCamera =
            audienceCameras != null && index >= 0 && index < audienceCameras.Length
                ? audienceCameras[index]
                : null;
        UpdateManagedCameraTags(activeAudienceCamera);

        if (playerController != null)
        {
            playerController.movementEnabled = false;
            playerController.lookEnabled = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (logViewChanges)
            Debug.Log("Audience Camera " + (index + 1) + " Active");
    }

    public void ToggleVrSelfViewMonitor()
    {
        if (!enableVrSelfView)
            return;

        if (vrMonitorVisible)
        {
            HideVrSelfViewMonitor();
            return;
        }

        int targetIndex = activeAudienceIndex >= 0 ? activeAudienceIndex : 0;
        ShowVrSelfViewCamera(targetIndex);
    }

    public void CycleVrSelfViewCamera(int step)
    {
        if (!enableVrSelfView || audienceCameras == null || audienceCameras.Length == 0)
            return;

        int nextIndex = activeAudienceIndex;
        if (nextIndex < 0)
            nextIndex = 0;
        else
            nextIndex = ((nextIndex + step) % audienceCameras.Length + audienceCameras.Length) % audienceCameras.Length;

        ShowVrSelfViewCamera(nextIndex);
    }

    void ResolveReferencesIfNeeded()
    {
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        }

        if (presenterCamera == null)
        {
            if (playerController != null)
            {
                presenterCamera = playerController.GetComponentInChildren<Camera>(true);
            }

            if (presenterCamera == null)
            {
                presenterCamera = Camera.main;
            }

            if (presenterCamera == null)
            {
                presenterCamera = FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
            }
        }
    }

    void HandleVrSelfViewShortcuts(bool xrRuntimeActive)
    {
        if (!enableVrSelfView || !xrRuntimeActive)
        {
            leftThumbstickWasPressed = false;
            rightThumbstickWasPressed = false;
            return;
        }

        bool leftThumbstickPressed = GetXrNodeButton(XRNode.LeftHand, UnityEngine.XR.CommonUsages.primary2DAxisClick);
        bool rightThumbstickPressed = GetXrNodeButton(XRNode.RightHand, UnityEngine.XR.CommonUsages.primary2DAxisClick);

        if (leftThumbstickPressed && !leftThumbstickWasPressed)
        {
            ToggleVrSelfViewMonitor();
        }

        if (rightThumbstickPressed && !rightThumbstickWasPressed)
        {
            CycleVrSelfViewCamera(+1);
        }

        leftThumbstickWasPressed = leftThumbstickPressed;
        rightThumbstickWasPressed = rightThumbstickPressed;
    }

    void ShowVrSelfViewCamera(int index)
    {
        ResolveReferencesIfNeeded();

        if (!enableVrSelfView || presenterCamera == null || audienceCameras == null || audienceCameras.Length == 0)
            return;

        index = Mathf.Clamp(index, 0, audienceCameras.Length - 1);

        EnsureVrMonitorVisuals();

        for (int i = 0; i < audienceCameras.Length; i++)
        {
            Camera camera = audienceCameras[i];
            if (camera == null)
                continue;

            bool isActive = i == index;
            camera.targetTexture = isActive ? vrMonitorRenderTexture : null;
            camera.enabled = isActive;
            camera.tag = "Untagged";

            AudioListener listener = camera.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = false;
        }

        presenterCamera.enabled = true;
        presenterCamera.tag = "MainCamera";

        activeAudienceIndex = index;
        vrMonitorVisible = true;
        if (vrMonitorCanvas != null)
            vrMonitorCanvas.gameObject.SetActive(true);

        UpdateVrMonitorLabel();

        if (logViewChanges)
            Debug.Log("VR Self View Camera " + (index + 1) + " Active");
    }

    void HideVrSelfViewMonitor()
    {
        if (audienceCameras != null)
        {
            for (int i = 0; i < audienceCameras.Length; i++)
            {
                Camera camera = audienceCameras[i];
                if (camera == null)
                    continue;

                camera.targetTexture = null;
                camera.enabled = false;
                camera.tag = "Untagged";
            }
        }

        if (presenterCamera != null)
        {
            presenterCamera.enabled = true;
            presenterCamera.tag = "MainCamera";
        }

        vrMonitorVisible = false;
        activeAudienceIndex = -1;

        if (vrMonitorCanvas != null)
            vrMonitorCanvas.gameObject.SetActive(false);
    }

    void EnsureVrMonitorVisuals()
    {
        if (presenterCamera == null)
            return;

        if (vrMonitorRenderTexture == null)
        {
            vrMonitorRenderTexture = new RenderTexture(vrRenderTextureWidth, vrRenderTextureHeight, 16)
            {
                name = "VrSelfViewMonitorRT"
            };
            vrMonitorRenderTexture.Create();
        }

        if (vrMonitorCanvas != null)
            return;

        GameObject monitorRoot = new GameObject("VrSelfViewMonitor");
        monitorRoot.transform.SetParent(presenterCamera.transform, false);
        monitorRoot.transform.localPosition = vrMonitorLocalPosition;
        monitorRoot.transform.localRotation = Quaternion.Euler(vrMonitorLocalEulerAngles);
        monitorRoot.transform.localScale = Vector3.one * vrMonitorScale;

        vrMonitorCanvas = monitorRoot.AddComponent<Canvas>();
        vrMonitorCanvas.renderMode = RenderMode.WorldSpace;
        vrMonitorCanvas.worldCamera = presenterCamera;
        vrMonitorCanvas.sortingOrder = 50;

        CanvasScaler scaler = monitorRoot.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        scaler.referencePixelsPerUnit = 100f;

        RectTransform rootRect = monitorRoot.GetComponent<RectTransform>();
        rootRect.sizeDelta = vrMonitorSize;

        GameObject backgroundRoot = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundRoot.transform.SetParent(monitorRoot.transform, false);
        RectTransform backgroundRect = backgroundRoot.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image backgroundImage = backgroundRoot.GetComponent<Image>();
        backgroundImage.color = new Color(0.03f, 0.05f, 0.08f, 0.92f);
        backgroundImage.raycastTarget = false;

        GameObject feedRoot = new GameObject("Feed", typeof(RectTransform), typeof(RawImage));
        feedRoot.transform.SetParent(monitorRoot.transform, false);
        RectTransform feedRect = feedRoot.GetComponent<RectTransform>();
        feedRect.anchorMin = new Vector2(0f, 0f);
        feedRect.anchorMax = new Vector2(1f, 1f);
        feedRect.offsetMin = new Vector2(18f, 18f);
        feedRect.offsetMax = new Vector2(-18f, -54f);
        vrMonitorFeedImage = feedRoot.GetComponent<RawImage>();
        vrMonitorFeedImage.texture = vrMonitorRenderTexture;
        vrMonitorFeedImage.raycastTarget = false;

        GameObject labelRoot = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelRoot.transform.SetParent(monitorRoot.transform, false);
        RectTransform labelRect = labelRoot.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(0f, -14f);
        labelRect.sizeDelta = new Vector2(-32f, 34f);

        vrMonitorLabel = labelRoot.GetComponent<TextMeshProUGUI>();
        vrMonitorLabel.fontSize = 22f;
        vrMonitorLabel.fontStyle = FontStyles.Bold;
        vrMonitorLabel.color = Color.white;
        vrMonitorLabel.alignment = TextAlignmentOptions.Center;
        vrMonitorLabel.raycastTarget = false;

        monitorRoot.SetActive(false);
    }

    void UpdateVrMonitorLabel()
    {
        if (vrMonitorLabel == null)
            return;

        string label = activeAudienceIndex >= 0
            ? $"SELF VIEW • CAM {activeAudienceIndex + 1}"
            : "SELF VIEW";

        if (showVrMonitorHint)
        {
            label += "\nL Stick Click: Toggle • R Stick Click: Next";
        }

        vrMonitorLabel.text = label;
    }

    void UpdateManagedCameraTags(Camera activeCamera)
    {
        if (presenterCamera != null)
        {
            presenterCamera.tag = activeCamera == presenterCamera ? "MainCamera" : "Untagged";
        }

        if (audienceCameras == null)
            return;

        for (int i = 0; i < audienceCameras.Length; i++)
        {
            if (audienceCameras[i] == null)
                continue;

            audienceCameras[i].tag = audienceCameras[i] == activeCamera ? "MainCamera" : "Untagged";
        }
    }

    static bool WasShortcutPressed(KeyControl primary, KeyControl secondary, KeyControl tertiary)
    {
        return (primary != null && primary.wasPressedThisFrame)
               || (secondary != null && secondary.wasPressedThisFrame)
               || (tertiary != null && tertiary.wasPressedThisFrame);
    }

    static bool IsXrDisplayActive()
    {
        var xrDisplays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(xrDisplays);

        for (int i = 0; i < xrDisplays.Count; i++)
        {
            if (xrDisplays[i] != null && xrDisplays[i].running)
                return true;
        }

        return false;
    }

    bool GetXrNodeButton(XRNode node, InputFeatureUsage<bool> usage)
    {
        List<XrInputDevice> devices = node == XRNode.LeftHand ? leftHandDevices : rightHandDevices;
        devices.Clear();
        InputDevices.GetDevicesAtXRNode(node, devices);

        for (int i = 0; i < devices.Count; i++)
        {
            if (devices[i].TryGetFeatureValue(usage, out bool value) && value)
                return true;
        }

        return false;
    }
}
