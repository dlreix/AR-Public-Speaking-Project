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
            ConfigureModeButton(guidedPracticeButton, guidedPracticeAvailable, "Guided Practice");
            ConfigureModeButton(freePracticeButton, freePracticeAvailable, "Free Practice");
            ConfigureModeButton(evaluationModeButton, evaluationModeAvailable, "Evaluation Mode");
            ConfigureModeButton(challengeModeButton, challengeModeAvailable, "Challenge Mode");

            if (availabilityLabel != null)
            {
                availabilityLabel.text = BuildAvailabilitySummary();
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
                buttonLabel.text = available ? "Select Mode" : "Coming Soon";
            }
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
