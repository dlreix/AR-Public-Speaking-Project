using System;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using VRPublicSpeaking.AppShell.UI;

namespace VRPublicSpeaking.AppShell.Flow
{
    [DisallowMultipleComponent]
    public class ShellSceneRigController : MonoBehaviour
    {
        [SerializeField] private XROrigin xrOrigin;
        [SerializeField] private Canvas shellCanvas;
        [SerializeField] private string backdropRootName = string.Empty;
        [SerializeField] private bool keepCanvasFixedToView = true;
        [SerializeField] private bool headLockedCanvas;
        [SerializeField] private Vector3 shellCanvasOffset = new Vector3(0f, -0.12f, 2.1f);
        [SerializeField] private float menuFollowPositionSpeed = 14f;
        [SerializeField] private float menuFollowRotationSpeed = 14f;
        [SerializeField] private bool keepRigStationary = true;
        [SerializeField] private XROrigin.TrackingOriginMode requestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
        [SerializeField] private float cameraYOffset = 1.62f;
        [SerializeField] private float stabilizationDuration;

        private const float FloatEpsilon = 0.0001f;

        private WorldSpaceCanvasFollower shellCanvasFollower;
        private Vector3 initialOriginPosition;
        private Quaternion initialOriginRotation;
        private float elapsed;
        private bool initialPoseCached;
        private bool shellCanvasSnapped;
        private bool rigSafetyApplied;
        private bool backdropSafetyApplied;

        private void Start()
        {
            InitializeNow();
        }

        private void LateUpdate()
        {
            elapsed += Time.unscaledDeltaTime;

            EnsureCanvasFollower();
            ApplyStableRigState();

            if (stabilizationDuration > 0f && elapsed >= stabilizationDuration)
            {
                enabled = false;
            }
        }

        public void Configure(
            Canvas targetCanvas,
            string targetBackdropRootName,
            bool keepCanvasInView,
            Vector3 canvasOffset,
            float canvasPositionSpeed,
            float canvasRotationSpeed,
            bool lockRigInPlace,
            float stableCameraYOffset,
            XROrigin.TrackingOriginMode trackingOriginMode)
        {
            shellCanvas = targetCanvas;
            backdropRootName = targetBackdropRootName ?? string.Empty;
            keepCanvasFixedToView = keepCanvasInView;
            shellCanvasOffset = canvasOffset;
            menuFollowPositionSpeed = canvasPositionSpeed;
            menuFollowRotationSpeed = canvasRotationSpeed;
            keepRigStationary = lockRigInPlace;
            cameraYOffset = stableCameraYOffset;
            requestedTrackingOriginMode = trackingOriginMode;
        }

        public void InitializeNow()
        {
            elapsed = 0f;
            EnsureBackdropSafety();
            EnsureCanvasFollower();
            ApplyStableRigState();
        }

        private void EnsureCanvasFollower()
        {
            if (!keepCanvasFixedToView)
            {
                return;
            }

            Canvas canvas = ResolveShellCanvas();
            if (canvas == null)
            {
                return;
            }

            Transform target = ResolveViewTarget(canvas);
            if (target == null)
            {
                return;
            }

            Camera targetCamera = target.GetComponent<Camera>();
            if (targetCamera != null && canvas.worldCamera != targetCamera)
            {
                canvas.worldCamera = targetCamera;
            }

            if (shellCanvasFollower == null)
            {
                shellCanvasFollower = canvas.GetComponent<WorldSpaceCanvasFollower>();
                if (shellCanvasFollower == null)
                {
                    shellCanvasFollower = canvas.gameObject.AddComponent<WorldSpaceCanvasFollower>();
                }
            }

            bool useHeadLockedCanvas = ShouldUseHeadLockedCanvas();
            float positionSpeed = useHeadLockedCanvas ? 0f : menuFollowPositionSpeed;
            float rotationSpeed = useHeadLockedCanvas ? 0f : menuFollowRotationSpeed;

            shellCanvasFollower.Configure(
                target,
                shellCanvasOffset,
                !useHeadLockedCanvas,
                useHeadLockedCanvas,
                positionSpeed,
                rotationSpeed);

            if (!shellCanvasSnapped)
            {
                shellCanvasFollower.SnapToTarget();
                shellCanvasSnapped = true;
            }
        }

        private bool ShouldUseHeadLockedCanvas()
        {
            if (headLockedCanvas)
            {
                return true;
            }

            return string.Equals(backdropRootName, "MainHubBackdrop", StringComparison.Ordinal);
        }

        private Canvas ResolveShellCanvas()
        {
            if (shellCanvas != null)
            {
                return shellCanvas;
            }

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < canvases.Length; index++)
            {
                Canvas candidate = canvases[index];
                if (candidate == null || candidate.renderMode != RenderMode.WorldSpace)
                {
                    continue;
                }

                if (candidate.name.StartsWith("WorldSpace", StringComparison.Ordinal))
                {
                    shellCanvas = candidate;
                    return shellCanvas;
                }
            }

