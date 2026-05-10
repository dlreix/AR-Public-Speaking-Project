using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;

namespace VRPublicSpeaking.AppShell.Integration
{
    public static class VrRigRuntimeUtility
    {
        public static Camera EnsureSceneVrReady(
            string context = null,
            bool? useFloorTrackingWhenXrRunning = null,
            float? deviceCameraYOffset = null,
            bool? keepGravityDisabled = null)
        {
            Camera camera = ResolveSceneCamera();
            if (camera == null)
            {
                Debug.LogWarning(BuildMessage(context, "No camera was found, so VR rig bootstrap could not run."));
                return null;
            }

            EnsureMainCameraTag(camera);
            EnsureAudioListener(camera.gameObject);
            EnsureTrackedPoseDriver(camera);
            EnsureHeadsetPoseRuntimeDriverFallback(camera);
            EnsureHeightSafety(
                camera,
                useFloorTrackingWhenXrRunning,
                deviceCameraYOffset,
                keepGravityDisabled);
            return camera;
        }

        public static Camera ResolveSceneCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera;
            }

            PlayerController playerController = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (playerController != null)
            {
                Camera playerCamera = playerController.GetComponentInChildren<Camera>(true);
                if (playerCamera != null)
                {
                    return playerCamera;
                }
            }

            Camera activeCamera = Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
            if (activeCamera != null)
            {
                return activeCamera;
            }

            return Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
        }

        private static void EnsureMainCameraTag(Camera camera)
        {
            if (camera == null || camera.CompareTag("MainCamera"))
            {
                return;
            }

            camera.tag = "MainCamera";
        }

        private static void EnsureAudioListener(GameObject cameraObject)
        {
            if (cameraObject == null || cameraObject.GetComponent<AudioListener>() != null)
            {
                return;
            }

            if (Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 0)
            {
                cameraObject.AddComponent<AudioListener>();
            }
        }

        private static void EnsureTrackedPoseDriver(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            var trackedPoseDriver = camera.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
            if (trackedPoseDriver == null)
            {
                trackedPoseDriver = camera.gameObject.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
            }

            trackedPoseDriver.trackingType = ResolveXrOrigin(camera) != null
                ? UnityEngine.InputSystem.XR.TrackedPoseDriver.TrackingType.RotationAndPosition
                : UnityEngine.InputSystem.XR.TrackedPoseDriver.TrackingType.RotationOnly;
            trackedPoseDriver.updateType = UnityEngine.InputSystem.XR.TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            trackedPoseDriver.ignoreTrackingState = false;

            if (trackedPoseDriver.positionInput.action == null)
            {
                trackedPoseDriver.positionInput = new InputActionProperty(CreatePoseAction(
                    "Position",
                    "Vector3",
                    "<XRHMD>/centerEyePosition",
                    "<HandheldARInputDevice>/devicePosition"));
            }

            if (trackedPoseDriver.rotationInput.action == null)
            {
                trackedPoseDriver.rotationInput = new InputActionProperty(CreatePoseAction(
                    "Rotation",
                    "Quaternion",
                    "<XRHMD>/centerEyeRotation",
                    "<HandheldARInputDevice>/deviceRotation"));
            }

            if (trackedPoseDriver.trackingStateInput.action == null)
            {
                trackedPoseDriver.trackingStateInput = new InputActionProperty(CreatePoseAction(
                    "Tracking State",
                    "Integer",
                    "<XRHMD>/trackingState"));
            }
        }

        private static InputAction CreatePoseAction(
            string name,
            string expectedControlType,
            string primaryBinding,
            string secondaryBinding = null)
        {
            var action = new InputAction(name, binding: primaryBinding, expectedControlType: expectedControlType);
            if (!string.IsNullOrWhiteSpace(secondaryBinding))
            {
                action.AddBinding(secondaryBinding);
            }

            return action;
        }

        private static void EnsureHeightSafety(
            Camera camera,
            bool? useFloorTrackingWhenXrRunning,
            float? deviceCameraYOffset,
            bool? keepGravityDisabled)
        {
            if (camera == null)
            {
                return;
            }

            XROrigin xrOrigin = ResolveXrOrigin(camera);

            GameObject safetyHost = xrOrigin != null ? xrOrigin.gameObject : camera.gameObject;
            VrRigHeightSafety safety = safetyHost.GetComponent<VrRigHeightSafety>();
            if (safety == null)
            {
                safety = safetyHost.AddComponent<VrRigHeightSafety>();
            }

            safety.Configure(camera, xrOrigin);
            if (useFloorTrackingWhenXrRunning.HasValue ||
                deviceCameraYOffset.HasValue ||
                keepGravityDisabled.HasValue)
            {
                safety.ConfigureRuntimeHeight(
                    useFloorTrackingWhenXrRunning,
                    deviceCameraYOffset,
                    keepGravityDisabled);
            }
        }

        private static void EnsureHeadsetPoseRuntimeDriverFallback(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            if (camera.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>() != null)
            {
                HeadsetPoseRuntimeDriver duplicateDriver = camera.GetComponent<HeadsetPoseRuntimeDriver>();
                if (duplicateDriver != null)
                {
                    Object.Destroy(duplicateDriver);
                }

                return;
            }

            if (camera.GetComponent<HeadsetPoseRuntimeDriver>() == null)
            {
                camera.gameObject.AddComponent<HeadsetPoseRuntimeDriver>();
            }
        }

        private static XROrigin ResolveXrOrigin(Camera camera)
        {
            XROrigin xrOrigin = camera != null ? camera.GetComponentInParent<XROrigin>(true) : null;
            if (xrOrigin != null || camera != null)
            {
                return xrOrigin;
            }

            return Object.FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
        }

        private static string BuildMessage(string context, string message)
        {
            return string.IsNullOrWhiteSpace(context)
                ? $"[VrRigRuntimeUtility] {message}"
                : $"{context} {message}";
        }
    }
}
