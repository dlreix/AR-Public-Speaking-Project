using UnityEngine;
using VRPublicSpeaking.AppShell.Data;

namespace VRPublicSpeaking.AppShell.Integration
{
    public class ScoringAdapter : MonoBehaviour
    {
        [SerializeField] private GazeScoringSystem gazeScoringSystem;
        [SerializeField] private PerformanceScoringEngine performanceScoringEngine;

        public void AutoWireIfNeeded()
        {
            if (gazeScoringSystem == null)
            {
                gazeScoringSystem = FindObjectOfType<GazeScoringSystem>(true);
            }

            if (performanceScoringEngine == null)
            {
                performanceScoringEngine = FindObjectOfType<PerformanceScoringEngine>(true);
            }
        }

        public SessionResultSummary CaptureSummary(float sessionDurationSeconds, float fallbackGazeScore = -1f)
        {
            AutoWireIfNeeded();

            var summary = new SessionResultSummary();
            summary.Reset();
            summary.DurationSeconds = sessionDurationSeconds;

            if (gazeScoringSystem != null)
            {
                summary.EyeContactScore = gazeScoringSystem.GazeScore;
                summary.HasEyeContactScore = true;
            }
            else if (fallbackGazeScore >= 0f)
            {
                summary.EyeContactScore = fallbackGazeScore;
                summary.HasEyeContactScore = true;
            }

            if (performanceScoringEngine != null)
            {
                performanceScoringEngine.CalculateSessionScore();
                FeedbackReport report = performanceScoringEngine.GetFeedbackReport();

                if (report != null)
                {
                    summary.TotalScore = report.totalScore;
                    summary.HasOverallScore = true;

                    summary.SpeechPaceScore = report.speechScore;
                    summary.HasSpeechPaceScore = true;

                    summary.PostureScore = report.postureScore;
                    summary.HasPostureScore = true;

                    if (!summary.HasEyeContactScore)
                    {
                        summary.EyeContactScore = report.eyeScore;
                        summary.HasEyeContactScore = true;
                    }

                    summary.StrongestArea = report.strongestArea;
                    summary.WeakestArea = report.weakestArea;
                    summary.PerformanceBand = report.performanceBand;
                    summary.SetRecommendations(report.improvements);

                    float estimatedFillerWords =
                        performanceScoringEngine.speechMetrics.fillerWordsPerMinute *
                        Mathf.Max(0f, sessionDurationSeconds / 60f);
                    summary.FillerWordCount = estimatedFillerWords;
                }
            }

            if (!summary.HasOverallScore && summary.HasEyeContactScore)
            {
                summary.TotalScore = summary.EyeContactScore;
                summary.HasOverallScore = true;
                summary.PerformanceBand = "Eye Contact";
            }

            return summary;
        }
    }
}
