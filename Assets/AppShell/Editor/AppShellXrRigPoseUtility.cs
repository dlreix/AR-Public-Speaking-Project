using System;
using System.Collections.Generic;
using System.IO;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using VRPublicSpeaking.AppShell.Integration;

namespace VRPublicSpeaking.AppShell.Editor
{
    internal static class AppShellXrRigPoseUtility
    {
        private const string MenuPath = "Tools/VR Public Speaking/App Shell/Fix XR Headset Camera Bindings In Scenes";
        private const string CameraOffsetName = "Camera Offset";

        [MenuItem(MenuPath)]
        public static void FixAllAppScenes()
        {
            string activeScenePath = SceneManager.GetActiveScene().path;
            int fixedCount = 0;

            foreach (string scenePath in EnumerateAppScenePaths())
            {
                if (string.IsNullOrWhiteSpace(scenePath) || !File.Exists(scenePath))
                {
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (FixScene(scene))
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    fixedCount++;
                }
            }

            if (!string.IsNullOrWhiteSpace(activeScenePath) && File.Exists(activeScenePath))
            {
                EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[AppShell] XR headset camera binding check complete. Updated {fixedCount} scene(s).");
        }

        internal static bool FixActiveScene(bool saveScene)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!IsAppScene(scene))
            {
                return false;
            }

            bool changed = FixScene(scene);
            if (!changed)
            {
                return false;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene)
            {
                EditorSceneManager.SaveScene(scene);
            }

            return true;
        }

        internal static bool FixScene(Scene scene)
        {
            if (!IsAppScene(scene))
            {
                return false;
            }

            bool changed = false;
            Camera sceneCamera = ResolvePrimarySceneCamera(scene);
            if (sceneCamera == null)
            {
                return false;
            }

            changed |= EnsureMainCamera(sceneCamera);
            XROrigin xrOrigin = ResolveOrCreateXrOrigin(scene, sceneCamera, ref changed);
            changed |= EnsureCameraOffsetParent(xrOrigin, sceneCamera);
            changed |= EnsureTrackedPoseDriver(sceneCamera);
            changed |= EnsureLocomotion(xrOrigin);

            if (changed)
            {
                AppShellEditorCommon.MarkDirty(sceneCamera, sceneCamera.transform, xrOrigin, xrOrigin != null ? xrOrigin.transform : null);
            }

            return changed;
        }

        private static IEnumerable<string> EnumerateAppScenePaths()
        {
            yield return AppShellEditorCommon.MainHubScenePath;
            yield return AppShellEditorCommon.ResultsScenePath;

            List<string> environmentScenePaths = AppShellEditorCommon.FindEnvironmentScenePaths();
            for (int index = 0; index < environmentScenePaths.Count; index++)
            {
                yield return environmentScenePaths[index];
            }
        }

        private static bool IsAppScene(Scene scene)
        {
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            {
                return false;
            }

            if (scene.path == AppShellEditorCommon.MainHubScenePath ||
                scene.path == AppShellEditorCommon.ResultsScenePath)
            {
                return true;
            }

            string fileName = Path.GetFileNameWithoutExtension(scene.path);
            return scene.path.StartsWith("Assets/Scenes/", StringComparison.Ordinal) &&
                fileName.StartsWith("Scene_", StringComparison.Ordinal);
        }

        private static Camera ResolvePrimarySceneCamera(Scene scene)
        {
            Camera namedMainCamera = null;
            Camera taggedMainCamera = null;
            Camera firstSceneCamera = null;

            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera camera = cameras[index];
                if (camera == null || camera.gameObject.scene != scene)
                {
                    continue;
                }

                firstSceneCamera ??= camera;

                if (camera.name == "Main Camera")
                {
                    namedMainCamera = camera;
                }

                if (camera.CompareTag("MainCamera"))
                {
                    taggedMainCamera ??= camera;
                }
            }

