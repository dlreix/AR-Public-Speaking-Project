using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRPublicSpeaking.AppShell.UI;

namespace VRPublicSpeaking.AppShell.Flow
{
    [DisallowMultipleComponent]
    public class MainHubTutorialController : MonoBehaviour
    {
        [SerializeField] private Canvas hubCanvas;
        [SerializeField] private bool installOnStart = true;
        [SerializeField] private bool repositionHubCanvas = true;
        [SerializeField] private bool applyTutorialSpaceLayout = true;
        [SerializeField] private Vector3 hubCanvasPosition = new Vector3(0f, 2.95f, 5.72f);
        [SerializeField] private Vector3 hubCanvasEulerAngles = Vector3.zero;
        [SerializeField] private Vector3 hubCanvasScale = new Vector3(0.0041f, 0.0041f, 0.0041f);
        [SerializeField] private float panelScale = 0.0038f;

        private const string TutorialRootName = "TutorialWallPanels";
        private const string TutorialSetDressingRootName = "TutorialSetDressing";
        private const string LegacyTutorialGuideRootName = "TutorialFloorGuides";
        private bool installed;

        private static readonly Color PanelColor = new Color(0.025f, 0.036f, 0.052f, 0.94f);
        private static readonly Color HeaderColor = new Color(1f, 0.64f, 0.24f, 1f);
        private static readonly Color BodyColor = new Color(0.88f, 0.93f, 0.98f, 1f);
        private static readonly Color MutedColor = new Color(0.48f, 0.62f, 0.76f, 1f);
        private static readonly Color BackplateColor = new Color(0.018f, 0.027f, 0.04f, 1f);
        private static readonly Color RugColor = new Color(0.055f, 0.016f, 0.026f, 1f);
        private static readonly Color TrimColor = new Color(0.95f, 0.54f, 0.18f, 1f);
        private static readonly Color MarkerColor = new Color(0.08f, 0.12f, 0.16f, 1f);

        private void Start()
        {
            if (installOnStart)
            {
                InstallNow();
            }
        }

        public void Configure(Canvas targetHubCanvas)
        {
            hubCanvas = targetHubCanvas;
            InstallNow();
        }

        public void InstallNow()
        {
            PositionHubCanvas();
            ApplyTutorialSpaceLayout();

            if (installed || transform.Find(TutorialRootName) != null)
            {
                installed = true;
                return;
            }

            Transform root = new GameObject(TutorialRootName).transform;
            root.SetParent(transform, false);

            CreatePanel(
                root,
                "MovementTutorialPanel",
                "Movement & UI",
                "Left stick: move around the hub.\nRight stick: turn your view.\nAim the controller ray at a menu item.\nTrigger: select buttons and cards.",
                "Walk to the wall menu when you are ready to start.",
                new Vector3(-5.78f, 2.25f, 1.05f),
                new Vector3(0f, -90f, 0f));

            CreatePanel(
                root,
                "SessionControlsTutorialPanel",
                "Session Controls",
                "A / X: start or stop the active session.\nB / Y tap: toggle debug guidance.\nB / Y hold: pause or resume during a session.\nGrip: trigger the circle event while recording.",
                "These controls apply after you launch a practice room.",
                new Vector3(5.78f, 2.25f, 1.05f),
                new Vector3(0f, 90f, 0f));

            CreatePanel(
                root,
                "DesktopTestingTutorialPanel",
                "Desktop Testing",
                "WASD: move.\nMouse: look around.\nR: start or stop.\nD: debug.\nEsc: pause.\nC or left click: circle event.",
                "Use this when checking the flow without a headset.",
                new Vector3(-2.25f, 2.2f, -1.48f),
                new Vector3(0f, 180f, 0f));

            CreatePanel(
                root,
                "PracticeFlowTutorialPanel",
                "Practice Flow",
                "1. Choose Practice Mode.\n2. Pick a room.\n3. Review setup.\n4. Start Session.\n5. Read Results and recommendations.",
                "The wall menu stays available as your main launch board.",
                new Vector3(2.25f, 2.2f, -1.48f),
                new Vector3(0f, 180f, 0f));

            installed = true;
        }

        private void ApplyTutorialSpaceLayout()
        {
            if (!applyTutorialSpaceLayout)
            {
                return;
            }

            Transform backdropRoot = FindSceneRoot("MainHubBackdrop");
            if (backdropRoot != null)
            {
                SetChildActive(backdropRoot, "StageFrontEdge", false);
                SetChildActive(backdropRoot, "Footlight_Left", false);
                SetChildActive(backdropRoot, "Footlight_Center", false);
                SetChildActive(backdropRoot, "Footlight_Right", false);

                Transform stageDeck = FindChildRecursive(backdropRoot, "StageDeck");
                if (stageDeck != null)
                {
                    stageDeck.localPosition = new Vector3(0f, 0.05f, 4.95f);
                    stageDeck.localScale = new Vector3(7.25f, 0.08f, 1.08f);
                }
            }

            EnsureTutorialSetDressing();
        }

