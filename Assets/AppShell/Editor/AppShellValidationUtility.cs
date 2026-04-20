using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;
using VRPublicSpeaking.AppShell.Flow;
using VRPublicSpeaking.AppShell.Integration;
using VRPublicSpeaking.AppShell.Results;
using VRPublicSpeaking.AppShell.UI;

namespace VRPublicSpeaking.AppShell.Editor
{
    public static class AppShellValidationUtility
    {
        [MenuItem("Tools/VR Public Speaking/App Shell/Validate App Shell")]
        public static void ValidateAppShellMenu()
        {
            ValidateAppShell();
        }

        public static bool ValidateAppShell(bool logSuccess = true)
        {
            var warnings = new List<string>();
            var openedScenes = new List<Scene>();

            try
            {
                ValidateCatalog(warnings);
                ValidateMainHubScene(warnings, openedScenes);
                ValidateResultsScene(warnings, openedScenes);
                ValidateEnvironmentScenes(warnings, openedScenes);
            }
            finally
            {
                CloseOpenedScenes(openedScenes);
            }

            for (int index = 0; index < warnings.Count; index++)
            {
                Debug.LogWarning($"[AppShellValidation] {warnings[index]}");
            }

            if (warnings.Count == 0)
            {
                if (logSuccess)
                {
                    Debug.Log("[AppShellValidation] App shell validation passed without warnings.");
                }

                return true;
            }

            if (logSuccess)
            {
                Debug.LogWarning($"[AppShellValidation] Validation completed with {warnings.Count} warning(s).");
            }

            return false;
        }

