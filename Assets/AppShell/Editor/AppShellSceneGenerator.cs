using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.XR.CoreUtils;
using VRPublicSpeaking.AppShell.Data;
using VRPublicSpeaking.AppShell.Flow;
using VRPublicSpeaking.AppShell.Integration;
using VRPublicSpeaking.AppShell.Results;
using VRPublicSpeaking.AppShell.UI;

namespace VRPublicSpeaking.AppShell.Editor
{
    public static class AppShellSceneGenerator
    {
        private static readonly string[] RigPrefabCandidates =
        {
            "Assets/Samples/XR Interaction Toolkit/3.3.0/Starter Assets/Prefabs/XR Origin (XR Rig).prefab",
            "Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab"
        };

        private const string LegacySharedRigPrefabPath = "Assets/Prefabs/share.prefab";

        [MenuItem("Tools/VR Public Speaking/App Shell/Build Or Refresh Full App Shell")]
        public static void BuildOrRefreshFullAppShell()
        {
            AppShellSetupUtility.CreateDefaultEnvironmentCatalog();

            CreateOrUpdateMainHubSceneInternal(AppShellEditorCommon.LoadEnvironmentCatalog());
            CreateOrUpdateResultsSceneInternal();
            InstallBindingsInAllEnvironmentScenesInternal();
            AppShellSetupUtility.AddAppShellScenesToBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            AppShellValidationUtility.ValidateAppShell(logSuccess: false);
        }

        [MenuItem("Tools/VR Public Speaking/App Shell/Create Or Update MainHubScene")]
        public static void CreateOrUpdateMainHubScene()
        {
            AppShellSetupUtility.CreateDefaultEnvironmentCatalog();
            CreateOrUpdateMainHubSceneInternal(AppShellEditorCommon.LoadEnvironmentCatalog());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            AppShellValidationUtility.ValidateAppShell(logSuccess: false);
        }

        [MenuItem("Tools/VR Public Speaking/App Shell/Create Or Update ResultsScene")]
        public static void CreateOrUpdateResultsScene()
        {
            CreateOrUpdateResultsSceneInternal();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            AppShellValidationUtility.ValidateAppShell(logSuccess: false);
        }

        [MenuItem("Tools/VR Public Speaking/App Shell/Install App Shell Bindings In Environment Scenes")]
        public static void InstallBindingsInAllEnvironmentScenes()
        {
            InstallBindingsInAllEnvironmentScenesInternal();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            AppShellValidationUtility.ValidateAppShell(logSuccess: false);
        }

        private static void CreateOrUpdateMainHubSceneInternal(AppEnvironmentCatalog catalog)
        {
            Scene scene = AppShellEditorCommon.OpenOrCreateScene(AppShellEditorCommon.MainHubScenePath);

            EnsureDirectionalLight(scene);
            EnsureBackdrop(scene, "MainHubBackdrop");
            EnsurePlayerRig(scene, "HubPlayerRig");
            EnsureEventSystem(scene);

            TransitionManager transitionManager = EnsureTransitionOverlay(scene);
            Canvas hubCanvas = EnsureWorldSpaceCanvas(
                scene,
                "WorldSpaceHubCanvas",
                new Vector2(1800f, 1200f),
                new Vector3(0f, 1.55f, 2.35f),
                new Vector3(0.00135f, 0.00135f, 0.00135f));

            Transform brandingRoot = AppShellEditorCommon.FindOrCreateChild(hubCanvas.transform, "BrandingRoot").transform;
            AppShellEditorCommon.ConfigureRect(
                brandingRoot as RectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(900f, 120f),
                new Vector2(0f, -90f));

            Transform panelsRoot = AppShellEditorCommon.FindOrCreateChild(hubCanvas.transform, "PanelsRoot").transform;
            AppShellEditorCommon.ConfigureStretchRect(panelsRoot as RectTransform);

            BuildBranding(brandingRoot);

            AppPanelView homePanel = AppShellEditorUi.CreatePanelRoot(panelsRoot, "HomePanel", AppPanelType.Home, new Vector2(980f, 860f), new Vector2(0f, -20f));
            AppPanelView practicePanel = AppShellEditorUi.CreatePanelRoot(panelsRoot, "PracticeModePanel", AppPanelType.PracticeMode, new Vector2(980f, 860f), new Vector2(0f, -20f));
            AppPanelView environmentPanel = AppShellEditorUi.CreatePanelRoot(panelsRoot, "EnvironmentSelectionPanel", AppPanelType.EnvironmentSelection, new Vector2(980f, 860f), new Vector2(0f, -20f));
            AppPanelView setupPanel = AppShellEditorUi.CreatePanelRoot(panelsRoot, "SessionSetupPanel", AppPanelType.SessionSetup, new Vector2(980f, 860f), new Vector2(0f, -20f));
            AppPanelView readyPanel = AppShellEditorUi.CreatePanelRoot(panelsRoot, "ReadyPanel", AppPanelType.Ready, new Vector2(980f, 860f), new Vector2(0f, -20f));
            AppPanelView progressPanel = AppShellEditorUi.CreatePanelRoot(panelsRoot, "ProgressPanel", AppPanelType.Progress, new Vector2(980f, 860f), new Vector2(0f, -20f));
            AppPanelView settingsPanel = AppShellEditorUi.CreatePanelRoot(panelsRoot, "SettingsPanel", AppPanelType.Settings, new Vector2(980f, 860f), new Vector2(0f, -20f));
            AppPanelView resultsPanel = AppShellEditorUi.CreatePanelRoot(panelsRoot, "ResultsSummaryPanel", AppPanelType.ResultsSummary, new Vector2(980f, 860f), new Vector2(0f, -20f));

            GameObject mainHubRoot = AppShellEditorCommon.FindOrCreateSceneRoot(scene, "MainHubRoot");
            AppFlowManager appFlowManager = AppShellEditorCommon.GetOrAddComponent<AppFlowManager>(mainHubRoot);
            ShellSceneRigController shellSceneRigController = AppShellEditorCommon.GetOrAddComponent<ShellSceneRigController>(mainHubRoot);
            UIStateController uiStateController = AppShellEditorCommon.GetOrAddComponent<UIStateController>(mainHubRoot);
            SessionLaunchController sessionLaunchController = AppShellEditorCommon.GetOrAddComponent<SessionLaunchController>(mainHubRoot);

            HomePanelPresenter homePresenter = AppShellEditorCommon.GetOrAddComponent<HomePanelPresenter>(homePanel.gameObject);
            PracticeModePanelPresenter practicePresenter = AppShellEditorCommon.GetOrAddComponent<PracticeModePanelPresenter>(practicePanel.gameObject);
            EnvironmentSelectionController environmentController = AppShellEditorCommon.GetOrAddComponent<EnvironmentSelectionController>(environmentPanel.gameObject);
            SessionConfigController sessionConfigController = AppShellEditorCommon.GetOrAddComponent<SessionConfigController>(setupPanel.gameObject);
            ReadyPanelPresenter readyPresenter = AppShellEditorCommon.GetOrAddComponent<ReadyPanelPresenter>(readyPanel.gameObject);
            ProgressPanelPresenter progressPresenter = AppShellEditorCommon.GetOrAddComponent<ProgressPanelPresenter>(progressPanel.gameObject);
            SettingsPanelPresenter settingsPresenter = AppShellEditorCommon.GetOrAddComponent<SettingsPanelPresenter>(settingsPanel.gameObject);
            DashboardAdapter progressDashboardAdapter = AppShellEditorCommon.GetOrAddComponent<DashboardAdapter>(progressPanel.gameObject);
            ResultsSummaryPresenter resultsPresenter = AppShellEditorCommon.GetOrAddComponent<ResultsSummaryPresenter>(resultsPanel.gameObject);
            ResultsFlowController resultsFlowController = AppShellEditorCommon.GetOrAddComponent<ResultsFlowController>(resultsPanel.gameObject);
            DashboardAdapter resultsDashboardAdapter = AppShellEditorCommon.GetOrAddComponent<DashboardAdapter>(resultsPanel.gameObject);

            BuildHomePanel(homePanel.transform, homePresenter, catalog);
            BuildPracticeModePanel(practicePanel.transform, practicePresenter, appFlowManager);
            BuildEnvironmentSelectionPanel(environmentPanel.transform, environmentController, appFlowManager, catalog);
            BuildSessionSetupPanel(setupPanel.transform, sessionConfigController, appFlowManager);
            BuildReadyPanel(readyPanel.transform, readyPresenter);
            BuildProgressPanel(progressPanel.transform, progressPresenter);
            BuildSettingsPanel(settingsPanel.transform, settingsPresenter);
            BuildResultsPanel(resultsPanel.transform, resultsPresenter, resultsFlowController, resultsDashboardAdapter);

            WireMainHubReferences(
                catalog,
                transitionManager,
                appFlowManager,
                shellSceneRigController,
                uiStateController,
                sessionLaunchController,
                homePresenter,
                practicePresenter,
                environmentController,
                sessionConfigController,
                readyPresenter,
                progressPresenter,
                settingsPresenter,
                resultsPresenter,
                resultsFlowController,
                progressDashboardAdapter,
                resultsDashboardAdapter,
                homePanel,
                practicePanel,
                environmentPanel,
                setupPanel,
                readyPanel,
                progressPanel,
                settingsPanel,
                resultsPanel);

            ConfigureShellSceneRigController(shellSceneRigController, hubCanvas, "MainHubBackdrop", true);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, AppShellEditorCommon.MainHubScenePath);
        }

