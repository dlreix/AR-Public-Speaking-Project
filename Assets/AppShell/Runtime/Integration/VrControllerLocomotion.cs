using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using VRPublicSpeaking.AppShell.Core;

namespace VRPublicSpeaking.AppShell.Integration
{
    [DisallowMultipleComponent]
    public class VrControllerLocomotion : MonoBehaviour
    {
        [SerializeField] private XROrigin xrOrigin;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private float moveSpeed = 2.2f;
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private bool useGravity;
        [SerializeField] private float moveDeadzone = 0.18f;
        [SerializeField] private float turnDeadzone = 0.72f;
        [SerializeField] private float snapTurnAmount = 45f;
        [SerializeField] private float snapTurnCooldown = 0.35f;
        [SerializeField] private bool blockWhenSessionOverlayVisible = true;

        private readonly List<InputDevice> leftControllers = new List<InputDevice>();
        private readonly List<InputDevice> rightControllers = new List<InputDevice>();
        private readonly List<InputDevice> allControllers = new List<InputDevice>();
        private AppRuntimeState runtimeState;
        private MainController mainController;
        private float verticalVelocity;
        private float nextSnapTurnTime;

        public static VrControllerLocomotion EnsureForScene(Camera camera)
        {
            Transform host = ResolveRigHost(camera);
            if (host == null)
            {
                return null;
            }

            VrControllerLocomotion locomotion = host.GetComponent<VrControllerLocomotion>();
            if (locomotion == null)
            {
                locomotion = host.gameObject.AddComponent<VrControllerLocomotion>();
            }

            locomotion.Configure(camera);
            return locomotion;
        }

        public void Configure(Camera camera)
        {
            targetCamera = camera != null ? camera : targetCamera;
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            RefreshControllerDevices();
        }

        private void Update()
        {
            ResolveReferences();

            if (!ShouldProcessInput())
            {
                return;
            }

            Vector2 moveInput = ReadMoveInput();
            ApplyMove(moveInput);
            ApplySnapTurn(ReadTurnInput());
        }

        private void ResolveReferences()
        {
            if (targetCamera == null)
            {
                targetCamera = VrRigRuntimeUtility.ResolveSceneCamera();
            }

            if (xrOrigin == null && targetCamera != null)
            {
                xrOrigin = targetCamera.GetComponentInParent<XROrigin>(true);
            }

            if (playerController == null && targetCamera != null)
            {
                playerController = targetCamera.GetComponentInParent<PlayerController>(true);
            }

            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }

            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (characterController == null && playerController != null)
            {
                characterController = playerController.GetComponent<CharacterController>();
            }

            if (characterController == null)
            {
                characterController = gameObject.AddComponent<CharacterController>();
            }

            ApplyCharacterControllerDefaults();

            if (mainController == null)
            {
                mainController = FindFirstObjectByType<MainController>(FindObjectsInactive.Include);
            }

            if (runtimeState == null && AppRuntimeState.HasInstance)
            {
                runtimeState = AppRuntimeState.Instance;
            }
        }

        private void ApplyCharacterControllerDefaults()
        {
            if (characterController == null)
            {
                return;
            }

            float height = playerController != null
                ? Mathf.Max(1.1f, playerController.playerHeight)
                : 1.7f;
            float radius = playerController != null
                ? Mathf.Max(0.1f, playerController.controllerRadius)
                : 0.28f;

            characterController.height = height;
            characterController.radius = radius;
            characterController.center = new Vector3(0f, height * 0.5f, 0f);
        }

        private bool ShouldProcessInput()
        {
            if (!HasControllerDevice())
            {
                RefreshControllerDevices();
                if (!HasControllerDevice())
                {
                    return false;
                }
            }

            if (!IsXrDisplayRunning())
            {
                return false;
            }

            if (!blockWhenSessionOverlayVisible)
            {
                return true;
            }

            if (mainController != null && mainController.IsSessionPaused)
            {
                return false;
            }

            var runtime = runtimeState != null ? runtimeState.CurrentRuntimeState : null;
            return runtime == null || (!runtime.PauseMenuVisible && !runtime.ResultsOverlayVisible);
        }

        private Vector2 ReadMoveInput()
        {
            Vector2 left = ReadPrimaryAxis(leftControllers);
            if (left.sqrMagnitude >= moveDeadzone * moveDeadzone)
            {
                return left;
            }

            return Vector2.zero;
        }

        private Vector2 ReadTurnInput()
        {
            Vector2 right = ReadPrimaryAxis(rightControllers);
            return Mathf.Abs(right.x) >= turnDeadzone ? right : Vector2.zero;
        }

