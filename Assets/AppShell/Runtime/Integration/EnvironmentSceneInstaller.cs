using UnityEngine;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;

namespace VRPublicSpeaking.AppShell.Integration
{
    public class EnvironmentSceneInstaller : MonoBehaviour
    {
        [SerializeField] private AppRuntimeState runtimeState;
        [SerializeField] private PlayerRigAdapter playerRigAdapter;
        [SerializeField] private TrackingAdapter trackingAdapter;
        [SerializeField] private ScoringAdapter scoringAdapter;
        [SerializeField] private ExistingSceneFlowAdapter existingSceneFlowAdapter;
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

            bool movedToSpawn = playerRigAdapter.TryMoveToSpawn(config.SelectedSpawnPointName);
            trackingAdapter.Apply(config);
            scoringAdapter.AutoWireIfNeeded();
            existingSceneFlowAdapter.AutoWireIfNeeded();
            existingSceneFlowAdapter.Configure(runtimeState, config);

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
    }
}
