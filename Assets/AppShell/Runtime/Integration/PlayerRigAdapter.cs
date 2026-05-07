using UnityEngine;
using Unity.XR.CoreUtils;

namespace VRPublicSpeaking.AppShell.Integration
{
    public class PlayerRigAdapter : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;
        [SerializeField] private Transform explicitSpawnPoint;

        public bool TryMoveToSpawn(string requestedSpawnPointName = null)
        {
            if (playerController == null)
            {
                playerController = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            }

            if (playerController == null)
            {
                Debug.LogWarning("[PlayerRigAdapter] No PlayerController was found in the scene.");
                return false;
            }

            Transform spawnPoint = ResolveSpawnPoint(requestedSpawnPointName);
            if (spawnPoint == null)
            {
                Debug.LogWarning(
                    $"[PlayerRigAdapter] No spawn point could be resolved. Checked requested name '{requestedSpawnPointName}', then 'PlayerSpawnPoint', then 'SpawnPoint'.");
                return false;
            }

            Camera sceneCamera = VrRigRuntimeUtility.ResolveSceneCamera();
            XROrigin xrOrigin = VrRigRuntimeUtility.EnsureCameraInXrOrigin(sceneCamera, "[PlayerRigAdapter]");
            Transform rigTransform = xrOrigin != null ? xrOrigin.transform : playerController.transform;

            CharacterController characterController = rigTransform.GetComponent<CharacterController>();
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            rigTransform.position = spawnPoint.position;
            rigTransform.rotation = spawnPoint.rotation;

            if (characterController != null)
            {
                characterController.enabled = true;
            }

            return true;
        }

        private Transform ResolveSpawnPoint(string requestedSpawnPointName)
        {
            if (explicitSpawnPoint != null)
            {
                return explicitSpawnPoint;
            }

            if (!string.IsNullOrWhiteSpace(requestedSpawnPointName))
            {
                GameObject namedSpawn = GameObject.Find(requestedSpawnPointName);
                if (namedSpawn != null)
                {
                    return namedSpawn.transform;
                }

                Debug.LogWarning($"[PlayerRigAdapter] Requested spawn point '{requestedSpawnPointName}' was not found.");
            }

            GameObject playerSpawnPoint = GameObject.Find("PlayerSpawnPoint");
            if (playerSpawnPoint != null)
            {
                return playerSpawnPoint.transform;
            }

            GameObject spawnPoint = GameObject.Find("SpawnPoint");
            return spawnPoint != null ? spawnPoint.transform : null;
        }
    }
}
