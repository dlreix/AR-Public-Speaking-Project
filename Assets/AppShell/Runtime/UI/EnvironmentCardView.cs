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
        [SerializeField] private TMP_Text badgeLabel;
        [SerializeField] private Image badgeBackground;
        [SerializeField] private TMP_Text stateLabel;
        [SerializeField] private Image cardBackground;
        [SerializeField] private TMP_Text buttonLabel;

        private AppEnvironmentDefinition environmentDefinition;
        private Action<EnvironmentCardView> onSelected;
        private bool isSelected;

        private static readonly Color DefaultSurfaceColor = new Color(0.11f, 0.15f, 0.21f, 0.98f);
        private static readonly Color SelectedSurfaceColor = new Color(0.14f, 0.20f, 0.31f, 0.99f);
        private static readonly Color DisabledSurfaceColor = new Color(0.10f, 0.12f, 0.16f, 0.96f);
        private static readonly Color WarningSurfaceColor = new Color(0.24f, 0.18f, 0.14f, 0.98f);
        private static readonly Color ReadyAccentColor = new Color(0.72f, 0.84f, 0.96f, 1f);
        private static readonly Color SelectedAccentColor = new Color(0.32f, 0.72f, 1f, 1f);
        private static readonly Color WarningAccentColor = new Color(0.98f, 0.74f, 0.39f, 1f);
        private static readonly Color DisabledAccentColor = new Color(0.53f, 0.59f, 0.66f, 1f);
        private static readonly Color BadgeReadyColor = new Color(0.17f, 0.23f, 0.32f, 0.98f);
        private static readonly Color BadgeWarningColor = new Color(0.34f, 0.23f, 0.14f, 0.98f);
        private static readonly Color BadgeDisabledColor = new Color(0.18f, 0.19f, 0.23f, 0.98f);
        private static readonly Color PrimaryButtonColor = new Color(0.21f, 0.63f, 0.96f, 1f);
        private static readonly Color SuccessButtonColor = new Color(0.31f, 0.67f, 0.56f, 1f);
        private static readonly Color DisabledButtonColor = new Color(0.20f, 0.24f, 0.30f, 0.98f);

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
                if (definition?.PreviewSprite != null)
                {
                    previewImage.sprite = definition.PreviewSprite;
                    previewImage.type = Image.Type.Simple;
                    previewImage.preserveAspect = true;
                    previewImage.color = Color.white;
                }
                else
                {
                    previewImage.sprite = null;
                    previewImage.type = Image.Type.Sliced;
                    previewImage.preserveAspect = false;
                    previewImage.color = new Color(0.17f, 0.22f, 0.29f, 1f);
                }
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
            string badgeText = string.Empty;
            string buttonText = "Select Room";
            bool showBadge = false;
            bool interactable = false;
            Color stateColor = ReadyAccentColor;
            Color surfaceColor = DefaultSurfaceColor;
            Color badgeColor = BadgeReadyColor;
            Color buttonColor = DisabledButtonColor;

            if (selectionHighlight != null)
            {
                selectionHighlight.SetActive(isSelected && environmentDefinition != null && environmentDefinition.IsSelectable);
            }

            if (environmentDefinition == null)
            {
                showBadge = true;
                badgeText = DefaultMissingBadgeText;
                buttonText = DefaultMissingBadgeText;
                stateColor = DisabledAccentColor;
                surfaceColor = DisabledSurfaceColor;
                badgeColor = BadgeDisabledColor;
            }
            else if (!environmentDefinition.Available)
            {
                showBadge = true;
                badgeText = string.IsNullOrWhiteSpace(environmentDefinition.AvailabilityReason)
                    ? DefaultUnavailableBadgeText
                    : environmentDefinition.AvailabilityReason;
                buttonText = DefaultUnavailableBadgeText;
                stateColor = DisabledAccentColor;
                surfaceColor = DisabledSurfaceColor;
                badgeColor = BadgeDisabledColor;
            }
            else if (environmentDefinition.IsMisconfigured)
            {
                showBadge = true;
                badgeText = DefaultMisconfiguredBadgeText;
                buttonText = DefaultMisconfiguredBadgeText;
                stateColor = WarningAccentColor;
                surfaceColor = WarningSurfaceColor;
                badgeColor = BadgeWarningColor;
            }
            else
            {
                interactable = true;
                stateColor = isSelected ? SelectedAccentColor : ReadyAccentColor;
                surfaceColor = isSelected ? SelectedSurfaceColor : DefaultSurfaceColor;
                badgeColor = BadgeReadyColor;
                buttonColor = isSelected ? SuccessButtonColor : PrimaryButtonColor;
                buttonText = isSelected ? "Selected" : "Select Room";
            }

            if (stateLabel != null)
            {
                stateLabel.text = BuildStateText();
                stateLabel.color = stateColor;
            }

            if (cardBackground != null)
            {
                cardBackground.color = surfaceColor;
            }

            if (badgeLabel != null)
            {
                badgeLabel.text = badgeText;
                badgeLabel.color = showBadge && environmentDefinition != null && environmentDefinition.IsMisconfigured
                    ? new Color(1f, 0.93f, 0.85f, 1f)
                    : Color.white;
            }

            if (badgeBackground != null)
            {
                badgeBackground.color = badgeColor;
            }

            if (selectButton != null)
            {
                selectButton.interactable = interactable;

                if (buttonLabel != null)
                {
                    buttonLabel.text = buttonText;
                    buttonLabel.color = Color.white;
                }

                Image buttonImage = selectButton.targetGraphic as Image;
                if (buttonImage != null)
                {
                    buttonImage.color = interactable ? buttonColor : DisabledButtonColor;
                }

                ColorBlock colors = selectButton.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.94f, 0.94f, 0.94f, 1f);
                colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
                colors.selectedColor = Color.white;
                colors.disabledColor = new Color(0.72f, 0.72f, 0.72f, 1f);
                colors.fadeDuration = 0.08f;
                selectButton.colors = colors;

                if (!interactable && buttonLabel != null)
                {
                    buttonLabel.color = new Color(0.84f, 0.87f, 0.92f, 1f);
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
