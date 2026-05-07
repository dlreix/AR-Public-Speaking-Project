using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRPublicSpeaking.AppShell.Data;
using VRPublicSpeaking.AppShell.Flow;

namespace VRPublicSpeaking.AppShell.UI
{
    public class PracticeModePanelPresenter : MonoBehaviour
    {
        [Header("Flow")]
        [SerializeField] private AppFlowManager appFlowManager;

        [Header("Buttons")]
        [SerializeField] private Button guidedPracticeButton;
        [SerializeField] private Button freePracticeButton;
        [SerializeField] private Button evaluationModeButton;
        [SerializeField] private Button challengeModeButton;

        [Header("Availability")]
        [SerializeField] private bool showOnlyPrimaryPracticeMode = true;
        [SerializeField] private bool guidedPracticeAvailable = true;
        [SerializeField] private bool freePracticeAvailable = true;
        [SerializeField] private bool evaluationModeAvailable = true;
        [SerializeField] private bool challengeModeAvailable = false;
        [SerializeField] private TMP_Text availabilityLabel;

        private void Awake()
        {
            RefreshAvailability();
        }

        private void OnEnable()
        {
            RefreshAvailability();
        }

        public void RefreshAvailability()
        {
            if (showOnlyPrimaryPracticeMode)
            {
                ConfigurePrimaryModeOnly();
                return;
            }

            ConfigureModeButton(guidedPracticeButton, guidedPracticeAvailable, "Guided Practice");
            ConfigureModeButton(freePracticeButton, freePracticeAvailable, "Free Practice");
            ConfigureModeButton(evaluationModeButton, evaluationModeAvailable, "Evaluation Mode");
            ConfigureModeButton(challengeModeButton, challengeModeAvailable, "Challenge Mode");

            if (availabilityLabel != null)
            {
                availabilityLabel.text = BuildAvailabilitySummary();
            }
        }

        private void ConfigurePrimaryModeOnly()
        {
            guidedPracticeAvailable = true;

            SetModeCardVisible(guidedPracticeButton, true);
            SetModeCardVisible(freePracticeButton, false);
            SetModeCardVisible(evaluationModeButton, false);
            SetModeCardVisible(challengeModeButton, false);

            ConfigureModeButton(guidedPracticeButton, true, "Continue");

            if (availabilityLabel != null)
            {
                availabilityLabel.text = "Final build uses one guided practice flow. Room, timing, audience, and feedback are selected in the next steps.";
            }
        }

        public void SelectGuidedPractice()
        {
            TrySelect(PracticeMode.GuidedPractice, guidedPracticeAvailable, "Guided Practice is not available yet.");
        }

        public void SelectFreePractice()
        {
            TrySelect(PracticeMode.FreePractice, freePracticeAvailable, "Free Practice is not available yet.");
        }

        public void SelectEvaluationMode()
        {
            TrySelect(PracticeMode.EvaluationMode, evaluationModeAvailable, "Evaluation Mode is not available yet.");
        }

        public void SelectChallengeMode()
        {
            TrySelect(PracticeMode.ChallengeMode, challengeModeAvailable, "Challenge Mode is not available yet.");
        }

        private void TrySelect(PracticeMode practiceMode, bool available, string unavailableMessage)
        {
            if (!available)
            {
                if (availabilityLabel != null)
                {
                    availabilityLabel.text = $"{unavailableMessage} Choose one of the currently available modes to continue.";
                }

                return;
            }

            if (availabilityLabel != null)
            {
                availabilityLabel.text = string.Empty;
            }

            appFlowManager?.SelectPracticeMode(practiceMode);
        }

        private void ConfigureModeButton(Button button, bool available, string availableLabel)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = available;

            TMP_Text buttonLabel = button.GetComponentInChildren<TMP_Text>(true);
            if (buttonLabel != null)
            {
                buttonLabel.text = available ? availableLabel : "Coming Soon";
            }
        }

        private static void SetModeCardVisible(Button button, bool visible)
        {
            GameObject card = FindModeCard(button);
            if (card != null)
            {
                card.SetActive(visible);
            }
            else if (button != null)
            {
                button.gameObject.SetActive(visible);
            }
        }

        private static GameObject FindModeCard(Button button)
        {
            if (button == null)
            {
                return null;
            }

            Transform current = button.transform;
            while (current != null)
            {
                if (current.name.EndsWith("Card", System.StringComparison.Ordinal))
                {
                    return current.gameObject;
                }

                current = current.parent;
            }

            return null;
        }

        private string BuildAvailabilitySummary()
        {
            var availableModes = new List<string>();
            var unavailableModes = new List<string>();

            AppendAvailability(availableModes, unavailableModes, "Guided Practice", guidedPracticeAvailable);
            AppendAvailability(availableModes, unavailableModes, "Free Practice", freePracticeAvailable);
            AppendAvailability(availableModes, unavailableModes, "Evaluation Mode", evaluationModeAvailable);
            AppendAvailability(availableModes, unavailableModes, "Challenge Mode", challengeModeAvailable);

            if (availableModes.Count == 0)
            {
                return "No practice modes are currently available in this build.";
            }

            if (unavailableModes.Count == 0)
            {
                return $"Available now: {string.Join(", ", availableModes)}.";
            }

            return $"Available now: {string.Join(", ", availableModes)}. Coming later: {string.Join(", ", unavailableModes)}.";
        }

        private static void AppendAvailability(
            List<string> availableModes,
            List<string> unavailableModes,
            string label,
            bool available)
        {
            if (available)
            {
                availableModes.Add(label);
            }
            else
            {
                unavailableModes.Add(label);
            }
        }
    }
}
