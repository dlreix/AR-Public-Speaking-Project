using System.Collections.Generic;
using UnityEngine;

namespace VRPublicSpeaking.AppShell.Data
{
    [CreateAssetMenu(
        fileName = "AppEnvironmentCatalog",
        menuName = "VR Public Speaking/App Shell/Environment Catalog")]
    public class AppEnvironmentCatalog : ScriptableObject
    {
        [SerializeField] private List<AppEnvironmentDefinition> environments = new List<AppEnvironmentDefinition>();

        public IReadOnlyList<AppEnvironmentDefinition> Environments => environments;

        public void SetEnvironments(IEnumerable<AppEnvironmentDefinition> values)
        {
            environments.Clear();

            if (values == null)
            {
                return;
            }

            foreach (AppEnvironmentDefinition value in values)
            {
                if (value != null)
                {
                    environments.Add(value.Clone());
                }
            }
        }

        public bool TryGetEnvironmentById(string environmentId, out AppEnvironmentDefinition environmentDefinition)
        {
            for (int index = 0; index < environments.Count; index++)
            {
                AppEnvironmentDefinition candidate = environments[index];
                if (candidate != null && candidate.Id == environmentId)
                {
                    environmentDefinition = candidate.Clone();
                    return true;
                }
            }

            environmentDefinition = null;
            return false;
        }

        public bool TryGetEnvironmentBySceneName(string sceneName, out AppEnvironmentDefinition environmentDefinition)
        {
            for (int index = 0; index < environments.Count; index++)
            {
                AppEnvironmentDefinition candidate = environments[index];
                if (candidate != null && candidate.SceneName == sceneName)
                {
                    environmentDefinition = candidate.Clone();
                    return true;
                }
            }

            environmentDefinition = null;
            return false;
        }
    }
}