        private static void ValidateCatalog(List<string> warnings)
        {
            AppEnvironmentCatalog catalog = AppShellEditorCommon.LoadEnvironmentCatalog();
            if (catalog == null)
            {
                warnings.Add("Environment catalog is missing at 'Assets/AppShell/Config/DefaultEnvironmentCatalog.asset'.");
                return;
            }

            IReadOnlyList<AppEnvironmentDefinition> environments = catalog.Environments;
            if (environments == null || environments.Count == 0)
            {
                warnings.Add("Environment catalog does not contain any environment definitions.");
                return;
            }

            for (int index = 0; index < environments.Count; index++)
            {
                AppEnvironmentDefinition environment = environments[index];
                if (environment == null)
                {
                    warnings.Add($"Environment catalog entry #{index} is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(environment.SceneName))
                {
                    warnings.Add($"Environment '{environment.DisplayName}' does not reference a scene name.");
                    continue;
                }

                string scenePath = AppShellSetupUtility.FindScenePathByName(environment.SceneName);
                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    warnings.Add(
                        $"Environment '{environment.DisplayName}' points to missing scene '{environment.SceneName}'.");
                    continue;
                }

                if (!AppShellSetupUtility.IsSceneInBuildSettings(scenePath))
                {
                    warnings.Add(
                        $"Environment '{environment.DisplayName}' points to '{environment.SceneName}', but that scene is not enabled in build settings.");
                }
            }
        }

        private static void ValidateMainHubScene(List<string> warnings, List<Scene> openedScenes)
        {
            string scenePath = ResolveScenePath("MainHubScene", AppShellEditorCommon.MainHubScenePath);
            ValidateBuildSettingsEntry(scenePath, "MainHubScene", warnings);

            Scene scene = OpenSceneForValidation(scenePath, openedScenes, warnings, "MainHubScene");
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            AppFlowManager appFlowManager = RequireSceneComponent<AppFlowManager>(
                scene,
                "MainHubScene is missing AppFlowManager.",
                warnings);

            RequireSceneComponent<HomePanelPresenter>(scene, "MainHubScene is missing HomePanelPresenter.", warnings);
            RequireSceneComponent<PracticeModePanelPresenter>(
                scene,
                "MainHubScene is missing PracticeModePanelPresenter.",
                warnings);
            RequireSceneComponent<EnvironmentSelectionController>(
                scene,
                "MainHubScene is missing EnvironmentSelectionController.",
                warnings);
            RequireSceneComponent<SessionConfigController>(
                scene,
                "MainHubScene is missing SessionConfigController.",
                warnings);
            RequireSceneComponent<ReadyPanelPresenter>(
                scene,
                "MainHubScene is missing ReadyPanelPresenter.",
                warnings);
            RequireSceneComponent<ProgressPanelPresenter>(
                scene,
                "MainHubScene is missing ProgressPanelPresenter.",
                warnings);
            RequireSceneComponent<SettingsPanelPresenter>(
                scene,
                "MainHubScene is missing SettingsPanelPresenter.",
                warnings);
            RequireSceneComponent<ResultsSummaryPresenter>(
                scene,
                "MainHubScene is missing ResultsSummaryPresenter.",
                warnings);
            RequireSceneComponent<ResultsFlowController>(
                scene,
                "MainHubScene is missing ResultsFlowController.",
                warnings);

            UIStateController uiStateController = FindSceneComponent<UIStateController>(scene);
            if (uiStateController == null)
            {
                warnings.Add("MainHubScene is missing UIStateController.");
            }
            else
            {
                IList panels = GetFieldValue<IList>(uiStateController, "panels");
                if (panels == null || panels.Count == 0)
                {
                    warnings.Add("MainHubScene UIStateController has no configured panel list.");
                }
            }

            if (appFlowManager == null)
            {
                return;
            }

            ValidateFieldReference(
                appFlowManager,
                "uiStateController",
                "MainHubScene AppFlowManager is missing its UIStateController reference.",
                warnings);
            ValidateFieldReference(
                appFlowManager,
                "environmentSelectionController",
                "MainHubScene AppFlowManager is missing its EnvironmentSelectionController reference.",
                warnings);
            ValidateFieldReference(
                appFlowManager,
                "sessionConfigController",
                "MainHubScene AppFlowManager is missing its SessionConfigController reference.",
                warnings);
            ValidateFieldReference(
                appFlowManager,
                "readyPanelPresenter",
                "MainHubScene AppFlowManager is missing its ReadyPanelPresenter reference.",
                warnings);
            ValidateFieldReference(
                appFlowManager,
                "progressPanelPresenter",
                "MainHubScene AppFlowManager is missing its ProgressPanelPresenter reference.",
                warnings);
            ValidateFieldReference(
                appFlowManager,
                "settingsPanelPresenter",
                "MainHubScene AppFlowManager is missing its SettingsPanelPresenter reference.",
                warnings);
            ValidateFieldReference(
                appFlowManager,
                "resultsSummaryPresenter",
                "MainHubScene AppFlowManager is missing its ResultsSummaryPresenter reference.",
                warnings);
            ValidateFieldReference(
                appFlowManager,
                "sessionLaunchController",
                "MainHubScene AppFlowManager is missing its SessionLaunchController reference.",
                warnings);
        }

        private static void ValidateResultsScene(List<string> warnings, List<Scene> openedScenes)
        {
            string scenePath = ResolveScenePath("ResultsScene", AppShellEditorCommon.ResultsScenePath);
            ValidateBuildSettingsEntry(scenePath, "ResultsScene", warnings);

            Scene scene = OpenSceneForValidation(scenePath, openedScenes, warnings, "ResultsScene");
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            RequireSceneComponent<TransitionManager>(scene, "ResultsScene is missing TransitionManager.", warnings);
            RequireSceneComponent<ShellSceneRigController>(
                scene,
                "ResultsScene is missing ShellSceneRigController.",
                warnings);

            ResultsSummaryPresenter summaryPresenter = RequireSceneComponent<ResultsSummaryPresenter>(
                scene,
                "ResultsScene is missing ResultsSummaryPresenter.",
                warnings);
            ResultsFlowController flowController = RequireSceneComponent<ResultsFlowController>(
                scene,
                "ResultsScene is missing ResultsFlowController.",
                warnings);
            RequireSceneComponent<DashboardAdapter>(scene, "ResultsScene is missing DashboardAdapter.", warnings);

            ValidateFieldReference(
                summaryPresenter,
                "summaryLabel",
                "ResultsScene ResultsSummaryPresenter is missing summaryLabel wiring.",
                warnings);
            ValidateFieldReference(
                summaryPresenter,
                "recommendationsLabel",
                "ResultsScene ResultsSummaryPresenter is missing recommendationsLabel wiring.",
                warnings);
            ValidateFieldReference(
                flowController,
                "statusLabel",
                "ResultsScene ResultsFlowController is missing statusLabel wiring.",
                warnings);
            ValidateFieldReference(
                flowController,
                "dashboardAdapter",
                "ResultsScene ResultsFlowController is missing DashboardAdapter wiring.",
                warnings);
        }

        private static void ValidateEnvironmentScenes(List<string> warnings, List<Scene> openedScenes)
        {
            AppEnvironmentCatalog catalog = AppShellEditorCommon.LoadEnvironmentCatalog();
            List<string> scenePaths = AppShellEditorCommon.FindEnvironmentScenePaths();
            if (scenePaths.Count == 0)
            {
                warnings.Add("No environment scenes were found under 'Assets/Scenes'.");
                return;
            }

            for (int index = 0; index < scenePaths.Count; index++)
            {
                string scenePath = scenePaths[index];
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                ValidateBuildSettingsEntry(scenePath, $"Environment scene '{sceneName}'", warnings);

                Scene scene = OpenSceneForValidation(scenePath, openedScenes, warnings, $"Environment scene '{sceneName}'");
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                if (!SceneHasRoot(scene, "AppShellSceneBindings"))
                {
                    warnings.Add($"Environment scene '{sceneName}' is missing the AppShellSceneBindings root.");
                }

                RequireSceneComponent<EnvironmentSceneInstaller>(
                    scene,
                    $"Environment scene '{sceneName}' is missing EnvironmentSceneInstaller.",
                    warnings);
                RequireSceneComponent<ExistingSceneFlowAdapter>(
                    scene,
                    $"Environment scene '{sceneName}' is missing ExistingSceneFlowAdapter.",
                    warnings);
                RequireSceneComponent<PlayerRigAdapter>(
                    scene,
                    $"Environment scene '{sceneName}' is missing PlayerRigAdapter.",
                    warnings);
                RequireSceneComponent<InSessionHudPresenter>(
                    scene,
                    $"Environment scene '{sceneName}' is missing InSessionHudPresenter.",
                    warnings);
                RequireSceneComponent<MainController>(
                    scene,
                    $"Environment scene '{sceneName}' is missing MainController.",
                    warnings);
                RequireSceneComponent<PlayerController>(
                    scene,
                    $"Environment scene '{sceneName}' is missing PlayerController.",
                    warnings);

                ValidateSpawnPoints(scene, catalog, warnings);
            }
        }

        private static void ValidateSpawnPoints(Scene scene, AppEnvironmentCatalog catalog, List<string> warnings)
        {
            bool hasFallbackSpawn =
                SceneHasTransform(scene, "PlayerSpawnPoint") ||
                SceneHasTransform(scene, "SpawnPoint");

            if (!hasFallbackSpawn)
            {
                warnings.Add(
                    $"Environment scene '{scene.name}' is missing both 'PlayerSpawnPoint' and 'SpawnPoint'.");
            }

            if (catalog == null || !catalog.TryGetEnvironmentBySceneName(scene.name, out AppEnvironmentDefinition environmentDefinition))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(environmentDefinition.SpawnPointName) &&
                !SceneHasTransform(scene, environmentDefinition.SpawnPointName))
            {
                warnings.Add(
                    $"Environment scene '{scene.name}' expects spawn '{environmentDefinition.SpawnPointName}' from the catalog, but that transform was not found.");
            }
        }

