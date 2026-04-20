using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRPublicSpeaking.AppShell.Flow;

namespace VRPublicSpeaking.AppShell.UI
{
    public class SettingsPanelPresenter : MonoBehaviour
    {
        [SerializeField] private AppFlowManager appFlowManager;
        [SerializeField] private Toggle comfortVignetteToggle;
        [SerializeField] private Toggle audioPromptToggle;
        [SerializeField] private Toggle rayAssistToggle;
        [SerializeField] private TMP_Text previewStateLabel;
        [SerializeField] private TMP_Text noteLabel;

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            RefreshPreviewSummary();
            SetNote("Shell preview only. Connect comfort, audio, and calibration adapters here when they are ready.");
        }

        public void OnComfortVignetteChanged(bool isEnabled)
        {
            RefreshPreviewSummary();
            SetNote(isEnabled
                ? "Comfort vignette preview is enabled."
                : "Comfort vignette preview is disabled.");
        }

        public void OnAudioPromptChanged(bool isEnabled)
        {
            RefreshPreviewSummary();
            SetNote(isEnabled
                ? "Audio prompt guidance preview is enabled."
                : "Audio prompt guidance preview is disabled.");
        }

        public void OnRayAssistChanged(bool isEnabled)
        {
            RefreshPreviewSummary();
            SetNote(isEnabled
                ? "Enhanced ray assist preview is enabled."
                : "Enhanced ray assist preview is disabled.");
        }

        public void OpenComfortSettings()
        {
            SetNote("Comfort shortcut is staged. No dedicated comfort flow is wired yet.");
        }

        public void OpenAudioSettings()
        {
            SetNote("Audio shortcut is staged. No mixer flow is wired yet.");
        }

        public void OpenCalibration()
        {
            SetNote("Calibration shortcut is staged. No calibration flow is connected yet.");
        }

        public void GoBack()
        {
            appFlowManager?.GoBack();
        }

        private void RefreshPreviewSummary()
        {
            if (previewStateLabel == null)
            {
                return;
            }

            previewStateLabel.text =
                "Preview toggles\n" +
                $"Comfort: {FormatToggleState(comfortVignetteToggle)}\n" +
                $"Audio: {FormatToggleState(audioPromptToggle)}\n" +
                $"Ray Assist: {FormatToggleState(rayAssistToggle)}";
        }

        private static string FormatToggleState(Toggle toggle)
        {
            return toggle != null && toggle.isOn ? "On" : "Off";
        }

        private void SetNote(string message)
        {
            if (noteLabel != null)
            {
                noteLabel.text = message ?? string.Empty;
            }
        }
    }
}