            return namedMainCamera != null ? namedMainCamera : taggedMainCamera != null ? taggedMainCamera : firstSceneCamera;
        }

        private static bool EnsureMainCamera(Camera sceneCamera)
        {
            bool changed = false;

            if (!sceneCamera.CompareTag("MainCamera"))
            {
                sceneCamera.tag = "MainCamera";
                changed = true;
            }

            GameObject[] taggedMainCameraObjects = GameObject.FindGameObjectsWithTag("MainCamera");
            for (int index = 0; index < taggedMainCameraObjects.Length; index++)
            {
                GameObject taggedObject = taggedMainCameraObjects[index];
                if (taggedObject == null ||
                    taggedObject == sceneCamera.gameObject ||
                    taggedObject.scene != sceneCamera.gameObject.scene)
                {
                    continue;
                }

                taggedObject.tag = "Untagged";
                changed = true;
            }

            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera otherCamera = cameras[index];
                if (otherCamera == null ||
                    otherCamera == sceneCamera ||
                    otherCamera.gameObject.scene != sceneCamera.gameObject.scene ||
                    !otherCamera.CompareTag("MainCamera"))
                {
                    continue;
                }

                otherCamera.tag = "Untagged";
                changed = true;
            }

            if (sceneCamera.GetComponent<AudioListener>() == null &&
                sceneCamera.gameObject.scene.GetRootGameObjects().Length > 0 &&
                UnityEngine.Object.FindFirstObjectByType<AudioListener>(FindObjectsInactive.Include) == null)
            {
                sceneCamera.gameObject.AddComponent<AudioListener>();
                changed = true;
            }

            return changed;
        }

        private static XROrigin ResolveOrCreateXrOrigin(Scene scene, Camera sceneCamera, ref bool changed)
        {
            XROrigin xrOrigin = sceneCamera.GetComponentInParent<XROrigin>(true) ?? FindSceneComponent<XROrigin>(scene);
            if (xrOrigin != null)
            {
                xrOrigin.Camera = sceneCamera;
                changed = true;
                return xrOrigin;
            }

            Transform rigRoot = ResolveRigRoot(scene, sceneCamera);
            xrOrigin = rigRoot.gameObject.AddComponent<XROrigin>();
            xrOrigin.Camera = sceneCamera;
            changed = true;

            return xrOrigin;
        }

        private static Transform ResolveRigRoot(Scene scene, Camera sceneCamera)
        {
            PlayerController playerController = sceneCamera.GetComponentInParent<PlayerController>(true) ??
                FindSceneComponent<PlayerController>(scene);

            if (playerController != null)
            {
                return playerController.transform;
            }

            return sceneCamera.transform.parent != null ? sceneCamera.transform.parent : sceneCamera.transform;
        }

        private static bool EnsureCameraOffsetParent(XROrigin xrOrigin, Camera sceneCamera)
        {
            if (xrOrigin == null || sceneCamera == null)
            {
                return false;
            }

            bool changed = false;
            Transform cameraOffset = ResolveCameraOffset(xrOrigin, ref changed);
            if (cameraOffset == null)
            {
                return changed;
            }

            if (sceneCamera.transform.parent != cameraOffset)
            {
                Undo.SetTransformParent(sceneCamera.transform, cameraOffset, "Move Main Camera under XR Camera Offset");
                changed = true;
            }

            if (sceneCamera.transform.localRotation != Quaternion.identity)
            {
                sceneCamera.transform.localRotation = Quaternion.identity;
                changed = true;
            }

            if (sceneCamera.transform.localPosition.x != 0f ||
                sceneCamera.transform.localPosition.z != 0f)
            {
                sceneCamera.transform.localPosition = new Vector3(0f, sceneCamera.transform.localPosition.y, 0f);
                changed = true;
            }

            if (xrOrigin.Camera != sceneCamera)
            {
                xrOrigin.Camera = sceneCamera;
                changed = true;
            }

            return changed;
        }

        private static Transform ResolveCameraOffset(XROrigin xrOrigin, ref bool changed)
        {
            if (xrOrigin.CameraFloorOffsetObject != null)
            {
                return xrOrigin.CameraFloorOffsetObject.transform;
            }

            Transform existing = xrOrigin.transform.Find(CameraOffsetName);
            if (existing == null)
            {
                GameObject cameraOffset = new GameObject(CameraOffsetName);
                Undo.RegisterCreatedObjectUndo(cameraOffset, "Create XR Camera Offset");
                cameraOffset.transform.SetParent(xrOrigin.transform, false);
                existing = cameraOffset.transform;
                changed = true;
            }

            xrOrigin.CameraFloorOffsetObject = existing.gameObject;
            changed = true;
            return existing;
        }

        private static bool EnsureTrackedPoseDriver(Camera sceneCamera)
        {
            bool changed = false;
            TrackedPoseDriver trackedPoseDriver = sceneCamera.GetComponent<TrackedPoseDriver>();
            if (trackedPoseDriver == null)
            {
                trackedPoseDriver = sceneCamera.gameObject.AddComponent<TrackedPoseDriver>();
                changed = true;
            }

            if (trackedPoseDriver.trackingType != TrackedPoseDriver.TrackingType.RotationAndPosition)
            {
                trackedPoseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
                changed = true;
            }

            if (trackedPoseDriver.updateType != TrackedPoseDriver.UpdateType.UpdateAndBeforeRender)
            {
                trackedPoseDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
                changed = true;
            }

            if (trackedPoseDriver.ignoreTrackingState)
            {
                trackedPoseDriver.ignoreTrackingState = false;
                changed = true;
            }

            changed |= ReplacePoseActionIfNeeded(
                trackedPoseDriver.positionInput,
                property => trackedPoseDriver.positionInput = property,
                "Position",
                "Vector3",
                "<XRHMD>/centerEyePosition",
                "<HandheldARInputDevice>/devicePosition");

            changed |= ReplacePoseActionIfNeeded(
                trackedPoseDriver.rotationInput,
                property => trackedPoseDriver.rotationInput = property,
                "Rotation",
                "Quaternion",
                "<XRHMD>/centerEyeRotation",
                "<HandheldARInputDevice>/deviceRotation");

            changed |= ReplacePoseActionIfNeeded(
                trackedPoseDriver.trackingStateInput,
                property => trackedPoseDriver.trackingStateInput = property,
                "Tracking State",
                "Integer",
                "<XRHMD>/trackingState",
                null);

            if (changed)
            {
                EditorUtility.SetDirty(trackedPoseDriver);
            }

            return changed;
        }

        private static bool ReplacePoseActionIfNeeded(
            InputActionProperty currentProperty,
            Action<InputActionProperty> assign,
            string name,
            string expectedControlType,
            string requiredPath,
            string blockedPath)
        {
            if (!NeedsPoseAction(currentProperty, requiredPath, blockedPath))
            {
                return false;
            }

            assign(new InputActionProperty(CreatePoseAction(name, expectedControlType, requiredPath)));
            return true;
        }

        private static bool NeedsPoseAction(InputActionProperty property, string requiredPath, string blockedPath)
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
                if (string.Equals(path, requiredPath, StringComparison.Ordinal))
                {
                    hasRequiredPath = true;
                }

                if (!string.IsNullOrWhiteSpace(blockedPath) &&
                    string.Equals(path, blockedPath, StringComparison.Ordinal))
                {
                    hasBlockedPath = true;
                }
            }

            return !hasRequiredPath || hasBlockedPath;
        }

        private static InputAction CreatePoseAction(string name, string expectedControlType, string binding)
        {
            return new InputAction(name, binding: binding, expectedControlType: expectedControlType);
        }

        private static bool EnsureLocomotion(XROrigin xrOrigin)
        {
            if (xrOrigin == null)
            {
                return false;
            }

            EnvironmentControllerLocomotion locomotion = xrOrigin.GetComponent<EnvironmentControllerLocomotion>();
            if (locomotion == null)
            {
                locomotion = xrOrigin.gameObject.AddComponent<EnvironmentControllerLocomotion>();
                locomotion.Configure(xrOrigin);
                EditorUtility.SetDirty(locomotion);
                return true;
            }

            locomotion.Configure(xrOrigin);
            EditorUtility.SetDirty(locomotion);
            return false;
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            T[] components = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < components.Length; index++)
            {
                T component = components[index];
                if (component != null && component.gameObject.scene == scene)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
