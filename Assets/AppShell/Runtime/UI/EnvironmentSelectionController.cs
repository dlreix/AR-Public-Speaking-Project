using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;

namespace VRPublicSpeaking.AppShell.UI
{
    public class EnvironmentSelectionController : MonoBehaviour
    {
        [SerializeField] private AppRuntimeState runtimeState;
        [SerializeField] private AppEnvironmentCatalog environmentCatalog;
        [SerializeField] private List<AppEnvironmentDefinition> environments = new List<AppEnvironmentDefinition>();
        [SerializeField] private List<EnvironmentCardView> cardViews = new List<EnvironmentCardView>();
        [SerializeField] private Button confirmSelectionButton;
        [SerializeField] private TMP_Text helperLabel;
        [SerializeField] private bool autoSelectFirstAvailable = true;

        private int selectedIndex = -1;

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (runtimeState == null)
            {
                runtimeState = AppRuntimeState.GetOrCreate();
            }

            BindCards();
            TryRestoreSelectionFromRuntime();

            if (selectedIndex < 0 && autoSelectFirstAvailable)
            {
                SelectFirstAvailable();
            }

            RefreshSelectionState();
        }

        public void SetEnvironmentCatalog(AppEnvironmentCatalog catalog)
        {
            environmentCatalog = catalog;
            selectedIndex = -1;
            Refresh();
        }

        public bool TryGetSelectedEnvironment(out AppEnvironmentDefinition environmentDefinition)
        {
            if (TryGetEnvironmentAt(selectedIndex, out environmentDefinition))
            {
                return environmentDefinition != null && environmentDefinition.Available && environmentDefinition.IsConfigured;
            }

            environmentDefinition = runtimeState != null ? runtimeState.SelectedEnvironment : null;
            return environmentDefinition != null && environmentDefinition.Available && environmentDefinition.IsConfigured;
        }

        public void ConfirmSelection()
        {
            if (!TryGetSelectedEnvironment(out AppEnvironmentDefinition environmentDefinition))
            {
                if (helperLabel != null)
                {
                    helperLabel.text = BuildCurrentSelectionMessage();
                }

                return;
            }

            runtimeState?.SetSelectedEnvironment(environmentDefinition);

            if (helperLabel != null)
            {
                helperLabel.text = BuildSelectionConfirmedMessage(environmentDefinition);
            }
        }

        private void BindCards()
        {
            IReadOnlyList<AppEnvironmentDefinition> environmentSource = GetEnvironmentSource();

            for (int index = 0; index < cardViews.Count; index++)
            {
                EnvironmentCardView cardView = cardViews[index];
                if (cardView == null)
                {
                    continue;
                }

                AppEnvironmentDefinition definition = index < environmentSource.Count ? environmentSource[index] : null;
                bool isSelected = definition != null && index == selectedIndex;
                cardView.Bind(definition, isSelected, HandleCardSelected);
            }
        }

        private void HandleCardSelected(EnvironmentCardView cardView)
        {
            for (int index = 0; index < cardViews.Count; index++)
            {
                if (cardViews[index] == cardView)
                {
                    selectedIndex = index;
                    break;
                }
            }

            ConfirmSelection();
            RefreshSelectionState();
        }

        private void RefreshSelectionState()
        {
            for (int index = 0; index < cardViews.Count; index++)
            {
                EnvironmentCardView cardView = cardViews[index];
                if (cardView == null)
                {
                    continue;
                }

                cardView.SetSelected(index == selectedIndex);
            }

            if (confirmSelectionButton != null)
            {
                confirmSelectionButton.interactable = TryGetSelectedEnvironment(out _);
            }

            if (helperLabel != null)
            {
                helperLabel.text = BuildCurrentSelectionMessage();
            }
        }

