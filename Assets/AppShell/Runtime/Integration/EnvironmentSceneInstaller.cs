using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;
using VRPublicSpeaking.AppShell.UI;

namespace VRPublicSpeaking.AppShell.Integration
{
    public class EnvironmentSceneInstaller : MonoBehaviour
    {
        [SerializeField] private AppRuntimeState runtimeState;
        [SerializeField] private PlayerRigAdapter playerRigAdapter;
        [SerializeField] private TrackingAdapter trackingAdapter;
        [SerializeField] private ScoringAdapter scoringAdapter;
        [SerializeField] private ExistingSceneFlowAdapter existingSceneFlowAdapter;
        [SerializeField] private EnvironmentSessionOverlayController environmentSessionOverlayController;
        [SerializeField] private bool installOnStart = true;

        private void Start()
        {
            if (installOnStart)
            {
                Install();
            }
        }

        [ContextMenu("Install Runtime Bindings")]
        public void Install()
        {
            if (runtimeState == null)
            {
                runtimeState = AppRuntimeState.GetOrCreate();
            }

            if (runtimeState == null)
            {
                Debug.LogWarning("[EnvironmentSceneInstaller] No AppRuntimeState found in the scene.");
                return;
            }

            SessionConfig config = runtimeState.GetSessionConfigCopy();

            playerRigAdapter ??= GetOrAdd<PlayerRigAdapter>();
            trackingAdapter ??= GetOrAdd<TrackingAdapter>();
            scoringAdapter ??= GetOrAdd<ScoringAdapter>();
            existingSceneFlowAdapter ??= GetOrAdd<ExistingSceneFlowAdapter>();
            environmentSessionOverlayController ??= GetComponentInChildren<EnvironmentSessionOverlayController>(true);
            MainController resolvedMainController = FindFirstObjectByType<MainController>(FindObjectsInactive.Include);

            EnsureEventSystemSupport();
            EnsureOverlayInputSupport();

            bool movedToSpawn = playerRigAdapter.TryMoveToSpawn(config.SelectedSpawnPointName);
            trackingAdapter.Apply(config);
            scoringAdapter.AutoWireIfNeeded();
            existingSceneFlowAdapter.AutoWireIfNeeded();
            existingSceneFlowAdapter.Configure(runtimeState, config);
            environmentSessionOverlayController?.Configure(
                runtimeState,
                resolvedMainController);

            if (!movedToSpawn)
            {
                Debug.LogWarning("[EnvironmentSceneInstaller] Player rig could not be moved to a valid spawn point.");
            }

            if (FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude) == null)
            {
                Debug.LogWarning("[EnvironmentSceneInstaller] No active camera was found in the environment scene.");
            }

            if (runtimeState.CurrentRuntimeState.SessionLaunchRequested && config.AutoStartOnSceneLoad)
            {
                existingSceneFlowAdapter.TryAutoStartSession();
            }
        }

        private T GetOrAdd<T>() where T : Component
        {
            T component = GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private void EnsureEventSystemSupport()
        {
            EventSystem eventSystem = FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (eventSystem == null)
            {
                GameObject eventSystemRoot = new GameObject("EventSystem");
                eventSystem = eventSystemRoot.AddComponent<EventSystem>();
            }

            Type inputSystemModuleType =
                Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

            if (inputSystemModuleType != null)
            {
                Component inputModule = eventSystem.GetComponent(inputSystemModuleType);
                if (inputModule == null)
                {
                    inputModule = eventSystem.gameObject.AddComponent(inputSystemModuleType);
                }

                EnsureDefaultInputSystemUiActions(inputModule);

                StandaloneInputModule standaloneModule = eventSystem.GetComponent<StandaloneInputModule>();
                if (standaloneModule != null)
                {
                    Destroy(standaloneModule);
                }

                return;
            }

            if (eventSystem.GetComponent<StandaloneInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }
        }

        private void EnsureOverlayInputSupport()
        {
            if (environmentSessionOverlayController == null)
            {
                return;
            }

            GameObject overlayRoot = environmentSessionOverlayController.gameObject;
            Canvas overlayCanvas = overlayRoot.GetComponent<Canvas>();
            if (overlayCanvas != null && overlayCanvas.worldCamera == null)
            {
                Camera activeCamera = FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
                if (activeCamera != null)
                {
                    overlayCanvas.worldCamera = activeCamera;
                }
            }

            if (overlayRoot.GetComponent<GraphicRaycaster>() == null)
            {
                overlayRoot.AddComponent<GraphicRaycaster>();
            }

            Type trackedRaycasterType =
                Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (trackedRaycasterType != null && overlayRoot.GetComponent(trackedRaycasterType) == null)
            {
                overlayRoot.AddComponent(trackedRaycasterType);
            }

            DisableLegacyCameraCanvasRaycasters(overlayRoot.transform);
        }

        private void DisableLegacyCameraCanvasRaycasters(Transform overlayRoot)
        {
            GraphicRaycaster[] raycasters = FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < raycasters.Length; index++)
            {
                GraphicRaycaster raycaster = raycasters[index];
                if (raycaster == null)
                {
                    continue;
                }

                if (overlayRoot != null && raycaster.transform.IsChildOf(overlayRoot))
                {
                    continue;
                }

                Canvas ownerCanvas = raycaster.GetComponent<Canvas>();
                if (ownerCanvas == null || ownerCanvas.renderMode != RenderMode.WorldSpace)
                {
                    continue;
                }

                Transform parent = ownerCanvas.transform.parent;
                if (parent == null || parent.GetComponentInParent<Camera>() == null)
                {
                    continue;
                }

                raycaster.enabled = false;

                Graphic[] graphics = ownerCanvas.GetComponentsInChildren<Graphic>(true);
                for (int graphicIndex = 0; graphicIndex < graphics.Length; graphicIndex++)
                {
                    graphics[graphicIndex].raycastTarget = false;
                }
            }
        }

        private static void EnsureDefaultInputSystemUiActions(Component inputModule)
        {
            if (inputModule == null)
            {
                return;
            }

            Type inputSystemUiInputModuleType = inputModule.GetType();
            var assignDefaultActionsMethod = inputSystemUiInputModuleType.GetMethod("AssignDefaultActions", Type.EmptyTypes);
            assignDefaultActionsMethod?.Invoke(inputModule, null);
        }
    }
}
