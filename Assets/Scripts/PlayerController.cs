using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// VR-first player controller.
/// XR is the primary runtime mode.
/// Keyboard and mouse are only used for desktop scene testing.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Movement (Desktop Testing)")]
    public float moveSpeed = 3f;
    public float sprintSpeed = 6f;
    public float gravity = -9.81f;
    public bool movementEnabled = true;
    public bool lookEnabled = true;

    [Header("Mouse Look (Desktop Testing)")]
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 80f;

    [Header("Controller")]
    public float playerHeight = 1.7f;
    public float controllerRadius = 0.3f;

    [Header("Mode")]
    public bool enableDesktopTesting = true;
    public bool forceDesktopTestingInEditor = false;
    public float xrStartupCheckDuration = 2f;

    private enum ControlMode
    {
        XRRuntime,
        DesktopTesting,
        Idle
    }

    private CharacterController cc;
    private Camera cam;
    private float rotX = 0f;
    private float verticalVelocity = 0f;
    private float xrCheckTimer = 0f;
    private bool cameraPoseCaptured = false;
    private Vector3 initialCameraLocalPosition = Vector3.zero;
    private Quaternion initialCameraLocalRotation = Quaternion.identity;
    private ControlMode currentMode = ControlMode.Idle;

    void Start()
    {
        EnsureCharacterController();
        EnsureCamera();
        CaptureInitialCameraPose();

        xrCheckTimer = Mathf.Max(0f, xrStartupCheckDuration);
        RefreshMode(forceApply: true);
    }

    void OnValidate()
    {
        if (cc == null)
        {
            cc = GetComponent<CharacterController>();
        }

        if (cc != null)
        {
            ApplyCharacterControllerSettings();
        }
    }

    void OnDisable()
    {
        SetCursorLocked(false);
        RestoreInitialCameraPose();
    }

    void Update()
    {
        if (ShouldContinueXRStartupCheck())
        {
            xrCheckTimer -= Time.deltaTime;
            RefreshMode();
        }

        switch (currentMode)
        {
            case ControlMode.DesktopTesting:
                HandleDesktopCursorToggle();
                HandleDesktopLook();
                HandleDesktopMove();
                break;

            case ControlMode.XRRuntime:
            case ControlMode.Idle:
                ApplyMotion(Vector3.zero);
                break;
        }
    }

    void EnsureCharacterController()
    {
        cc = GetComponent<CharacterController>();
        if (cc == null)
        {
            cc = gameObject.AddComponent<CharacterController>();
        }

        ApplyCharacterControllerSettings();
    }

    void ApplyCharacterControllerSettings()
    {
        cc.height = playerHeight;
        cc.center = new Vector3(0f, playerHeight / 2f, 0f);
        cc.radius = controllerRadius;
    }

    void EnsureCamera()
    {
        cam = GetComponentInChildren<Camera>();
        if (cam == null)
        {
            var camGO = new GameObject("PlayerCamera");
            camGO.transform.SetParent(transform, false);
            camGO.transform.localPosition = new Vector3(0f, playerHeight - 0.1f, 0f);
            camGO.transform.localRotation = Quaternion.identity;

            cam = camGO.AddComponent<Camera>();
            cam.tag = "MainCamera";
            EnsureAudioListener(camGO);
            return;
        }

        EnsureAudioListener(cam.gameObject);
    }

    void EnsureAudioListener(GameObject cameraObject)
    {
        if (cameraObject.GetComponent<AudioListener>() != null)
        {
            return;
        }

        if (FindObjectsOfType<AudioListener>().Length == 0)
        {
            cameraObject.AddComponent<AudioListener>();
        }
    }

    void CaptureInitialCameraPose()
    {
        if (cam == null || cameraPoseCaptured)
        {
            return;
        }

        initialCameraLocalPosition = cam.transform.localPosition;
        initialCameraLocalRotation = cam.transform.localRotation;
        cameraPoseCaptured = true;
    }

    void PositionCameraForDesktopTesting()
    {
        if (cam == null)
        {
            return;
        }

        cam.transform.localPosition = new Vector3(0f, playerHeight - 0.1f, 0f);
        cam.transform.localRotation = Quaternion.Euler(rotX, 0f, 0f);
    }

    void RestoreInitialCameraPose()
    {
        if (cam == null || !cameraPoseCaptured)
        {
            return;
        }

        cam.transform.localPosition = initialCameraLocalPosition;
        cam.transform.localRotation = initialCameraLocalRotation;
    }

    bool ShouldContinueXRStartupCheck()
    {
        if (ShouldForceDesktopTesting())
        {
            return false;
        }

        if (currentMode == ControlMode.XRRuntime)
        {
            return false;
        }

        return xrCheckTimer > 0f;
    }

    void RefreshMode(bool forceApply = false)
    {
        ControlMode nextMode = DetermineMode();
        if (!forceApply && nextMode == currentMode)
        {
            return;
        }

        currentMode = nextMode;
        ApplyCurrentMode();
        LogCurrentMode();
    }

    ControlMode DetermineMode()
    {
        if (ShouldUseXRRuntime())
        {
            return ControlMode.XRRuntime;
        }

        if (enableDesktopTesting)
        {
            return ControlMode.DesktopTesting;
        }

        return ControlMode.Idle;
    }

    bool ShouldUseXRRuntime()
    {
        if (ShouldForceDesktopTesting())
        {
            return false;
        }

        return DetectXRRunning();
    }

    bool ShouldForceDesktopTesting()
    {
#if UNITY_EDITOR
        return forceDesktopTestingInEditor;
#else
        return false;
#endif
    }

    bool DetectXRRunning()
    {
        var xrDisplays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(xrDisplays);

        for (int i = 0; i < xrDisplays.Count; i++)
        {
            if (xrDisplays[i] != null && xrDisplays[i].running)
            {
                return true;
            }
        }

        return false;
    }

    void ApplyCurrentMode()
    {
        ApplyCharacterControllerSettings();

        switch (currentMode)
        {
            case ControlMode.XRRuntime:
                rotX = 0f;
                RestoreInitialCameraPose();
                SetCursorLocked(false);
                break;

            case ControlMode.DesktopTesting:
                rotX = 0f;
                PositionCameraForDesktopTesting();
                SetCursorLocked(true);
                break;

            case ControlMode.Idle:
                rotX = 0f;
                RestoreInitialCameraPose();
                SetCursorLocked(false);
                break;
        }
    }

    void LogCurrentMode()
    {
        switch (currentMode)
        {
            case ControlMode.XRRuntime:
                Debug.Log("[PlayerController] XR runtime mode active.");
                break;

            case ControlMode.DesktopTesting:
                Debug.Log("[PlayerController] Desktop testing mode active (WASD + mouse).");
                break;

            case ControlMode.Idle:
                Debug.Log("[PlayerController] XR not detected and desktop testing disabled.");
                break;
        }
    }

    void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void HandleDesktopCursorToggle()
    {
        if (!GetEscapePressed())
        {
            return;
        }

        bool shouldLock = Cursor.lockState != CursorLockMode.Locked;
        SetCursorLocked(shouldLock);
    }

    bool GetEscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return UnityEngine.InputSystem.Keyboard.current != null &&
               UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    void HandleDesktopLook()
    {
        if (!lookEnabled)
        {
            return;
        }
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        float mouseX = 0f;
        float mouseY = 0f;

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            Vector2 delta = UnityEngine.InputSystem.Mouse.current.delta.ReadValue();
            mouseX = delta.x * 0.05f * mouseSensitivity;
            mouseY = delta.y * 0.05f * mouseSensitivity;
        }