        private void TryRestoreSelectionFromRuntime()
        {
            if (runtimeState == null || runtimeState.SelectedEnvironment == null)
            {
                return;
            }

            string selectedId = runtimeState.SelectedEnvironment.Id;
            if (string.IsNullOrWhiteSpace(selectedId))
            {
                return;
            }

            IReadOnlyList<AppEnvironmentDefinition> environmentSource = GetEnvironmentSource();
            for (int index = 0; index < environmentSource.Count; index++)
            {
                AppEnvironmentDefinition environmentDefinition = environmentSource[index];
                if (environmentDefinition != null && environmentDefinition.Id == selectedId)
                {
                    selectedIndex = index;
                    return;
                }
            }
        }

        private void SelectFirstAvailable()
        {
            IReadOnlyList<AppEnvironmentDefinition> environmentSource = GetEnvironmentSource();
            for (int index = 0; index < environmentSource.Count; index++)
            {
                AppEnvironmentDefinition environment = environmentSource[index];
                if (environment != null && environment.Available && environment.IsConfigured)
                {
                    selectedIndex = index;
                    runtimeState?.SetSelectedEnvironment(environment);
                    return;
                }
            }
        }

        private IReadOnlyList<AppEnvironmentDefinition> GetEnvironmentSource()
        {
            return environmentCatalog != null ? environmentCatalog.Environments : environments;
        }

        private bool TryGetEnvironmentAt(int index, out AppEnvironmentDefinition environmentDefinition)
        {
            IReadOnlyList<AppEnvironmentDefinition> environmentSource = GetEnvironmentSource();

            if (index >= 0 && index < environmentSource.Count)
            {
                environmentDefinition = environmentSource[index];
                return environmentDefinition != null;
            }

            environmentDefinition = null;
            return false;
        }

        private string BuildCurrentSelectionMessage()
        {
            if (TryGetEnvironmentAt(selectedIndex, out AppEnvironmentDefinition environmentDefinition))
            {
                if (environmentDefinition == null)
                {
                    return "Select the room that best matches the speaking context you want to rehearse.";
                }

                if (!environmentDefinition.Available)
                {
                    string reason = string.IsNullOrWhiteSpace(environmentDefinition.AvailabilityReason)
                        ? "This environment is visible in the shell, but it is not available right now."
                        : environmentDefinition.AvailabilityReason;
                    return $"{environmentDefinition.DisplayName} is unavailable. {reason}";
                }

                if (environmentDefinition.IsMisconfigured)
                {
                    return $"{environmentDefinition.DisplayName} is visible, but its scene wiring is incomplete. Add the missing scene/spawn configuration before launching.";
                }

                return BuildSelectionPreviewMessage(environmentDefinition);
            }

            return HasSelectableEnvironment()
                ? "Select the room that best matches the speaking context you want to rehearse."
                : "No launch-ready environments are configured yet. The visible cards stay here so the product shell remains intact while scene setup is completed.";
        }

        private static string BuildSelectionPreviewMessage(AppEnvironmentDefinition environmentDefinition)
        {
            string message = $"{environmentDefinition.DisplayName} is ready.";

            if (!string.IsNullOrWhiteSpace(environmentDefinition.RecommendedModeLabel))
            {
                message += $" Recommended mode: {environmentDefinition.RecommendedModeLabel}.";
            }

            if (!string.IsNullOrWhiteSpace(environmentDefinition.AudienceHint))
            {
                message += $" {environmentDefinition.AudienceHint}";
            }

            return message.Trim();
        }

        private static string BuildSelectionConfirmedMessage(AppEnvironmentDefinition environmentDefinition)
        {
            string message = $"{environmentDefinition.DisplayName} selected.";

            if (!string.IsNullOrWhiteSpace(environmentDefinition.RecommendedModeLabel))
            {
                message += $" Recommended: {environmentDefinition.RecommendedModeLabel}.";
            }

            return message;
        }

        private bool HasSelectableEnvironment()
        {
            IReadOnlyList<AppEnvironmentDefinition> environmentSource = GetEnvironmentSource();

            for (int index = 0; index < environmentSource.Count; index++)
            {
                AppEnvironmentDefinition environmentDefinition = environmentSource[index];
                if (environmentDefinition != null && environmentDefinition.IsSelectable)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
