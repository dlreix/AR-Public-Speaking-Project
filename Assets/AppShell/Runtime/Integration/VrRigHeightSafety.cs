using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

namespace VRPublicSpeaking.AppShell.Integration
{
    [DisallowMultipleComponent]
    public class VrRigHeightSafety : MonoBehaviour
    {
        [SerializeField] private XROrigin xrOrigin;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private bool useFloorTrackingWhenXrRunning = true;
        [SerializeField] private bool keepGravityDisabled = true;
        [SerializeField] private float deviceCameraYOffset = 1.36f;
        [SerializeField] private float floorTrackingFallbackDelay = 0.5f;
        [SerializeField] private float floorTrackingMinimumHeadHeight = 0.35f;

        private const float FloatEpsilon = 0.0001f;
        private float elapsed;

        public void Configure(Camera camera, XROrigin origin = null)
        {
            targetCamera = camera != null ? camera : targetCamera;
            xrOrigin = origin != null ? origin : ResolveXrOrigin(targetCamera);

            if (playerController == null)
            {
                playerController = targetCamera != null
                    ? targetCamera.GetComponentInParent<PlayerController>(true)
                    : FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            }
        }

        public void ConfigureRuntimeHeight(
            bool? useFloorTracking = null,
            float? deviceYOffset = null,
            bool? disableGravity = null)
        {
            if (useFloorTracking.HasValue)
            {
                useFloorTrackingWhenXrRunning = useFloorTracking.Value;
            }

            if (deviceYOffset.HasValue)
            {
                deviceCameraYOffset = Mathf.Max(0.1f, deviceYOffset.Value);
            }

            if (disableGravity.HasValue)
            {
                keepGravityDisabled = disableGravity.Value;
            }

            elapsed = 0f;
            ApplyConfiguredDeviceHeightIfNeeded();
        }

        private void ApplyConfiguredDeviceHeightIfNeeded()
        {
            if (useFloorTrackingWhenXrRunning || xrOrigin == null)
            {
                return;
            }

            if (xrOrigin.RequestedTrackingOriginMode != XROrigin.TrackingOriginMode.Device)
            {
                xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            }

            if (Mathf.Abs(xrOrigin.CameraYOffset - deviceCameraYOffset) > FloatEpsilon)
            {
                xrOrigin.CameraYOffset = deviceCameraYOffset;
            }

            if (xrOrigin.CameraFloorOffsetObject != null)
            {
                Vector3 offsetPosition = xrOrigin.CameraFloorOffsetObject.transform.localPosition;
                if (Mathf.Abs(offsetPosition.y - deviceCameraYOffset) > FloatEpsilon)
                {
                    offsetPosition.y = deviceCameraYOffset;
                    xrOrigin.CameraFloorOffsetObject.transform.localPosition = offsetPosition;
                }
            }
        }

        private void Awake()
        {
            Configure(targetCamera, xrOrigin);
        }

        private void LateUpdate()
        {
            elapsed += Time.unscaledDeltaTime;

            if (!IsXrDisplayRunning())
            {
                return;
            }

            if (xrOrigin != null)
            {
                ApplyXrOriginHeightSafety();
                return;
            }

            ApplyCameraOnlyHeightSafety();
        }

        private void ApplyXrOriginHeightSafety()
        {
            XROrigin.TrackingOriginMode trackingOriginMode =
                useFloorTrackingWhenXrRunning
                    ? XROrigin.TrackingOriginMode.Floor
                    : XROrigin.TrackingOriginMode.Device;
            float cameraYOffset = trackingOriginMode == XROrigin.TrackingOriginMode.Floor
                ? 0f
                : deviceCameraYOffset;

            if (ShouldFallbackToDeviceHeight(trackingOriginMode))
            {
                trackingOriginMode = XROrigin.TrackingOriginMode.Device;
                cameraYOffset = deviceCameraYOffset;
            }

            if (xrOrigin.RequestedTrackingOriginMode != trackingOriginMode)
            {
                xrOrigin.RequestedTrackingOriginMode = trackingOriginMode;
            }

            if (Mathf.Abs(xrOrigin.CameraYOffset - cameraYOffset) > FloatEpsilon)
            {
                xrOrigin.CameraYOffset = cameraYOffset;
            }

            if (xrOrigin.CameraFloorOffsetObject != null)
            {
                Vector3 offsetPosition = xrOrigin.CameraFloorOffsetObject.transform.localPosition;
                if (Mathf.Abs(offsetPosition.y - cameraYOffset) > FloatEpsilon)
                {
                    offsetPosition.y = cameraYOffset;
                    xrOrigin.CameraFloorOffsetObject.transform.localPosition = offsetPosition;
                }
            }

            if (keepGravityDisabled)
            {
                ToggleChildActive(xrOrigin.transform, "Gravity", false);
            }
        }

        private void ApplyCameraOnlyHeightSafety()
        {
            if (targetCamera == null)
            {
                targetCamera = VrRigRuntimeUtility.ResolveSceneCamera();
            }

            if (targetCamera == null)
            {
                return;
            }

            var trackedPoseDriver = targetCamera.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
            if (trackedPoseDriver != null &&
                trackedPoseDriver.trackingType != UnityEngine.InputSystem.XR.TrackedPoseDriver.TrackingType.RotationOnly)
            {
                trackedPoseDriver.trackingType = UnityEngine.InputSystem.XR.TrackedPoseDriver.TrackingType.RotationOnly;
            }

            float desiredHeight = ResolveCameraOnlyHeight();
            Vector3 localPosition = targetCamera.transform.localPosition;
            if (localPosition.y < floorTrackingMinimumHeadHeight ||
                Mathf.Abs(localPosition.y - desiredHeight) > FloatEpsilon)
            {
                localPosition.y = desiredHeight;
                targetCamera.transform.localPosition = localPosition;
            }
        }

        private float ResolveCameraOnlyHeight()
        {
            if (playerController == null && targetCamera != null)
            {
                playerController = targetCamera.GetComponentInParent<PlayerController>(true);
            }

            return playerController != null
                ? Mathf.Max(0.1f, playerController.playerHeight - 0.1f)
                : deviceCameraYOffset;
        }

        private bool ShouldFallbackToDeviceHeight(XROrigin.TrackingOriginMode trackingOriginMode)
        {
            if (trackingOriginMode != XROrigin.TrackingOriginMode.Floor ||
                xrOrigin == null ||
                xrOrigin.Camera == null)
            {
                return false;
            }

            float headHeight = xrOrigin.Camera.transform.localPosition.y;
            return headHeight <= FloatEpsilon ||
                   (elapsed >= floorTrackingFallbackDelay && headHeight < floorTrackingMinimumHeadHeight);
        }

        private static XROrigin ResolveXrOrigin(Camera camera)
        {
            XROrigin origin = camera != null ? camera.GetComponentInParent<XROrigin>(true) : null;
            return origin != null ? origin : FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
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

        private static void ToggleChildActive(Transform root, string childName, bool isActive)
        {
            Transform child = FindChildRecursive(root, childName);
            if (child != null && child.gameObject.activeSelf != isActive)
            {
                child.gameObject.SetActive(isActive);
            }
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == childName)
            {
                return root;
            }

            for (int index = 0; index < root.childCount; index++)
            {
                Transform match = FindChildRecursive(root.GetChild(index), childName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