        private void EnsureTutorialSetDressing()
        {
            Transform legacyGuides = transform.Find(LegacyTutorialGuideRootName);
            if (legacyGuides != null)
            {
                legacyGuides.gameObject.SetActive(false);
            }

            if (transform.Find(TutorialSetDressingRootName) != null)
            {
                return;
            }

            Transform setRoot = new GameObject(TutorialSetDressingRootName).transform;
            setRoot.SetParent(transform, false);

            Material backplateMaterial = CreateGuideMaterial(BackplateColor, "Tutorial_Backplate");
            Material rugMaterial = CreateGuideMaterial(RugColor, "Tutorial_Rug");
            Material trimMaterial = CreateGuideMaterial(TrimColor, "Tutorial_Trim");
            Material markerMaterial = CreateGuideMaterial(MarkerColor, "Tutorial_Marker");

            CreateDesignBlock(setRoot, "MenuBackplate", new Vector3(0f, 2.95f, 5.86f), Vector3.zero, new Vector3(8.8f, 5.15f, 0.08f), backplateMaterial);
            CreateDesignBlock(setRoot, "MenuTopTrim", new Vector3(0f, 5.56f, 5.66f), Vector3.zero, new Vector3(8.9f, 0.08f, 0.08f), trimMaterial);
            CreateDesignBlock(setRoot, "MenuBottomTrim", new Vector3(0f, 0.34f, 5.66f), Vector3.zero, new Vector3(8.9f, 0.08f, 0.08f), trimMaterial);

            CreateDesignBlock(setRoot, "LeftPanelBackplate", new Vector3(-5.88f, 2.25f, 1.05f), Vector3.zero, new Vector3(0.08f, 3.25f, 4.95f), backplateMaterial);
            CreateDesignBlock(setRoot, "RightPanelBackplate", new Vector3(5.88f, 2.25f, 1.05f), Vector3.zero, new Vector3(0.08f, 3.25f, 4.95f), backplateMaterial);
            CreateDesignBlock(setRoot, "DesktopPanelBackplate", new Vector3(-2.25f, 2.2f, -1.56f), Vector3.zero, new Vector3(4.7f, 3.05f, 0.08f), backplateMaterial);
            CreateDesignBlock(setRoot, "FlowPanelBackplate", new Vector3(2.25f, 2.2f, -1.56f), Vector3.zero, new Vector3(4.7f, 3.05f, 0.08f), backplateMaterial);

            CreateDesignBlock(setRoot, "CentralGalleryRug", new Vector3(0f, 0.034f, 2.1f), Vector3.zero, new Vector3(3.15f, 0.018f, 4.8f), rugMaterial);
            CreateDesignBlock(setRoot, "RugLeftTrim", new Vector3(-1.62f, 0.046f, 2.1f), Vector3.zero, new Vector3(0.055f, 0.02f, 4.8f), trimMaterial);
            CreateDesignBlock(setRoot, "RugRightTrim", new Vector3(1.62f, 0.046f, 2.1f), Vector3.zero, new Vector3(0.055f, 0.02f, 4.8f), trimMaterial);
            CreateDesignBlock(setRoot, "RugFrontTrim", new Vector3(0f, 0.047f, -0.3f), Vector3.zero, new Vector3(3.25f, 0.02f, 0.055f), trimMaterial);
            CreateDesignBlock(setRoot, "RugBackTrim", new Vector3(0f, 0.047f, 4.5f), Vector3.zero, new Vector3(3.25f, 0.02f, 0.055f), trimMaterial);

            CreateStandingMarker(setRoot, "StartMarker", new Vector3(0f, 0.055f, 0.22f), new Vector3(0.72f, 0.012f, 0.72f), markerMaterial);
            CreateStandingMarker(setRoot, "MenuMarker", new Vector3(0f, 0.058f, 3.55f), new Vector3(0.86f, 0.012f, 0.86f), trimMaterial);
        }

        private static void CreateDesignBlock(
            Transform parent,
            string objectName,
            Vector3 position,
            Vector3 eulerAngles,
            Vector3 scale,
            Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = objectName;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localRotation = Quaternion.Euler(eulerAngles);
            block.transform.localScale = scale;
            ApplyGuideMaterial(block, material);
            DisableCollider(block);
        }

        private static void CreateStandingMarker(Transform parent, string objectName, Vector3 position, Vector3 scale, Material material)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = objectName;
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = position;
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = scale;
            ApplyGuideMaterial(marker, material);
            DisableCollider(marker);
        }

