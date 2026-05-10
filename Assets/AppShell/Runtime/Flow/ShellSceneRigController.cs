using System;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using VRPublicSpeaking.AppShell.Integration;
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
        [SerializeField] private float cameraYOffset = 1.36f;
        [SerializeField] private float stabilizationDuration;
        [SerializeField] private bool useFloorTrackingWhenXrRunning = true;
        [SerializeField] private bool keepUnlockedRigGravityDisabled = true;
        [SerializeField] private float floorTrackingFallbackDelay = 0.5f;
        [SerializeField] private float floorTrackingMinimumHeadHeight = 0.35f;

        private const float FloatEpsilon = 0.0001f;
        private const float MainHubMinimumCameraYOffset = 1.62f;
        private const string MainHubBackdropName = "MainHubBackdrop";

        private WorldSpaceCanvasFollower shellCanvasFollower;
        private Vector3 initialOriginPosition;
        private Quaternion initialOriginRotation;
        private float elapsed;
        private bool initialPoseCached;
        private bool shellCanvasSnapped;
        private bool rigSafetyApplied;
        private bool backdropSafetyApplied;

        private static readonly string[] MainHubBackdropViewBlockers =
        {
            "CeilingBeam_Front",
            "CeilingBeam_Mid",
            "CeilingBeam_Back",
            "CeilingLightRail",
            "LightCan_Left",
            "LightCan_Center",
            "LightCan_Right",
            "LightCan_LeftWide",
            "LightCan_LeftTight",
            "LightCan_RightTight",
            "LightCan_RightWide"
        };

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
            XROrigin.TrackingOriginMode trackingOriginMode,
            bool? useFloorTrackingInXr = null)
        {
            shellCanvas = targetCanvas;
            backdropRootName = targetBackdropRootName ?? string.Empty;
            keepCanvasFixedToView = keepCanvasInView;
            shellCanvasOffset = canvasOffset;
            menuFollowPositionSpeed = canvasPositionSpeed;
            menuFollowRotationSpeed = canvasRotationSpeed;
            keepRigStationary = lockRigInPlace;
            requestedTrackingOriginMode = trackingOriginMode;
            cameraYOffset = string.Equals(backdropRootName, MainHubBackdropName, StringComparison.Ordinal)
                ? Mathf.Max(MainHubMinimumCameraYOffset, stableCameraYOffset)
                : stableCameraYOffset;

            if (useFloorTrackingInXr.HasValue)
            {
                useFloorTrackingWhenXrRunning = useFloorTrackingInXr.Value;
            }
            else
            {
                useFloorTrackingWhenXrRunning = trackingOriginMode == XROrigin.TrackingOriginMode.Floor;
            }
        }

        public void InitializeNow()
        {
            elapsed = 0f;
            VrRigRuntimeUtility.EnsureSceneVrReady(
                "[ShellSceneRigController]",
                ShouldUseFloorTrackingInXr(),
                cameraYOffset,
                keepUnlockedRigGravityDisabled);
            EnsureBackdropSafety();
            EnsureCanvasFollower();
            ApplyStableRigState();
        }

        private void EnsureCanvasFollower()
        {
            if (!keepCanvasFixedToView)
            {
                DisableCanvasFollower();
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

            if (!shellCanvasFollower.enabled)
            {
                shellCanvasFollower.enabled = true;
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

        private void DisableCanvasFollower()
        {
            Canvas canvas = ResolveShellCanvas();
            if (canvas == null)
            {
                return;
            }

            if (shellCanvasFollower == null)
            {
                shellCanvasFollower = canvas.GetComponent<WorldSpaceCanvasFollower>();
            }

            if (shellCanvasFollower != null && shellCanvasFollower.enabled)
            {
                shellCanvasFollower.enabled = false;
            }

            shellCanvasSnapped = false;
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

            ApplyMainHubBackdropViewSafety(backdropRoot);

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

        private static void ApplyMainHubBackdropViewSafety(Transform backdropRoot)
        {
            if (backdropRoot == null ||
                !string.Equals(backdropRoot.name, "MainHubBackdrop", StringComparison.Ordinal))
            {
                return;
            }

            for (int index = 0; index < MainHubBackdropViewBlockers.Length; index++)
            {
                ToggleChildActive(backdropRoot, MainHubBackdropViewBlockers[index], false);
            }

            SetBackdropChildPose(backdropRoot, "CeilingWall", new Vector2(0f, 6.35f), 2.25f, new Vector3(11.85f, 0.16f, 7.8f));
            SetBackdropChildPose(backdropRoot, "BackWall", new Vector2(0f, 3.15f), 6.15f, new Vector3(11.85f, 6.3f, 0.2f));
            SetBackdropChildPose(backdropRoot, "FrontWall", new Vector2(0f, 3.15f), -1.65f, new Vector3(11.85f, 6.3f, 0.2f));
            SetBackdropChildPose(backdropRoot, "LeftWall", new Vector2(-5.92f, 3.15f), 2.25f, new Vector3(0.2f, 6.3f, 7.8f));
            SetBackdropChildPose(backdropRoot, "RightWall", new Vector2(5.92f, 3.15f), 2.25f, new Vector3(0.2f, 6.3f, 7.8f));
            SetBackdropChildPose(backdropRoot, "BackCurtainPanel", new Vector2(0f, 3.12f), 6.03f, new Vector3(11.5f, 5.86f, 0.08f));
            SetBackdropChildPose(backdropRoot, "BackCurtainFold_Left01", new Vector2(-5.15f, 3.12f), 5.95f, new Vector3(0.24f, 5.68f, 0.14f));
            SetBackdropChildPose(backdropRoot, "BackCurtainFold_Left02", new Vector2(-2.55f, 3.08f), 5.94f, new Vector3(0.16f, 5.56f, 0.12f));
            SetBackdropChildPose(backdropRoot, "BackCurtainFold_Right01", new Vector2(5.15f, 3.12f), 5.95f, new Vector3(0.24f, 5.68f, 0.14f));
            SetBackdropChildPose(backdropRoot, "BackCurtainFold_Right02", new Vector2(2.55f, 3.08f), 5.94f, new Vector3(0.16f, 5.56f, 0.12f));

            EnsureBackdropVisualPrimitive(backdropRoot, "BackCurtainValance", PrimitiveType.Cube, new Vector3(0f, 5.72f, 5.88f), Vector3.zero, new Vector3(11.62f, 0.46f, 0.18f), new Color(0.115f, 0.022f, 0.03f, 1f), 0.58f);
            EnsureBackdropVisualPrimitive(backdropRoot, "BackCurtainBottomHem", PrimitiveType.Cube, new Vector3(0f, 0.38f, 5.88f), Vector3.zero, new Vector3(11.48f, 0.18f, 0.16f), new Color(0.055f, 0.010f, 0.016f, 1f), 0.46f);
            EnsureBackdropVisualPrimitive(backdropRoot, "BackCurtainPleat_LeftOuter", PrimitiveType.Cube, new Vector3(-4.15f, 3.04f, 5.88f), Vector3.zero, new Vector3(0.10f, 5.42f, 0.16f), new Color(0.055f, 0.010f, 0.016f, 1f), 0.46f);
            EnsureBackdropVisualPrimitive(backdropRoot, "BackCurtainPleat_LeftInner", PrimitiveType.Cube, new Vector3(-1.35f, 3.02f, 5.87f), Vector3.zero, new Vector3(0.08f, 5.32f, 0.14f), new Color(0.055f, 0.010f, 0.016f, 1f), 0.46f);
            EnsureBackdropVisualPrimitive(backdropRoot, "BackCurtainPleat_CenterLeft", PrimitiveType.Cube, new Vector3(-0.38f, 3.02f, 5.865f), Vector3.zero, new Vector3(0.06f, 5.24f, 0.12f), new Color(0.055f, 0.010f, 0.016f, 1f), 0.46f);
            EnsureBackdropVisualPrimitive(backdropRoot, "BackCurtainPleat_CenterRight", PrimitiveType.Cube, new Vector3(0.38f, 3.02f, 5.865f), Vector3.zero, new Vector3(0.06f, 5.24f, 0.12f), new Color(0.055f, 0.010f, 0.016f, 1f), 0.46f);
            EnsureBackdropVisualPrimitive(backdropRoot, "BackCurtainPleat_RightInner", PrimitiveType.Cube, new Vector3(1.35f, 3.02f, 5.87f), Vector3.zero, new Vector3(0.08f, 5.32f, 0.14f), new Color(0.055f, 0.010f, 0.016f, 1f), 0.46f);
            EnsureBackdropVisualPrimitive(backdropRoot, "BackCurtainPleat_RightOuter", PrimitiveType.Cube, new Vector3(4.15f, 3.04f, 5.88f), Vector3.zero, new Vector3(0.10f, 5.42f, 0.16f), new Color(0.055f, 0.010f, 0.016f, 1f), 0.46f);
            EnsureBackdropVisualPrimitive(backdropRoot, "StageDeck", PrimitiveType.Cube, new Vector3(0f, 0.13f, 4.72f), Vector3.zero, new Vector3(8.65f, 0.24f, 1.72f), new Color(0.028f, 0.032f, 0.039f, 1f), 0.64f);
            EnsureBackdropVisualPrimitive(backdropRoot, "StageFrontEdge", PrimitiveType.Cube, new Vector3(0f, 0.30f, 3.84f), Vector3.zero, new Vector3(8.65f, 0.16f, 0.08f), new Color(1f, 0.62f, 0.22f, 1f), 0.72f);
            EnsureBackdropVisualPrimitive(backdropRoot, "CenterFloorRunner", PrimitiveType.Cube, new Vector3(0f, 0.028f, 1.28f), Vector3.zero, new Vector3(2.18f, 0.022f, 3.55f), new Color(0.135f, 0.018f, 0.024f, 1f), 0.55f);
            EnsureBackdropVisualPrimitive(backdropRoot, "RunnerEdge_Left", PrimitiveType.Cube, new Vector3(-1.14f, 0.04f, 1.28f), Vector3.zero, new Vector3(0.045f, 0.018f, 3.55f), new Color(0.95f, 0.54f, 0.18f, 1f), 0.5f);
            EnsureBackdropVisualPrimitive(backdropRoot, "RunnerEdge_Right", PrimitiveType.Cube, new Vector3(1.14f, 0.04f, 1.28f), Vector3.zero, new Vector3(0.045f, 0.018f, 3.55f), new Color(0.95f, 0.54f, 0.18f, 1f), 0.5f);
            EnsureBackdropVisualPrimitive(backdropRoot, "Footlight_Left", PrimitiveType.Cube, new Vector3(-2.7f, 0.24f, 3.68f), new Vector3(-8f, 0f, 0f), new Vector3(0.36f, 0.16f, 0.20f), new Color(1f, 0.62f, 0.22f, 1f), 0.72f);
            EnsureBackdropVisualPrimitive(backdropRoot, "Footlight_Center", PrimitiveType.Cube, new Vector3(0f, 0.24f, 3.68f), new Vector3(-8f, 0f, 0f), new Vector3(0.36f, 0.16f, 0.20f), new Color(1f, 0.62f, 0.22f, 1f), 0.72f);
            EnsureBackdropVisualPrimitive(backdropRoot, "Footlight_Right", PrimitiveType.Cube, new Vector3(2.7f, 0.24f, 3.68f), new Vector3(-8f, 0f, 0f), new Vector3(0.36f, 0.16f, 0.20f), new Color(1f, 0.62f, 0.22f, 1f), 0.72f);
            EnsureBackdropVisualPrimitive(backdropRoot, "LeftWingFlat", PrimitiveType.Cube, new Vector3(-4.88f, 2.72f, 5.34f), new Vector3(0f, -10f, 0f), new Vector3(0.18f, 4.62f, 0.86f), new Color(0.028f, 0.032f, 0.039f, 1f), 0.64f);
            EnsureBackdropVisualPrimitive(backdropRoot, "RightWingFlat", PrimitiveType.Cube, new Vector3(4.88f, 2.72f, 5.34f), new Vector3(0f, 10f, 0f), new Vector3(0.18f, 4.62f, 0.86f), new Color(0.028f, 0.032f, 0.039f, 1f), 0.64f);
        }

        private static void SetBackdropChildPose(
            Transform root,
            string childName,
            Vector2 anchoredPosition,
            float localZ,
            Vector3 localScale)
        {
            Transform child = FindChildRecursive(root, childName);
            if (child == null)
            {
                return;
            }

            RectTransform rectTransform = child as RectTransform;
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = anchoredPosition;
                Vector3 localPosition = rectTransform.localPosition;
                localPosition.z = localZ;
                rectTransform.localPosition = localPosition;
            }
            else
            {
                child.localPosition = new Vector3(anchoredPosition.x, anchoredPosition.y, localZ);
            }

            child.localScale = localScale;
        }

        private static void EnsureBackdropVisualPrimitive(
            Transform root,
            string childName,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale,
            Color color,
            float smoothness)
        {
            if (root == null)
            {
                return;
            }

            Transform child = FindChildRecursive(root, childName);
            if (child == null)
            {
                child = new GameObject(childName).transform;
                child.SetParent(root, false);
            }

            MeshFilter meshFilter = child.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = child.gameObject.AddComponent<MeshFilter>();
            }

            MeshRenderer meshRenderer = child.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = child.gameObject.AddComponent<MeshRenderer>();
            }

            if (meshFilter.sharedMesh == null)
            {
                GameObject primitive = GameObject.CreatePrimitive(primitiveType);
                MeshFilter primitiveFilter = primitive.GetComponent<MeshFilter>();
                if (primitiveFilter != null)
                {
                    meshFilter.sharedMesh = primitiveFilter.sharedMesh;
                }

                if (Application.isPlaying)
                {
                    Destroy(primitive);
                }
                else
                {
                    DestroyImmediate(primitive);
                }
            }

            Collider collider = child.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            child.localPosition = localPosition;
            child.localRotation = Quaternion.Euler(localEulerAngles);
            child.localScale = localScale;
            child.gameObject.SetActive(true);

            Material material = CreateRuntimeBackdropMaterial(color, smoothness);
            if (material != null)
            {
                meshRenderer.sharedMaterial = material;
            }
        }

        private static Material CreateRuntimeBackdropMaterial(Color color, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || shader.name == "Hidden/InternalErrorShader")
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                return null;
            }

            Material material = new Material(shader)
            {
                color = color
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            return material;
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

            bool xrRunning = IsXrDisplayRunning();
            bool useFloorTrackingInXr = ShouldUseFloorTrackingInXr();
            XROrigin.TrackingOriginMode effectiveTrackingOriginMode =
                xrRunning && useFloorTrackingInXr
                    ? XROrigin.TrackingOriginMode.Floor
                    : requestedTrackingOriginMode;
            float effectiveCameraYOffset =
                effectiveTrackingOriginMode == XROrigin.TrackingOriginMode.Floor
                    ? 0f
                    : cameraYOffset;

            if (ShouldFallbackToDeviceHeight(xrRunning, effectiveTrackingOriginMode))
            {
                effectiveTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
                effectiveCameraYOffset = cameraYOffset;
            }

            if (xrOrigin.RequestedTrackingOriginMode != effectiveTrackingOriginMode)
            {
                xrOrigin.RequestedTrackingOriginMode = effectiveTrackingOriginMode;
            }

            if (Mathf.Abs(xrOrigin.CameraYOffset - effectiveCameraYOffset) > FloatEpsilon)
            {
                xrOrigin.CameraYOffset = effectiveCameraYOffset;
            }

            if (xrOrigin.CameraFloorOffsetObject != null)
            {
                Vector3 localPosition = xrOrigin.CameraFloorOffsetObject.transform.localPosition;
                if (Mathf.Abs(localPosition.y - effectiveCameraYOffset) > FloatEpsilon)
                {
                    localPosition.y = effectiveCameraYOffset;
                    xrOrigin.CameraFloorOffsetObject.transform.localPosition = localPosition;
                }
            }

            if (!keepRigStationary)
            {
                ApplyUnlockedRigState();
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
            return IsXrDisplayRunning();
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

        private bool ShouldFallbackToDeviceHeight(bool xrRunning, XROrigin.TrackingOriginMode effectiveTrackingOriginMode)
        {
            if (!xrRunning ||
                effectiveTrackingOriginMode != XROrigin.TrackingOriginMode.Floor ||
                elapsed < floorTrackingFallbackDelay ||
                xrOrigin == null ||
                xrOrigin.Camera == null)
            {
                return false;
            }

            return xrOrigin.Camera.transform.localPosition.y < floorTrackingMinimumHeadHeight;
        }

        private bool ShouldUseFloorTrackingInXr()
        {
            if (ShouldUseStableDeviceHeight())
            {
                return false;
            }

            return useFloorTrackingWhenXrRunning;
        }

        private bool ShouldUseStableDeviceHeight()
        {
            return keepRigStationary &&
                   string.Equals(backdropRootName, MainHubBackdropName, StringComparison.Ordinal);
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

        private void ApplyUnlockedRigState()
        {
            if (xrOrigin == null)
            {
                return;
            }

            ToggleChildActive(xrOrigin.transform, "Gravity", !keepUnlockedRigGravityDisabled);
            ToggleChildActive(xrOrigin.transform, "Teleportation", true);
            ToggleChildActive(xrOrigin.transform, "Locomotion", true);

            CharacterController characterController = xrOrigin.GetComponent<CharacterController>();
            if (characterController != null && !characterController.enabled)
            {
                characterController.enabled = true;
            }

            rigSafetyApplied = false;
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