        private static void CreateOrUpdateResultsSceneInternal()
        {
            Scene scene = AppShellEditorCommon.OpenOrCreateScene(AppShellEditorCommon.ResultsScenePath);

            EnsureDirectionalLight(scene);
            EnsurePlayerRig(scene, "ResultsPlayerRig");
            EnsureEventSystem(scene);

            TransitionManager transitionManager = EnsureTransitionOverlay(scene);
            GameObject resultsSceneRoot = AppShellEditorCommon.FindOrCreateSceneRoot(scene, "ResultsSceneRoot");
            ShellSceneRigController shellSceneRigController = AppShellEditorCommon.GetOrAddComponent<ShellSceneRigController>(resultsSceneRoot);
            Canvas resultsCanvas = EnsureWorldSpaceCanvas(
                scene,
                "WorldSpaceResultsCanvas",
                new Vector2(1600f, 1000f),
                new Vector3(0f, 1.55f, 2.20f),
                new Vector3(0.00135f, 0.00135f, 0.00135f));

            Transform root = AppShellEditorCommon.FindOrCreateChild(resultsCanvas.transform, "ResultsRoot").transform;
            AppShellEditorCommon.ConfigureStretchRect(root as RectTransform);

            BuildResultsScene(root, transitionManager);
            ConfigureShellSceneRigController(shellSceneRigController, resultsCanvas, string.Empty, false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, AppShellEditorCommon.ResultsScenePath);
        }

        private static void InstallBindingsInAllEnvironmentScenesInternal()
        {
            List<string> scenePaths = AppShellEditorCommon.FindEnvironmentScenePaths();
            for (int index = 0; index < scenePaths.Count; index++)
            {
                string scenePath = scenePaths[index];
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                InstallBindingsInEnvironmentScene(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, scenePath);
            }
        }

        private static void InstallBindingsInEnvironmentScene(Scene scene)
        {
            GameObject bindingsRoot = AppShellEditorCommon.FindOrCreateSceneRoot(scene, "AppShellSceneBindings");
            EnvironmentSceneInstaller installer = AppShellEditorCommon.GetOrAddComponent<EnvironmentSceneInstaller>(bindingsRoot);
            ExistingSceneFlowAdapter flowAdapter = AppShellEditorCommon.GetOrAddComponent<ExistingSceneFlowAdapter>(bindingsRoot);
            TrackingAdapter trackingAdapter = AppShellEditorCommon.GetOrAddComponent<TrackingAdapter>(bindingsRoot);
            ScoringAdapter scoringAdapter = AppShellEditorCommon.GetOrAddComponent<ScoringAdapter>(bindingsRoot);
            PlayerRigAdapter playerRigAdapter = AppShellEditorCommon.GetOrAddComponent<PlayerRigAdapter>(bindingsRoot);

            AppShellEditorCommon.SetField(installer, "trackingAdapter", trackingAdapter);
            AppShellEditorCommon.SetField(installer, "scoringAdapter", scoringAdapter);
            AppShellEditorCommon.SetField(installer, "playerRigAdapter", playerRigAdapter);
            AppShellEditorCommon.SetField(installer, "existingSceneFlowAdapter", flowAdapter);
            AppShellEditorCommon.SetField(flowAdapter, "scoringAdapter", scoringAdapter);

            BuildInSessionHud(bindingsRoot.transform, scene);

            AppShellEditorCommon.MarkDirty(bindingsRoot, installer, flowAdapter, trackingAdapter, scoringAdapter, playerRigAdapter);
        }

        private static void EnsureDirectionalLight(Scene scene)
        {
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < lights.Length; index++)
            {
                if (lights[index] != null &&
                    lights[index].type == LightType.Directional &&
                    lights[index].gameObject.scene == scene)
                {
                    return;
                }
            }