        private static Vector2 ReadPrimaryAxis(List<InputDevice> devices)
        {
            for (int index = 0; index < devices.Count; index++)
            {
                InputDevice device = devices[index];
                if (!device.isValid)
                {
                    continue;
                }

                if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 value))
                {
                    return value;
                }
            }

            return Vector2.zero;
        }

        private void ApplyMove(Vector2 moveInput)
        {
            Vector3 horizontalVelocity = Vector3.zero;
            if (moveInput.sqrMagnitude >= moveDeadzone * moveDeadzone)
            {
                Transform viewTransform = ResolveViewTransform();
                Vector3 forward = Vector3.ProjectOnPlane(viewTransform.forward, Vector3.up);
                if (forward.sqrMagnitude < 0.0001f)
                {
                    forward = Vector3.forward;
                }

                forward.Normalize();
                Vector3 right = Vector3.ProjectOnPlane(viewTransform.right, Vector3.up);
                if (right.sqrMagnitude < 0.0001f)
                {
                    right = Vector3.right;
                }

                right.Normalize();
                Vector3 desired = (forward * moveInput.y) + (right * moveInput.x);
                if (desired.sqrMagnitude > 1f)
                {
                    desired.Normalize();
                }

                horizontalVelocity = desired * moveSpeed;
            }

            MoveRig(horizontalVelocity);
        }

        private void MoveRig(Vector3 horizontalVelocity)
        {
            float deltaTime = Time.deltaTime;

            if (characterController != null && characterController.enabled && characterController.gameObject.activeInHierarchy)
            {
                if (useGravity)
                {
                    if (characterController.isGrounded && verticalVelocity < 0f)
                    {
                        verticalVelocity = -2f;
                    }

                    verticalVelocity += gravity * deltaTime;
                }
                else
                {
                    verticalVelocity = 0f;
                }

                Vector3 motion = horizontalVelocity;
                motion.y = verticalVelocity;
                characterController.Move(motion * deltaTime);
                return;
            }

            Transform rigTransform = ResolveRigTransform();
            if (rigTransform != null)
            {
                rigTransform.position += horizontalVelocity * deltaTime;
            }
        }

        private void ApplySnapTurn(Vector2 turnInput)
        {
            if (turnInput == Vector2.zero || Time.unscaledTime < nextSnapTurnTime)
            {
                return;
            }

            float direction = Mathf.Sign(turnInput.x);
            float angle = snapTurnAmount * direction;

            if (xrOrigin != null)
            {
                xrOrigin.RotateAroundCameraUsingOriginUp(angle);
            }
            else
            {
                Transform rigTransform = ResolveRigTransform();
                Transform viewTransform = ResolveViewTransform();
                if (rigTransform != null && viewTransform != null)
                {
                    rigTransform.RotateAround(viewTransform.position, Vector3.up, angle);
                }
            }

            nextSnapTurnTime = Time.unscaledTime + snapTurnCooldown;
        }

        private Transform ResolveViewTransform()
        {
            if (targetCamera != null)
            {
                return targetCamera.transform;
            }

            Camera mainCamera = Camera.main;
            return mainCamera != null ? mainCamera.transform : transform;
        }

        private Transform ResolveRigTransform()
        {
            if (xrOrigin != null && xrOrigin.Origin != null)
            {
                return xrOrigin.Origin.transform;
            }

            return transform;
        }

        private bool HasControllerDevice()
        {
            for (int index = 0; index < allControllers.Count; index++)
            {
                if (allControllers[index].isValid)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshControllerDevices()
        {
            leftControllers.Clear();
            rightControllers.Clear();
            allControllers.Clear();

            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left,
                leftControllers);
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right,
                rightControllers);
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Controller,
                allControllers);
        }

        private static bool IsXrDisplayRunning()
        {
            var xrDisplays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(xrDisplays);

            for (int index = 0; index < xrDisplays.Count; index++)
            {
                XRDisplaySubsystem display = xrDisplays[index];
                if (display != null && display.running)
                {
                    return true;
                }
            }

            return false;
        }

        private static Transform ResolveRigHost(Camera camera)
        {
            if (camera == null)
            {
                camera = VrRigRuntimeUtility.ResolveSceneCamera();
            }

            XROrigin origin = camera != null ? camera.GetComponentInParent<XROrigin>(true) : null;
            if (origin != null)
            {
                return origin.transform;
            }

            PlayerController player = camera != null ? camera.GetComponentInParent<PlayerController>(true) : null;
            if (player != null)
            {
                return player.transform;
            }

            return camera != null ? camera.transform.root : null;
        }
    }
}
