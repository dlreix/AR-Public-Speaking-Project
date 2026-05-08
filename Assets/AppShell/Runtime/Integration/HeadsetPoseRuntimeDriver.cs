using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace VRPublicSpeaking.AppShell.Integration
{
    [DisallowMultipleComponent]
    public class HeadsetPoseRuntimeDriver : MonoBehaviour
    {
        [SerializeField] private bool applyPosition = true;
        [SerializeField] private bool applyRotation = true;

        private readonly List<InputDevice> headDevices = new();

        private void OnEnable()
        {
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
                transform.localPosition = position;
            }

            if (applyRotation &&
                headDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            {
                transform.localRotation = rotation;
            }
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