            GameObject lightRoot = AppShellEditorCommon.FindOrCreateSceneRoot(scene, "Directional Light");
            Light light = AppShellEditorCommon.GetOrAddComponent<Light>(lightRoot);
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.97f, 0.92f, 1f);
            lightRoot.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
        }

        private static void EnsureBackdrop(Scene scene, string rootName)
        {
            GameObject backdropRoot = AppShellEditorCommon.FindOrCreateSceneRoot(scene, rootName);

            GameObject floor = AppShellEditorCommon.FindOrCreateChild(backdropRoot.transform, "Floor");
            MeshFilter floorFilter = AppShellEditorCommon.GetOrAddComponent<MeshFilter>(floor);
            MeshRenderer floorRenderer = AppShellEditorCommon.GetOrAddComponent<MeshRenderer>(floor);
            if (floorFilter.sharedMesh == null)
            {
                GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floorFilter.sharedMesh = primitive.GetComponent<MeshFilter>().sharedMesh;
                floorRenderer.sharedMaterials = primitive.GetComponent<MeshRenderer>().sharedMaterials;
                UnityEngine.Object.DestroyImmediate(primitive);
            }

            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(2.2f, 1f, 2.2f);

            GameObject backWall = AppShellEditorCommon.FindOrCreateChild(backdropRoot.transform, "BackWall");
            MeshFilter wallFilter = AppShellEditorCommon.GetOrAddComponent<MeshFilter>(backWall);
            MeshRenderer wallRenderer = AppShellEditorCommon.GetOrAddComponent<MeshRenderer>(backWall);
            if (wallFilter.sharedMesh == null)
            {
                GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wallFilter.sharedMesh = primitive.GetComponent<MeshFilter>().sharedMesh;
                wallRenderer.sharedMaterials = primitive.GetComponent<MeshRenderer>().sharedMaterials;
                UnityEngine.Object.DestroyImmediate(primitive);
            }

            backWall.transform.position = new Vector3(0f, 1.5f, 4.2f);
            backWall.transform.localScale = new Vector3(7.5f, 3.2f, 0.25f);

            EnsureBackdropCollisionSafety(backdropRoot.transform);
        }

        private static void EnsureBackdropCollisionSafety(Transform backdropRoot)
        {
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
                    MeshFilter floorFilter = child.GetComponent<MeshFilter>();
                    if (floorFilter == null || floorFilter.sharedMesh == null)
                    {
                        continue;
                    }

                    MeshCollider floorCollider = AppShellEditorCommon.GetOrAddComponent<MeshCollider>(child.gameObject);
                    floorCollider.sharedMesh = floorFilter.sharedMesh;
                    floorCollider.enabled = true;
                    continue;
                }

                if (child.name.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                Collider existingCollider = child.GetComponent<Collider>();
                if (existingCollider != null)
                {
                    existingCollider.enabled = true;
                    continue;
                }

                BoxCollider wallCollider = AppShellEditorCommon.GetOrAddComponent<BoxCollider>(child.gameObject);
                wallCollider.center = Vector3.zero;
                wallCollider.size = Vector3.one;
                wallCollider.enabled = true;
            }
        }

        private static GameObject EnsurePlayerRig(Scene scene, string rigName)
        {
            GameObject existingRig = FindExistingRig(scene);
            if (existingRig != null)
            {
                if (ShouldReplaceExistingRig(existingRig))
                {
                    UnityEngine.Object.DestroyImmediate(existingRig);
                }
                else
                {
                    existingRig.name = rigName;
                    existingRig.transform.position = Vector3.zero;
                    existingRig.transform.rotation = Quaternion.identity;
                    EnsurePlayerRigSupport(existingRig);
                    return existingRig;
                }
            }

            for (int index = 0; index < RigPrefabCandidates.Length; index++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabCandidates[index]);
                if (prefab == null)
                {
                    continue;
                }

                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance == null)
                {
                    continue;
                }

                instance.name = rigName;
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;
                EnsurePlayerRigSupport(instance);
                return instance;
            }

            GameObject fallbackRig = AppShellEditorCommon.FindOrCreateSceneRoot(scene, rigName);
            GameObject cameraRoot = AppShellEditorCommon.FindOrCreateChild(fallbackRig.transform, "Main Camera");
            AppShellEditorCommon.GetOrAddComponent<Camera>(cameraRoot);
            AppShellEditorCommon.GetOrAddComponent<AudioListener>(cameraRoot);
            fallbackRig.transform.position = Vector3.zero;
            fallbackRig.transform.rotation = Quaternion.identity;
            EnsurePlayerRigSupport(fallbackRig);
            return fallbackRig;
        }

        private static void EnsurePlayerRigSupport(GameObject rigRoot)
        {
            if (rigRoot == null)
            {
                return;
            }

            PlayerController playerController = AppShellEditorCommon.GetOrAddComponent<PlayerController>(rigRoot);
            playerController.enableDesktopTesting = true;
            playerController.forceDesktopTestingInEditor = false;
            playerController.lookEnabled = true;
            playerController.movementEnabled = true;

            AppShellEditorCommon.MarkDirty(rigRoot, playerController);
        }

        private static GameObject FindExistingRig(Scene scene)
        {
            PlayerController[] playerControllers = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < playerControllers.Length; index++)
            {
                PlayerController controller = playerControllers[index];
                if (controller != null && controller.gameObject.scene == scene)
                {
                    return controller.transform.root.gameObject;
                }
            }

            Camera existingCamera = AppShellEditorCommon.FindSceneCamera(scene);
            if (existingCamera != null)
            {
                return existingCamera.transform.root.gameObject;
            }

            return null;
        }

        private static bool ShouldReplaceExistingRig(GameObject existingRig)
        {
            if (existingRig == null)
            {
                return false;
            }

            string prefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(existingRig);
            return string.Equals(prefabAssetPath, LegacySharedRigPrefabPath, StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureEventSystem(Scene scene)
        {
            EventSystem[] systems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < systems.Length; index++)
            {
                if (systems[index] != null && systems[index].gameObject.scene == scene)
                {
                    EnsureInputModule(systems[index].gameObject);
                    return;
                }
            }

            GameObject eventSystemRoot = AppShellEditorCommon.FindOrCreateSceneRoot(scene, "EventSystem");
            AppShellEditorCommon.GetOrAddComponent<EventSystem>(eventSystemRoot);
            EnsureInputModule(eventSystemRoot);
        }

        private static void EnsureInputModule(GameObject eventSystemRoot)
        {
            Component inputSystemModule = AppShellEditorCommon.TryGetOrAddComponentByName(
                eventSystemRoot,
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

            if (inputSystemModule == null)
            {
                AppShellEditorCommon.GetOrAddComponent<StandaloneInputModule>(eventSystemRoot);
            }
        }

        private static Canvas EnsureWorldSpaceCanvas(Scene scene, string name, Vector2 size, Vector3 position, Vector3 scale)
        {
            GameObject canvasRoot = AppShellEditorCommon.FindOrCreateSceneRoot(scene, name);
            RectTransform rectTransform = AppShellEditorCommon.GetOrAddComponent<RectTransform>(canvasRoot);
            rectTransform.sizeDelta = size;
            canvasRoot.transform.position = position;
            canvasRoot.transform.rotation = Quaternion.identity;
            canvasRoot.transform.localScale = scale;

            Canvas canvas = AppShellEditorCommon.GetOrAddComponent<Canvas>(canvasRoot);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = AppShellEditorCommon.FindSceneCamera(scene);

            CanvasScaler scaler = AppShellEditorCommon.GetOrAddComponent<CanvasScaler>(canvasRoot);
            scaler.dynamicPixelsPerUnit = 16f;
            scaler.referencePixelsPerUnit = 100f;

            AppShellEditorCommon.GetOrAddComponent<GraphicRaycaster>(canvasRoot);
            AppShellEditorCommon.TryGetOrAddComponentByName(
                canvasRoot,
                "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");

            return canvas;
        }

        private static TransitionManager EnsureTransitionOverlay(Scene scene)
        {
            Canvas overlayCanvas = EnsureWorldSpaceCanvas(
                scene,
                "TransitionOverlayRoot",
                new Vector2(2600f, 1600f),
                new Vector3(0f, 1.55f, 1.05f),
                new Vector3(0.0010f, 0.0010f, 0.0010f));

            GameObject overlayRoot = overlayCanvas.gameObject;
            Image overlayImage = AppShellEditorCommon.GetOrAddComponent<Image>(overlayRoot);
            overlayImage.color = AppShellEditorCommon.OverlayColor;

            CanvasGroup canvasGroup = AppShellEditorCommon.GetOrAddComponent<CanvasGroup>(overlayRoot);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            WorldSpaceCanvasFollower follower = AppShellEditorCommon.GetOrAddComponent<WorldSpaceCanvasFollower>(overlayRoot);
            AppShellEditorCommon.SetField(follower, "offset", new Vector3(0f, 0f, 1.0f));

            TMP_Text loadingLabel = AppShellEditorUi.FindOrCreateHudLabel(overlayRoot.transform, "LoadingLabel", "Loading...", 56f, Vector2.zero);
            loadingLabel.alignment = TextAlignmentOptions.Center;
            loadingLabel.color = AppShellEditorCommon.TextColor;

            TransitionManager transitionManager = AppShellEditorCommon.GetOrAddComponent<TransitionManager>(overlayRoot);
            AppShellEditorCommon.SetField(transitionManager, "overlayCanvasGroup", canvasGroup);
            AppShellEditorCommon.SetField(transitionManager, "loadingLabel", loadingLabel);
            AppShellEditorCommon.SetField(transitionManager, "persistAcrossSceneLoads", true);
            return transitionManager;
        }

        private static void BuildBranding(Transform brandingRoot)
        {
            AppShellEditorUi.ClearGeneratedChildren(brandingRoot);

            Image background = AppShellEditorCommon.GetOrAddComponent<Image>(brandingRoot.gameObject);
            AppShellEditorCommon.StyleSlicedImage(background, AppShellEditorCommon.HeaderSurfaceColor);

            VerticalLayoutGroup layout = AppShellEditorCommon.GetOrAddComponent<VerticalLayoutGroup>(brandingRoot.gameObject);
            layout.spacing = 2f;
            layout.padding = new RectOffset(24, 24, 14, 14);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = AppShellEditorCommon.GetOrAddComponent<ContentSizeFitter>(brandingRoot.gameObject);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AppShellEditorUi.CreateTextBlock(brandingRoot, "BrandTitle", "Orator VR", 50f, FontStyles.Bold, TextAlignmentOptions.Center, 58f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(
                brandingRoot,
                "BrandSubtitle",
                "Soft dashboard shell for immersive public speaking practice.",
                18f,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                30f,
                AppShellEditorCommon.MutedTextColor);
        }

        private static void BuildHomePanel(Transform panelTransform, HomePanelPresenter presenter, AppEnvironmentCatalog catalog)
        {
            AppShellEditorUi.ClearGeneratedChildren(panelTransform);

            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelTitle", "Main Hub", 42f, FontStyles.Bold, TextAlignmentOptions.Left, 56f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelSubtitle", "Choose a soft, guided entry point into the full VR practice flow.", 20f, FontStyles.Normal, TextAlignmentOptions.Left, 48f, AppShellEditorCommon.MutedTextColor);

            GameObject dashboardRow = AppShellEditorUi.CreateDashboardRow(panelTransform, "DashboardRow", 22f);

            GameObject featuredColumn = AppShellEditorUi.CreateSectionCard(dashboardRow.transform, "FeaturedColumn", AppShellEditorCommon.HeroSurfaceColor, 24, 24, 16f);
            AppShellEditorCommon.ConfigureLayoutElement(featuredColumn, 560f, 612f);
            AppShellEditorUi.CreateTextBlock(featuredColumn.transform, "SectionTitle", "Featured Paths", 18f, FontStyles.Bold, TextAlignmentOptions.Left, 24f, AppShellEditorCommon.SoftAccentColor);

            GameObject startCard = AppShellEditorUi.CreateFeatureCard(
                featuredColumn.transform,
                "StartPracticeCard",
                "Start Practice",
                "Move into mode, environment, setup, and launch with the current product-ready flow.",
                "Primary Path",
                GetEnvironmentPreviewSprite(catalog, 0),
                -1f,
                380f);
            Button startButton = AppShellEditorUi.CreateButton(startCard.transform, "StartPracticeButton", "Open Practice Flow", AppShellEditorCommon.AccentColor, -1f, 58f);

            GameObject featuredQuickRow = AppShellEditorUi.CreateDashboardRow(featuredColumn.transform, "FeaturedQuickRow", 12f);
            Button environmentsButton = AppShellEditorUi.CreateUtilityTile(featuredQuickRow.transform, "EnvironmentsButton", "Rooms", "Browse speaking spaces", AppShellEditorCommon.TileSurfaceColor, 240f, 126f);
            Button resultsButton = AppShellEditorUi.CreateUtilityTile(featuredQuickRow.transform, "ResultsButton", "Results", "Summaries and progress", AppShellEditorCommon.TileSurfaceColor, 240f, 126f);

            GameObject utilityColumn = AppShellEditorUi.CreateSectionCard(dashboardRow.transform, "UtilityColumn", AppShellEditorCommon.ElevatedSurfaceColor, 20, 20, 12f);
            AppShellEditorCommon.ConfigureLayoutElement(utilityColumn, 318f, 612f);
            AppShellEditorUi.CreateTextBlock(utilityColumn.transform, "UtilityTitle", "Quick Access", 18f, FontStyles.Bold, TextAlignmentOptions.Left, 24f, AppShellEditorCommon.SoftAccentColor);

            Button settingsButton = AppShellEditorUi.CreateButton(utilityColumn.transform, "SettingsButton", "Settings", AppShellEditorCommon.TileSurfaceColor, -1f, 54f);
            AppShellEditorUi.CreateTextBlock(utilityColumn.transform, "SettingsInfo", "Comfort, controls, and calibration", 16f, FontStyles.Normal, TextAlignmentOptions.Left, 34f, AppShellEditorCommon.MutedTextColor);

            Button exitButton = AppShellEditorUi.CreateButton(utilityColumn.transform, "ExitButton", "Exit", AppShellEditorCommon.DangerColor, -1f, 54f);
            AppShellEditorUi.CreateTextBlock(utilityColumn.transform, "ExitInfo", "Close the current app shell session", 16f, FontStyles.Normal, TextAlignmentOptions.Left, 34f, AppShellEditorCommon.MutedTextColor);

            GameObject flowStrip = AppShellEditorUi.CreateSummaryStrip(
                utilityColumn.transform,
                "FlowStrip",
                "Unified Flow",
                "Mode -> Environment -> Setup -> Launch");
            AppShellEditorCommon.ConfigureLayoutElement(flowStrip, -1f, 88f);

            AppShellEditorCommon.SetButtonEvent(startButton, presenter.OpenStartPractice);
            AppShellEditorCommon.SetButtonEvent(environmentsButton, presenter.OpenEnvironments);
            AppShellEditorCommon.SetButtonEvent(resultsButton, presenter.OpenResults);
            AppShellEditorCommon.SetButtonEvent(settingsButton, presenter.OpenSettings);
            AppShellEditorCommon.SetButtonEvent(exitButton, presenter.ExitApplication);
        }

        private static void BuildPracticeModePanel(Transform panelTransform, PracticeModePanelPresenter presenter, AppFlowManager appFlowManager)
        {
            AppShellEditorUi.ClearGeneratedChildren(panelTransform);

            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelTitle", "Practice Mode", 42f, FontStyles.Bold, TextAlignmentOptions.Left, 56f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelSubtitle", "Select how the session should behave. Modes remain visible even when their runtime support is not ready.", 20f, FontStyles.Normal, TextAlignmentOptions.Left, 48f, AppShellEditorCommon.MutedTextColor);

            GameObject modeSection = AppShellEditorUi.CreateSectionCard(panelTransform, "ModeGridSection", AppShellEditorCommon.ElevatedSurfaceColor, 24, 22, 16f);
            AppShellEditorUi.CreateTextBlock(modeSection.transform, "ModeSectionTitle", "Available Modes", 18f, FontStyles.Bold, TextAlignmentOptions.Left, 24f, AppShellEditorCommon.SoftAccentColor);
            AppShellEditorCommon.ConfigureLayoutElement(modeSection, -1f, 520f);

            GameObject rowA = AppShellEditorUi.CreateDashboardRow(modeSection.transform, "ModeRowA", 16f);
            GameObject rowB = AppShellEditorUi.CreateDashboardRow(modeSection.transform, "ModeRowB", 16f);

            Button guidedButton = CreateModeSelectionCard(
                rowA.transform,
                "GuidedPracticeCard",
                "Guided Practice",
                "Structured practice with step-by-step flow guidance.",
                "GuidedPracticeButton",
                "Select Guided Practice",
                AppShellEditorCommon.AccentColor);
            Button freeButton = CreateModeSelectionCard(
                rowA.transform,
                "FreePracticeCard",
                "Free Practice",
                "Open rehearsal with less structure and faster iteration.",
                "FreePracticeButton",
                "Select Free Practice",
                AppShellEditorCommon.TileSurfaceColor);
            Button evaluationButton = CreateModeSelectionCard(
                rowB.transform,
                "EvaluationModeCard",
                "Evaluation Mode",
                "Score-focused runs for more formal performance checks.",
                "EvaluationModeButton",
                "Select Evaluation Mode",
                AppShellEditorCommon.TileSurfaceColor);
            Button challengeButton = CreateModeSelectionCard(
                rowB.transform,
                "ChallengeModeCard",
                "Challenge Mode",
                "Pressure-based scenarios for advanced practice loops.",
                "ChallengeModeButton",
                "Select Challenge Mode",
                AppShellEditorCommon.TileSurfaceColor);
            TMP_Text availabilityLabel = AppShellEditorUi.CreateTextBlock(modeSection.transform, "AvailabilityLabel", string.Empty, 18f, FontStyles.Italic, TextAlignmentOptions.Left, 52f, AppShellEditorCommon.MutedTextColor);

            Transform footer = AppShellEditorUi.CreateHorizontalContainer(panelTransform, "FooterActions", 16f).transform;
            Button backButton = AppShellEditorUi.CreateButton(footer, "BackButton", "Back", AppShellEditorCommon.SecondaryColor, 240f, 58f);

            AppShellEditorCommon.SetField(presenter, "guidedPracticeButton", guidedButton);
            AppShellEditorCommon.SetField(presenter, "freePracticeButton", freeButton);
            AppShellEditorCommon.SetField(presenter, "evaluationModeButton", evaluationButton);
            AppShellEditorCommon.SetField(presenter, "challengeModeButton", challengeButton);
            AppShellEditorCommon.SetField(presenter, "availabilityLabel", availabilityLabel);

            AppShellEditorCommon.SetButtonEvent(guidedButton, presenter.SelectGuidedPractice);
            AppShellEditorCommon.SetButtonEvent(freeButton, presenter.SelectFreePractice);
            AppShellEditorCommon.SetButtonEvent(evaluationButton, presenter.SelectEvaluationMode);
            AppShellEditorCommon.SetButtonEvent(challengeButton, presenter.SelectChallengeMode);
            AppShellEditorCommon.SetButtonEvent(backButton, appFlowManager.GoBack);
        }

        private static void BuildEnvironmentSelectionPanel(Transform panelTransform, EnvironmentSelectionController controller, AppFlowManager appFlowManager, AppEnvironmentCatalog catalog)
        {
            AppShellEditorUi.ClearGeneratedChildren(panelTransform);

            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelTitle", "Choose Environment", 42f, FontStyles.Bold, TextAlignmentOptions.Left, 56f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelSubtitle", "Compare rooms through larger previews and continue only when a launch-ready space is selected.", 20f, FontStyles.Normal, TextAlignmentOptions.Left, 48f, AppShellEditorCommon.MutedTextColor);

            GameObject dashboardRow = AppShellEditorUi.CreateDashboardRow(panelTransform, "EnvironmentDashboardRow", 20f);
            GameObject catalogSection = AppShellEditorUi.CreateSectionCard(dashboardRow.transform, "CatalogSection", AppShellEditorCommon.ElevatedSurfaceColor, 20, 20, 14f);
            GameObject summarySection = AppShellEditorUi.CreateSectionCard(dashboardRow.transform, "SelectionSection", AppShellEditorCommon.HeroSurfaceColor, 22, 22, 14f);
            AppShellEditorCommon.ConfigureLayoutElement(catalogSection, 620f, 648f);
            AppShellEditorCommon.ConfigureLayoutElement(summarySection, 266f, 648f);

            AppShellEditorUi.CreateTextBlock(catalogSection.transform, "CatalogSectionTitle", "Available Rooms", 18f, FontStyles.Bold, TextAlignmentOptions.Left, 24f, AppShellEditorCommon.SoftAccentColor);

            GameObject cardGrid = AppShellEditorCommon.FindOrCreateChild(catalogSection.transform, "EnvironmentCardGrid");
            GridLayoutGroup grid = AppShellEditorCommon.GetOrAddComponent<GridLayoutGroup>(cardGrid);
            grid.cellSize = new Vector2(278f, 280f);
            grid.spacing = new Vector2(14f, 14f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            LayoutElement gridLayout = AppShellEditorCommon.GetOrAddComponent<LayoutElement>(cardGrid);
            gridLayout.preferredHeight = 574f;

            List<EnvironmentCardView> cardViews = new List<EnvironmentCardView>();
            int environmentCount = catalog != null && catalog.Environments != null && catalog.Environments.Count > 0
                ? catalog.Environments.Count
                : 3;

            for (int index = 0; index < environmentCount; index++)
            {
                EnvironmentCardView card = AppShellEditorUi.CreateEnvironmentCard(cardGrid.transform, $"EnvironmentCard_{index + 1}");
                cardViews.Add(card);
            }

            AppShellEditorUi.CreateTextBlock(summarySection.transform, "SelectionSectionTitle", "Selection", 18f, FontStyles.Bold, TextAlignmentOptions.Left, 24f, AppShellEditorCommon.SoftAccentColor);
            AppShellEditorUi.CreateSummaryStrip(summarySection.transform, "SummaryStripA", "Flow", "Select a room, confirm it, then continue to setup.");
            AppShellEditorUi.CreateSummaryStrip(summarySection.transform, "SummaryStripB", "Comfort", "Only launch-ready rooms can move forward in the shell.");
            TMP_Text helperLabel = AppShellEditorUi.CreateTextBlock(summarySection.transform, "HelperLabel", "Select the room that best matches the speaking context you want to rehearse.", 18f, FontStyles.Normal, TextAlignmentOptions.Left, 220f, AppShellEditorCommon.MutedTextColor);
            Button confirmButton = AppShellEditorUi.CreateButton(summarySection.transform, "ConfirmButton", "Continue To Setup", AppShellEditorCommon.AccentColor, -1f, 66f);
            Button backButton = AppShellEditorUi.CreateButton(summarySection.transform, "BackButton", "Back", AppShellEditorCommon.SecondaryColor, -1f, 58f);

            AppShellEditorCommon.SetButtonEvent(backButton, appFlowManager.GoBack);
            AppShellEditorCommon.SetButtonEvent(confirmButton, appFlowManager.ContinueFromEnvironmentSelection);

            AppShellEditorCommon.SetField(controller, "helperLabel", helperLabel);
            AppShellEditorCommon.SetField(controller, "confirmSelectionButton", confirmButton);
            AppShellEditorCommon.SetField(controller, "cardViews", cardViews);
        }

        private static void BuildSessionSetupPanel(Transform panelTransform, SessionConfigController controller, AppFlowManager appFlowManager)
        {
            AppShellEditorUi.ClearGeneratedChildren(panelTransform);

            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelTitle", "Session Setup", 42f, FontStyles.Bold, TextAlignmentOptions.Left, 56f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelSubtitle", "Tune duration, audience pressure, and analysis layers without touching the existing runtime wiring.", 20f, FontStyles.Normal, TextAlignmentOptions.Left, 48f, AppShellEditorCommon.MutedTextColor);

            GameObject setupRow = AppShellEditorUi.CreateDashboardRow(panelTransform, "SetupDashboardRow", 20f);
            GameObject leftColumn = AppShellEditorUi.CreateSectionCard(setupRow.transform, "LeftSetupColumn", AppShellEditorCommon.ElevatedSurfaceColor, 22, 22, 14f);
            GameObject rightColumn = AppShellEditorUi.CreateSectionCard(setupRow.transform, "RightSetupColumn", AppShellEditorCommon.HeroSurfaceColor, 22, 22, 14f);
            AppShellEditorCommon.ConfigureLayoutElement(leftColumn, 432f, 620f);
            AppShellEditorCommon.ConfigureLayoutElement(rightColumn, 434f, 620f);

            GameObject timingSection = AppShellEditorUi.CreateSectionCard(leftColumn.transform, "TimingSection", AppShellEditorCommon.TileSurfaceColor, 18, 18, 10f);
            AppShellEditorUi.CreateTextBlock(timingSection.transform, "TimingTitle", "Duration", 18f, FontStyles.Bold, TextAlignmentOptions.Left, 24f, AppShellEditorCommon.SoftAccentColor);
            TMP_Text durationLabel = AppShellEditorUi.CreateTextBlock(timingSection.transform, "DurationValueLabel", "5 min", 22f, FontStyles.Bold, TextAlignmentOptions.Left, 30f, AppShellEditorCommon.AccentColor);
            Slider durationSlider = AppShellEditorUi.CreateSlider(timingSection.transform, "DurationSlider", 1f, 15f, true, 5f);

            GameObject analysisSection = AppShellEditorUi.CreateSectionCard(leftColumn.transform, "AnalysisSection", AppShellEditorCommon.TileSurfaceColor, 18, 18, 8f);
            AppShellEditorUi.CreateTextBlock(analysisSection.transform, "AnalysisTitle", "Analysis Systems", 18f, FontStyles.Bold, TextAlignmentOptions.Left, 24f, AppShellEditorCommon.SoftAccentColor);
            GameObject analysisRowA = AppShellEditorUi.CreateDashboardRow(analysisSection.transform, "AnalysisRowA", 12f);
            GameObject analysisRowB = AppShellEditorUi.CreateDashboardRow(analysisSection.transform, "AnalysisRowB", 12f);
            GameObject analysisRowC = AppShellEditorUi.CreateDashboardRow(analysisSection.transform, "AnalysisRowC", 12f);
            Toggle eyeTrackingToggle = AppShellEditorUi.CreateToggle(analysisRowA.transform, "EyeTrackingToggle", "Eye Tracking", true);
            Toggle gazeScoringToggle = AppShellEditorUi.CreateToggle(analysisRowA.transform, "GazeScoringToggle", "Gaze Scoring", true);
            Toggle performanceScoringToggle = AppShellEditorUi.CreateToggle(analysisRowB.transform, "PerformanceScoringToggle", "Performance Scoring", true);
            Toggle voiceAnalysisToggle = AppShellEditorUi.CreateToggle(analysisRowB.transform, "VoiceAnalysisToggle", "Voice Analysis", false);
            Toggle postureAnalysisToggle = AppShellEditorUi.CreateToggle(analysisRowC.transform, "PostureAnalysisToggle", "Posture Analysis", false);
            AppShellEditorCommon.ConfigureLayoutElement(eyeTrackingToggle.gameObject, 188f, 42f);
            AppShellEditorCommon.ConfigureLayoutElement(gazeScoringToggle.gameObject, 188f, 42f);
            AppShellEditorCommon.ConfigureLayoutElement(performanceScoringToggle.gameObject, 188f, 42f);
            AppShellEditorCommon.ConfigureLayoutElement(voiceAnalysisToggle.gameObject, 188f, 42f);
            AppShellEditorCommon.ConfigureLayoutElement(postureAnalysisToggle.gameObject, 188f, 42f);

            GameObject contextSection = AppShellEditorUi.CreateSectionCard(rightColumn.transform, "ContextSection", AppShellEditorCommon.TileSurfaceColor, 18, 18, 10f);
            AppShellEditorUi.CreateTextBlock(contextSection.transform, "ContextTitle", "Session Context", 18f, FontStyles.Bold, TextAlignmentOptions.Left, 24f, AppShellEditorCommon.SoftAccentColor);
            AppShellEditorUi.CreateTextBlock(contextSection.transform, "DifficultyLabel", "Difficulty", 16f, FontStyles.Bold, TextAlignmentOptions.Left, 22f, AppShellEditorCommon.TextColor);
            TMP_Dropdown difficultyDropdown = AppShellEditorUi.CreateDropdown(contextSection.transform, "DifficultyDropdown", AppShellEditorCommon.EnumNames<SessionDifficulty>());
            AppShellEditorUi.CreateTextBlock(contextSection.transform, "AudienceLabel", "Audience", 16f, FontStyles.Bold, TextAlignmentOptions.Left, 22f, AppShellEditorCommon.TextColor);
            TMP_Dropdown audienceDropdown = AppShellEditorUi.CreateDropdown(contextSection.transform, "AudienceDropdown", AppShellEditorCommon.EnumNames<AudiencePreset>());
            AppShellEditorUi.CreateTextBlock(contextSection.transform, "FeedbackLabel", "Feedback Detail", 16f, FontStyles.Bold, TextAlignmentOptions.Left, 22f, AppShellEditorCommon.TextColor);
            TMP_Dropdown feedbackDropdown = AppShellEditorUi.CreateDropdown(contextSection.transform, "FeedbackDropdown", AppShellEditorCommon.EnumNames<FeedbackLevel>());

            AppShellEditorUi.CreateTextBlock(rightColumn.transform, "SummaryTitle", "Live Summary", 18f, FontStyles.Bold, TextAlignmentOptions.Left, 24f, AppShellEditorCommon.SoftAccentColor);
            TMP_Text summaryPreview = AppShellEditorUi.CreateTextBlock(rightColumn.transform, "SummaryPreviewLabel", string.Empty, 19f, FontStyles.Normal, TextAlignmentOptions.Left, 160f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateSummaryStrip(rightColumn.transform, "SummaryHint", "Safe Integration", "Only configuration changes here. Existing session logic stays intact.");

            Transform actions = AppShellEditorUi.CreateVerticalContainer(rightColumn.transform, "SummaryActions", 12f).transform;
            Button continueButton = AppShellEditorUi.CreateButton(actions, "ContinueButton", "Review Setup", AppShellEditorCommon.AccentColor, -1f, 66f);
            Button backButton = AppShellEditorUi.CreateButton(actions, "BackButton", "Back", AppShellEditorCommon.SecondaryColor, -1f, 58f);

            AppShellEditorCommon.SetButtonEvent(backButton, appFlowManager.GoBack);
            AppShellEditorCommon.SetButtonEvent(continueButton, appFlowManager.ContinueFromSessionSetup);

            AppShellEditorCommon.SetField(controller, "durationSlider", durationSlider);
            AppShellEditorCommon.SetField(controller, "durationValueLabel", durationLabel);
            AppShellEditorCommon.SetField(controller, "difficultyDropdown", difficultyDropdown);
            AppShellEditorCommon.SetField(controller, "audienceDropdown", audienceDropdown);
            AppShellEditorCommon.SetField(controller, "feedbackDropdown", feedbackDropdown);
            AppShellEditorCommon.SetField(controller, "eyeTrackingToggle", eyeTrackingToggle);
            AppShellEditorCommon.SetField(controller, "gazeScoringToggle", gazeScoringToggle);
            AppShellEditorCommon.SetField(controller, "performanceScoringToggle", performanceScoringToggle);
            AppShellEditorCommon.SetField(controller, "voiceAnalysisToggle", voiceAnalysisToggle);
            AppShellEditorCommon.SetField(controller, "postureAnalysisToggle", postureAnalysisToggle);
            AppShellEditorCommon.SetField(controller, "summaryPreviewLabel", summaryPreview);
        }

        private static void BuildReadyPanel(Transform panelTransform, ReadyPanelPresenter presenter)
        {
            AppShellEditorUi.ClearGeneratedChildren(panelTransform);

            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelTitle", "Ready To Launch", 42f, FontStyles.Bold, TextAlignmentOptions.Left, 56f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelSubtitle", "Review the final session configuration before entering the selected environment.", 20f, FontStyles.Normal, TextAlignmentOptions.Left, 48f, AppShellEditorCommon.MutedTextColor);

            GameObject readyRow = AppShellEditorUi.CreateDashboardRow(panelTransform, "ReadyDashboardRow", 20f);
            GameObject launchCard = AppShellEditorUi.CreateFeatureCard(
                readyRow.transform,
                "LaunchCard",
                "Session Launch",
                "This review screen is the final confirmation step before scene load and adapter handoff.",
                "Final Check",
                null,
                300f,
                520f);
            AppShellEditorUi.CreateSummaryStrip(launchCard.transform, "LaunchStrip", "Flow", "Review setup -> Start session -> Enter environment");

            GameObject reviewCard = AppShellEditorUi.CreateSectionCard(readyRow.transform, "ReviewCard", AppShellEditorCommon.ElevatedSurfaceColor, 24, 24, 14f);
            AppShellEditorCommon.ConfigureLayoutElement(reviewCard, 586f, 520f);
            AppShellEditorUi.CreateTextBlock(reviewCard.transform, "ReviewTitle", "Session Summary", 18f, FontStyles.Bold, TextAlignmentOptions.Left, 24f, AppShellEditorCommon.SoftAccentColor);
            AppShellEditorUi.CreateTextBlock(reviewCard.transform, "SummaryLabel", string.Empty, 22f, FontStyles.Normal, TextAlignmentOptions.Left, 300f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(reviewCard.transform, "WarningLabel", string.Empty, 18f, FontStyles.Normal, TextAlignmentOptions.Left, 96f, new Color(1f, 0.78f, 0.44f, 1f));

            Transform footer = AppShellEditorUi.CreateHorizontalContainer(panelTransform, "FooterActions", 16f).transform;
            Button backButton = AppShellEditorUi.CreateButton(footer, "BackButton", "Back", AppShellEditorCommon.SecondaryColor, 240f, 58f);
            Button startButton = AppShellEditorUi.CreateButton(footer, "StartSessionButton", "Start Session", AppShellEditorCommon.AccentColor, 320f, 66f);

            AppShellEditorCommon.SetButtonEvent(backButton, presenter.GoBack);
            AppShellEditorCommon.SetButtonEvent(startButton, presenter.StartSession);
        }

        private static void BuildProgressPanel(Transform panelTransform, ProgressPanelPresenter presenter)
        {
            AppShellEditorUi.ClearGeneratedChildren(panelTransform);

            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelTitle", "Review Center", 42f, FontStyles.Bold, TextAlignmentOptions.Left, 56f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelSubtitle", "Check the latest session summary or open connected analytics.", 20f, FontStyles.Normal, TextAlignmentOptions.Left, 40f, AppShellEditorCommon.MutedTextColor);

            Transform dashboardRow = AppShellEditorUi.CreateDashboardRow(panelTransform, "ProgressDashboardRow", 18f).transform;

            Transform overviewCard = AppShellEditorUi.CreateSectionCard(
                dashboardRow,
                "ProgressOverviewCard",
                AppShellEditorCommon.ElevatedSurfaceColor,
                24,
                22,
                8f).transform;

            AppShellEditorCommon.ConfigureLayoutElement(overviewCard.gameObject, -1f, 530f);
            AppShellEditorCommon.GetOrAddComponent<LayoutElement>(overviewCard.gameObject).flexibleWidth = 1f;
            AppShellEditorUi.CreateTextBlock(overviewCard, "OverviewTitle", "Session Status", 24f, FontStyles.Bold, TextAlignmentOptions.Left, 32f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(overviewCard, "BodyCopy", "Summary and dashboard adapters stay behind this shell, so the front-end remains stable.", 16f, FontStyles.Normal, TextAlignmentOptions.Left, 48f, AppShellEditorCommon.MutedTextColor);
            TMP_Text summaryStatusLabel = AppShellEditorUi.CreateTextBlock(overviewCard, "SummaryStatusLabel", string.Empty, 18f, FontStyles.Bold, TextAlignmentOptions.Left, 74f, AppShellEditorCommon.TextColor);
            TMP_Text dashboardStatusLabel = AppShellEditorUi.CreateTextBlock(overviewCard, "DashboardStatusLabel", string.Empty, 16f, FontStyles.Normal, TextAlignmentOptions.Left, 54f, AppShellEditorCommon.MutedTextColor);
            TMP_Text noteLabel = AppShellEditorUi.CreateTextBlock(overviewCard, "ActionNoteLabel", string.Empty, 16f, FontStyles.Italic, TextAlignmentOptions.Left, 70f, AppShellEditorCommon.MutedTextColor);

            Transform actionCard = AppShellEditorUi.CreateSectionCard(
                dashboardRow,
                "ProgressActionCard",
                AppShellEditorCommon.TileSurfaceColor,
                20,
                20,
                8f).transform;

            AppShellEditorCommon.ConfigureLayoutElement(actionCard.gameObject, 316f, 530f);
            AppShellEditorUi.CreateTextBlock(actionCard, "ActionTitle", "Quick Access", 22f, FontStyles.Bold, TextAlignmentOptions.Left, 32f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(actionCard, "ActionLead", "Use the safest review routes first.", 16f, FontStyles.Normal, TextAlignmentOptions.Left, 34f, AppShellEditorCommon.MutedTextColor);
            Button openResultsButton = AppShellEditorUi.CreateButton(actionCard, "OpenSummaryButton", "Latest Summary", AppShellEditorCommon.SoftAccentColor, -1f, 46f);
            AppShellEditorUi.CreateTextBlock(actionCard, "OpenSummaryInfo", "Open the newest session recap.", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 26f, AppShellEditorCommon.MutedTextColor);
            Button openDashboardButton = AppShellEditorUi.CreateButton(actionCard, "OpenDashboardButton", "Dashboard Entry", AppShellEditorCommon.TileSurfaceColor, -1f, 46f);
            AppShellEditorUi.CreateTextBlock(actionCard, "OpenDashboardInfo", "Jump to charts and long-term review.", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 28f, AppShellEditorCommon.MutedTextColor);
            Button backButton = AppShellEditorUi.CreateButton(actionCard, "BackButton", "Back", AppShellEditorCommon.SecondaryColor, -1f, 52f);

            GameObject summaryStrip = AppShellEditorUi.CreateSummaryStrip(panelTransform, "ProgressSummaryStrip", "Flow Status", "Adapter routes keep scoring and dashboard logic outside the shell UI.");
            AppShellEditorCommon.ConfigureLayoutElement(summaryStrip, -1f, 84f);

            AppShellEditorCommon.SetField(presenter, "summaryStatusLabel", summaryStatusLabel);
            AppShellEditorCommon.SetField(presenter, "dashboardStatusLabel", dashboardStatusLabel);
            AppShellEditorCommon.SetField(presenter, "noteLabel", noteLabel);

            AppShellEditorCommon.SetButtonEvent(openResultsButton, presenter.OpenLatestSummary);
            AppShellEditorCommon.SetButtonEvent(openDashboardButton, presenter.OpenDashboardEntry);
            AppShellEditorCommon.SetButtonEvent(backButton, presenter.GoBack);
        }

        private static void BuildSettingsPanel(Transform panelTransform, SettingsPanelPresenter presenter)
        {
            AppShellEditorUi.ClearGeneratedChildren(panelTransform);

            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelTitle", "Settings", 42f, FontStyles.Bold, TextAlignmentOptions.Left, 56f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelSubtitle", "Comfort, audio, and calibration entry points live here.", 20f, FontStyles.Normal, TextAlignmentOptions.Left, 44f, AppShellEditorCommon.MutedTextColor);

            Transform dashboardRow = AppShellEditorUi.CreateDashboardRow(panelTransform, "SettingsDashboardRow", 18f).transform;

            Transform comfortCard = AppShellEditorUi.CreateSectionCard(
                dashboardRow,
                "ComfortCard",
                AppShellEditorCommon.ElevatedSurfaceColor,
                24,
                24,
                12f).transform;

            AppShellEditorCommon.ConfigureLayoutElement(comfortCard.gameObject, -1f, 548f);
            AppShellEditorCommon.GetOrAddComponent<LayoutElement>(comfortCard.gameObject).flexibleWidth = 1f;
            AppShellEditorUi.CreateTextBlock(comfortCard, "ComfortTitle", "Comfort And Access", 24f, FontStyles.Bold, TextAlignmentOptions.Left, 34f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(comfortCard, "ComfortBody", "Keep the highest-impact comfort controls in one place so the user can adjust quickly without leaving the shell.", 17f, FontStyles.Normal, TextAlignmentOptions.Left, 56f, AppShellEditorCommon.MutedTextColor);

            Toggle comfortToggle = AppShellEditorUi.CreateToggle(comfortCard, "ComfortVignetteToggle", "Comfort Vignette", true);
            Toggle audioToggle = AppShellEditorUi.CreateToggle(comfortCard, "AudioPromptToggle", "Audio Prompt Guidance", true);
            Toggle rayAssistToggle = AppShellEditorUi.CreateToggle(comfortCard, "RayAssistToggle", "Enhanced Ray Assist", true);
            TMP_Text previewStateLabel = AppShellEditorUi.CreateTextBlock(comfortCard, "PreviewStateLabel", string.Empty, 17f, FontStyles.Bold, TextAlignmentOptions.Left, 84f, AppShellEditorCommon.TextColor);

            Transform actionCard = AppShellEditorUi.CreateSectionCard(
                dashboardRow,
                "SettingsActionCard",
                AppShellEditorCommon.TileSurfaceColor,
                20,
                20,
                12f).transform;

            AppShellEditorCommon.ConfigureLayoutElement(actionCard.gameObject, 332f, 548f);
            AppShellEditorUi.CreateTextBlock(actionCard, "SettingsActionTitle", "Settings Shortcuts", 22f, FontStyles.Bold, TextAlignmentOptions.Left, 34f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(actionCard, "SettingsActionLead", "Quick links for shell-safe controls.", 16f, FontStyles.Normal, TextAlignmentOptions.Left, 40f, AppShellEditorCommon.MutedTextColor);
            Button comfortEntryButton = AppShellEditorUi.CreateButton(actionCard, "ComfortEntryButton", "Comfort Options", AppShellEditorCommon.SoftAccentColor, -1f, 46f);
            AppShellEditorUi.CreateTextBlock(actionCard, "ComfortEntryInfo", "Open comfort helpers and safety options.", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 30f, AppShellEditorCommon.MutedTextColor);
            Button audioEntryButton = AppShellEditorUi.CreateButton(actionCard, "AudioEntryButton", "Audio Options", AppShellEditorCommon.TileSurfaceColor, -1f, 46f);
            AppShellEditorUi.CreateTextBlock(actionCard, "AudioEntryInfo", "Adjust prompt and guidance previews.", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 30f, AppShellEditorCommon.MutedTextColor);
            Button calibrationButton = AppShellEditorUi.CreateButton(actionCard, "CalibrationButton", "Room Calibration", AppShellEditorCommon.TileSurfaceColor, -1f, 46f);
            AppShellEditorUi.CreateTextBlock(actionCard, "CalibrationInfo", "Launch room setup if a calibration path exists.", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 30f, AppShellEditorCommon.MutedTextColor);
            Button backButton = AppShellEditorUi.CreateButton(actionCard, "BackButton", "Back", AppShellEditorCommon.SecondaryColor, -1f, 52f);
            TMP_Text noteLabel = AppShellEditorUi.CreateTextBlock(actionCard, "SettingsNote", string.Empty, 16f, FontStyles.Normal, TextAlignmentOptions.Left, 76f, AppShellEditorCommon.MutedTextColor);

            AppShellEditorCommon.SetField(presenter, "comfortVignetteToggle", comfortToggle);
            AppShellEditorCommon.SetField(presenter, "audioPromptToggle", audioToggle);
            AppShellEditorCommon.SetField(presenter, "rayAssistToggle", rayAssistToggle);
            AppShellEditorCommon.SetField(presenter, "previewStateLabel", previewStateLabel);
            AppShellEditorCommon.SetField(presenter, "noteLabel", noteLabel);

            SetToggleEvent(comfortToggle, presenter.OnComfortVignetteChanged);
            SetToggleEvent(audioToggle, presenter.OnAudioPromptChanged);
            SetToggleEvent(rayAssistToggle, presenter.OnRayAssistChanged);

            AppShellEditorCommon.SetButtonEvent(comfortEntryButton, presenter.OpenComfortSettings);
            AppShellEditorCommon.SetButtonEvent(audioEntryButton, presenter.OpenAudioSettings);
            AppShellEditorCommon.SetButtonEvent(calibrationButton, presenter.OpenCalibration);
            AppShellEditorCommon.SetButtonEvent(backButton, presenter.GoBack);
        }

        private static void BuildResultsPanel(Transform panelTransform, ResultsSummaryPresenter presenter, ResultsFlowController flowController, DashboardAdapter dashboardAdapter)
        {
            AppShellEditorUi.ClearGeneratedChildren(panelTransform);

            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelTitle", "Results Summary", 42f, FontStyles.Bold, TextAlignmentOptions.Left, 56f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelSubtitle", "See the latest session snapshot, then choose the next route.", 20f, FontStyles.Normal, TextAlignmentOptions.Left, 44f, AppShellEditorCommon.MutedTextColor);

            Transform dashboardRow = AppShellEditorUi.CreateDashboardRow(panelTransform, "ResultsDashboardRow", 18f).transform;

            Transform summaryCard = AppShellEditorUi.CreateSectionCard(
                dashboardRow,
                "ResultsOverviewCard",
                AppShellEditorCommon.ElevatedSurfaceColor,
                24,
                22,
                8f).transform;

            AppShellEditorCommon.ConfigureLayoutElement(summaryCard.gameObject, -1f, 548f);
            AppShellEditorCommon.GetOrAddComponent<LayoutElement>(summaryCard.gameObject).flexibleWidth = 1f;
            AppShellEditorUi.CreateTextBlock(summaryCard, "ResultsTitle", "Session Snapshot", 24f, FontStyles.Bold, TextAlignmentOptions.Left, 32f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(summaryCard, "ResultsLead", "Top-line feedback stays readable first; deeper review lives in a separate rail.", 16f, FontStyles.Normal, TextAlignmentOptions.Left, 44f, AppShellEditorCommon.MutedTextColor);
            AppShellEditorUi.CreateTextBlock(summaryCard, "SummarySectionTitle", "Latest Metrics", 16f, FontStyles.Bold, TextAlignmentOptions.Left, 22f, AppShellEditorCommon.SoftAccentColor);
            AppShellEditorUi.CreateTextBlock(summaryCard, "SummaryValue", "No session summary is available yet.", 19f, FontStyles.Normal, TextAlignmentOptions.Left, 180f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(summaryCard, "RecommendationsSectionTitle", "Coach Notes", 16f, FontStyles.Bold, TextAlignmentOptions.Left, 22f, AppShellEditorCommon.SoftAccentColor);
            AppShellEditorUi.CreateTextBlock(summaryCard, "RecommendationsValue", "Recommendations will appear after a completed session.", 16f, FontStyles.Normal, TextAlignmentOptions.Left, 112f, AppShellEditorCommon.MutedTextColor);

            Transform actionCard = AppShellEditorUi.CreateSectionCard(
                dashboardRow,
                "ResultsActionCard",
                AppShellEditorCommon.TileSurfaceColor,
                20,
                20,
                8f).transform;

            AppShellEditorCommon.ConfigureLayoutElement(actionCard.gameObject, 320f, 548f);
            AppShellEditorUi.CreateTextBlock(actionCard, "ResultsActionTitle", "Next Step", 22f, FontStyles.Bold, TextAlignmentOptions.Left, 32f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(actionCard, "ResultsActionLead", "Keep the session context intact while routing the user forward.", 16f, FontStyles.Normal, TextAlignmentOptions.Left, 34f, AppShellEditorCommon.MutedTextColor);
            Button retryButton = AppShellEditorUi.CreateButton(actionCard, "RetryButton", "Retry Setup", AppShellEditorCommon.SoftAccentColor, -1f, 46f);
            AppShellEditorUi.CreateTextBlock(actionCard, "RetryInfo", "Launch the same setup again.", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 26f, AppShellEditorCommon.MutedTextColor);
            Button changeEnvironmentButton = AppShellEditorUi.CreateButton(actionCard, "ChangeEnvironmentButton", "Change Environment", AppShellEditorCommon.TileSurfaceColor, -1f, 46f);
            AppShellEditorUi.CreateTextBlock(actionCard, "ChangeEnvironmentInfo", "Return to room selection with the current config.", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 26f, AppShellEditorCommon.MutedTextColor);
            Button dashboardButton = AppShellEditorUi.CreateButton(actionCard, "DashboardButton", "Dashboard Entry", AppShellEditorCommon.TileSurfaceColor, -1f, 46f);
            AppShellEditorUi.CreateTextBlock(actionCard, "DashboardInfo", "Open deeper scoring when that adapter is ready.", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 26f, AppShellEditorCommon.MutedTextColor);
            Button hubButton = AppShellEditorUi.CreateButton(actionCard, "ReturnToHubButton", "Return To Hub", AppShellEditorCommon.SecondaryColor, -1f, 52f);
            TMP_Text statusLabel = AppShellEditorUi.CreateTextBlock(actionCard, "RouteStatusLabel", string.Empty, 16f, FontStyles.Italic, TextAlignmentOptions.Left, 72f, AppShellEditorCommon.MutedTextColor);

            AppShellEditorCommon.SetField(flowController, "dashboardAdapter", dashboardAdapter);
            AppShellEditorCommon.SetField(flowController, "statusLabel", statusLabel);

            AppShellEditorCommon.SetButtonEvent(retryButton, flowController.RetryLastSession);
            AppShellEditorCommon.SetButtonEvent(changeEnvironmentButton, flowController.ChangeEnvironment);
            AppShellEditorCommon.SetButtonEvent(dashboardButton, flowController.OpenDashboard);
            AppShellEditorCommon.SetButtonEvent(hubButton, flowController.ReturnToHub);
        }

        private static void BuildResultsScene(Transform root, TransitionManager transitionManager)
        {
            AppShellEditorUi.ClearGeneratedChildren(root);

            VerticalLayoutGroup layout = AppShellEditorCommon.GetOrAddComponent<VerticalLayoutGroup>(root.gameObject);
            layout.spacing = 12f;
            layout.padding = new RectOffset(280, 280, 70, 70);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AppShellEditorUi.CreateTextBlock(root, "ResultsSceneTitle", "Session Results", 44f, FontStyles.Bold, TextAlignmentOptions.Center, 80f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(root, "ResultsSceneSubtitle", "Review the latest session, retry instantly, or route back into the app shell.", 24f, FontStyles.Normal, TextAlignmentOptions.Center, 70f, AppShellEditorCommon.MutedTextColor);
            AppShellEditorUi.CreateSpacer(root, "SpacerA", 16f);

            AppPanelView resultsPanel = AppShellEditorUi.CreatePanelRoot(root, "ResultsPanel", AppPanelType.ResultsSummary, new Vector2(960f, 760f), new Vector2(0f, -20f), false);
            LayoutElement panelLayout = AppShellEditorCommon.GetOrAddComponent<LayoutElement>(resultsPanel.gameObject);
            panelLayout.preferredWidth = 960f;
            panelLayout.preferredHeight = 760f;
            ResultsSummaryPresenter resultsPresenter = AppShellEditorCommon.GetOrAddComponent<ResultsSummaryPresenter>(resultsPanel.gameObject);
            ResultsFlowController resultsFlowController = AppShellEditorCommon.GetOrAddComponent<ResultsFlowController>(resultsPanel.gameObject);
            DashboardAdapter dashboardAdapter = AppShellEditorCommon.GetOrAddComponent<DashboardAdapter>(resultsPanel.gameObject);

            BuildResultsPanel(resultsPanel.transform, resultsPresenter, resultsFlowController, dashboardAdapter);

            AppShellEditorCommon.SetField(resultsFlowController, "transitionManager", transitionManager);
            AppShellEditorCommon.SetField(resultsFlowController, "dashboardAdapter", dashboardAdapter);
            AppShellEditorCommon.SetField(resultsPresenter, "summaryLabel", AppShellEditorCommon.FindDescendantComponent<TMP_Text>(resultsPanel.transform, "SummaryValue"));
            AppShellEditorCommon.SetField(resultsPresenter, "recommendationsLabel", AppShellEditorCommon.FindDescendantComponent<TMP_Text>(resultsPanel.transform, "RecommendationsValue"));
        }

        private static Button CreateModeSelectionCard(
            Transform parent,
            string cardName,
            string title,
            string description,
            string buttonName,
            string buttonLabel,
            Color buttonColor)
        {
            GameObject card = AppShellEditorUi.CreateSectionCard(parent, cardName, AppShellEditorCommon.TileSurfaceColor, 18, 18, 8f);
            AppShellEditorCommon.ConfigureLayoutElement(card, 426f, 178f);

            AppShellEditorUi.CreateTextBlock(card.transform, "ModeTitle", title, 23f, FontStyles.Bold, TextAlignmentOptions.Left, 30f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(card.transform, "ModeDescription", description, 15f, FontStyles.Normal, TextAlignmentOptions.Left, 52f, AppShellEditorCommon.MutedTextColor);

            return AppShellEditorUi.CreateButton(card.transform, buttonName, buttonLabel, buttonColor, -1f, 48f);
        }

        private static void ConfigureShellSceneRigController(
            ShellSceneRigController controller,
            Canvas shellCanvas,
            string backdropRootName,
            bool headLockedCanvas)
        {
            if (controller == null)
            {
                return;
            }

            AppShellEditorCommon.SetField(controller, "shellCanvas", shellCanvas);
            AppShellEditorCommon.SetField(controller, "backdropRootName", backdropRootName ?? string.Empty);
            AppShellEditorCommon.SetField(controller, "keepCanvasFixedToView", true);
            AppShellEditorCommon.SetField(controller, "headLockedCanvas", headLockedCanvas);
            AppShellEditorCommon.SetField(controller, "shellCanvasOffset", new Vector3(0f, -0.12f, 2.1f));
            AppShellEditorCommon.SetField(controller, "menuFollowPositionSpeed", 14f);
            AppShellEditorCommon.SetField(controller, "menuFollowRotationSpeed", 14f);
            AppShellEditorCommon.SetField(controller, "keepRigStationary", true);
            AppShellEditorCommon.SetField(controller, "requestedTrackingOriginMode", XROrigin.TrackingOriginMode.Device);
            AppShellEditorCommon.SetField(controller, "cameraYOffset", 1.62f);
            AppShellEditorCommon.SetField(controller, "stabilizationDuration", 0f);
        }

        private static void BuildInSessionHud(Transform parent, Scene scene)
        {
            GameObject hudRoot = AppShellEditorCommon.FindOrCreateChild(parent, "InSessionHUD");
            Canvas hudCanvas = AppShellEditorCommon.GetOrAddComponent<Canvas>(hudRoot);
            hudCanvas.renderMode = RenderMode.WorldSpace;
            hudCanvas.worldCamera = AppShellEditorCommon.FindSceneCamera(scene);

            CanvasScaler hudScaler = AppShellEditorCommon.GetOrAddComponent<CanvasScaler>(hudRoot);
            hudScaler.dynamicPixelsPerUnit = 12f;
            hudScaler.referencePixelsPerUnit = 100f;

            CanvasGroup hudCanvasGroup = AppShellEditorCommon.GetOrAddComponent<CanvasGroup>(hudRoot);
            WorldSpaceCanvasFollower hudFollower = AppShellEditorCommon.GetOrAddComponent<WorldSpaceCanvasFollower>(hudRoot);
            InSessionHudPresenter hudPresenter = AppShellEditorCommon.GetOrAddComponent<InSessionHudPresenter>(hudRoot);

            RectTransform hudRect = hudRoot.GetComponent<RectTransform>();
            hudRect.sizeDelta = new Vector2(460f, 140f);
            hudRoot.transform.localScale = new Vector3(0.00125f, 0.00125f, 0.00125f);

            Image hudBackground = AppShellEditorCommon.GetOrAddComponent<Image>(hudRoot);
            hudBackground.color = new Color(0.04f, 0.06f, 0.10f, 0.82f);

            TMP_Text timerLabel = AppShellEditorUi.FindOrCreateHudLabel(hudRoot.transform, "TimerLabel", "05:00", 34f, new Vector2(0f, 18f));
            TMP_Text statusLabel = AppShellEditorUi.FindOrCreateHudLabel(hudRoot.transform, "StatusLabel", "Waiting for session start", 18f, new Vector2(0f, -24f));
            statusLabel.color = AppShellEditorCommon.MutedTextColor;

            AppShellEditorCommon.SetField(hudPresenter, "canvasGroup", hudCanvasGroup);
            AppShellEditorCommon.SetField(hudPresenter, "timerLabel", timerLabel);
            AppShellEditorCommon.SetField(hudPresenter, "statusLabel", statusLabel);
            AppShellEditorCommon.SetField(hudPresenter, "inactiveStatusText", "Waiting for session start");
            AppShellEditorCommon.SetField(hudFollower, "offset", new Vector3(0f, -0.88f, 1.4f));
            AppShellEditorCommon.SetField(hudFollower, "yawOnly", false);
            AppShellEditorCommon.SetField(hudFollower, "positionLerpSpeed", 0f);
            AppShellEditorCommon.SetField(hudFollower, "rotationLerpSpeed", 0f);
            AppShellEditorCommon.SetField(hudFollower, "followContinuously", false);
        }

        private static void WireMainHubReferences(
            AppEnvironmentCatalog catalog,
            TransitionManager transitionManager,
            AppFlowManager appFlowManager,
            ShellSceneRigController shellSceneRigController,
            UIStateController uiStateController,
            SessionLaunchController sessionLaunchController,
            HomePanelPresenter homePresenter,
            PracticeModePanelPresenter practicePresenter,
            EnvironmentSelectionController environmentController,
            SessionConfigController sessionConfigController,
            ReadyPanelPresenter readyPresenter,
            ProgressPanelPresenter progressPresenter,
            SettingsPanelPresenter settingsPresenter,
            ResultsSummaryPresenter resultsPresenter,
            ResultsFlowController resultsFlowController,
            DashboardAdapter progressDashboardAdapter,
            DashboardAdapter resultsDashboardAdapter,
            AppPanelView homePanel,
            AppPanelView practicePanel,
            AppPanelView environmentPanel,
            AppPanelView setupPanel,
            AppPanelView readyPanel,
            AppPanelView progressPanel,
            AppPanelView settingsPanel,
            AppPanelView resultsPanel)
        {
            AppShellEditorCommon.SetField(homePresenter, "appFlowManager", appFlowManager);
            AppShellEditorCommon.SetField(practicePresenter, "appFlowManager", appFlowManager);
            AppShellEditorCommon.SetField(environmentController, "environmentCatalog", catalog);
            AppShellEditorCommon.SetField(readyPresenter, "appFlowManager", appFlowManager);
            AppShellEditorCommon.SetField(readyPresenter, "summaryLabel", AppShellEditorCommon.FindDescendantComponent<TMP_Text>(readyPanel.transform, "SummaryLabel"));
            AppShellEditorCommon.SetField(readyPresenter, "warningLabel", AppShellEditorCommon.FindDescendantComponent<TMP_Text>(readyPanel.transform, "WarningLabel"));
            AppShellEditorCommon.SetField(progressPresenter, "appFlowManager", appFlowManager);
            AppShellEditorCommon.SetField(progressPresenter, "dashboardAdapter", progressDashboardAdapter);
            AppShellEditorCommon.SetField(settingsPresenter, "appFlowManager", appFlowManager);
            AppShellEditorCommon.SetField(resultsPresenter, "summaryLabel", AppShellEditorCommon.FindDescendantComponent<TMP_Text>(resultsPanel.transform, "SummaryValue"));
            AppShellEditorCommon.SetField(resultsPresenter, "recommendationsLabel", AppShellEditorCommon.FindDescendantComponent<TMP_Text>(resultsPanel.transform, "RecommendationsValue"));
            AppShellEditorCommon.SetField(resultsFlowController, "transitionManager", transitionManager);
            AppShellEditorCommon.SetField(resultsFlowController, "dashboardAdapter", resultsDashboardAdapter);

            List<AppPanelView> panels = new List<AppPanelView>
            {
                homePanel,
                practicePanel,
                environmentPanel,
                setupPanel,
                readyPanel,
                progressPanel,
                settingsPanel,
                resultsPanel
            };

            AppShellEditorCommon.SetField(uiStateController, "panels", panels);
            AppShellEditorCommon.SetField(uiStateController, "defaultPanel", AppPanelType.Home);

            AppShellEditorCommon.SetField(appFlowManager, "uiStateController", uiStateController);
            AppShellEditorCommon.SetField(appFlowManager, "environmentSelectionController", environmentController);
            AppShellEditorCommon.SetField(appFlowManager, "sessionConfigController", sessionConfigController);
            AppShellEditorCommon.SetField(appFlowManager, "readyPanelPresenter", readyPresenter);
            AppShellEditorCommon.SetField(appFlowManager, "progressPanelPresenter", progressPresenter);
            AppShellEditorCommon.SetField(appFlowManager, "settingsPanelPresenter", settingsPresenter);
            AppShellEditorCommon.SetField(appFlowManager, "resultsSummaryPresenter", resultsPresenter);
            AppShellEditorCommon.SetField(appFlowManager, "sessionLaunchController", sessionLaunchController);
            AppShellEditorCommon.SetField(appFlowManager, "shellSceneRigController", shellSceneRigController);
            AppShellEditorCommon.SetField(appFlowManager, "mainHubSceneName", "MainHubScene");
            AppShellEditorCommon.SetField(appFlowManager, "resultsSceneName", "ResultsScene");

            AppShellEditorCommon.SetField(sessionLaunchController, "environmentSelectionController", environmentController);
            AppShellEditorCommon.SetField(sessionLaunchController, "sessionConfigController", sessionConfigController);
            AppShellEditorCommon.SetField(sessionLaunchController, "transitionManager", transitionManager);
            AppShellEditorCommon.SetField(sessionLaunchController, "validationLabel", AppShellEditorCommon.FindDescendantComponent<TMP_Text>(readyPanel.transform, "WarningLabel"));

            AppShellEditorCommon.MarkDirty(
                appFlowManager,
                shellSceneRigController,
                uiStateController,
                sessionLaunchController,
                homePresenter,
                practicePresenter,
                environmentController,
                sessionConfigController,
                readyPresenter,
                progressPresenter,
                settingsPresenter,
                resultsPresenter,
                resultsFlowController,
                progressDashboardAdapter,
                resultsDashboardAdapter);
        }

        private static void SetToggleEvent(Toggle toggle, UnityEngine.Events.UnityAction<bool> action)
        {
            if (toggle == null)
            {
                return;
            }

            while (toggle.onValueChanged.GetPersistentEventCount() > 0)
            {
                UnityEventTools.RemovePersistentListener(toggle.onValueChanged, 0);
            }

            toggle.onValueChanged.RemoveAllListeners();

            if (action != null)
            {
                UnityEventTools.AddPersistentListener(toggle.onValueChanged, action);
            }

            EditorUtility.SetDirty(toggle);
        }

        private static Sprite GetEnvironmentPreviewSprite(AppEnvironmentCatalog catalog, int index)
        {
            if (catalog == null || catalog.Environments == null || index < 0 || index >= catalog.Environments.Count)
            {
                return null;
            }

            AppEnvironmentDefinition environment = catalog.Environments[index];
            return environment != null ? environment.PreviewSprite : null;
        }
    }
}
