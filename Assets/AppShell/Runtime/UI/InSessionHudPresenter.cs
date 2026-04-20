using TMPro;
using UnityEngine;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;

namespace VRPublicSpeaking.AppShell.UI
{
    public class InSessionHudPresenter : MonoBehaviour
    {
        [SerializeField] private AppRuntimeState runtimeState;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text timerLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private bool hideWhenSessionInactive = true;
        [SerializeField] private string inactiveStatusText = "Session idle";

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        private void Update()
        {
            if (runtimeState == null)
            {
                runtimeState = AppRuntimeState.GetOrCreate();
            }

            Refresh();
        }

        public void Refresh()
        {
            if (runtimeState == null)
            {
                return;
            }

            SessionRuntimeState runtime = runtimeState.CurrentRuntimeState;
            SessionConfig config = runtimeState.CurrentSessionConfig;
            bool isActive = runtime != null && runtime.SessionRunning;
            float targetDurationSeconds = config?.SessionDurationSeconds ?? SessionConfig.DefaultDurationSeconds;
            float elapsedSeconds = isActive
                ? Mathf.Max(0f, targetDurationSeconds - runtime.TimeRemainingSeconds)
                : 0f;

            SetVisible(!hideWhenSessionInactive || isActive);

            if (timerLabel != null)
            {
                timerLabel.text = isActive
                    ? FormatStopwatchTime(elapsedSeconds)
                    : FormatStopwatchTime(0f);
            }

            if (statusLabel != null)
            {
                statusLabel.text = isActive
                    ? $"Target: {FormatStopwatchTime(targetDurationSeconds)} | {GetPracticeModeLabel(config?.PracticeMode ?? PracticeMode.GuidedPractice)}"
                    : inactiveStatusText;
            }
        }

        private void SetVisible(bool isVisible)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = isVisible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private static string FormatStopwatchTime(float seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int minutes = totalSeconds / 60;
            int remainingSeconds = totalSeconds % 60;
            return $"{minutes:00}:{remainingSeconds:00}";
        }

        private static string GetPracticeModeLabel(PracticeMode practiceMode)
        {
            return practiceMode switch
            {
                PracticeMode.GuidedPractice => "Guided Practice",
                PracticeMode.FreePractice => "Free Practice",
                PracticeMode.EvaluationMode => "Evaluation Mode",
                PracticeMode.ChallengeMode => "Challenge Mode",
                _ => "Practice"
            };
        }
    }
}
