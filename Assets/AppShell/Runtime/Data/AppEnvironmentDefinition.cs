using System;
using UnityEngine;

namespace VRPublicSpeaking.AppShell.Data
{
    [Serializable]
    public class AppEnvironmentDefinition
    {
        [SerializeField] private string id = "environment";
        [SerializeField] private string displayName = "Environment";
        [SerializeField] [TextArea(2, 4)] private string description = "Configure this environment in the hub.";
        [SerializeField] private string sceneName = string.Empty;
        [SerializeField] private string spawnPointName = string.Empty;
        [SerializeField] private Sprite previewSprite;
        [SerializeField] private bool available = true;
        [SerializeField] private string availabilityReason = string.Empty;
        [SerializeField] private string recommendedModeLabel = string.Empty;
        [SerializeField] [TextArea(2, 3)] private string audienceHint = string.Empty;

        public string Id
        {
            get => id;
            set => id = string.IsNullOrWhiteSpace(value) ? "environment" : value;
        }

        public string DisplayName
        {
            get => displayName;
            set => displayName = string.IsNullOrWhiteSpace(value) ? "Environment" : value;
        }

        public string Description
        {
            get => description;
            set => description = value ?? string.Empty;
        }

        public string SceneName
        {
            get => sceneName;
            set => sceneName = value ?? string.Empty;
        }

        public string SpawnPointName
        {
            get => spawnPointName;
            set => spawnPointName = value ?? string.Empty;
        }

        public Sprite PreviewSprite
        {
            get => previewSprite;
            set => previewSprite = value;
        }

        public bool Available
        {
            get => available;
            set => available = value;
        }

        public string AvailabilityReason
        {
            get => availabilityReason;
            set => availabilityReason = value ?? string.Empty;
        }

        public string RecommendedModeLabel
        {
            get => recommendedModeLabel;
            set => recommendedModeLabel = value ?? string.Empty;
        }

        public string AudienceHint
        {
            get => audienceHint;
            set => audienceHint = value ?? string.Empty;
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(sceneName);
        public bool IsSelectable => Available && IsConfigured;
        public bool IsMisconfigured => Available && !IsConfigured;

        public AppEnvironmentDefinition Clone()
        {
            return (AppEnvironmentDefinition)MemberwiseClone();
        }
    }
}
