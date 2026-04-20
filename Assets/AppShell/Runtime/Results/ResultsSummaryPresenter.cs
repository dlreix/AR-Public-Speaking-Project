using System.Text;
using TMPro;
using UnityEngine;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;

namespace VRPublicSpeaking.AppShell.Results
{
    public class ResultsSummaryPresenter : MonoBehaviour
    {
        [SerializeField] private AppRuntimeState runtimeState;
        [SerializeField] private TMP_Text summaryLabel;
        [SerializeField] private TMP_Text recommendationsLabel;

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

            if (runtimeState == null)
            {
                return;
            }

            SessionResultSummary summary = runtimeState.GetLastSessionResultCopy();
            SessionConfig config = runtimeState.GetSessionConfigCopy();
            string environmentName = runtimeState.SelectedEnvironment != null
                ? runtimeState.SelectedEnvironment.DisplayName
                : "No environment selected";

            if (summaryLabel != null)
            {
                var builder = new StringBuilder();
                builder.AppendLine($"Environment: {environmentName}");
                builder.AppendLine($"Mode: {config.PracticeMode}");
                builder.AppendLine(summary.HasOverallScore
                    ? $"Overall: {summary.TotalScore:0.0}{BuildBandSuffix(summary.PerformanceBand)}"
                    : "Overall: Unavailable");
                builder.AppendLine(summary.HasEyeContactScore
                    ? $"Eye Contact: {summary.EyeContactScore:0.0}"
                    : "Eye Contact: Unavailable");
                builder.AppendLine(summary.HasSpeechPaceScore
                    ? $"Speech Pace: {summary.SpeechPaceScore:0.0}"
                    : "Speech Pace: Unavailable");
                builder.AppendLine(summary.HasPostureScore
                    ? $"Posture: {summary.PostureScore:0.0}"
                    : "Posture: Unavailable");
                builder.AppendLine($"Filler Words: {summary.FillerWordCount:0}  |  Time: {summary.DurationSeconds / 60f:0.#} min");

                string focusLine = BuildFocusLine(summary);
                if (!string.IsNullOrWhiteSpace(focusLine))
                {
                    builder.AppendLine(focusLine);
                }

                summaryLabel.text = builder.ToString().TrimEnd();
            }

            if (recommendationsLabel != null)
            {
                if (summary.Recommendations.Count == 0)
                {
                    recommendationsLabel.text = "No coach notes yet. Complete a session to populate recommendations.";
                }
                else
                {
                    var builder = new StringBuilder();
                    int visibleRecommendations = Mathf.Min(summary.Recommendations.Count, 3);
                    for (int index = 0; index < visibleRecommendations; index++)
                    {
                        builder.AppendLine($"- {CompactText(summary.Recommendations[index], 84)}");
                    }

                    if (summary.Recommendations.Count > visibleRecommendations)
                    {
                        builder.Append($"+{summary.Recommendations.Count - visibleRecommendations} more insight(s).");
                    }

                    recommendationsLabel.text = builder.ToString().TrimEnd();
                }
            }
        }

        private static string BuildBandSuffix(string performanceBand)
        {
            return string.IsNullOrWhiteSpace(performanceBand) ? string.Empty : $" ({performanceBand})";
        }

        private static string BuildFocusLine(SessionResultSummary summary)
        {
            string strongest = string.IsNullOrWhiteSpace(summary.StrongestArea) ? string.Empty : CompactText(summary.StrongestArea, 28);
            string weakest = string.IsNullOrWhiteSpace(summary.WeakestArea) ? string.Empty : CompactText(summary.WeakestArea, 28);

            if (string.IsNullOrWhiteSpace(strongest) && string.IsNullOrWhiteSpace(weakest))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(strongest))
            {
                return $"Focus: Improve {weakest}";
            }

            if (string.IsNullOrWhiteSpace(weakest))
            {
                return $"Focus: Keep building {strongest}";
            }

            return $"Focus: {strongest} / Improve {weakest}";
        }

        private static string CompactText(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            if (trimmed.Length <= maxLength)
            {
                return trimmed;
            }

            return trimmed.Substring(0, maxLength - 3).TrimEnd() + "...";
        }
    }
}
