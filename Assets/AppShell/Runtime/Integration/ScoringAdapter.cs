using System;
using UnityEngine;
using VRPublicSpeaking.AppShell.Data;

namespace VRPublicSpeaking.AppShell.Integration
{
    public class ScoringAdapter : MonoBehaviour
    {
        [SerializeField] private GazeScoringSystem gazeScoringSystem;
        [SerializeField] private PerformanceScoringEngine performanceScoringEngine;

        public PerformanceScoringEngine PerformanceScoringEngine => performanceScoringEngine;

        public void SetPerformanceScoringEngine(PerformanceScoringEngine engine)
        {
            performanceScoringEngine = engine;
        }

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
            summary.SessionId = Guid.NewGuid().ToString("N");
            summary.SessionTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            summary.DurationSeconds = sessionDurationSeconds;

            // Gaze skoru: fallback öncelikli, yoksa GazeScoringSystem'den al
            float gazeScore = fallbackGazeScore >= 0f
                ? fallbackGazeScore
                : (gazeScoringSystem != null ? gazeScoringSystem.GazeScore : 0f);

            summary.EyeContactScore    = gazeScore;
            summary.HasEyeContactScore = true;

            // PerformanceScoringEngine'e set et ki toplam skor doğru hesaplansın
            if (performanceScoringEngine != null)
            {
                performanceScoringEngine.SetEyeContactRatio(gazeScore / 100f);
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

                    // EyeContactScore zaten yukarıda set edildi

                    summary.StrongestArea = report.strongestArea;
                    summary.WeakestArea = report.weakestArea;
                    summary.PerformanceBand = report.performanceBand;
                    summary.SetRecommendations(report.improvements);
                    summary.SetDetailedReport(report);

                    float estimatedFillerWords =
                        performanceScoringEngine.speechMetrics.fillerWordsPerMinute *
                        Mathf.Max(0f, sessionDurationSeconds / 60f);
                    summary.FillerWordCount = estimatedFillerWords;

                    summary.Wpm = performanceScoringEngine.speechMetrics.wpm;
                    summary.HasWpm = true;
                    summary.FillerWordsPerMinute = performanceScoringEngine.speechMetrics.fillerWordsPerMinute;
                    summary.HasFillerWordsPerMinute = true;
                    summary.AveragePauseDuration = performanceScoringEngine.speechMetrics.averagePauseDuration;
                    summary.HasAveragePauseDuration = true;
                    summary.ToneVariationScore = performanceScoringEngine.speechMetrics.toneVariationScore;
                    summary.HasToneVariationScore = true;

                    summary.HeadMovementPercent = performanceScoringEngine.postureMetrics.swayDurationPercent;
                    summary.HasHeadMovementPercent = true;
                    summary.HeadSpeedEventsPerMinute = performanceScoringEngine.postureMetrics.slouchEventsPerMinute;
                    summary.HasHeadSpeedEventsPerMinute = true;
                    summary.CrossedArmsPercent = performanceScoringEngine.postureMetrics.crossedArmsPercent;
                    summary.HasCrossedArmsPercent = true;
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
