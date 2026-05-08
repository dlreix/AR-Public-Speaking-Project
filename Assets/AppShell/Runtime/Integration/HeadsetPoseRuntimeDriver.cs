using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

namespace VRPublicSpeaking.AppShell.Integration
{
    [DisallowMultipleComponent]
    public class HeadsetPoseRuntimeDriver : MonoBehaviour
    {
        [SerializeField] private bool applyPosition = true;
        [SerializeField] private bool applyRotation = true;
        [SerializeField] private float cameraOnlyFallbackHeight = 1.6f;
        [SerializeField] private float minimumValidHeadHeight = 0.35f;

        private readonly List<InputDevice> headDevices = new();
        private XROrigin xrOrigin;
        private PlayerController playerController;

        private void OnEnable()
        {
            CacheSceneReferences();
            RefreshHeadDevices();
            Application.onBeforeRender += ApplyHeadsetPose;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= ApplyHeadsetPose;
        }

        private void LateUpdate()
        {
            ApplyHeadsetPose();
        }

        private void ApplyHeadsetPose()
        {
            if (!TryGetHeadDevice(out InputDevice headDevice))
            {
                return;
            }

            if (applyPosition &&
                headDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
            {
                transform.localPosition = ResolveSafeLocalPosition(position);
            }

            if (applyRotation &&
                headDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            {
                transform.localRotation = rotation;
            }
        }

        private void CacheSceneReferences()
        {
            xrOrigin = GetComponentInParent<XROrigin>(true);
            playerController = GetComponentInParent<PlayerController>(true);
        }

        private Vector3 ResolveSafeLocalPosition(Vector3 headsetLocalPosition)
        {
            if (xrOrigin != null || headsetLocalPosition.y >= minimumValidHeadHeight)
            {
                return headsetLocalPosition;
            }

            headsetLocalPosition.y = ResolveCameraOnlyFallbackHeight();
            return headsetLocalPosition;
        }

        private float ResolveCameraOnlyFallbackHeight()
        {
            if (playerController == null)
            {
                playerController = GetComponentInParent<PlayerController>(true);
            }

            return playerController != null
                ? Mathf.Max(minimumValidHeadHeight, playerController.playerHeight - 0.1f)
                : Mathf.Max(minimumValidHeadHeight, cameraOnlyFallbackHeight);
        }

        private bool TryGetHeadDevice(out InputDevice headDevice)
        {
            if (headDevices.Count == 0 || !HasValidDevice())
            {
                RefreshHeadDevices();
            }

            for (int index = 0; index < headDevices.Count; index++)
            {
                InputDevice device = headDevices[index];
                if (device.isValid)
                {
                    headDevice = device;
                    return true;
                }
            }

            headDevice = default;
            return false;
        }

        private void RefreshHeadDevices()
        {
            headDevices.Clear();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.HeadMounted,
                headDevices);
        }

        private bool HasValidDevice()
        {
            for (int index = 0; index < headDevices.Count; index++)
            {
                if (headDevices[index].isValid)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