            return null;
        }

        private static Transform ResolveViewTarget(Canvas canvas)
        {
            if (canvas != null && canvas.worldCamera != null)
            {
                return canvas.worldCamera.transform;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera.transform;
            }

            Camera fallbackCamera = FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
            return fallbackCamera != null ? fallbackCamera.transform : null;
        }

        private void EnsureBackdropSafety()
        {
            if (backdropSafetyApplied || string.IsNullOrWhiteSpace(backdropRootName))
            {
                return;
            }

            Transform backdropRoot = FindSceneRoot(backdropRootName);
            if (backdropRoot == null)
            {
                return;
            }

            Transform[] children = backdropRoot.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < children.Length; index++)
            {
                Transform child = children[index];
                if (child == null)
                {
                    continue;
                }

                if (string.Equals(child.name, "Floor", StringComparison.OrdinalIgnoreCase))
                {
                    EnsureMeshCollider(child.gameObject);
                    continue;
                }

                if (child.name.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    EnsureCollider(child.gameObject);
                }
            }

            backdropSafetyApplied = true;
        }

        private void ApplyStableRigState()
        {
            if (!TryResolveOrigin())
            {
                return;
            }

            if (!rigSafetyApplied)
            {
                ApplyRigSafety();
                rigSafetyApplied = true;
            }

            if (!initialPoseCached)
            {
                CacheInitialPose();
            }

            if (xrOrigin.RequestedTrackingOriginMode != requestedTrackingOriginMode)
            {
                xrOrigin.RequestedTrackingOriginMode = requestedTrackingOriginMode;
            }

            if (Mathf.Abs(xrOrigin.CameraYOffset - cameraYOffset) > FloatEpsilon)
            {
                xrOrigin.CameraYOffset = cameraYOffset;
            }

            if (xrOrigin.CameraFloorOffsetObject != null)
            {
                Vector3 localPosition = xrOrigin.CameraFloorOffsetObject.transform.localPosition;
                if (Mathf.Abs(localPosition.y - cameraYOffset) > FloatEpsilon)
                {
                    localPosition.y = cameraYOffset;
                    xrOrigin.CameraFloorOffsetObject.transform.localPosition = localPosition;
                }
            }

            if (!keepRigStationary)
            {
                return;
            }

            Transform originTransform = xrOrigin.Origin.transform;
            if ((originTransform.position - initialOriginPosition).sqrMagnitude > FloatEpsilon)
            {
                originTransform.position = initialOriginPosition;
            }

            if (ShouldLockOriginRotation() &&
                Quaternion.Angle(originTransform.rotation, initialOriginRotation) > 0.01f)
            {
                originTransform.rotation = initialOriginRotation;
            }
        }

        private static bool ShouldLockOriginRotation()
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

        private bool TryResolveOrigin()
        {
            if (xrOrigin != null && xrOrigin.Camera != null && xrOrigin.Origin != null)
            {
                return true;
            }

            xrOrigin = FindFirstObjectByType<XROrigin>(FindObjectsInactive.Exclude);
            return xrOrigin != null && xrOrigin.Camera != null && xrOrigin.Origin != null;
        }

        private void CacheInitialPose()
        {
            if (xrOrigin == null || xrOrigin.Origin == null)
            {
                return;
            }

            initialOriginPosition = xrOrigin.Origin.transform.position;
            initialOriginRotation = xrOrigin.Origin.transform.rotation;
            initialPoseCached = true;
        }

        private void ApplyRigSafety()
        {
            if (!keepRigStationary || xrOrigin == null)
            {
                return;
            }

            ToggleChildActive(xrOrigin.transform, "Gravity", false);
            ToggleChildActive(xrOrigin.transform, "Teleportation", false);
            ToggleChildActive(xrOrigin.transform, "Locomotion", false);

            CharacterController characterController = xrOrigin.GetComponent<CharacterController>();
            if (characterController != null && characterController.enabled)
            {
                characterController.enabled = false;
            }
        }

        private static void EnsureMeshCollider(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            MeshCollider collider = target.GetComponent<MeshCollider>();
            if (collider != null)
            {
                collider.enabled = true;
                return;
            }

            MeshFilter meshFilter = target.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return;
            }

            collider = target.AddComponent<MeshCollider>();
            collider.sharedMesh = meshFilter.sharedMesh;
        }

        private static void EnsureCollider(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            Collider existingCollider = target.GetComponent<Collider>();
            if (existingCollider != null)
            {
                existingCollider.enabled = true;
                return;
            }

            MeshFilter meshFilter = target.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                MeshCollider meshCollider = target.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                return;
            }

            BoxCollider boxCollider = target.AddComponent<BoxCollider>();
            boxCollider.center = Vector3.zero;
            boxCollider.size = Vector3.one;
        }

        private static Transform FindSceneRoot(string objectName)
        {
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform candidate = transforms[index];
                if (candidate == null || candidate.parent != null || candidate.name != objectName)
                {
                    continue;
                }

                return candidate;
            }

            return null;
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
