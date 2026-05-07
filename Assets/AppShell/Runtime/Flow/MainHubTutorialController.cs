using System.Collections.Generic;
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

        [Header("Enhanced Tutorial Systems")]
        [SerializeField] private bool enableProgressTracking = true;
        [SerializeField] private bool enablePanelAnimations = true;
        [SerializeField] private bool enableAmbientLighting = true;
        [SerializeField] private bool enableWelcomePanel = false;
        [SerializeField] private bool enableProgressHud = false;
        [SerializeField] private bool enableFloorGuides = true;
        [SerializeField] private bool enableCompletionCelebration = true;
        [SerializeField] private bool enableSequentialReveal = true;

        [Header("Sequential Tutorial Mode")]
        [Tooltip("When enabled, tutorial panels appear one at a time in front of the user.")]
        [SerializeField] private bool enableSequentialMode = true;
        [SerializeField] private float sequentialStartDelay = 1.5f;

        private const string TutorialRootName = "TutorialWallPanels";
        private const string TutorialSetDressingRootName = "TutorialSetDressing";
        private const string LegacyTutorialGuideRootName = "TutorialFloorGuides";
        private bool installed;

        private TutorialProgressTracker progressTracker;
        private TutorialAmbientLighting ambientLighting;
        private TutorialWelcomePanel welcomePanel;
        private TutorialProgressHud progressHud;
        private TutorialFloorGuideSystem floorGuideSystem;
        private TutorialCompletionCelebration completionCelebration;
        private TutorialSequentialPresenter sequentialPresenter;
        private readonly List<TutorialPanelAnimator> panelAnimators = new List<TutorialPanelAnimator>();
        private readonly Dictionary<string, TutorialPanelAnimator> animatorsByName = new Dictionary<string, TutorialPanelAnimator>();
        private readonly List<Vector3> panelPositions = new List<Vector3>();
        private readonly List<string> panelNames = new List<string>();
        private int panelCreationIndex;
        private int totalPanelCount = 4;
        private bool celebrationTriggered;

        private static readonly Color PanelColor = new Color(0.015f, 0.02f, 0.035f, 0.88f);
        private static readonly Color HeaderColor = new Color(0.12f, 0.78f, 0.96f, 1f);
        private static readonly Color BodyColor = new Color(0.92f, 0.95f, 0.98f, 1f);
        private static readonly Color MutedColor = new Color(0.55f, 0.65f, 0.75f, 0.8f);
        private static readonly Color PanelSectionColor = new Color(0.055f, 0.09f, 0.13f, 0.92f);
        private static readonly Color FooterPanelColor = new Color(0.035f, 0.055f, 0.075f, 0.95f);
        private static readonly Color BackplateColor = new Color(0.01f, 0.015f, 0.025f, 1f);
        private static readonly Color RugColor = new Color(0.015f, 0.03f, 0.05f, 1f);
        private static readonly Color TrimColor = new Color(0.12f, 0.78f, 0.96f, 1f);
        private static readonly Color MarkerColor = new Color(0.04f, 0.06f, 0.08f, 1f);

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

            if (installed)
            {
                return;
            }

            Transform existingRoot = transform.Find(TutorialRootName);
            if (existingRoot != null)
            {
                existingRoot.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(existingRoot.gameObject);
                }
                else
                {
                    DestroyImmediate(existingRoot.gameObject);
                }
            }

            panelAnimators.Clear();
            animatorsByName.Clear();
            panelPositions.Clear();
            panelNames.Clear();
            panelCreationIndex = 0;
            celebrationTriggered = false;

            // Initialize subsystems before creating panels
            InitializeSubsystems();

            Transform root = new GameObject(TutorialRootName).transform;
            root.SetParent(transform, false);

            CreatePanel(
                root,
                "MovementTutorialPanel",
                "Move & Select",
                "<b>MOVE</b>\nLeft stick walks. Right stick turns your view.\n\n<b>SELECT</b>\nAim the controller ray at a button, then press trigger.",
                "Start at the wall menu when you are ready.",
                new Vector3(-5.78f, 2.25f, 1.05f),
                new Vector3(0f, -90f, 0f));

            CreatePanel(
                root,
                "SessionControlsTutorialPanel",
                "Session Controls",
                "<b>DURING PRACTICE</b>\nA / X starts or stops the session.\nB / Y hold opens pause.\nGrip triggers the circle event.",
                "Use these after the practice room opens.",
                new Vector3(5.78f, 2.25f, 1.05f),
                new Vector3(0f, 90f, 0f));

            CreatePanel(
                root,
                "DesktopTestingTutorialPanel",
                "Keyboard Fallback",
                "<b>DESKTOP CHECKS</b>\nWASD moves. Mouse looks.\nR starts or stops.\nEsc pauses.\nC or click triggers the circle event.",
                "Useful for quick testing without the headset.",
                new Vector3(-2.25f, 2.2f, -1.48f),
                new Vector3(0f, 180f, 0f));

            CreatePanel(
                root,
                "PracticeFlowTutorialPanel",
                "Launch Flow",
                "<b>1</b> Start Practice\n<b>2</b> Pick a room\n<b>3</b> Review setup\n<b>4</b> Start Session\n<b>5</b> Open Results or Dashboard",
                "Dashboard is available from Main Hub and results.",
                new Vector3(2.25f, 2.2f, -1.48f),
                new Vector3(0f, 180f, 0f));

            installed = true;

            // Finalize subsystems after all panels are created
            FinalizeSubsystems();

            // Setup sequential mode if enabled
            if (enableSequentialMode)
            {
                SetupSequentialPresentation();
            }
        }

        private void SetupSequentialPresentation()
        {
            sequentialPresenter = GetComponent<TutorialSequentialPresenter>();
            if (sequentialPresenter == null)
                sequentialPresenter = gameObject.AddComponent<TutorialSequentialPresenter>();

            // Register slides with the sequential presenter
            sequentialPresenter.AddSlide(
                "Welcome",
                "- Move and select from the wall menu\n- Review in-session controls\n- Check the launch flow\n- Open dashboard from Main Hub or Results",
                "This short guide replaces the separate welcome page, so you can begin faster.",
                "VR");

            sequentialPresenter.AddSlide(
                "Movement & UI",
                "- Left stick: move around the hub\n- Right stick: turn your view\n- Aim the controller ray at a menu item\n- Trigger: select buttons and cards",
                "Walk to the wall menu when you are ready to start.",
                "VR");

            sequentialPresenter.AddSlide(
                "Session Controls",
                "- A / X: start or stop the active session\n- B / Y hold: pause or resume\n- Grip: trigger the circle event while recording",
                "These controls apply after you launch a practice room.",
                "XR");

            sequentialPresenter.AddSlide(
                "Keyboard Fallback",
                "- WASD: move\n- Mouse: look around\n- R: start or stop\n- Esc: pause\n- C or left click: circle event",
                "Use this when checking the flow without a headset.",
                "PC");

            sequentialPresenter.AddSlide(
                "Launch Flow",
                "1. Start Practice\n2. Pick a room\n3. Review setup\n4. Start Session\n5. Read Results and recommendations",
                "Dashboard is available from Main Hub and Results.",
                "GO");

            // Wire events
            sequentialPresenter.AllSlidesCompleted += OnSequentialCompleted;
            sequentialPresenter.TutorialSkipped += OnSequentialSkipped;
            sequentialPresenter.SlideShown += OnSequentialSlideShown;

            sequentialPresenter.StartPresentationDelayed(sequentialStartDelay);
        }

        private void OnSequentialCompleted()
        {
            Debug.Log("[Tutorial] Sequential presentation completed!");
            if (enableCompletionCelebration && completionCelebration != null && !celebrationTriggered)
            {
                celebrationTriggered = true;
                completionCelebration.PlayCelebration();
            }
        }

        private void OnSequentialSkipped()
        {
            Debug.Log("[Tutorial] Sequential presentation skipped by user.");
        }

        private void OnSequentialSlideShown(int slideIndex)
        {
            Debug.Log($"[Tutorial] Showing slide {slideIndex + 1}/{sequentialPresenter.TotalSlides}");

            // Update progress HUD if available
            if (progressHud != null && sequentialPresenter != null)
            {
                progressHud.UpdateProgress(
                    slideIndex + 1,
                    sequentialPresenter.TotalSlides,
                    (float)(slideIndex + 1) / sequentialPresenter.TotalSlides);
            }
        }

        private void InitializeSubsystems()
        {
            panelCreationIndex = 0;

            if (enableProgressTracking)
            {
                progressTracker = GetComponent<TutorialProgressTracker>();
                if (progressTracker == null)
                    progressTracker = gameObject.AddComponent<TutorialProgressTracker>();

                progressTracker.PanelFirstVisited += OnPanelFirstVisited;
                progressTracker.PanelCompleted += OnPanelCompleted;
                progressTracker.OverallProgressChanged += OnOverallProgressChanged;
            }

            if (enableAmbientLighting)
            {
                ambientLighting = GetComponent<TutorialAmbientLighting>();
                if (ambientLighting == null)
                    ambientLighting = gameObject.AddComponent<TutorialAmbientLighting>();
                ambientLighting.Initialize();
            }

            if (enableWelcomePanel)
            {
                welcomePanel = GetComponent<TutorialWelcomePanel>();
                if (welcomePanel != null && welcomePanel.IsShowing)
                {
                    welcomePanel.Dismiss();
                }
            }

            if (enableFloorGuides)
            {
                floorGuideSystem = GetComponent<TutorialFloorGuideSystem>();
                if (floorGuideSystem == null)
                    floorGuideSystem = gameObject.AddComponent<TutorialFloorGuideSystem>();
                floorGuideSystem.Initialize();
            }

            if (enableCompletionCelebration)
            {
                completionCelebration = GetComponent<TutorialCompletionCelebration>();
                if (completionCelebration == null)
                    completionCelebration = gameObject.AddComponent<TutorialCompletionCelebration>();
            }
        }

        private void FinalizeSubsystems()
        {
            if (enableProgressHud)
            {
                progressHud = GetComponent<TutorialProgressHud>();
                if (progressHud == null)
                    progressHud = gameObject.AddComponent<TutorialProgressHud>();
                progressHud.Initialize(progressTracker != null ? progressTracker.TotalPanels : panelAnimators.Count);
            }

            // Create floor guide paths between panels
            if (enableFloorGuides && floorGuideSystem != null && panelPositions.Count > 1)
            {
                // Paths connecting adjacent panels in visit order
                for (int i = 0; i < panelPositions.Count - 1; i++)
                {
                    Vector3 start = panelPositions[i];
                    Vector3 end = panelPositions[i + 1];
                    floorGuideSystem.CreatePathSegment("Path_" + i, start, end);
                }

                // Station markers at each panel location
                for (int i = 0; i < panelPositions.Count; i++)
                {
                    floorGuideSystem.CreateStationMarker(panelNames[i], panelPositions[i], i);
                }

                // Close the loop: last panel back to the start area
                floorGuideSystem.CreatePathSegment("Path_Return",
                    panelPositions[panelPositions.Count - 1],
                    new Vector3(0f, 0f, 3.5f));
            }

            // Trigger reveal animations
            if (enablePanelAnimations)
            {
                if (enableSequentialReveal)
                {
                    // Sequential reveal with staggered delays
                    for (int i = 0; i < panelAnimators.Count; i++)
                    {
                        TutorialPanelAnimator animator = panelAnimators[i];
                        float delay = i * 0.35f;
                        StartCoroutine(DelayedReveal(animator, delay));
                    }
                }
                else
                {
                    foreach (var animator in panelAnimators)
                    {
                        animator.TriggerReveal();
                    }
                }
            }
        }

        private System.Collections.IEnumerator DelayedReveal(TutorialPanelAnimator animator, float delay)
        {
            yield return new WaitForSeconds(delay);
            animator.TriggerReveal();
        }

        private void Update()
        {
            if (!installed || progressTracker == null) return;

            // Update proximity state on each animator
            foreach (var kvp in animatorsByName)
            {
                bool inRange = progressTracker.IsPanelInRange(kvp.Key);
                float dwellProgress = progressTracker.GetPanelDwellProgress(kvp.Key);
                kvp.Value.SetProximity(inRange, dwellProgress);
            }
        }

        private void OnPanelFirstVisited(string panelId)
        {
            Debug.Log($"[Tutorial] Panel visited: {panelId}");
        }

        private void OnPanelCompleted(string panelId)
        {
            Debug.Log($"[Tutorial] Panel completed: {panelId}");
            if (animatorsByName.TryGetValue(panelId, out var animator))
            {
                animator.MarkCompleted();
            }

            // Mark floor guide station as completed
            if (enableFloorGuides && floorGuideSystem != null)
            {
                int stationIndex = panelNames.IndexOf(panelId);
                if (stationIndex >= 0)
                {
                    floorGuideSystem.MarkStationCompleted(stationIndex);
                }
            }

            UpdateProgressHud();
        }

        private void OnOverallProgressChanged(float progress)
        {
            UpdateProgressHud();

            // Dismiss welcome panel once user starts exploring
            if (progress > 0f && welcomePanel != null && welcomePanel.IsShowing)
            {
                welcomePanel.Dismiss();
            }

            // Trigger celebration when all panels completed
            if (progress >= 1f && !celebrationTriggered)
            {
                celebrationTriggered = true;
                if (enableCompletionCelebration && completionCelebration != null)
                {
                    completionCelebration.PlayCelebration();
                    Debug.Log("[Tutorial] All panels completed! Celebration triggered.");
                }
            }
        }

        private void UpdateProgressHud()
        {
            if (progressHud == null || progressTracker == null) return;
            progressHud.UpdateProgress(
                progressTracker.GetCompletedCount(),
                progressTracker.TotalPanels,
                progressTracker.OverallProgress);
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
                SetChildActive(backdropRoot, "BackCeilingLightBar", false);

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
            panelCreationIndex++;
            string stepLabel = $"Step {panelCreationIndex}/{totalPanelCount}";

            // Track positions for floor guide generation
            panelPositions.Add(position);
            panelNames.Add(panelName);

            GameObject canvasObject = new GameObject(panelName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
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

            CanvasGroup canvasGroup = canvasObject.GetComponent<CanvasGroup>();

            CreateImage(canvasRect, "Background", StretchRect(Vector2.zero, Vector2.zero), PanelColor, true);
            Image accentBar = CreateImage(canvasRect, "AccentBar", TopRect(0f, 0f, 12f), HeaderColor, false);
            CreateImage(
                canvasRect,
                "BodyPanel",
                new RectTransformSetup(
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(52f, 98f),
                    new Vector2(-52f, -154f),
                    Vector2.zero),
                PanelSectionColor,
                false);
            CreateImage(
                canvasRect,
                "FooterPanel",
                new RectTransformSetup(
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(52f, 28f),
                    new Vector2(-52f, 92f),
                    Vector2.zero),
                FooterPanelColor,
                false);

            // Step badge (top-right corner)
            CreateText(
                canvasRect,
                "StepBadge",
                stepLabel,
                new RectOffset(0, 44, 42, 0),
                26f,
                MutedColor,
                TextAlignmentOptions.TopRight,
                FontStyles.Normal,
                new Vector2(0.7f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 40f));

            CreateText(
                canvasRect,
                "Title",
                title,
                new RectOffset(54, 120, 38, 0),
                60f,
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
                new RectOffset(80, 80, 172, 152),
                34f,
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
                new RectOffset(80, 80, 0, 42),
                26f,
                MutedColor,
                TextAlignmentOptions.BottomLeft,
                FontStyles.Bold,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 54f));

            // Progress fill bar at bottom of panel
            Image progressFill = CreateImage(canvasRect, "ProgressFill",
                new RectTransformSetup(
                    new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
                    Vector2.zero, new Vector2(0f, 5f), Vector2.zero),
                new Color(HeaderColor.r, HeaderColor.g, HeaderColor.b, 0.7f), false);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillAmount = 0f;

            // Attach animator for reveal and proximity effects
            if (enablePanelAnimations)
            {
                TutorialPanelAnimator animator = canvasObject.AddComponent<TutorialPanelAnimator>();
                animator.Initialize(canvasGroup, accentBar, null, progressFill);
                panelAnimators.Add(animator);
                animatorsByName[panelName] = animator;
            }

            // Register with progress tracker
            if (enableProgressTracking && progressTracker != null)
            {
                progressTracker.RegisterPanel(panelName, canvasObject.transform);
            }

            // Add spotlight for this panel
            if (enableAmbientLighting && ambientLighting != null)
            {
                Vector3 panelForward = Quaternion.Euler(eulerAngles) * Vector3.forward;
                ambientLighting.AddPanelSpotLight(panelName, position, panelForward);
            }
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