#else
        mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
#endif

        transform.Rotate(Vector3.up * mouseX);

        rotX -= mouseY;
        rotX = Mathf.Clamp(rotX, -maxLookAngle, maxLookAngle);
        cam.transform.localRotation = Quaternion.Euler(rotX, 0f, 0f);
    }

    void HandleDesktopMove()
    {
        if (!movementEnabled)
        {
            ApplyMotion(Vector3.zero);
            return;
        }
        float h = 0f;
        float v = 0f;
        bool sprint = false;

        if (Cursor.lockState == CursorLockMode.Locked)
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.aKey.isPressed) h -= 1f;
                if (kb.dKey.isPressed) h += 1f;
                if (kb.sKey.isPressed) v -= 1f;
                if (kb.wKey.isPressed) v += 1f;
                sprint = kb.leftShiftKey.isPressed;
            }
#else
            h = Input.GetAxis("Horizontal");
            v = Input.GetAxis("Vertical");
            sprint = Input.GetKey(KeyCode.LeftShift);
#endif
        }

        float speed = sprint ? sprintSpeed : moveSpeed;
        Vector3 horizontal = transform.right * h + transform.forward * v;
        if (horizontal.sqrMagnitude > 1f)
        {
            horizontal.Normalize();
        }

        ApplyMotion(horizontal * speed);
    }

    void ApplyMotion(Vector3 horizontalVelocity)
    {
        if (cc.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 motion = horizontalVelocity;
        motion.y = verticalVelocity;
        cc.Move(motion * Time.deltaTime);
    }
}
