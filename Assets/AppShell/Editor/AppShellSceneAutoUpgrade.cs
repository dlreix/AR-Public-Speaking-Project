using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

namespace VRPublicSpeaking.AppShell.Editor
{
    [InitializeOnLoad]
    internal static class AppShellSceneAutoUpgrade
    {
        private const string LegacySubtitle = "Choose a product entry point and continue into a complete VR practice flow.";

        static AppShellSceneAutoUpgrade()
        {
            EditorApplication.delayCall += TryUpgradeOpenAppShellScene;
        }

        private static void TryUpgradeOpenAppShellScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (AppShellXrRigPoseUtility.FixActiveScene(saveScene: true))
            {
                Debug.Log("[AppShell] XR headset camera bindings were normalized for the active scene.");
            }

            if (activeScene.path == AppShellEditorCommon.MainHubScenePath)
            {
                if (!NeedsMainHubUpgrade())
                {
                    return;
                }

                AppShellSceneGenerator.CreateOrUpdateMainHubScene();
                EditorSceneManager.OpenScene(AppShellEditorCommon.MainHubScenePath, OpenSceneMode.Single);

                Debug.Log("[AppShell] Legacy MainHubScene layout detected and upgraded to the current soft dashboard shell.");
                return;
            }

            if (activeScene.path != AppShellEditorCommon.ResultsScenePath || !NeedsResultsSceneUpgrade())
            {
                return;
            }

            AppShellSceneGenerator.CreateOrUpdateResultsScene();
            EditorSceneManager.OpenScene(AppShellEditorCommon.ResultsScenePath, OpenSceneMode.Single);
            Debug.Log("[AppShell] Legacy ResultsScene layout detected and upgraded to the current soft results shell.");
        }

        private static bool NeedsMainHubUpgrade()
        {
            GameObject homePanel = GameObject.Find("HomePanel");
            if (homePanel == null)
            {
                return false;
            }

            Transform dashboardRow = AppShellEditorCommon.FindDescendant(homePanel.transform, "DashboardRow");
            if (dashboardRow != null)
            {
                if (AppShellEditorCommon.FindDescendant(homePanel.transform, "SettingsInfo") == null)
                {
                    return true;
                }

                GameObject practicePanel = GameObject.Find("PracticeModePanel");
                if (practicePanel != null && AppShellEditorCommon.FindDescendant(practicePanel.transform, "GuidedPracticeCard") == null)
                {
                    return true;
                }

                GameObject settingsPanel = GameObject.Find("SettingsPanel");
                if (settingsPanel != null && AppShellEditorCommon.FindDescendant(settingsPanel.transform, "ComfortEntryInfo") == null)
                {
                    return true;
                }

                GameObject environmentPanel = GameObject.Find("EnvironmentSelectionPanel");
                if (environmentPanel != null)
                {
                    Transform environmentGrid = AppShellEditorCommon.FindDescendant(environmentPanel.transform, "EnvironmentCardGrid");
                    GridLayoutGroup environmentGridLayout = environmentGrid != null ? environmentGrid.GetComponent<GridLayoutGroup>() : null;
                    if (environmentGridLayout != null && environmentGridLayout.cellSize.y > 300f)
                    {
                        return true;
                    }
                }

                GameObject progressPanel = GameObject.Find("ProgressPanel");
                if (progressPanel != null && AppShellEditorCommon.FindDescendant(progressPanel.transform, "OpenSummaryInfo") == null)
                {
                    return true;
                }

                GameObject resultsSummaryPanel = GameObject.Find("ResultsSummaryPanel");
                if (resultsSummaryPanel != null && AppShellEditorCommon.FindDescendant(resultsSummaryPanel.transform, "RetryInfo") == null)
                {
                    return true;
                }

                GameObject hubPlayerRig = GameObject.Find("HubPlayerRig");
                if (hubPlayerRig != null && hubPlayerRig.GetComponent<PlayerController>() == null)
                {
                    return true;
                }

                if (hubPlayerRig != null && hubPlayerRig.GetComponent<XROrigin>() == null)
                {
                    Camera sceneCamera = AppShellEditorCommon.FindSceneCamera(SceneManager.GetActiveScene());
                    if (sceneCamera != null && sceneCamera.transform.root == hubPlayerRig.transform)
                    {
                        return true;
                    }
                }

                HorizontalLayoutGroup rowLayout = dashboardRow.GetComponent<HorizontalLayoutGroup>();
                if (rowLayout != null && rowLayout.childControlWidth)
                {
                    return false;
                }

                return true;
            }

            TMP_Text subtitle = AppShellEditorCommon.FindDescendantComponent<TMP_Text>(homePanel.transform, "PanelSubtitle");
            if (subtitle != null && subtitle.text == LegacySubtitle)
            {
                return true;
            }

            return AppShellEditorCommon.FindDescendant(homePanel.transform, "StartPracticeButton") != null;
        }

        private static bool NeedsResultsSceneUpgrade()
        {
            GameObject resultsPanel = GameObject.Find("ResultsPanel");
            if (resultsPanel == null)
            {
                return false;
            }

            return AppShellEditorCommon.FindDescendant(resultsPanel.transform, "RetryInfo") == null;
        }
    }
}