        private void PositionHubCanvas()
        {
            if (!repositionHubCanvas || hubCanvas == null)
            {
                return;
            }

            hubCanvas.renderMode = RenderMode.WorldSpace;
            if (hubCanvas.worldCamera == null && Camera.main != null)
            {
                hubCanvas.worldCamera = Camera.main;
            }

            Transform canvasTransform = hubCanvas.transform;
            canvasTransform.position = hubCanvasPosition;
            canvasTransform.rotation = Quaternion.Euler(hubCanvasEulerAngles);
            canvasTransform.localScale = hubCanvasScale;

            WorldSpaceCanvasFollower follower = hubCanvas.GetComponent<WorldSpaceCanvasFollower>();
            if (follower != null)
            {
                follower.enabled = false;
            }
        }

        private void CreatePanel(
            Transform root,
            string panelName,
            string title,
            string body,
            string footer,
            Vector3 position,
            Vector3 eulerAngles)
        {
            GameObject canvasObject = new GameObject(panelName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(root, false);
            canvasObject.transform.position = position;
            canvasObject.transform.rotation = Quaternion.Euler(eulerAngles);
            canvasObject.transform.localScale = Vector3.one * panelScale;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1120f, 700f);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 2;
            if (Camera.main != null)
            {
                canvas.worldCamera = Camera.main;
            }

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 12f;

            CreateImage(canvasRect, "Background", StretchRect(Vector2.zero, Vector2.zero), PanelColor, true);
            CreateImage(canvasRect, "AccentBar", TopRect(0f, 0f, 12f), HeaderColor, false);

            CreateText(
                canvasRect,
                "Title",
                title,
                new RectOffset(54, 54, 38, 0),
                58f,
                HeaderColor,
                TextAlignmentOptions.TopLeft,
                FontStyles.Bold,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, 118f));

            CreateText(
                canvasRect,
                "Body",
                body,
                new RectOffset(58, 58, 150, 86),
                35f,
                BodyColor,
                TextAlignmentOptions.TopLeft,
                FontStyles.Normal,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero);

            CreateText(
                canvasRect,
                "Footer",
                footer,
                new RectOffset(58, 58, 0, 34),
                25f,
                MutedColor,
                TextAlignmentOptions.BottomLeft,
                FontStyles.Italic,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 62f));
        }

        private static Image CreateImage(
            RectTransform parent,
            string objectName,
            RectTransformSetup rectSetup,
            Color color,
            bool raycastTarget)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rectSetup.Apply(rect);

            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static TextMeshProUGUI CreateText(
            RectTransform parent,
            string objectName,
            string text,
            RectOffset padding,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            FontStyles fontStyle,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 sizeDelta)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.offsetMin = new Vector2(padding.left, padding.bottom);
            rect.offsetMax = new Vector2(-padding.right, -padding.top);
            if (sizeDelta != Vector2.zero)
            {
                rect.sizeDelta = sizeDelta;
            }

            TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = fontStyle;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            return label;
        }

        private static void ApplyGuideMaterial(GameObject target, Material material)
        {
            if (target == null || material == null)
            {
                return;
            }

            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void DisableCollider(GameObject target)
        {
            Collider collider = target != null ? target.GetComponent<Collider>() : null;
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        private static Material CreateGuideMaterial(Color color, string materialName)
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
                name = materialName,
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
                material.SetFloat("_Smoothness", 0.42f);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.42f);
            }

            return material;
        }

        private static Transform FindSceneRoot(string objectName)
        {
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform candidate = transforms[index];
                if (candidate != null && candidate.parent == null && candidate.name == objectName)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void SetChildActive(Transform root, string childName, bool isActive)
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
                Transform child = root.GetChild(index);
                Transform match = FindChildRecursive(child, childName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static RectTransformSetup StretchRect(Vector2 offsetMin, Vector2 offsetMax)
        {
            return new RectTransformSetup(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), offsetMin, offsetMax, Vector2.zero);
        }

        private static RectTransformSetup TopRect(float left, float right, float height)
        {
            return new RectTransformSetup(new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(left, -height), new Vector2(-right, 0f), Vector2.zero);
        }

        private readonly struct RectTransformSetup
        {
            private readonly Vector2 anchorMin;
            private readonly Vector2 anchorMax;
            private readonly Vector2 pivot;
            private readonly Vector2 offsetMin;
            private readonly Vector2 offsetMax;
            private readonly Vector2 sizeDelta;

            public RectTransformSetup(
                Vector2 anchorMin,
                Vector2 anchorMax,
                Vector2 pivot,
                Vector2 offsetMin,
                Vector2 offsetMax,
                Vector2 sizeDelta)
            {
                this.anchorMin = anchorMin;
                this.anchorMax = anchorMax;
                this.pivot = pivot;
                this.offsetMin = offsetMin;
                this.offsetMax = offsetMax;
                this.sizeDelta = sizeDelta;
            }

            public void Apply(RectTransform rect)
            {
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.pivot = pivot;
                rect.offsetMin = offsetMin;
                rect.offsetMax = offsetMax;
                rect.sizeDelta = sizeDelta;
            }
        }
    }
}
