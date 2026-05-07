using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

namespace VRPublicSpeaking.AppShell.Integration
{
    [DisallowMultipleComponent]
    public class EnvironmentControllerLocomotion : MonoBehaviour
    {
        [SerializeField] private XROrigin xrOrigin;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private float moveSpeed = 1.6f;
        [SerializeField] private float snapTurnDegrees = 35f;
        [SerializeField] private float snapTurnDeadzone = 0.65f;
        [SerializeField] private float snapTurnCooldown = 0.35f;
        [SerializeField] private float inputDeadzone = 0.18f;
        [SerializeField] private bool movementEnabled = true;
        [SerializeField] private bool snapTurnEnabled = true;

        private readonly List<InputDevice> leftDevices = new();
        private readonly List<InputDevice> rightDevices = new();
        private float nextSnapTurnTime;
        private bool rightStickReturnedToCenter = true;

        public void Configure(XROrigin origin)
        {
            xrOrigin = origin != null ? origin : GetComponent<XROrigin>();
            EnsureCharacterController();
        }

        private void Awake()
        {
            Configure(xrOrigin);
        }

        private void OnEnable()
        {
            RefreshDevices();
        }

        private void Update()
        {
            if (!IsXrDisplayRunning())
            {
                return;
            }

            if (xrOrigin == null)
            {
                xrOrigin = GetComponent<XROrigin>() ?? FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
            }

            if (xrOrigin == null)
            {
                return;
            }

            Vector2 leftStick = ReadStick(leftDevices, InputDeviceCharacteristics.Left);
            Vector2 rightStick = ReadStick(rightDevices, InputDeviceCharacteristics.Right);

            if (movementEnabled)
            {
                ApplyMove(leftStick);
            }

            if (snapTurnEnabled)
            {
                ApplySnapTurn(rightStick);
            }
        }

        private void ApplyMove(Vector2 input)
        {
            if (input.sqrMagnitude < inputDeadzone * inputDeadzone)
            {
                return;
            }

            Vector3 forward;
            Vector3 right;
            ResolveHeadRelativeAxes(out forward, out right);

            Vector3 move = forward * input.y + right * input.x;
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            Vector3 delta = move * (moveSpeed * Time.deltaTime);
            EnsureCharacterController();
            if (characterController != null && characterController.enabled)
            {
                characterController.Move(delta);
                return;
            }

            xrOrigin.transform.position += delta;
        }

        private void ApplySnapTurn(Vector2 input)
        {
            if (Mathf.Abs(input.x) < snapTurnDeadzone)
            {
                rightStickReturnedToCenter = true;
                return;
            }

            if (!rightStickReturnedToCenter || Time.unscaledTime < nextSnapTurnTime)
            {
                return;
            }

            float direction = input.x > 0f ? 1f : -1f;
            Transform pivot = ResolveHeadTransform();
            Vector3 pivotPosition = pivot != null ? pivot.position : xrOrigin.transform.position;

            xrOrigin.transform.RotateAround(pivotPosition, Vector3.up, snapTurnDegrees * direction);
            nextSnapTurnTime = Time.unscaledTime + snapTurnCooldown;
            rightStickReturnedToCenter = false;
        }

        private void ResolveHeadRelativeAxes(out Vector3 forward, out Vector3 right)
        {
            Transform head = ResolveHeadTransform();
            forward = head != null ? head.forward : xrOrigin.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = xrOrigin.transform.forward;
                forward.y = 0f;
            }

            forward.Normalize();
            right = Vector3.Cross(Vector3.up, forward).normalized;
        }

        private Transform ResolveHeadTransform()
        {
            return xrOrigin != null && xrOrigin.Camera != null
                ? xrOrigin.Camera.transform
                : Camera.main != null
                    ? Camera.main.transform
                    : null;
        }

        private void EnsureCharacterController()
        {
            if (xrOrigin == null)
            {
                xrOrigin = GetComponent<XROrigin>();
            }

            if (xrOrigin == null)
            {
                return;
            }

            characterController = xrOrigin.GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = xrOrigin.gameObject.AddComponent<CharacterController>();
            }

            characterController.height = 1.7f;
            characterController.radius = 0.3f;
            characterController.center = new Vector3(0f, 0.85f, 0f);
        }

        private Vector2 ReadStick(List<InputDevice> devices, InputDeviceCharacteristics handedness)
        {
            if (devices.Count == 0 || !HasValidDevice(devices))
            {
                RefreshDevices(handedness, devices);
            }

            for (int index = 0; index < devices.Count; index++)
            {
                InputDevice device = devices[index];
                if (device.isValid && device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
                {
                    return axis;
                }
            }

            return Vector2.zero;
        }

        private void RefreshDevices()
        {
            RefreshDevices(InputDeviceCharacteristics.Left, leftDevices);
            RefreshDevices(InputDeviceCharacteristics.Right, rightDevices);
        }

        private static void RefreshDevices(InputDeviceCharacteristics handedness, List<InputDevice> devices)
        {
            devices.Clear();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Controller | handedness,
                devices);
        }

        private static bool HasValidDevice(List<InputDevice> devices)
        {
            for (int index = 0; index < devices.Count; index++)
            {
                if (devices[index].isValid)
                {
                    return true;
                }
            }

            return false;
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
    }
}