        private static Scene OpenSceneForValidation(
            string scenePath,
            List<Scene> openedScenes,
            List<string> warnings,
            string label)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                warnings.Add($"{label} could not be found on disk.");
                return default;
            }

            Scene existingScene = SceneManager.GetSceneByPath(scenePath);
            if (existingScene.IsValid() && existingScene.isLoaded)
            {
                return existingScene;
            }

            Scene loadedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            openedScenes.Add(loadedScene);
            return loadedScene;
        }

        private static void CloseOpenedScenes(List<Scene> openedScenes)
        {
            for (int index = openedScenes.Count - 1; index >= 0; index--)
            {
                Scene scene = openedScenes[index];
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ValidateBuildSettingsEntry(string scenePath, string label, List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                warnings.Add($"{label} could not be resolved, so build-settings validation could not run.");
                return;
            }

            if (!AppShellSetupUtility.IsSceneInBuildSettings(scenePath))
            {
                warnings.Add($"{label} is missing from enabled build settings.");
            }
        }

        private static string ResolveScenePath(string sceneName, string fallbackPath)
        {
            string scenePath = AppShellSetupUtility.FindScenePathByName(sceneName);
            return string.IsNullOrWhiteSpace(scenePath) ? fallbackPath : scenePath;
        }

        private static T RequireSceneComponent<T>(Scene scene, string warning, List<string> warnings) where T : Component
        {
            T component = FindSceneComponent<T>(scene);
            if (component == null)
            {
                warnings.Add(warning);
            }

            return component;
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                T component = roots[index].GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static bool SceneHasRoot(Scene scene, string rootName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                if (roots[index].name == rootName)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SceneHasTransform(Scene scene, string transformName)
        {
            if (string.IsNullOrWhiteSpace(transformName))
            {
                return false;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                Transform[] transforms = roots[index].GetComponentsInChildren<Transform>(true);
                for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                {
                    if (transforms[transformIndex].name == transformName)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void ValidateFieldReference(object target, string fieldName, string warning, List<string> warnings)
        {
            if (target == null)
            {
                return;
            }

            object value = GetFieldValue<object>(target, fieldName);
            if (value == null)
            {
                warnings.Add(warning);
            }
        }

        private static T GetFieldValue<T>(object target, string fieldName)
        {
            if (target == null)
            {
                return default;
            }

            Type fieldOwner = target.GetType();
            FieldInfo fieldInfo = null;

            while (fieldOwner != null && fieldInfo == null)
            {
                fieldInfo = fieldOwner.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                fieldOwner = fieldOwner.BaseType;
            }

            if (fieldInfo == null)
            {
                return default;
            }

            return (T)fieldInfo.GetValue(target);
        }
    }
}
