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
        [SerializeField] private TMP_Text scoreLabel;
        [SerializeField] private TMP_Text summaryLabel;
        [SerializeField] private TMP_Text metricsLabel;
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

            if (scoreLabel != null)
            {
                scoreLabel.text = BuildScoreText(summary);
            }

            if (summaryLabel != null)
            {
                summaryLabel.text = BuildOverviewText(summary, config, environmentName);
            }

            if (metricsLabel != null)
            {
                metricsLabel.text = BuildMetricsText(summary);
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

        private static string BuildScoreText(SessionResultSummary summary)
        {
            if (!summary.HasOverallScore)
            {
                return "<size=42><b>Latest Run</b></size>\n<size=20>Score pending</size>";
            }

            string bandSuffix = string.IsNullOrWhiteSpace(summary.PerformanceBand)
                ? "Latest overall score"
                : summary.PerformanceBand;
            return $"<size=58><b>{summary.TotalScore:0.0}</b></size>\n<size=21>{bandSuffix}</size>";
        }

        private static string BuildOverviewText(SessionResultSummary summary, SessionConfig config, string environmentName)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Environment: {environmentName}");
            builder.AppendLine(
                $"Mode: {config.PracticeMode}  |  Duration: {(summary.DurationSeconds > 0f ? $"{summary.DurationSeconds / 60f:0.#} min" : config.GetDurationDisplay())}  |  Filler: {summary.FillerWordCount:0}");

            string focusLine = BuildFocusLine(summary);
            if (!string.IsNullOrWhiteSpace(focusLine))
            {
                builder.AppendLine(focusLine);
            }

            return builder.ToString().TrimEnd();
        }

        private static string BuildMetricsText(SessionResultSummary summary)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Eye Contact   {FormatMetric(summary.HasEyeContactScore, summary.EyeContactScore)}");
            builder.AppendLine($"Speech Pace   {FormatMetric(summary.HasSpeechPaceScore, summary.SpeechPaceScore)}");
            builder.AppendLine($"Posture       {FormatMetric(summary.HasPostureScore, summary.PostureScore)}");

            if (!string.IsNullOrWhiteSpace(summary.StrongestArea))
            {
                builder.AppendLine($"Strongest    {CompactText(summary.StrongestArea, 28)}");
            }

            if (!string.IsNullOrWhiteSpace(summary.WeakestArea))
            {
                builder.Append($"Improve      {CompactText(summary.WeakestArea, 28)}");
            }

            return builder.ToString().TrimEnd();
        }

        private static string BuildBandSuffix(string performanceBand)
        {
            return string.IsNullOrWhiteSpace(performanceBand) ? string.Empty : $" ({performanceBand})";
        }

        private static string FormatMetric(bool hasValue, float value)
        {
            return hasValue ? $"{value:0.0}" : "Unavailable";
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
