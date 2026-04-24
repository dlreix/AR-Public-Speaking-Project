using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using VRPublicSpeaking.AppShell.Data;

namespace VRPublicSpeaking.AppShell.Editor
{
    public static class AppShellSetupUtility
    {
        private const string ConfigFolderPath = "Assets/AppShell/Config";
        private const string CatalogAssetPath = ConfigFolderPath + "/DefaultEnvironmentCatalog.asset";
        private const string ScenesFolderPath = "Assets/Scenes";
        private const string PreviewFolderPath = "Assets/Materials";
        private const string EnvironmentScenePrefix = "Scene_";

        [MenuItem("Tools/VR Public Speaking/App Shell/Create Default Environment Catalog")]
        public static void CreateDefaultEnvironmentCatalog()
        {
            EnsureFolder(ConfigFolderPath);

            AppEnvironmentCatalog catalog =
                AssetDatabase.LoadAssetAtPath<AppEnvironmentCatalog>(CatalogAssetPath);

            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AppEnvironmentCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            }

            catalog.SetEnvironments(BuildEnvironmentDefinitions(catalog));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = catalog;

            Debug.Log(
                $"[AppShellSetupUtility] Environment catalog refreshed at '{CatalogAssetPath}'.");
        }

        [MenuItem("Tools/VR Public Speaking/App Shell/Add App Shell Scenes To Build Settings")]
        public static void AddAppShellScenesToBuildSettings()
        {
            var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            TryAddScene(buildScenes, FindScenePathByName("MainHubScene"));
            TryAddScene(buildScenes, FindScenePathByName("ResultsScene"));

            List<string> environmentScenePaths = FindEnvironmentScenePaths();
            for (int index = 0; index < environmentScenePaths.Count; index++)
            {
                TryAddScene(buildScenes, environmentScenePaths[index]);
            }

            EditorBuildSettings.scenes = buildScenes.ToArray();

            Debug.Log("[AppShellSetupUtility] App shell scenes have been added to build settings.");
        }

        private static List<AppEnvironmentDefinition> BuildEnvironmentDefinitions(AppEnvironmentCatalog existingCatalog)
        {
            List<string> scenePaths = FindEnvironmentScenePaths();
            var definitions = new List<AppEnvironmentDefinition>(scenePaths.Count);

            for (int index = 0; index < scenePaths.Count; index++)
            {
                string scenePath = scenePaths[index];
                string sceneName = Path.GetFileNameWithoutExtension(scenePath);
                string rawName = sceneName.StartsWith(EnvironmentScenePrefix, StringComparison.Ordinal)
                    ? sceneName.Substring(EnvironmentScenePrefix.Length)
                    : sceneName;
                string displayName = ObjectNames.NicifyVariableName(rawName.Replace("_", " "));

                var definition = new AppEnvironmentDefinition
                {
                    Id = MakeEnvironmentId(rawName),
                    DisplayName = displayName,
                    Description = BuildDefaultDescription(displayName, rawName),
                    SceneName = sceneName,
                    SpawnPointName = string.Empty,
                    PreviewSprite = LoadPreviewSprite(rawName),
                    Available = true,
                    RecommendedModeLabel = BuildRecommendedMode(rawName),
                    AudienceHint = BuildAudienceHint(rawName),
                    AvailabilityReason = string.Empty
                };

                MergeExistingValues(definition, FindExistingDefinition(existingCatalog, definition.Id, sceneName));
                definitions.Add(definition);
            }

            return definitions;
        }

        private static List<string> FindEnvironmentScenePaths()
        {
            var scenePaths = new List<string>();
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { ScenesFolderPath });

            for (int index = 0; index < sceneGuids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(sceneGuids[index]);
                string sceneName = Path.GetFileNameWithoutExtension(path);

                if (sceneName.StartsWith(EnvironmentScenePrefix, StringComparison.Ordinal))
                {
                    scenePaths.Add(path);
                }
            }

