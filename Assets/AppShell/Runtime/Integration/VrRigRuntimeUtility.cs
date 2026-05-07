using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

namespace VRPublicSpeaking.AppShell.Integration
{
    public static class VrRigRuntimeUtility
    {
        public static Camera EnsureSceneVrReady(string context = null)
        {
            Camera camera = ResolveSceneCamera();
            if (camera == null)
            {
                Debug.LogWarning(BuildMessage(context, "No camera was found, so VR rig bootstrap could not run."));
                return null;
            }

            EnsureMainCameraTag(camera);
            EnsureAudioListener(camera.gameObject);
            EnsureCameraInXrOrigin(camera, context);
            EnsureTrackedPoseDriver(camera);
            EnsureHeadsetPoseRuntimeDriver(camera);
            EnsureHeightSafety(camera);
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

            if (NeedsPoseAction(trackedPoseDriver.positionInput, "<XRHMD>/centerEyePosition", "<HandheldARInputDevice>/devicePosition"))
            {
                trackedPoseDriver.positionInput = new InputActionProperty(CreatePoseAction(
                    "Position",
                    "Vector3",
                    "<XRHMD>/centerEyePosition"));
            }

            if (NeedsPoseAction(trackedPoseDriver.rotationInput, "<XRHMD>/centerEyeRotation", "<HandheldARInputDevice>/deviceRotation"))
            {
                trackedPoseDriver.rotationInput = new InputActionProperty(CreatePoseAction(
                    "Rotation",
                    "Quaternion",
                    "<XRHMD>/centerEyeRotation"));
            }

            if (NeedsPoseAction(trackedPoseDriver.trackingStateInput, "<XRHMD>/trackingState"))
            {
                trackedPoseDriver.trackingStateInput = new InputActionProperty(CreatePoseAction(
                    "Tracking State",
                    "Integer",
                    "<XRHMD>/trackingState"));
            }
        }

        public static XROrigin EnsureCameraInXrOrigin(Camera camera, string context = null)
        {
            if (camera == null)
            {
                return null;
            }

            XROrigin xrOrigin = ResolveXrOrigin(camera);
            if (xrOrigin == null)
            {
                xrOrigin = TryCreateRuntimeXrOrigin(camera, context);
                if (xrOrigin == null)
                {
                    return null;
                }
            }

            Transform cameraOffset = ResolveCameraOffsetTransform(xrOrigin);
            if (cameraOffset == null)
            {
                return xrOrigin;
            }

            if (camera.transform.parent != cameraOffset)
            {
                camera.transform.SetParent(cameraOffset, false);
                Debug.Log(BuildMessage(context, $"Reparented '{camera.name}' under '{cameraOffset.name}' so HMD pose drives the view."));
            }

            camera.transform.localPosition = Vector3.zero;
            camera.transform.localRotation = Quaternion.identity;
            xrOrigin.Camera = camera;

            return xrOrigin;
        }

        private static XROrigin TryCreateRuntimeXrOrigin(Camera camera, string context)
        {
            if (camera == null || !IsXrDisplayRunning())
            {
                return null;
            }

            Transform rigRoot = ResolveRigRoot(camera);
            GameObject originObject;
            if (rigRoot == camera.transform)
            {
                originObject = new GameObject("XR Origin (Runtime)");
                originObject.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
                Transform originalParent = camera.transform.parent;
                if (originalParent != null)
                {
                    originObject.transform.SetParent(originalParent, true);
                }
            }
            else
            {
                originObject = rigRoot.gameObject;
            }

            XROrigin xrOrigin = originObject.GetComponent<XROrigin>();
            if (xrOrigin == null)
            {
                xrOrigin = originObject.AddComponent<XROrigin>();
                Debug.Log(BuildMessage(context, $"Added XROrigin to '{originObject.name}' for headset-driven camera pose."));
            }

            return xrOrigin;
        }

        private static Transform ResolveRigRoot(Camera camera)
        {
            PlayerController playerController = camera != null
                ? camera.GetComponentInParent<PlayerController>(true)
                : null;

            if (playerController == null)
            {
                playerController = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            }

            if (playerController != null)
            {
                return playerController.transform;
            }

            return camera != null && camera.transform.parent != null
                ? camera.transform.parent
                : camera != null
                    ? camera.transform
                    : null;
        }

        private static Transform ResolveCameraOffsetTransform(XROrigin xrOrigin)
        {
            if (xrOrigin == null)
            {
                return null;
            }

            if (xrOrigin.CameraFloorOffsetObject != null)
            {
                return xrOrigin.CameraFloorOffsetObject.transform;
            }

            Transform existingOffset = xrOrigin.transform.Find("Camera Offset");
            if (existingOffset != null)
            {
                xrOrigin.CameraFloorOffsetObject = existingOffset.gameObject;
                return existingOffset;
            }

            GameObject offsetObject = new GameObject("Camera Offset");
            offsetObject.transform.SetParent(xrOrigin.transform, false);
            xrOrigin.CameraFloorOffsetObject = offsetObject;
            return offsetObject.transform;
        }

        private static bool NeedsPoseAction(InputActionProperty property, string requiredPath, string blockedPath = null)
        {
            InputAction action = property.action;
            if (action == null)
            {
                return true;
            }

            bool hasRequiredPath = false;
            bool hasBlockedPath = false;
            for (int index = 0; index < action.bindings.Count; index++)
            {
                string path = action.bindings[index].effectivePath;
                if (string.Equals(path, requiredPath, System.StringComparison.Ordinal))
                {
                    hasRequiredPath = true;
                }

                if (!string.IsNullOrWhiteSpace(blockedPath) &&
                    string.Equals(path, blockedPath, System.StringComparison.Ordinal))
                {
                    hasBlockedPath = true;
                }
            }

            return !hasRequiredPath || hasBlockedPath;
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

        private static void EnsureHeightSafety(Camera camera)
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
        }

        private static void EnsureHeadsetPoseRuntimeDriver(Camera camera)
        {
            if (camera == null)
            {
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
            return xrOrigin != null
                ? xrOrigin
                : Object.FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
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

        private static string BuildMessage(string context, string message)
        {
            return string.IsNullOrWhiteSpace(context)
                ? $"[VrRigRuntimeUtility] {message}"
                : $"{context} {message}";
        }
    }
}
