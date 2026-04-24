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
        private const string HubSkyboxMaterialPath = "Assets/VRTemplateAssets/Materials/Skybox/Hub Skybox Blue 2.mat";
        private const string HubFloorMaterialPath = "Assets/VRTemplateAssets/Materials/Environment/Concrete Grey.mat";
        private const string HubWallMaterialPath = "Assets/VRTemplateAssets/Materials/Environment/Wall Default.mat";
        private const string HubAccentMaterialPath = "Assets/VRTemplateAssets/Materials/Environment/Concrete Blue.mat";
        private const string HubGridMaterialPath = "Assets/VRTemplateAssets/Materials/Environment/Grid Dark Tight.mat";
        private const string HubLargeGridMaterialPath = "Assets/VRTemplateAssets/Materials/Environment/Grid Dark Large.mat";
        private const string HubChromeMaterialPath = "Assets/VRTemplateAssets/Materials/Environment/Chrome.mat";
        private const string HubCurtainMaterialPath = "Assets/VRTemplateAssets/Materials/Primitive/Cube_Fabric.mat";
        private const string HubCaseMaterialPath = "Assets/VRTemplateAssets/Materials/Primitive/Interactables 2.mat";
        private const string HubGeneratedMaterialFolder = "Assets/AppShell/Generated/Materials";
        private const string HubBackstageFloorMaterialPath = HubGeneratedMaterialFolder + "/Hub_Backstage_Floor.mat";
        private const string HubBackstageBlackMaterialPath = HubGeneratedMaterialFolder + "/Hub_Backstage_Black.mat";
        private const string HubBackstageCurtainMaterialPath = HubGeneratedMaterialFolder + "/Hub_Backstage_Curtain.mat";
        private const string HubBackstageCurtainShadowMaterialPath = HubGeneratedMaterialFolder + "/Hub_Backstage_CurtainShadow.mat";
        private const string HubBackstageCaseMaterialPath = HubGeneratedMaterialFolder + "/Hub_Backstage_RoadCase.mat";
        private const string HubBackstageTapeMaterialPath = HubGeneratedMaterialFolder + "/Hub_Backstage_Tape.mat";
        private const string HubBackstageRunnerMaterialPath = HubGeneratedMaterialFolder + "/Hub_Backstage_Runner.mat";
        private const string HubBackstageGlowMaterialPath = HubGeneratedMaterialFolder + "/Hub_Backstage_Glow.mat";
        private const string HubBackstageWallMaterialPath = HubGeneratedMaterialFolder + "/Hub_Backstage_RoomWall.mat";
        private const string HubBackstageCeilingMaterialPath = HubGeneratedMaterialFolder + "/Hub_Backstage_Ceiling.mat";
        private const string HubBackstageTrimMaterialPath = HubGeneratedMaterialFolder + "/Hub_Backstage_Trim.mat";

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
                new Vector2(920f, 148f),
                new Vector2(0f, -82f));

            Transform panelsRoot = AppShellEditorCommon.FindOrCreateChild(hubCanvas.transform, "PanelsRoot").transform;
            AppShellEditorCommon.ConfigureStretchRect(panelsRoot as RectTransform);

            BuildBranding(brandingRoot);

            Vector2 mainPanelSize = new Vector2(980f, 940f);
            Vector2 mainPanelPosition = new Vector2(0f, -64f);
            AppPanelView homePanel = AppShellEditorUi.CreatePanelRoot(panelsRoot, "HomePanel", AppPanelType.Home, mainPanelSize, mainPanelPosition);
            AppPanelView practicePanel = AppShellEditorUi.CreatePanelRoot(panelsRoot, "PracticeModePanel", AppPanelType.PracticeMode, mainPanelSize, mainPanelPosition);
            AppPanelView environmentPanel = AppShellEditorUi.CreatePanelRoot(panelsRoot, "EnvironmentSelectionPanel", AppPanelType.EnvironmentSelection, mainPanelSize, mainPanelPosition);
            AppPanelView setupPanel = AppShellEditorUi.CreatePanelRoot(panelsRoot, "SessionSetupPanel", AppPanelType.SessionSetup, mainPanelSize, mainPanelPosition);
            AppPanelView readyPanel = AppShellEditorUi.CreatePanelRoot(panelsRoot, "ReadyPanel", AppPanelType.Ready, mainPanelSize, mainPanelPosition);
            AppPanelView progressPanel = AppShellEditorUi.CreatePanelRoot(panelsRoot, "ProgressPanel", AppPanelType.Progress, mainPanelSize, mainPanelPosition);
            AppPanelView settingsPanel = AppShellEditorUi.CreatePanelRoot(panelsRoot, "SettingsPanel", AppPanelType.Settings, mainPanelSize, mainPanelPosition);
            AppPanelView resultsPanel = AppShellEditorUi.CreatePanelRoot(panelsRoot, "ResultsSummaryPanel", AppPanelType.ResultsSummary, mainPanelSize, mainPanelPosition);

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
            EnsureEventSystem(scene);

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

            EnvironmentSessionOverlayController overlayController = BuildInSessionHud(bindingsRoot.transform, scene);
            AppShellEditorCommon.SetField(installer, "environmentSessionOverlayController", overlayController);
            AppShellEditorCommon.SetField(flowAdapter, "environmentSessionOverlayController", overlayController);

            AppShellEditorCommon.MarkDirty(bindingsRoot, installer, flowAdapter, trackingAdapter, scoringAdapter, playerRigAdapter, overlayController);
        }

        private static void EnsureDirectionalLight(Scene scene)
        {
            Light light = null;
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < lights.Length; index++)
            {
                if (lights[index] != null &&
                    lights[index].type == LightType.Directional &&
                    lights[index].gameObject.scene == scene)
                {
                    light = lights[index];
                    break;
                }
            }

            GameObject lightRoot = light != null
                ? light.gameObject
                : AppShellEditorCommon.FindOrCreateSceneRoot(scene, "Directional Light");
            light = AppShellEditorCommon.GetOrAddComponent<Light>(lightRoot);
            light.type = LightType.Directional;
            light.intensity = 1.22f;
            light.color = new Color(1f, 0.965f, 0.91f, 1f);
            light.shadows = LightShadows.Soft;
            lightRoot.transform.rotation = Quaternion.Euler(36f, -32f, 0f);

            Material hubSkybox = AssetDatabase.LoadAssetAtPath<Material>(HubSkyboxMaterialPath);
            if (hubSkybox != null)
            {
                RenderSettings.skybox = hubSkybox;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.50f, 0.58f, 0.68f, 1f);
            RenderSettings.fog = false;
        }

        private static void EnsureBackdrop(Scene scene, string rootName)
        {
            GameObject backdropRoot = AppShellEditorCommon.FindOrCreateSceneRoot(scene, rootName);
            backdropRoot.transform.position = Vector3.zero;
            backdropRoot.transform.rotation = Quaternion.identity;
            backdropRoot.transform.localScale = Vector3.one;
            ClearGeneratedBackdropChildren(backdropRoot.transform);

            Material wallMaterial = AssetDatabase.LoadAssetAtPath<Material>(HubWallMaterialPath);
            Material accentMaterial = AssetDatabase.LoadAssetAtPath<Material>(HubAccentMaterialPath);
            Material chromeMaterial = AssetDatabase.LoadAssetAtPath<Material>(HubChromeMaterialPath);
            Material backstageFloorMaterial = EnsureColoredBackdropMaterial(HubBackstageFloorMaterialPath, new Color(0.075f, 0.083f, 0.09f, 1f), 0f, 0.42f);
            Material backstageBlackMaterial = EnsureColoredBackdropMaterial(HubBackstageBlackMaterialPath, new Color(0.028f, 0.032f, 0.039f, 1f), 0f, 0.64f);
            Material backstageCurtainMaterial = EnsureColoredBackdropMaterial(HubBackstageCurtainMaterialPath, new Color(0.115f, 0.022f, 0.03f, 1f), 0f, 0.58f);
            Material backstageCurtainShadowMaterial = EnsureColoredBackdropMaterial(HubBackstageCurtainShadowMaterialPath, new Color(0.055f, 0.010f, 0.016f, 1f), 0f, 0.46f);
            Material backstageCaseMaterial = EnsureColoredBackdropMaterial(HubBackstageCaseMaterialPath, new Color(0.12f, 0.135f, 0.145f, 1f), 0.05f, 0.55f);
            Material backstageTapeMaterial = EnsureColoredBackdropMaterial(HubBackstageTapeMaterialPath, new Color(0.95f, 0.54f, 0.18f, 1f), 0f, 0.5f);
            Material backstageRunnerMaterial = EnsureColoredBackdropMaterial(HubBackstageRunnerMaterialPath, new Color(0.135f, 0.018f, 0.024f, 1f), 0f, 0.55f);
            Material backstageGlowMaterial = EnsureColoredBackdropMaterial(HubBackstageGlowMaterialPath, new Color(1f, 0.62f, 0.22f, 1f), 0f, 0.72f);
            Material roomWallMaterial = EnsureColoredBackdropMaterial(HubBackstageWallMaterialPath, new Color(0.105f, 0.118f, 0.13f, 1f), 0f, 0.47f);
            Material roomCeilingMaterial = EnsureColoredBackdropMaterial(HubBackstageCeilingMaterialPath, new Color(0.045f, 0.05f, 0.058f, 1f), 0f, 0.5f);
            Material roomTrimMaterial = EnsureColoredBackdropMaterial(HubBackstageTrimMaterialPath, new Color(0.25f, 0.29f, 0.32f, 1f), 0.12f, 0.5f);
            Material backstageDarkMaterial = backstageBlackMaterial ?? wallMaterial;
            Material backstageStageFloorMaterial = backstageFloorMaterial ?? wallMaterial;
            roomWallMaterial = roomWallMaterial ?? wallMaterial;
            roomCeilingMaterial = roomCeilingMaterial ?? backstageDarkMaterial;
            roomTrimMaterial = roomTrimMaterial ?? chromeMaterial ?? accentMaterial ?? wallMaterial;
            backstageCurtainMaterial = backstageCurtainMaterial ?? wallMaterial;
            backstageCurtainShadowMaterial = backstageCurtainShadowMaterial ?? backstageCurtainMaterial;
            backstageCaseMaterial = backstageCaseMaterial ?? backstageDarkMaterial;
            backstageRunnerMaterial = backstageRunnerMaterial ?? backstageCurtainMaterial;
            backstageGlowMaterial = backstageGlowMaterial ?? backstageTapeMaterial ?? accentMaterial ?? chromeMaterial ?? wallMaterial;

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "Floor",
                PrimitiveType.Plane,
                new Vector3(0f, 0f, 2.25f),
                Vector3.zero,
                new Vector3(1.22f, 1f, 0.82f),
                backstageStageFloorMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "BackWall",
                PrimitiveType.Cube,
                new Vector3(0f, 2.65f, 6.15f),
                Vector3.zero,
                new Vector3(11.85f, 5.3f, 0.2f),
                roomWallMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "LeftWall",
                PrimitiveType.Cube,
                new Vector3(-5.92f, 2.65f, 2.25f),
                Vector3.zero,
                new Vector3(0.2f, 5.3f, 7.8f),
                roomWallMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "RightWall",
                PrimitiveType.Cube,
                new Vector3(5.92f, 2.65f, 2.25f),
                Vector3.zero,
                new Vector3(0.2f, 5.3f, 7.8f),
                roomWallMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "FrontWall",
                PrimitiveType.Cube,
                new Vector3(0f, 2.65f, -1.65f),
                Vector3.zero,
                new Vector3(11.85f, 5.3f, 0.2f),
                roomWallMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "CeilingWall",
                PrimitiveType.Cube,
                new Vector3(0f, 5.35f, 2.25f),
                Vector3.zero,
                new Vector3(11.85f, 0.24f, 7.8f),
                roomCeilingMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "BackCurtainPanel",
                PrimitiveType.Cube,
                new Vector3(0f, 3.12f, 6.03f),
                Vector3.zero,
                new Vector3(11.5f, 5.86f, 0.08f),
                backstageCurtainMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "BackCurtainFold_Left01",
                PrimitiveType.Cube,
                new Vector3(-5.15f, 3.12f, 5.95f),
                Vector3.zero,
                new Vector3(0.24f, 5.68f, 0.14f),
                backstageCurtainMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "BackCurtainFold_Left02",
                PrimitiveType.Cube,
                new Vector3(-2.55f, 3.08f, 5.94f),
                Vector3.zero,
                new Vector3(0.16f, 5.56f, 0.12f),
                backstageCurtainMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "BackCurtainFold_Right01",
                PrimitiveType.Cube,
                new Vector3(5.15f, 3.12f, 5.95f),
                Vector3.zero,
                new Vector3(0.24f, 5.68f, 0.14f),
                backstageCurtainMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "BackCurtainFold_Right02",
                PrimitiveType.Cube,
                new Vector3(2.55f, 3.08f, 5.94f),
                Vector3.zero,
                new Vector3(0.16f, 5.56f, 0.12f),
                backstageCurtainMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "BackCurtainValance",
                PrimitiveType.Cube,
                new Vector3(0f, 5.72f, 5.88f),
                Vector3.zero,
                new Vector3(11.62f, 0.46f, 0.18f),
                backstageCurtainMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "BackCurtainBottomHem",
                PrimitiveType.Cube,
                new Vector3(0f, 0.38f, 5.88f),
                Vector3.zero,
                new Vector3(11.48f, 0.18f, 0.16f),
                backstageCurtainShadowMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "BackCurtainPleat_LeftOuter",
                PrimitiveType.Cube,
                new Vector3(-4.15f, 3.04f, 5.88f),
                Vector3.zero,
                new Vector3(0.10f, 5.42f, 0.16f),
                backstageCurtainShadowMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "BackCurtainPleat_LeftInner",
                PrimitiveType.Cube,
                new Vector3(-1.35f, 3.02f, 5.87f),
                Vector3.zero,
                new Vector3(0.08f, 5.32f, 0.14f),
                backstageCurtainShadowMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "BackCurtainPleat_CenterLeft",
                PrimitiveType.Cube,
                new Vector3(-0.38f, 3.02f, 5.865f),
                Vector3.zero,
                new Vector3(0.06f, 5.24f, 0.12f),
                backstageCurtainShadowMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "BackCurtainPleat_CenterRight",
                PrimitiveType.Cube,
                new Vector3(0.38f, 3.02f, 5.865f),
                Vector3.zero,
                new Vector3(0.06f, 5.24f, 0.12f),
                backstageCurtainShadowMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "BackCurtainPleat_RightInner",
                PrimitiveType.Cube,
                new Vector3(1.35f, 3.02f, 5.87f),
                Vector3.zero,
                new Vector3(0.08f, 5.32f, 0.14f),
                backstageCurtainShadowMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "BackCurtainPleat_RightOuter",
                PrimitiveType.Cube,
                new Vector3(4.15f, 3.04f, 5.88f),
                Vector3.zero,
                new Vector3(0.10f, 5.42f, 0.16f),
                backstageCurtainShadowMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "StageDeck",
                PrimitiveType.Cube,
                new Vector3(0f, 0.13f, 4.72f),
                Vector3.zero,
                new Vector3(8.65f, 0.24f, 1.72f),
                backstageDarkMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "StageFrontEdge",
                PrimitiveType.Cube,
                new Vector3(0f, 0.30f, 3.84f),
                Vector3.zero,
                new Vector3(8.65f, 0.16f, 0.08f),
                backstageGlowMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "CenterFloorRunner",
                PrimitiveType.Cube,
                new Vector3(0f, 0.028f, 1.28f),
                Vector3.zero,
                new Vector3(2.18f, 0.022f, 3.55f),
                backstageRunnerMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "RunnerEdge_Left",
                PrimitiveType.Cube,
                new Vector3(-1.14f, 0.04f, 1.28f),
                Vector3.zero,
                new Vector3(0.045f, 0.018f, 3.55f),
                backstageTapeMaterial ?? backstageGlowMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "RunnerEdge_Right",
                PrimitiveType.Cube,
                new Vector3(1.14f, 0.04f, 1.28f),
                Vector3.zero,
                new Vector3(0.045f, 0.018f, 3.55f),
                backstageTapeMaterial ?? backstageGlowMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "Footlight_Left",
                PrimitiveType.Cube,
                new Vector3(-2.7f, 0.24f, 3.68f),
                new Vector3(-8f, 0f, 0f),
                new Vector3(0.36f, 0.16f, 0.20f),
                backstageGlowMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "Footlight_Center",
                PrimitiveType.Cube,
                new Vector3(0f, 0.24f, 3.68f),
                new Vector3(-8f, 0f, 0f),
                new Vector3(0.36f, 0.16f, 0.20f),
                backstageGlowMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "Footlight_Right",
                PrimitiveType.Cube,
                new Vector3(2.7f, 0.24f, 3.68f),
                new Vector3(-8f, 0f, 0f),
                new Vector3(0.36f, 0.16f, 0.20f),
                backstageGlowMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "LeftWingFlat",
                PrimitiveType.Cube,
                new Vector3(-4.88f, 2.72f, 5.34f),
                new Vector3(0f, -10f, 0f),
                new Vector3(0.18f, 4.62f, 0.86f),
                backstageDarkMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "RightWingFlat",
                PrimitiveType.Cube,
                new Vector3(4.88f, 2.72f, 5.34f),
                new Vector3(0f, 10f, 0f),
                new Vector3(0.18f, 4.62f, 0.86f),
                backstageDarkMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "BackWallBaseTrim",
                PrimitiveType.Cube,
                new Vector3(0f, 0.28f, 5.98f),
                Vector3.zero,
                new Vector3(11.4f, 0.18f, 0.1f),
                roomTrimMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "LeftWallBaseTrim",
                PrimitiveType.Cube,
                new Vector3(-5.78f, 0.28f, 2.25f),
                Vector3.zero,
                new Vector3(0.1f, 0.18f, 7.4f),
                roomTrimMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "RightWallBaseTrim",
                PrimitiveType.Cube,
                new Vector3(5.78f, 0.28f, 2.25f),
                Vector3.zero,
                new Vector3(0.1f, 0.18f, 7.4f),
                roomTrimMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "FrontWallBaseTrim",
                PrimitiveType.Cube,
                new Vector3(0f, 0.28f, -1.48f),
                Vector3.zero,
                new Vector3(11.4f, 0.18f, 0.1f),
                roomTrimMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "BackCeilingLightBar",
                PrimitiveType.Cube,
                new Vector3(0f, 4.9f, 5.72f),
                Vector3.zero,
                new Vector3(5.9f, 0.08f, 0.08f),
                chromeMaterial ?? roomTrimMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "LeftWallSconce",
                PrimitiveType.Cube,
                new Vector3(-5.78f, 3.25f, 3.75f),
                new Vector3(0f, 90f, 0f),
                new Vector3(0.07f, 0.28f, 0.42f),
                chromeMaterial ?? roomTrimMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "RightWallSconce",
                PrimitiveType.Cube,
                new Vector3(5.78f, 3.25f, 3.75f),
                new Vector3(0f, 90f, 0f),
                new Vector3(0.07f, 0.28f, 0.42f),
                chromeMaterial ?? roomTrimMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "RoadCase_Left",
                PrimitiveType.Cube,
                new Vector3(-4.55f, 0.37f, 0.38f),
                new Vector3(0f, 4f, 0f),
                new Vector3(1.1f, 0.54f, 0.72f),
                backstageCaseMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "RoadCase_Right",
                PrimitiveType.Cube,
                new Vector3(4.55f, 0.37f, 0.38f),
                new Vector3(0f, -4f, 0f),
                new Vector3(1.1f, 0.54f, 0.72f),
                backstageCaseMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "SpeakerStack_Left",
                PrimitiveType.Cube,
                new Vector3(-5.23f, 0.96f, 2.75f),
                Vector3.zero,
                new Vector3(0.62f, 1.42f, 0.64f),
                backstageDarkMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "SpeakerStack_Right",
                PrimitiveType.Cube,
                new Vector3(5.23f, 0.96f, 2.75f),
                Vector3.zero,
                new Vector3(0.62f, 1.42f, 0.64f),
                backstageDarkMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "CableCoil_Left",
                PrimitiveType.Cylinder,
                new Vector3(-3.28f, 0.24f, 0.52f),
                new Vector3(90f, 0f, 0f),
                new Vector3(0.32f, 0.14f, 0.32f),
                chromeMaterial ?? backstageDarkMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "CableCoil_Right",
                PrimitiveType.Cylinder,
                new Vector3(3.28f, 0.24f, 0.52f),
                new Vector3(90f, 0f, 0f),
                new Vector3(0.32f, 0.14f, 0.32f),
                chromeMaterial ?? backstageDarkMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "FloorTape_Left",
                PrimitiveType.Cube,
                new Vector3(-1.75f, 0.025f, 0.68f),
                new Vector3(0f, -12f, 0f),
                new Vector3(1.15f, 0.018f, 0.055f),
                backstageTapeMaterial ?? accentMaterial ?? chromeMaterial ?? wallMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "FloorTape_Right",
                PrimitiveType.Cube,
                new Vector3(1.75f, 0.025f, 0.68f),
                new Vector3(0f, 12f, 0f),
                new Vector3(1.15f, 0.018f, 0.055f),
                backstageTapeMaterial ?? accentMaterial ?? chromeMaterial ?? wallMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "CallTimeBoard",
                PrimitiveType.Cube,
                new Vector3(-5.75f, 1.88f, 4.3f),
                new Vector3(0f, 90f, 0f),
                new Vector3(0.08f, 0.76f, 1.08f),
                accentMaterial ?? wallMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "ExitDoorPanel",
                PrimitiveType.Cube,
                new Vector3(5.78f, 1.35f, 4.55f),
                new Vector3(0f, 90f, 0f),
                new Vector3(0.08f, 2.3f, 1.08f),
                backstageDarkMaterial);

            EnsurePrimitiveBackdropChild(
                backdropRoot.transform,
                "ExitDoorHandle",
                PrimitiveType.Cube,
                new Vector3(5.72f, 1.35f, 4.17f),
                new Vector3(0f, 90f, 0f),
                new Vector3(0.08f, 0.06f, 0.28f),
                roomTrimMaterial);

            EnsureBackdropSpotlight(
                backdropRoot.transform,
                "WarmRoomSpot_Left",
                new Vector3(-2.85f, 4.45f, 4.62f),
                new Vector3(58f, -14f, 0f),
                new Color(1f, 0.79f, 0.54f, 1f),
                1.95f);

            EnsureBackdropSpotlight(
                backdropRoot.transform,
                "CoolRoomSpot_Right",
                new Vector3(2.85f, 4.45f, 4.62f),
                new Vector3(58f, 14f, 0f),
                new Color(0.55f, 0.74f, 1f, 1f),
                1.25f);

            EnsureBackdropCollisionSafety(backdropRoot.transform);
        }

        private static void ClearGeneratedBackdropChildren(Transform backdropRoot)
        {
            if (backdropRoot == null)
            {
                return;
            }

            for (int index = backdropRoot.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(backdropRoot.GetChild(index).gameObject);
            }
        }

        private static void EnsureBackdropSpotlight(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Color color,
            float intensity)
        {
            GameObject child = AppShellEditorCommon.FindOrCreateChild(parent, name);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.Euler(localEulerAngles);
            child.transform.localScale = Vector3.one;

            Light light = AppShellEditorCommon.GetOrAddComponent<Light>(child);
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = 7.5f;
            light.spotAngle = 38f;
            light.innerSpotAngle = 18f;
            light.shadows = LightShadows.None;
        }

        private static Material EnsureColoredBackdropMaterial(
            string assetPath,
            Color color,
            float metallic,
            float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                EnsureGeneratedMaterialFolder();

                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null || shader.name == "Hidden/InternalErrorShader")
                {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureGeneratedMaterialFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/AppShell/Generated"))
            {
                AssetDatabase.CreateFolder("Assets/AppShell", "Generated");
            }

            if (!AssetDatabase.IsValidFolder(HubGeneratedMaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/AppShell/Generated", "Materials");
            }
        }

        private static void EnsurePrimitiveBackdropChild(
            Transform parent,
            string name,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale,
            Material sharedMaterial)
        {
            GameObject child = AppShellEditorCommon.FindOrCreateChild(parent, name);
            MeshFilter meshFilter = AppShellEditorCommon.GetOrAddComponent<MeshFilter>(child);
            MeshRenderer meshRenderer = AppShellEditorCommon.GetOrAddComponent<MeshRenderer>(child);

            if (meshFilter.sharedMesh == null)
            {
                GameObject primitive = GameObject.CreatePrimitive(primitiveType);
                meshFilter.sharedMesh = primitive.GetComponent<MeshFilter>().sharedMesh;
                MeshRenderer primitiveRenderer = primitive.GetComponent<MeshRenderer>();
                if (primitiveRenderer != null)
                {
                    meshRenderer.sharedMaterials = primitiveRenderer.sharedMaterials;
                }

                UnityEngine.Object.DestroyImmediate(primitive);
            }

            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.Euler(localEulerAngles);
            child.transform.localScale = localScale;

            if (sharedMaterial != null)
            {
                meshRenderer.sharedMaterial = sharedMaterial;
            }
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
                return;
            }

            var assignDefaultActionsMethod = inputSystemModule.GetType().GetMethod("AssignDefaultActions", Type.EmptyTypes);
            assignDefaultActionsMethod?.Invoke(inputSystemModule, null);
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
            AppShellEditorCommon.ApplyOutline(brandingRoot.gameObject, AppShellEditorCommon.SoftBorderColor, new Vector2(1f, -1f));

            VerticalLayoutGroup layout = AppShellEditorCommon.GetOrAddComponent<VerticalLayoutGroup>(brandingRoot.gameObject);
            layout.spacing = 6f;
            layout.padding = new RectOffset(28, 28, 18, 18);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = AppShellEditorCommon.GetOrAddComponent<ContentSizeFitter>(brandingRoot.gameObject);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AppShellEditorUi.CreateTextBlock(brandingRoot, "BrandTitle", "Orator VR", 48f, FontStyles.Bold, TextAlignmentOptions.Center, 54f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(
                brandingRoot,
                "BrandSubtitle",
                "Immersive public speaking practice from launch flow to review.",
                18f,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                38f,
                AppShellEditorCommon.MutedTextColor);
        }

        private static void BuildHomePanel(Transform panelTransform, HomePanelPresenter presenter, AppEnvironmentCatalog catalog)
        {
            AppShellEditorUi.ClearGeneratedChildren(panelTransform);

            int environmentCount = catalog != null && catalog.Environments != null ? catalog.Environments.Count : 0;

            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelTitle", "Practice Hub", 44f, FontStyles.Bold, TextAlignmentOptions.Left, 56f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelSubtitle", "Start a run quickly, compare rooms, and keep support routes close without crowding the shell.", 18f, FontStyles.Normal, TextAlignmentOptions.Left, 40f, AppShellEditorCommon.MutedTextColor);

            GameObject dashboardRow = AppShellEditorUi.CreateDashboardRow(panelTransform, "DashboardRow", 22f);

            GameObject featuredColumn = AppShellEditorUi.CreateSectionCard(
                dashboardRow.transform,
                "FeaturedColumn",
                AppShellEditorCommon.HeroSurfaceColor,
                24,
                22,
                12f,
                AppShellEditorCommon.HeroAccentColor);
            AppShellEditorCommon.ConfigureLayoutElement(featuredColumn, 620f, 652f);
            AppShellEditorUi.CreateTextBlock(featuredColumn.transform, "SectionTitle", "FEATURED FLOW", 16f, FontStyles.Bold, TextAlignmentOptions.Left, 20f, AppShellEditorCommon.HeroAccentColor);
            AppShellEditorUi.CreateTextBlock(featuredColumn.transform, "SectionLead", "One strong entry point for the full menu-to-launch flow.", 16f, FontStyles.Normal, TextAlignmentOptions.Left, 34f, AppShellEditorCommon.MutedTextColor);

            GameObject startCard = AppShellEditorUi.CreateFeatureCard(
                featuredColumn.transform,
                "StartPracticeCard",
                "Start Practice",
                "Move from mode to room to setup with one clean guided path.",
                "Primary Run",
                GetEnvironmentPreviewSprite(catalog, 0),
                -1f,
                326f,
                118f,
                54f,
                AppShellEditorCommon.HeroSurfaceColor,
                AppShellEditorCommon.HeroAccentColor);
            Button startButton = AppShellEditorUi.CreateStyledButton(startCard.transform, "StartPracticeButton", "Open Practice Flow", AppShellEditorUi.ButtonTone.Primary, -1f, 60f);

            GameObject featuredQuickRow = AppShellEditorUi.CreateDashboardRow(featuredColumn.transform, "FeaturedQuickRow", 14f);
            AppShellEditorCommon.ConfigureLayoutElement(featuredQuickRow, -1f, 132f);
            Button environmentsButton = AppShellEditorUi.CreateUtilityTile(featuredQuickRow.transform, "EnvironmentsButton", "Rooms", "Compare launch-ready spaces with a dedicated room browser.", AppShellEditorCommon.TileSurfaceColor, 279f, 132f);
            Button resultsButton = AppShellEditorUi.CreateUtilityTile(featuredQuickRow.transform, "ResultsButton", "Results", "Jump back into the latest recap and analytics entry points.", AppShellEditorCommon.TileSurfaceColor, 279f, 132f);

            GameObject utilityColumn = AppShellEditorUi.CreateSectionCard(
                dashboardRow.transform,
                "UtilityColumn",
                AppShellEditorCommon.ElevatedSurfaceColor,
                22,
                22,
                14f,
                AppShellEditorCommon.AccentColor);
            AppShellEditorCommon.ConfigureLayoutElement(utilityColumn, 254f, 652f);
            AppShellEditorUi.CreateTextBlock(utilityColumn.transform, "UtilityTitle", "QUICK ACCESS", 16f, FontStyles.Bold, TextAlignmentOptions.Left, 20f, AppShellEditorCommon.SoftAccentColor);
            AppShellEditorUi.CreateTextBlock(utilityColumn.transform, "UtilityLead", "Support actions stay compact so the hero path keeps the focus.", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 40f, AppShellEditorCommon.MutedTextColor);
            AppShellEditorUi.CreateSummaryStrip(utilityColumn.transform, "CatalogStrip", "Rooms", environmentCount > 0 ? $"{environmentCount} room(s) visible in shell" : "No rooms listed yet");
            AppShellEditorUi.CreateSummaryStrip(utilityColumn.transform, "FlowStrip", "Launch Path", "Mode -> Room -> Setup -> Launch");

            Button settingsButton = AppShellEditorUi.CreateStyledButton(utilityColumn.transform, "SettingsButton", "Settings", AppShellEditorUi.ButtonTone.Secondary, -1f, 56f);
            AppShellEditorUi.CreateTextBlock(utilityColumn.transform, "SettingsInfo", "Comfort, controls, and calibration", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 34f, AppShellEditorCommon.MutedTextColor);

            Button exitButton = AppShellEditorUi.CreateStyledButton(utilityColumn.transform, "ExitButton", "Exit", AppShellEditorUi.ButtonTone.Danger, -1f, 56f);
            AppShellEditorUi.CreateTextBlock(utilityColumn.transform, "ExitInfo", "Close the current app shell session", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 34f, AppShellEditorCommon.MutedTextColor);

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
            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelSubtitle", "Browse the room atlas, compare context fast, and lock a launch-ready space before setup.", 18f, FontStyles.Normal, TextAlignmentOptions.Left, 40f, AppShellEditorCommon.MutedTextColor);

            GameObject dashboardRow = AppShellEditorUi.CreateDashboardRow(panelTransform, "EnvironmentDashboardRow", 18f);
            GameObject catalogSection = AppShellEditorUi.CreateSectionCard(dashboardRow.transform, "CatalogSection", AppShellEditorCommon.ElevatedSurfaceColor, 22, 22, 14f, AppShellEditorCommon.AccentColor);
            GameObject summarySection = AppShellEditorUi.CreateSectionCard(dashboardRow.transform, "SelectionSection", AppShellEditorCommon.HeroSurfaceColor, 22, 22, 12f, AppShellEditorCommon.HeroAccentColor);
            AppShellEditorCommon.ConfigureLayoutElement(catalogSection, 618f, 710f);
            AppShellEditorCommon.ConfigureLayoutElement(summarySection, 260f, 710f);

            int readyCount = 0;
            int totalCount = catalog != null && catalog.Environments != null ? catalog.Environments.Count : 0;
            if (catalog != null && catalog.Environments != null)
            {
                for (int index = 0; index < catalog.Environments.Count; index++)
                {
                    AppEnvironmentDefinition environment = catalog.Environments[index];
                    if (environment != null && environment.IsSelectable)
                    {
                        readyCount++;
                    }
                }
            }

            AppShellEditorUi.CreateTextBlock(catalogSection.transform, "CatalogSectionTitle", "ROOM ATLAS", 16f, FontStyles.Bold, TextAlignmentOptions.Left, 20f, AppShellEditorCommon.SoftAccentColor);
            AppShellEditorUi.CreateTextBlock(catalogSection.transform, "CatalogLead", "Card states now carry the decision: ready, selected, unavailable, or still waiting for setup.", 16f, FontStyles.Normal, TextAlignmentOptions.Left, 38f, AppShellEditorCommon.MutedTextColor);

            GameObject cardGrid = AppShellEditorCommon.FindOrCreateChild(catalogSection.transform, "EnvironmentCardGrid");
            GridLayoutGroup grid = AppShellEditorCommon.GetOrAddComponent<GridLayoutGroup>(cardGrid);
            grid.cellSize = new Vector2(281f, 284f);
            grid.spacing = new Vector2(12f, 12f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            LayoutElement gridLayout = AppShellEditorCommon.GetOrAddComponent<LayoutElement>(cardGrid);
            gridLayout.preferredHeight = 580f;

            List<EnvironmentCardView> cardViews = new List<EnvironmentCardView>();
            int environmentCount = catalog != null && catalog.Environments != null && catalog.Environments.Count > 0
                ? catalog.Environments.Count
                : 3;

            for (int index = 0; index < environmentCount; index++)
            {
                EnvironmentCardView card = AppShellEditorUi.CreateEnvironmentCard(cardGrid.transform, $"EnvironmentCard_{index + 1}");
                cardViews.Add(card);
            }

            AppShellEditorUi.CreateTextBlock(summarySection.transform, "SelectionSectionTitle", "ROOM BRIEF", 16f, FontStyles.Bold, TextAlignmentOptions.Left, 20f, AppShellEditorCommon.HeroAccentColor);
            AppShellEditorUi.CreateTextBlock(summarySection.transform, "SelectionLead", "Use this column as the final room check before you move on.", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 32f, AppShellEditorCommon.MutedTextColor);
            AppShellEditorUi.CreateSummaryStrip(summarySection.transform, "SummaryStripA", "Ready Rooms", totalCount > 0 ? $"{readyCount} of {totalCount} room(s) can launch" : "Room status will appear here");
            AppShellEditorUi.CreateSummaryStrip(summarySection.transform, "SummaryStripB", "Flow", "Pick room -> confirm -> continue");
            TMP_Text helperLabel = AppShellEditorUi.CreateTextBlock(summarySection.transform, "HelperLabel", "Select the room that best matches the speaking context you want to rehearse.", 17f, FontStyles.Normal, TextAlignmentOptions.Left, 168f, AppShellEditorCommon.TextColor);
            Button confirmButton = AppShellEditorUi.CreateStyledButton(summarySection.transform, "ConfirmButton", "Continue To Setup", AppShellEditorUi.ButtonTone.Primary, -1f, 64f);
            Button backButton = AppShellEditorUi.CreateStyledButton(summarySection.transform, "BackButton", "Back", AppShellEditorUi.ButtonTone.Secondary, -1f, 56f);

            AppShellEditorCommon.SetButtonEvent(backButton, appFlowManager.GoBack);
            AppShellEditorCommon.SetButtonEvent(confirmButton, appFlowManager.ContinueFromEnvironmentSelection);

            AppShellEditorCommon.SetField(controller, "helperLabel", helperLabel);
            AppShellEditorCommon.SetField(controller, "confirmSelectionButton", confirmButton);
            AppShellEditorCommon.SetField(controller, "cardViews", cardViews);
        }

        private static void BuildSessionSetupPanel(Transform panelTransform, SessionConfigController controller, AppFlowManager appFlowManager)
        {
            AppShellEditorUi.ClearGeneratedChildren(panelTransform);

            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelTitle", "Session Setup", 44f, FontStyles.Bold, TextAlignmentOptions.Left, 56f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelSubtitle", "Tighten the setup into one control board: timing, context, and active systems without hidden text.", 18f, FontStyles.Normal, TextAlignmentOptions.Left, 40f, AppShellEditorCommon.MutedTextColor);

            GameObject setupRow = AppShellEditorUi.CreateDashboardRow(panelTransform, "SetupDashboardRow", 18f);
            GameObject leftColumn = AppShellEditorUi.CreateSectionCard(setupRow.transform, "LeftSetupColumn", AppShellEditorCommon.ElevatedSurfaceColor, 22, 22, 10f, AppShellEditorCommon.AccentColor);
            GameObject rightColumn = AppShellEditorUi.CreateSectionCard(setupRow.transform, "RightSetupColumn", AppShellEditorCommon.HeroSurfaceColor, 22, 22, 12f, AppShellEditorCommon.HeroAccentColor);
            AppShellEditorCommon.ConfigureLayoutElement(leftColumn, 522f, 710f);
            AppShellEditorCommon.ConfigureLayoutElement(rightColumn, 356f, 710f);

            AppShellEditorUi.CreateTextBlock(leftColumn.transform, "ControlsTitle", "CONTROL BOARD", 16f, FontStyles.Bold, TextAlignmentOptions.Left, 20f, AppShellEditorCommon.SoftAccentColor);

            GameObject timingSection = AppShellEditorUi.CreateSectionCard(leftColumn.transform, "TimingSection", AppShellEditorCommon.HeroSurfaceColor, 18, 18, 8f, AppShellEditorCommon.HeroAccentColor);
            AppShellEditorUi.CreateTextBlock(timingSection.transform, "TimingTitle", "SESSION TEMPO", 15f, FontStyles.Bold, TextAlignmentOptions.Left, 20f, AppShellEditorCommon.HeroAccentColor);
            TMP_Text durationLabel = AppShellEditorUi.CreateTextBlock(timingSection.transform, "DurationValueLabel", "5 min", 28f, FontStyles.Bold, TextAlignmentOptions.Left, 32f, AppShellEditorCommon.TextColor);
            Slider durationSlider = AppShellEditorUi.CreateSlider(timingSection.transform, "DurationSlider", 1f, 15f, true, 5f);
            AppShellEditorCommon.ConfigureLayoutElement(timingSection, -1f, 142f);

            GameObject contextSection = AppShellEditorUi.CreateSectionCard(leftColumn.transform, "ContextSection", AppShellEditorCommon.TileSurfaceColor, 18, 18, 10f, AppShellEditorCommon.AccentColor);
            AppShellEditorUi.CreateTextBlock(contextSection.transform, "ContextTitle", "SESSION CONTEXT", 15f, FontStyles.Bold, TextAlignmentOptions.Left, 20f, AppShellEditorCommon.SoftAccentColor);
            GameObject contextRow = AppShellEditorUi.CreateDashboardRow(contextSection.transform, "ContextRow", 12f);
            TMP_Dropdown difficultyDropdown = CreateDropdownFieldCard(contextRow.transform, "DifficultyFieldCard", "Difficulty", AppShellEditorCommon.EnumNames<SessionDifficulty>(), 215f);
            TMP_Dropdown audienceDropdown = CreateDropdownFieldCard(contextRow.transform, "AudienceFieldCard", "Audience", AppShellEditorCommon.EnumNames<AudiencePreset>(), 215f);
            TMP_Dropdown feedbackDropdown = CreateDropdownFieldCard(contextSection.transform, "FeedbackFieldCard", "Feedback Detail", AppShellEditorCommon.EnumNames<FeedbackLevel>(), -1f);

            GameObject analysisSection = AppShellEditorUi.CreateSectionCard(leftColumn.transform, "AnalysisSection", AppShellEditorCommon.UtilitySurfaceColor, 18, 18, 8f, AppShellEditorCommon.WithAlpha(AppShellEditorCommon.AccentColor, 0.8f));
            AppShellEditorUi.CreateTextBlock(analysisSection.transform, "AnalysisTitle", "ANALYSIS SYSTEMS", 15f, FontStyles.Bold, TextAlignmentOptions.Left, 20f, AppShellEditorCommon.SoftAccentColor);
            GameObject analysisRowA = AppShellEditorUi.CreateDashboardRow(analysisSection.transform, "AnalysisRowA", 12f);
            GameObject analysisRowB = AppShellEditorUi.CreateDashboardRow(analysisSection.transform, "AnalysisRowB", 12f);
            GameObject analysisRowC = AppShellEditorUi.CreateDashboardRow(analysisSection.transform, "AnalysisRowC", 12f);
            Toggle eyeTrackingToggle = AppShellEditorUi.CreateToggle(analysisRowA.transform, "EyeTrackingToggle", "Eye Tracking", true);
            Toggle gazeScoringToggle = AppShellEditorUi.CreateToggle(analysisRowA.transform, "GazeScoringToggle", "Gaze Scoring", true);
            Toggle performanceScoringToggle = AppShellEditorUi.CreateToggle(analysisRowB.transform, "PerformanceScoringToggle", "Performance Scoring", true);
            Toggle voiceAnalysisToggle = AppShellEditorUi.CreateToggle(analysisRowB.transform, "VoiceAnalysisToggle", "Voice Analysis", false);
            Toggle postureAnalysisToggle = AppShellEditorUi.CreateToggle(analysisRowC.transform, "PostureAnalysisToggle", "Posture Analysis", false);
            AppShellEditorCommon.ConfigureLayoutElement(eyeTrackingToggle.gameObject, 215f, 44f);
            AppShellEditorCommon.ConfigureLayoutElement(gazeScoringToggle.gameObject, 215f, 44f);
            AppShellEditorCommon.ConfigureLayoutElement(performanceScoringToggle.gameObject, 215f, 44f);
            AppShellEditorCommon.ConfigureLayoutElement(voiceAnalysisToggle.gameObject, 215f, 44f);
            AppShellEditorCommon.ConfigureLayoutElement(postureAnalysisToggle.gameObject, 215f, 44f);

            AppShellEditorUi.CreateTextBlock(rightColumn.transform, "SummaryTitle", "LIVE BRIEF", 16f, FontStyles.Bold, TextAlignmentOptions.Left, 20f, AppShellEditorCommon.HeroAccentColor);
            AppShellEditorUi.CreateTextBlock(rightColumn.transform, "SummaryLead", "Keep this side short and readable like a final session card.", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 32f, AppShellEditorCommon.MutedTextColor);
            GameObject summaryCard = AppShellEditorUi.CreateSectionCard(rightColumn.transform, "SummaryCard", AppShellEditorCommon.TileSurfaceColor, 18, 18, 8f, AppShellEditorCommon.AccentColor);
            AppShellEditorUi.CreateTextBlock(summaryCard.transform, "SummaryCardTitle", "CURRENT CONFIG", 15f, FontStyles.Bold, TextAlignmentOptions.Left, 20f, AppShellEditorCommon.SoftAccentColor);
            TMP_Text summaryPreview = AppShellEditorUi.CreateTextBlock(summaryCard.transform, "SummaryPreviewLabel", string.Empty, 17f, FontStyles.Normal, TextAlignmentOptions.Left, 150f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateSummaryStrip(rightColumn.transform, "SummaryHint", "Safe Integration", "Only shell configuration changes here. Scene logic stays intact.");
            AppShellEditorUi.CreateSummaryStrip(rightColumn.transform, "CapabilityHint", "Visible Systems", "Unhooked integrations can stay visible as disabled choices.");

            Transform actions = AppShellEditorUi.CreateVerticalContainer(rightColumn.transform, "SummaryActions", 10f).transform;
            Button continueButton = AppShellEditorUi.CreateStyledButton(actions, "ContinueButton", "Review Setup", AppShellEditorUi.ButtonTone.Primary, -1f, 64f);
            Button backButton = AppShellEditorUi.CreateStyledButton(actions, "BackButton", "Back", AppShellEditorUi.ButtonTone.Secondary, -1f, 56f);

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

            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelTitle", "Ready To Launch", 44f, FontStyles.Bold, TextAlignmentOptions.Left, 58f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelSubtitle", "This screen should feel like a launch moment: clear summary, visible warnings, one confident CTA.", 20f, FontStyles.Normal, TextAlignmentOptions.Left, 48f, AppShellEditorCommon.MutedTextColor);

            GameObject readyRow = AppShellEditorUi.CreateDashboardRow(panelTransform, "ReadyDashboardRow", 22f);
            GameObject launchCard = AppShellEditorUi.CreateFeatureCard(
                readyRow.transform,
                "LaunchCard",
                "Launch Brief",
                "One last review before scene load, adapter handoff, and in-session HUD activation.",
                "Final Check",
                null,
                320f,
                548f,
                backgroundColor: AppShellEditorCommon.HeroSurfaceColor,
                accentColor: AppShellEditorCommon.HeroAccentColor);
            AppShellEditorUi.CreateSummaryStrip(launchCard.transform, "LaunchStrip", "Flow", "Review setup -> Start session -> Enter environment");
            AppShellEditorUi.CreateSummaryStrip(launchCard.transform, "LaunchNoteStrip", "Handoff", "Environment adapters take over only after confirmation.");

            GameObject reviewCard = AppShellEditorUi.CreateSectionCard(readyRow.transform, "ReviewCard", AppShellEditorCommon.ElevatedSurfaceColor, 24, 24, 16f, AppShellEditorCommon.AccentColor);
            AppShellEditorCommon.ConfigureLayoutElement(reviewCard, 548f, 548f);
            AppShellEditorUi.CreateTextBlock(reviewCard.transform, "ReviewTitle", "SESSION SUMMARY", 16f, FontStyles.Bold, TextAlignmentOptions.Left, 22f, AppShellEditorCommon.SoftAccentColor);
            AppShellEditorUi.CreateTextBlock(reviewCard.transform, "SummaryLabel", string.Empty, 22f, FontStyles.Normal, TextAlignmentOptions.Left, 264f, AppShellEditorCommon.TextColor);
            GameObject warningCard = AppShellEditorUi.CreateSectionCard(reviewCard.transform, "WarningCard", AppShellEditorCommon.WarningSurfaceColor, 18, 18, 8f, AppShellEditorCommon.WarningAccentColor);
            AppShellEditorUi.CreateTextBlock(warningCard.transform, "WarningTitle", "LAUNCH WARNINGS", 15f, FontStyles.Bold, TextAlignmentOptions.Left, 20f, AppShellEditorCommon.WarningAccentColor);
            AppShellEditorUi.CreateTextBlock(warningCard.transform, "WarningLabel", string.Empty, 17f, FontStyles.Normal, TextAlignmentOptions.Left, 96f, new Color(1f, 0.90f, 0.82f, 1f));

            Transform footer = AppShellEditorUi.CreateHorizontalContainer(reviewCard.transform, "FooterActions", 16f).transform;
            Button backButton = AppShellEditorUi.CreateStyledButton(footer, "BackButton", "Back", AppShellEditorUi.ButtonTone.Secondary, 220f, 58f);
            Button startButton = AppShellEditorUi.CreateStyledButton(footer, "StartSessionButton", "Start Session", AppShellEditorUi.ButtonTone.Primary, 286f, 66f);

            AppShellEditorCommon.SetButtonEvent(backButton, presenter.GoBack);
            AppShellEditorCommon.SetButtonEvent(startButton, presenter.StartSession);
        }

        private static void BuildProgressPanel(Transform panelTransform, ProgressPanelPresenter presenter)
        {
            AppShellEditorUi.ClearGeneratedChildren(panelTransform);

            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelTitle", "Review Center", 44f, FontStyles.Bold, TextAlignmentOptions.Left, 58f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelSubtitle", "Keep the latest session status readable first, then branch into summary or dashboard routes.", 20f, FontStyles.Normal, TextAlignmentOptions.Left, 42f, AppShellEditorCommon.MutedTextColor);

            Transform dashboardRow = AppShellEditorUi.CreateDashboardRow(panelTransform, "ProgressDashboardRow", 18f).transform;

            Transform overviewCard = AppShellEditorUi.CreateSectionCard(
                dashboardRow,
                "ProgressOverviewCard",
                AppShellEditorCommon.HeroSurfaceColor,
                24,
                24,
                10f,
                AppShellEditorCommon.HeroAccentColor).transform;

            AppShellEditorCommon.ConfigureLayoutElement(overviewCard.gameObject, -1f, 548f);
            AppShellEditorCommon.GetOrAddComponent<LayoutElement>(overviewCard.gameObject).flexibleWidth = 1f;
            AppShellEditorUi.CreateTextBlock(overviewCard, "OverviewTitle", "STATUS BOARD", 16f, FontStyles.Bold, TextAlignmentOptions.Left, 22f, AppShellEditorCommon.HeroAccentColor);
            AppShellEditorUi.CreateTextBlock(overviewCard, "BodyCopy", "Summary and dashboard adapters stay behind the shell so the front-end keeps one stable entry point.", 16f, FontStyles.Normal, TextAlignmentOptions.Left, 46f, AppShellEditorCommon.MutedTextColor);
            TMP_Text summaryStatusLabel = AppShellEditorUi.CreateTextBlock(overviewCard, "SummaryStatusLabel", string.Empty, 21f, FontStyles.Bold, TextAlignmentOptions.Left, 84f, AppShellEditorCommon.TextColor);
            TMP_Text dashboardStatusLabel = AppShellEditorUi.CreateTextBlock(overviewCard, "DashboardStatusLabel", string.Empty, 17f, FontStyles.Normal, TextAlignmentOptions.Left, 72f, AppShellEditorCommon.TextColor);
            TMP_Text noteLabel = AppShellEditorUi.CreateTextBlock(overviewCard, "ActionNoteLabel", string.Empty, 16f, FontStyles.Italic, TextAlignmentOptions.Left, 84f, AppShellEditorCommon.MutedTextColor);
            AppShellEditorUi.CreateSummaryStrip(overviewCard, "OverviewStrip", "Review Path", "Summary stays as the safest route even when analytics are still coming online.");

            Transform actionCard = AppShellEditorUi.CreateSectionCard(
                dashboardRow,
                "ProgressActionCard",
                AppShellEditorCommon.UtilitySurfaceColor,
                20,
                20,
                10f,
                AppShellEditorCommon.AccentColor).transform;

            AppShellEditorCommon.ConfigureLayoutElement(actionCard.gameObject, 304f, 548f);
            AppShellEditorUi.CreateTextBlock(actionCard, "ActionTitle", "NEXT STEP", 16f, FontStyles.Bold, TextAlignmentOptions.Left, 22f, AppShellEditorCommon.SoftAccentColor);
            AppShellEditorUi.CreateTextBlock(actionCard, "ActionLead", "Use the safest review routes first.", 16f, FontStyles.Normal, TextAlignmentOptions.Left, 34f, AppShellEditorCommon.MutedTextColor);
            Button openResultsButton = AppShellEditorUi.CreateStyledButton(actionCard, "OpenSummaryButton", "Latest Summary", AppShellEditorUi.ButtonTone.Primary, -1f, 50f);
            AppShellEditorUi.CreateTextBlock(actionCard, "OpenSummaryInfo", "Open the newest session recap.", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 26f, AppShellEditorCommon.MutedTextColor);
            Button openDashboardButton = AppShellEditorUi.CreateStyledButton(actionCard, "OpenDashboardButton", "Dashboard Entry", AppShellEditorUi.ButtonTone.Utility, -1f, 50f);
            AppShellEditorUi.CreateTextBlock(actionCard, "OpenDashboardInfo", "Jump to charts and long-term review.", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 28f, AppShellEditorCommon.MutedTextColor);
            Button backButton = AppShellEditorUi.CreateStyledButton(actionCard, "BackButton", "Back", AppShellEditorUi.ButtonTone.Secondary, -1f, 54f);

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

            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelTitle", "Results Summary", 44f, FontStyles.Bold, TextAlignmentOptions.Left, 58f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelSubtitle", "Turn the latest run into one clear scorecard first, then branch into the next action.", 20f, FontStyles.Normal, TextAlignmentOptions.Left, 44f, AppShellEditorCommon.MutedTextColor);

            Transform dashboardRow = AppShellEditorUi.CreateDashboardRow(panelTransform, "ResultsDashboardRow", 22f).transform;

            Transform summaryCard = AppShellEditorUi.CreateSectionCard(
                dashboardRow,
                "ResultsOverviewCard",
                AppShellEditorCommon.HeroSurfaceColor,
                24,
                24,
                10f,
                AppShellEditorCommon.HeroAccentColor).transform;

            AppShellEditorCommon.ConfigureLayoutElement(summaryCard.gameObject, -1f, 566f);
            AppShellEditorCommon.GetOrAddComponent<LayoutElement>(summaryCard.gameObject).flexibleWidth = 1f;
            AppShellEditorUi.CreateTextBlock(summaryCard, "ResultsTitle", "PERFORMANCE SCORECARD", 16f, FontStyles.Bold, TextAlignmentOptions.Left, 22f, AppShellEditorCommon.HeroAccentColor);
            AppShellEditorUi.CreateTextBlock(summaryCard, "ResultsLead", "Lead with the top-line score, then keep metrics and notes in shorter blocks.", 16f, FontStyles.Normal, TextAlignmentOptions.Left, 40f, AppShellEditorCommon.MutedTextColor);
            AppShellEditorUi.CreateTextBlock(summaryCard, "ScoreValue", "--", 44f, FontStyles.Bold, TextAlignmentOptions.Left, 110f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(summaryCard, "SummaryValue", "No session summary is available yet.", 18f, FontStyles.Normal, TextAlignmentOptions.Left, 110f, AppShellEditorCommon.TextColor);
            GameObject metricsCard = AppShellEditorUi.CreateSectionCard(summaryCard, "MetricsCard", AppShellEditorCommon.UtilitySurfaceColor, 18, 18, 8f, AppShellEditorCommon.WithAlpha(AppShellEditorCommon.AccentColor, 0.78f));
            AppShellEditorUi.CreateTextBlock(metricsCard.transform, "MetricsTitle", "KEY METRICS", 15f, FontStyles.Bold, TextAlignmentOptions.Left, 20f, AppShellEditorCommon.SoftAccentColor);
            AppShellEditorUi.CreateTextBlock(metricsCard.transform, "MetricsValue", "Eye Contact  Unavailable\nSpeech Pace  Unavailable\nPosture      Unavailable", 17f, FontStyles.Normal, TextAlignmentOptions.Left, 126f, AppShellEditorCommon.TextColor);
            GameObject notesCard = AppShellEditorUi.CreateSectionCard(summaryCard, "NotesCard", AppShellEditorCommon.ElevatedSurfaceColor, 18, 18, 8f, AppShellEditorCommon.HeroAccentColor);
            AppShellEditorUi.CreateTextBlock(notesCard.transform, "RecommendationsSectionTitle", "COACH NOTES", 15f, FontStyles.Bold, TextAlignmentOptions.Left, 20f, AppShellEditorCommon.SoftAccentColor);
            AppShellEditorUi.CreateTextBlock(notesCard.transform, "RecommendationsValue", "Recommendations will appear after a completed session.", 16f, FontStyles.Normal, TextAlignmentOptions.Left, 110f, AppShellEditorCommon.MutedTextColor);

            Transform actionCard = AppShellEditorUi.CreateSectionCard(
                dashboardRow,
                "ResultsActionCard",
                AppShellEditorCommon.UtilitySurfaceColor,
                20,
                20,
                10f,
                AppShellEditorCommon.AccentColor).transform;

            AppShellEditorCommon.ConfigureLayoutElement(actionCard.gameObject, 304f, 566f);
            AppShellEditorUi.CreateTextBlock(actionCard, "ResultsActionTitle", "NEXT ROUTE", 16f, FontStyles.Bold, TextAlignmentOptions.Left, 22f, AppShellEditorCommon.SoftAccentColor);
            AppShellEditorUi.CreateTextBlock(actionCard, "ResultsActionLead", "Keep the session context intact while routing the user forward.", 16f, FontStyles.Normal, TextAlignmentOptions.Left, 34f, AppShellEditorCommon.MutedTextColor);
            Button retryButton = AppShellEditorUi.CreateStyledButton(actionCard, "RetryButton", "Retry Setup", AppShellEditorUi.ButtonTone.Primary, -1f, 50f);
            AppShellEditorUi.CreateTextBlock(actionCard, "RetryInfo", "Launch the same setup again.", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 26f, AppShellEditorCommon.MutedTextColor);
            Button changeEnvironmentButton = AppShellEditorUi.CreateStyledButton(actionCard, "ChangeEnvironmentButton", "Change Environment", AppShellEditorUi.ButtonTone.Utility, -1f, 50f);
            AppShellEditorUi.CreateTextBlock(actionCard, "ChangeEnvironmentInfo", "Return to room selection with the current config.", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 26f, AppShellEditorCommon.MutedTextColor);
            Button dashboardButton = AppShellEditorUi.CreateStyledButton(actionCard, "DashboardButton", "Dashboard Entry", AppShellEditorUi.ButtonTone.Utility, -1f, 50f);
            AppShellEditorUi.CreateTextBlock(actionCard, "DashboardInfo", "Open deeper scoring when that adapter is ready.", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 26f, AppShellEditorCommon.MutedTextColor);
            Button hubButton = AppShellEditorUi.CreateStyledButton(actionCard, "ReturnToHubButton", "Return To Hub", AppShellEditorUi.ButtonTone.Secondary, -1f, 54f);
            TMP_Text statusLabel = AppShellEditorUi.CreateTextBlock(actionCard, "RouteStatusLabel", string.Empty, 16f, FontStyles.Italic, TextAlignmentOptions.Left, 82f, AppShellEditorCommon.MutedTextColor);

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

            AppPanelView resultsPanel = AppShellEditorUi.CreatePanelRoot(root, "ResultsPanel", AppPanelType.ResultsSummary, new Vector2(980f, 790f), new Vector2(0f, -20f), false);
            LayoutElement panelLayout = AppShellEditorCommon.GetOrAddComponent<LayoutElement>(resultsPanel.gameObject);
            panelLayout.preferredWidth = 980f;
            panelLayout.preferredHeight = 790f;
            ResultsSummaryPresenter resultsPresenter = AppShellEditorCommon.GetOrAddComponent<ResultsSummaryPresenter>(resultsPanel.gameObject);
            ResultsFlowController resultsFlowController = AppShellEditorCommon.GetOrAddComponent<ResultsFlowController>(resultsPanel.gameObject);
            DashboardAdapter dashboardAdapter = AppShellEditorCommon.GetOrAddComponent<DashboardAdapter>(resultsPanel.gameObject);

            BuildResultsPanel(resultsPanel.transform, resultsPresenter, resultsFlowController, dashboardAdapter);

            AppShellEditorCommon.SetField(resultsFlowController, "transitionManager", transitionManager);
            AppShellEditorCommon.SetField(resultsFlowController, "dashboardAdapter", dashboardAdapter);
            AppShellEditorCommon.SetField(resultsPresenter, "scoreLabel", AppShellEditorCommon.FindDescendantComponent<TMP_Text>(resultsPanel.transform, "ScoreValue"));
            AppShellEditorCommon.SetField(resultsPresenter, "summaryLabel", AppShellEditorCommon.FindDescendantComponent<TMP_Text>(resultsPanel.transform, "SummaryValue"));
            AppShellEditorCommon.SetField(resultsPresenter, "metricsLabel", AppShellEditorCommon.FindDescendantComponent<TMP_Text>(resultsPanel.transform, "MetricsValue"));
            AppShellEditorCommon.SetField(resultsPresenter, "recommendationsLabel", AppShellEditorCommon.FindDescendantComponent<TMP_Text>(resultsPanel.transform, "RecommendationsValue"));
        }

        private static TMP_Text BuildPauseOverlayPanel(Transform panelTransform, EnvironmentSessionOverlayController overlayController)
        {
            AppShellEditorUi.ClearGeneratedChildren(panelTransform);

            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelTitle", "Session Pause", 42f, FontStyles.Bold, TextAlignmentOptions.Left, 54f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateTextBlock(panelTransform, "PanelSubtitle", "Pause the live run safely, then decide whether to continue, restart, end, or leave the room.", 20f, FontStyles.Normal, TextAlignmentOptions.Left, 56f, AppShellEditorCommon.MutedTextColor);

            Transform dashboardRow = AppShellEditorUi.CreateDashboardRow(panelTransform, "PauseDashboardRow", 22f).transform;

            Transform summaryCard = AppShellEditorUi.CreateSectionCard(
                dashboardRow,
                "PauseSummaryCard",
                AppShellEditorCommon.HeroSurfaceColor,
                22,
                22,
                12f,
                AppShellEditorCommon.WarningAccentColor).transform;

            AppShellEditorCommon.ConfigureLayoutElement(summaryCard.gameObject, -1f, 430f);
            AppShellEditorCommon.GetOrAddComponent<LayoutElement>(summaryCard.gameObject).flexibleWidth = 1f;
            AppShellEditorUi.CreateTextBlock(summaryCard, "PauseBadge", "SESSION ON HOLD", 16f, FontStyles.Bold, TextAlignmentOptions.Left, 24f, AppShellEditorCommon.WarningAccentColor);
            AppShellEditorUi.CreateTextBlock(summaryCard, "PauseLead", "The environment stays visible while timing, tracking, and scoring remain frozen.", 18f, FontStyles.Normal, TextAlignmentOptions.Left, 52f, AppShellEditorCommon.MutedTextColor);
            TMP_Text pauseStatusLabel = AppShellEditorUi.CreateTextBlock(summaryCard, "PauseStatusLabel", "Resume to continue the same session. Restart or Return To Hub will exit this run safely.", 18f, FontStyles.Normal, TextAlignmentOptions.Left, 86f, AppShellEditorCommon.TextColor);
            AppShellEditorUi.CreateSummaryStrip(summaryCard, "PauseRuleA", "Session State", "Scoring is paused until you resume.");
            AppShellEditorUi.CreateSummaryStrip(summaryCard, "PauseRuleB", "Room Context", "The same environment remains loaded in the background.");
            AppShellEditorUi.CreateSummaryStrip(summaryCard, "PauseRuleC", "Safe Exit", "Restart or Return To Hub cancels this live run without opening results.");

            Transform actionCard = AppShellEditorUi.CreateSectionCard(
                dashboardRow,
                "PauseActionCard",
                AppShellEditorCommon.UtilitySurfaceColor,
                20,
                20,
                10f,
                AppShellEditorCommon.AccentColor).transform;

            AppShellEditorCommon.ConfigureLayoutElement(actionCard.gameObject, 288f, 430f);
            AppShellEditorUi.CreateTextBlock(actionCard, "PauseActionTitle", "CONTROLS", 16f, FontStyles.Bold, TextAlignmentOptions.Left, 22f, AppShellEditorCommon.SoftAccentColor);
            AppShellEditorUi.CreateTextBlock(actionCard, "PauseActionLead", "Use a focused set of actions that keep the live session consistent.", 16f, FontStyles.Normal, TextAlignmentOptions.Left, 52f, AppShellEditorCommon.MutedTextColor);
            Button resumeButton = AppShellEditorUi.CreateStyledButton(actionCard, "ResumeButton", "Resume", AppShellEditorUi.ButtonTone.Primary, -1f, 50f);
            AppShellEditorUi.CreateTextBlock(actionCard, "ResumeInfo", "Continue the same session from the paused timestamp.", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 38f, AppShellEditorCommon.MutedTextColor);
            Button restartButton = AppShellEditorUi.CreateStyledButton(actionCard, "RestartButton", "Restart Session", AppShellEditorUi.ButtonTone.Utility, -1f, 50f);
            AppShellEditorUi.CreateTextBlock(actionCard, "RestartInfo", "Reload this room and launch the same setup again.", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 38f, AppShellEditorCommon.MutedTextColor);
            Button endButton = AppShellEditorUi.CreateStyledButton(actionCard, "EndButton", "End Session", AppShellEditorUi.ButtonTone.Danger, -1f, 50f);
            AppShellEditorUi.CreateTextBlock(actionCard, "EndInfo", "Stop the current run and open the results overlay.", 15f, FontStyles.Normal, TextAlignmentOptions.Left, 38f, AppShellEditorCommon.MutedTextColor);
            Button hubButton = AppShellEditorUi.CreateStyledButton(actionCard, "HubButton", "Return To Hub", AppShellEditorUi.ButtonTone.Secondary, -1f, 54f);

            AppShellEditorCommon.SetButtonEvent(resumeButton, overlayController.ResumeSession);
            AppShellEditorCommon.SetButtonEvent(restartButton, overlayController.RestartSession);
            AppShellEditorCommon.SetButtonEvent(endButton, overlayController.EndSession);
            AppShellEditorCommon.SetButtonEvent(hubButton, overlayController.ReturnToHub);

            return pauseStatusLabel;
        }

        private static TMP_Dropdown CreateDropdownFieldCard(
            Transform parent,
            string name,
            string label,
            IReadOnlyList<string> options,
            float preferredWidth)
        {
            GameObject card = AppShellEditorUi.CreateSectionCard(parent, name, AppShellEditorCommon.UtilitySurfaceColor, 16, 16, 8f, AppShellEditorCommon.WithAlpha(AppShellEditorCommon.AccentColor, 0.72f));
            AppShellEditorCommon.ConfigureLayoutElement(card, preferredWidth, 96f);
            AppShellEditorUi.CreateTextBlock(card.transform, "FieldLabel", label.ToUpperInvariant(), 14f, FontStyles.Bold, TextAlignmentOptions.Left, 18f, AppShellEditorCommon.SoftAccentColor);
            return AppShellEditorUi.CreateDropdown(card.transform, "Dropdown", options);
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

        private static EnvironmentSessionOverlayController BuildInSessionHud(Transform parent, Scene scene)
        {
            Transform legacyHudRoot = parent.Find("InSessionHUD");
            if (legacyHudRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(legacyHudRoot.gameObject);
            }

            GameObject overlayRoot = AppShellEditorCommon.FindOrCreateChild(parent, "EnvironmentOverlayRoot");
            Canvas overlayCanvas = AppShellEditorCommon.GetOrAddComponent<Canvas>(overlayRoot);
            overlayCanvas.renderMode = RenderMode.WorldSpace;
            overlayCanvas.worldCamera = AppShellEditorCommon.FindSceneCamera(scene);

            CanvasScaler overlayScaler = AppShellEditorCommon.GetOrAddComponent<CanvasScaler>(overlayRoot);
            overlayScaler.dynamicPixelsPerUnit = 12f;
            overlayScaler.referencePixelsPerUnit = 100f;

            AppShellEditorCommon.GetOrAddComponent<GraphicRaycaster>(overlayRoot);
            AppShellEditorCommon.TryGetOrAddComponentByName(
                overlayRoot,
                "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");

            RectTransform overlayRect = overlayRoot.GetComponent<RectTransform>();
            overlayRect.sizeDelta = new Vector2(1540f, 1040f);
            overlayRoot.transform.localScale = new Vector3(0.00125f, 0.00125f, 0.00125f);

            WorldSpaceCanvasFollower overlayFollower = AppShellEditorCommon.GetOrAddComponent<WorldSpaceCanvasFollower>(overlayRoot);
            EnvironmentSessionOverlayController overlayController = AppShellEditorCommon.GetOrAddComponent<EnvironmentSessionOverlayController>(overlayRoot);

            GameObject dimmerRoot = AppShellEditorCommon.FindOrCreateChild(overlayRoot.transform, "Dimmer");
            RectTransform dimmerRect = dimmerRoot.GetComponent<RectTransform>();
            AppShellEditorCommon.ConfigureStretchRect(dimmerRect);
            Image dimmerImage = AppShellEditorCommon.GetOrAddComponent<Image>(dimmerRoot);
            AppShellEditorCommon.StyleSlicedImage(dimmerImage, AppShellEditorCommon.WithAlpha(AppShellEditorCommon.OverlayColor, 0.78f), false);
            CanvasGroup dimmerCanvasGroup = AppShellEditorCommon.GetOrAddComponent<CanvasGroup>(dimmerRoot);
            dimmerCanvasGroup.alpha = 0f;
            dimmerCanvasGroup.interactable = false;
            dimmerCanvasGroup.blocksRaycasts = false;
            AppShellEditorCommon.GetOrAddComponent<LayoutElement>(dimmerRoot).ignoreLayout = true;
            dimmerRoot.transform.SetAsFirstSibling();

            GameObject hudRoot = AppShellEditorCommon.FindOrCreateChild(overlayRoot.transform, "InSessionHUD");
            RectTransform hudRect = hudRoot.GetComponent<RectTransform>();
            AppShellEditorCommon.ConfigureRect(
                hudRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(460f, 140f),
                new Vector2(0f, -310f));

            CanvasGroup hudCanvasGroup = AppShellEditorCommon.GetOrAddComponent<CanvasGroup>(hudRoot);
            InSessionHudPresenter hudPresenter = AppShellEditorCommon.GetOrAddComponent<InSessionHudPresenter>(hudRoot);

            Image hudBackground = AppShellEditorCommon.GetOrAddComponent<Image>(hudRoot);
            AppShellEditorCommon.StyleSlicedImage(hudBackground, new Color(0.04f, 0.06f, 0.10f, 0.82f));
            AppShellEditorCommon.ApplyOutline(hudRoot, AppShellEditorCommon.WithAlpha(AppShellEditorCommon.AccentColor, 0.24f), new Vector2(1f, -1f));

            TMP_Text timerLabel = AppShellEditorUi.FindOrCreateHudLabel(hudRoot.transform, "TimerLabel", "05:00", 34f, new Vector2(0f, 18f));
            TMP_Text statusLabel = AppShellEditorUi.FindOrCreateHudLabel(hudRoot.transform, "StatusLabel", "Waiting for session start", 18f, new Vector2(0f, -24f));
            statusLabel.color = AppShellEditorCommon.MutedTextColor;

            GameObject warningRoot = AppShellEditorCommon.FindOrCreateChild(overlayRoot.transform, "LiveWarningPanel");
            RectTransform warningRootRect = warningRoot.GetComponent<RectTransform>();
            AppShellEditorCommon.ConfigureRect(
                warningRootRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(420f, 78f),
                Vector2.zero);
            CanvasGroup warningCanvasGroup = AppShellEditorCommon.GetOrAddComponent<CanvasGroup>(warningRoot);
            warningCanvasGroup.interactable = false;
            warningCanvasGroup.blocksRaycasts = false;
            Image warningBackground = AppShellEditorCommon.GetOrAddComponent<Image>(warningRoot);
            AppShellEditorCommon.StyleSlicedImage(warningBackground, new Color(0.08f, 0.11f, 0.15f, 0.92f), false);
            WorldSpaceCanvasFollower warningFollower = AppShellEditorCommon.GetOrAddComponent<WorldSpaceCanvasFollower>(warningRoot);
            AppShellEditorCommon.SetField(warningFollower, "offset", new Vector3(0f, -0.12f, 0.92f));
            AppShellEditorCommon.SetField(warningFollower, "yawOnly", false);
            AppShellEditorCommon.SetField(warningFollower, "keepUpright", true);
            AppShellEditorCommon.SetField(warningFollower, "positionLerpSpeed", 14f);
            AppShellEditorCommon.SetField(warningFollower, "rotationLerpSpeed", 16f);
            AppShellEditorCommon.SetField(warningFollower, "followContinuously", true);
            TMP_Text warningLabel = AppShellEditorUi.FindOrCreateHudLabel(warningRoot.transform, "WarningLabel", string.Empty, 15f, Vector2.zero);
            if (warningLabel.transform is RectTransform warningRect)
            {
                warningRect.sizeDelta = new Vector2(380f, 58f);
            }
            warningLabel.fontSize = 13f;
            warningLabel.color = AppShellEditorCommon.WarningAccentColor;
            warningLabel.textWrappingMode = TextWrappingModes.Normal;
            warningLabel.overflowMode = TextOverflowModes.Ellipsis;
            warningLabel.gameObject.SetActive(true);
            warningRoot.SetActive(false);

            AppPanelView pausePanel = AppShellEditorUi.CreatePanelRoot(
                overlayRoot.transform,
                "PauseOverlayPanel",
                AppPanelType.PauseOverlay,
                new Vector2(944f, 688f),
                new Vector2(0f, 6f));
            TMP_Text pauseStatusLabel = BuildPauseOverlayPanel(pausePanel.transform, overlayController);
            pausePanel.gameObject.SetActive(false);

            AppPanelView resultsPanel = AppShellEditorUi.CreatePanelRoot(
                overlayRoot.transform,
                "ResultsOverlayPanel",
                AppPanelType.ResultsSummary,
                new Vector2(980f, 790f),
                new Vector2(0f, -8f));
            ResultsSummaryPresenter resultsPresenter = AppShellEditorCommon.GetOrAddComponent<ResultsSummaryPresenter>(resultsPanel.gameObject);
            ResultsFlowController resultsFlowController = AppShellEditorCommon.GetOrAddComponent<ResultsFlowController>(resultsPanel.gameObject);
            DashboardAdapter dashboardAdapter = AppShellEditorCommon.GetOrAddComponent<DashboardAdapter>(resultsPanel.gameObject);
            BuildResultsPanel(resultsPanel.transform, resultsPresenter, resultsFlowController, dashboardAdapter);
            resultsPanel.gameObject.SetActive(false);

            AppShellEditorCommon.SetField(hudPresenter, "canvasGroup", hudCanvasGroup);
            AppShellEditorCommon.SetField(hudPresenter, "timerLabel", timerLabel);
            AppShellEditorCommon.SetField(hudPresenter, "statusLabel", statusLabel);
            AppShellEditorCommon.SetField(hudPresenter, "warningRoot", warningRootRect);
            AppShellEditorCommon.SetField(hudPresenter, "warningCanvasGroup", warningCanvasGroup);
            AppShellEditorCommon.SetField(hudPresenter, "warningFollower", warningFollower);
            AppShellEditorCommon.SetField(hudPresenter, "warningLabel", warningLabel);
            AppShellEditorCommon.SetField(hudPresenter, "inactiveStatusText", "Waiting for session start");
            AppShellEditorCommon.SetField(overlayFollower, "offset", new Vector3(0f, -0.16f, 1.58f));
            AppShellEditorCommon.SetField(overlayFollower, "yawOnly", false);
            AppShellEditorCommon.SetField(overlayFollower, "positionLerpSpeed", 0f);
            AppShellEditorCommon.SetField(overlayFollower, "rotationLerpSpeed", 0f);
            AppShellEditorCommon.SetField(overlayFollower, "followContinuously", false);

            AppShellEditorCommon.SetField(overlayController, "hudPresenter", hudPresenter);
            AppShellEditorCommon.SetField(overlayController, "overlayFollower", overlayFollower);
            AppShellEditorCommon.SetField(overlayController, "dimmerCanvasGroup", dimmerCanvasGroup);
            AppShellEditorCommon.SetField(overlayController, "pausePanel", pausePanel);
            AppShellEditorCommon.SetField(overlayController, "resultsPanel", resultsPanel);
            AppShellEditorCommon.SetField(overlayController, "resultsSummaryPresenter", resultsPresenter);
            AppShellEditorCommon.SetField(overlayController, "resultsFlowController", resultsFlowController);
            AppShellEditorCommon.SetField(overlayController, "pauseStatusLabel", pauseStatusLabel);

            AppShellEditorCommon.SetField(resultsFlowController, "dashboardAdapter", dashboardAdapter);
            AppShellEditorCommon.SetField(resultsFlowController, "environmentSessionOverlayController", overlayController);
            AppShellEditorCommon.SetField(resultsPresenter, "scoreLabel", AppShellEditorCommon.FindDescendantComponent<TMP_Text>(resultsPanel.transform, "ScoreValue"));
            AppShellEditorCommon.SetField(resultsPresenter, "summaryLabel", AppShellEditorCommon.FindDescendantComponent<TMP_Text>(resultsPanel.transform, "SummaryValue"));
            AppShellEditorCommon.SetField(resultsPresenter, "metricsLabel", AppShellEditorCommon.FindDescendantComponent<TMP_Text>(resultsPanel.transform, "MetricsValue"));
            AppShellEditorCommon.SetField(resultsPresenter, "recommendationsLabel", AppShellEditorCommon.FindDescendantComponent<TMP_Text>(resultsPanel.transform, "RecommendationsValue"));

            AppShellEditorCommon.MarkDirty(
                overlayRoot,
                overlayCanvas,
                overlayFollower,
                overlayController,
                dimmerCanvasGroup,
                hudPresenter,
                pausePanel,
                resultsPanel,
                resultsPresenter,
                resultsFlowController,
                dashboardAdapter);

            return overlayController;
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
            AppShellEditorCommon.SetField(resultsPresenter, "scoreLabel", AppShellEditorCommon.FindDescendantComponent<TMP_Text>(resultsPanel.transform, "ScoreValue"));
            AppShellEditorCommon.SetField(resultsPresenter, "summaryLabel", AppShellEditorCommon.FindDescendantComponent<TMP_Text>(resultsPanel.transform, "SummaryValue"));
            AppShellEditorCommon.SetField(resultsPresenter, "metricsLabel", AppShellEditorCommon.FindDescendantComponent<TMP_Text>(resultsPanel.transform, "MetricsValue"));
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