            scenePaths.Sort(StringComparer.OrdinalIgnoreCase);
            return scenePaths;
        }

        internal static string FindScenePathByName(string sceneName)
        {
            string[] sceneGuids = AssetDatabase.FindAssets($"{sceneName} t:Scene");

            for (int index = 0; index < sceneGuids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(sceneGuids[index]);
                if (Path.GetFileNameWithoutExtension(path) == sceneName)
                {
                    return path;
                }
            }

            return string.Empty;
        }

        internal static bool IsSceneInBuildSettings(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return false;
            }

            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            for (int index = 0; index < buildScenes.Length; index++)
            {
                EditorBuildSettingsScene buildScene = buildScenes[index];
                if (buildScene == null)
                {
                    continue;
                }

                if (buildScene.enabled && string.Equals(buildScene.path, scenePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void TryAddScene(List<EditorBuildSettingsScene> buildScenes, string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return;
            }

            for (int index = 0; index < buildScenes.Count; index++)
            {
                if (buildScenes[index].path == scenePath)
                {
                    return;
                }
            }

            buildScenes.Add(new EditorBuildSettingsScene(scenePath, true));
        }

        private static string MakeEnvironmentId(string rawName)
        {
            string sanitized = rawName.Replace("_", "-").Trim();
            return sanitized.ToLowerInvariant();
        }

        private static AppEnvironmentDefinition FindExistingDefinition(
            AppEnvironmentCatalog existingCatalog,
            string environmentId,
            string sceneName)
        {
            if (existingCatalog == null)
            {
                return null;
            }

            IReadOnlyList<AppEnvironmentDefinition> environments = existingCatalog.Environments;
            for (int index = 0; index < environments.Count; index++)
            {
                AppEnvironmentDefinition existing = environments[index];
                if (existing == null)
                {
                    continue;
                }

                if (existing.Id == environmentId || existing.SceneName == sceneName)
                {
                    return existing;
                }
            }

            return null;
        }

        private static void MergeExistingValues(AppEnvironmentDefinition definition, AppEnvironmentDefinition existing)
        {
            if (definition == null || existing == null)
            {
                return;
            }

            definition.Available = existing.Available;

            if (existing.PreviewSprite != null)
            {
                definition.PreviewSprite = existing.PreviewSprite;
            }

            if (!string.IsNullOrWhiteSpace(existing.SpawnPointName))
            {
                definition.SpawnPointName = existing.SpawnPointName;
            }

            if (!string.IsNullOrWhiteSpace(existing.Description) &&
                !IsLegacyGeneratedDescription(existing.Description, definition.DisplayName))
            {
                definition.Description = existing.Description;
            }

            if (!string.IsNullOrWhiteSpace(existing.AvailabilityReason))
            {
                definition.AvailabilityReason = existing.AvailabilityReason;
            }

            if (!string.IsNullOrWhiteSpace(existing.RecommendedModeLabel))
            {
                definition.RecommendedModeLabel = existing.RecommendedModeLabel;
            }

            if (!string.IsNullOrWhiteSpace(existing.AudienceHint))
            {
                definition.AudienceHint = existing.AudienceHint;
            }
        }

        private static Sprite LoadPreviewSprite(string rawName)
        {
            string environmentId = MakeEnvironmentId(rawName);
            string previewFileName;

            switch (environmentId)
            {
                case "conferencehall":
                    previewFileName = "ConferenceHallPreview.png";
                    break;

                case "meetingroom":
                    previewFileName = "MeetingRoomPreview.png";
                    break;

                default:
                    previewFileName = "ClassroomPreview.png";
                    break;
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>($"{PreviewFolderPath}/{previewFileName}");
        }

        private static bool IsLegacyGeneratedDescription(string description, string displayName)
        {
            string legacyDescription = $"Practice in the {displayName} environment.";
            return string.Equals(description?.Trim(), legacyDescription, StringComparison.Ordinal);
        }

        private static string BuildDefaultDescription(string displayName, string rawName)
        {
            switch (MakeEnvironmentId(rawName))
            {
                case "classroom":
                    return "Rehearse structured talks in a familiar learning space with clear sightlines and moderate audience pressure.";

                case "conferencehall":
                    return "Practice formal presentations in a larger venue where projection, pacing, and stage presence matter more.";

                case "meetingroom":
                    return "Prepare for stakeholder updates and team briefings in a smaller room built for conversational delivery.";

                default:
                    return $"Practice in the {displayName} environment.";
            }
        }

        private static string BuildRecommendedMode(string rawName)
        {
            switch (MakeEnvironmentId(rawName))
            {
                case "classroom":
                    return "Guided Practice";

                case "conferencehall":
                    return "Evaluation Mode";

                case "meetingroom":
                    return "Free Practice";

                default:
                    return string.Empty;
            }
        }

        private static string BuildAudienceHint(string rawName)
        {
            switch (MakeEnvironmentId(rawName))
            {
                case "classroom":
                    return "Best for confidence-building reps, lecture pacing, and steady eye-contact habits.";

                case "conferencehall":
                    return "Best for keynote-style delivery, stronger vocal projection, and larger-room pressure.";

                case "meetingroom":
                    return "Best for concise updates, conversational tone, and close-range audience feedback.";

                default:
                    return string.Empty;
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] segments = folderPath.Split('/');
            string currentPath = segments[0];

            for (int index = 1; index < segments.Length; index++)
            {
                string nextPath = currentPath + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[index]);
                }

                currentPath = nextPath;
            }
        }
    }
}
