using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRPublicSpeaking.AppShell.Data;

namespace VRPublicSpeaking.AppShell.UI
{
    public class EnvironmentCardView : MonoBehaviour
    {
        private const string DefaultMissingBadgeText = "Missing";
        private const string DefaultUnavailableBadgeText = "Unavailable";
        private const string DefaultMisconfiguredBadgeText = "Needs Setup";

        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private Image previewImage;
        [SerializeField] private Button selectButton;
        [SerializeField] private GameObject selectionHighlight;
        [SerializeField] private GameObject unavailableBadge;
        [SerializeField] private TMP_Text stateLabel;

        private AppEnvironmentDefinition environmentDefinition;
        private Action<EnvironmentCardView> onSelected;
        private bool isSelected;

        public AppEnvironmentDefinition EnvironmentDefinition => environmentDefinition;

        private void Awake()
        {
            if (selectButton != null)
            {
                selectButton.onClick.AddListener(NotifySelected);
            }
        }

        public void Bind(AppEnvironmentDefinition definition, bool selected, Action<EnvironmentCardView> selectionCallback)
        {
            environmentDefinition = definition;
            onSelected = selectionCallback;
            isSelected = selected;

            if (titleLabel != null)
            {
                titleLabel.text = definition?.DisplayName ?? "Unassigned";
            }

            if (descriptionLabel != null)
            {
                descriptionLabel.text = BuildDescription(definition);
            }

            if (previewImage != null)
            {
                previewImage.sprite = definition?.PreviewSprite;
                previewImage.enabled = definition?.PreviewSprite != null;
            }

            RefreshVisualState();
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;

            RefreshVisualState();
        }

        public void SetInteractable(bool interactable)
        {
            if (selectButton != null)
            {
                selectButton.interactable = interactable;
            }
        }

        private void RefreshVisualState()
        {
            if (selectionHighlight != null)
            {
                selectionHighlight.SetActive(isSelected && environmentDefinition != null && environmentDefinition.IsSelectable);
            }

            if (stateLabel != null)
            {
                stateLabel.text = BuildStateText();
            }

            TMP_Text badgeLabel = unavailableBadge != null ? unavailableBadge.GetComponent<TMP_Text>() : null;
            bool showBadge = false;
            string badgeText = string.Empty;
            bool interactable = false;
            string buttonText = "Select";

            if (environmentDefinition == null)
            {
                showBadge = true;
                badgeText = DefaultMissingBadgeText;
                buttonText = DefaultMissingBadgeText;
            }
            else if (!environmentDefinition.Available)
            {
                showBadge = true;
                badgeText = string.IsNullOrWhiteSpace(environmentDefinition.AvailabilityReason)
                    ? DefaultUnavailableBadgeText
                    : environmentDefinition.AvailabilityReason;
                buttonText = DefaultUnavailableBadgeText;
            }
            else if (environmentDefinition.IsMisconfigured)
            {
                showBadge = true;
                badgeText = DefaultMisconfiguredBadgeText;
                buttonText = DefaultMisconfiguredBadgeText;
            }
            else
            {
                interactable = true;
                buttonText = isSelected ? "Selected" : "Select";
            }

            if (badgeLabel != null)
            {
                badgeLabel.text = badgeText;
            }

            if (selectButton != null)
            {
                selectButton.interactable = interactable;

                TMP_Text buttonLabel = selectButton.GetComponentInChildren<TMP_Text>(true);
                if (buttonLabel != null)
                {
                    buttonLabel.text = buttonText;
                }
            }

            if (unavailableBadge != null)
            {
                unavailableBadge.SetActive(showBadge);
            }
        }

        private void NotifySelected()
        {
            onSelected?.Invoke(this);
        }

        private string BuildStateText()
        {
            if (environmentDefinition == null)
            {
                return "Missing";
            }

            if (!environmentDefinition.Available)
            {
                return "Unavailable";
            }

            if (environmentDefinition.IsMisconfigured)
            {
                return "Needs Setup";
            }

            return isSelected ? "Selected" : "Ready";
        }

        private static string BuildDescription(AppEnvironmentDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            string primary = CompactLine(definition.Description, 72);
            string secondary = !string.IsNullOrWhiteSpace(definition.RecommendedModeLabel)
                ? $"Mode: {CompactLine(definition.RecommendedModeLabel, 28)}"
                : (!string.IsNullOrWhiteSpace(definition.AudienceHint)
                    ? $"Audience: {CompactLine(definition.AudienceHint, 28)}"
                    : string.Empty);

            if (string.IsNullOrWhiteSpace(primary))
            {
                return secondary;
            }

            if (string.IsNullOrWhiteSpace(secondary))
            {
                return primary;
            }

            return $"{primary}\n{secondary}";
        }

        private static string CompactLine(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string compact = value.Replace("\r", " ").Replace("\n", " ").Trim();
            while (compact.Contains("  ", StringComparison.Ordinal))
            {
                compact = compact.Replace("  ", " ", StringComparison.Ordinal);
            }

            if (compact.Length <= maxLength)
            {
                return compact;
            }

            return compact.Substring(0, Math.Max(0, maxLength - 3)).TrimEnd() + "...";
        }
    }
}
