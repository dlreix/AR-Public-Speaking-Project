using System;
using System.Collections.Generic;
using UnityEngine;
using VRPublicSpeaking.AppShell.PresentationQuestioning;

namespace VRPublicSpeaking.AppShell.Data
{
    [Serializable]
    public class SessionResultSummary
    {
        [SerializeField] private float totalScore;
        [SerializeField] private float eyeContactScore;
        [SerializeField] private float speechPaceScore;
        [SerializeField] private float postureScore;
        [SerializeField] private float fillerWordCount;
        [SerializeField] private float durationSeconds;
        [SerializeField] private float wpm;
        [SerializeField] private float fillerWordsPerMinute;
        [SerializeField] private float averagePauseDuration;
        [SerializeField] private float toneVariationScore;
        [SerializeField] private float headMovementPercent;
        [SerializeField] private float headSpeedEventsPerMinute;
        [SerializeField] private float crossedArmsPercent;
        [SerializeField] private string sessionId = string.Empty;
        [SerializeField] private string strongestArea = string.Empty;
        [SerializeField] private string weakestArea = string.Empty;
        [SerializeField] private string performanceBand = string.Empty;
        [SerializeField] private long sessionTimestamp;
        [SerializeField] private bool hasOverallScore;
        [SerializeField] private bool hasEyeContactScore;
        [SerializeField] private bool hasSpeechPaceScore;
        [SerializeField] private bool hasPostureScore;
        [SerializeField] private bool hasWpm;
        [SerializeField] private bool hasFillerWordsPerMinute;
        [SerializeField] private bool hasAveragePauseDuration;
        [SerializeField] private bool hasToneVariationScore;
        [SerializeField] private bool hasHeadMovementPercent;
        [SerializeField] private bool hasHeadSpeedEventsPerMinute;
        [SerializeField] private bool hasCrossedArmsPercent;
        [SerializeField] private FeedbackReport detailedReport;
        [SerializeField] private PresentationQaResult qaResult;
        [SerializeField] private List<string> recommendations = new List<string>();

        public float TotalScore
        {
            get => totalScore;
            set => totalScore = value;
        }

        public float EyeContactScore
        {
            get => eyeContactScore;
            set => eyeContactScore = value;
        }

        public float SpeechPaceScore
        {
            get => speechPaceScore;
            set => speechPaceScore = value;
        }

        public float PostureScore
        {
            get => postureScore;
            set => postureScore = value;
        }

        public float FillerWordCount
        {
            get => fillerWordCount;
            set => fillerWordCount = value;
        }

        public float DurationSeconds
        {
            get => durationSeconds;
            set => durationSeconds = Mathf.Max(0f, value);
        }

        public float Wpm
        {
            get => wpm;
            set => wpm = Mathf.Max(0f, value);
        }

        public float FillerWordsPerMinute
        {
            get => fillerWordsPerMinute;
            set => fillerWordsPerMinute = Mathf.Max(0f, value);
        }

        public float AveragePauseDuration
        {
            get => averagePauseDuration;
            set => averagePauseDuration = Mathf.Max(0f, value);
        }

        public float ToneVariationScore
        {
            get => toneVariationScore;
            set => toneVariationScore = Mathf.Clamp(value, 0f, 100f);
        }

        public float HeadMovementPercent
        {
            get => headMovementPercent;
            set => headMovementPercent = Mathf.Clamp(value, 0f, 100f);
        }

        public float HeadSpeedEventsPerMinute
        {
            get => headSpeedEventsPerMinute;
            set => headSpeedEventsPerMinute = Mathf.Max(0f, value);
        }

        public float CrossedArmsPercent
        {
            get => crossedArmsPercent;
            set => crossedArmsPercent = Mathf.Clamp(value, 0f, 100f);
        }

        public string SessionId
        {
            get => sessionId;
            set => sessionId = value ?? string.Empty;
        }

        public string StrongestArea
        {
            get => strongestArea;
            set => strongestArea = value ?? string.Empty;
        }

        public string WeakestArea
        {
            get => weakestArea;
            set => weakestArea = value ?? string.Empty;
        }

        public string PerformanceBand
        {
            get => performanceBand;
            set => performanceBand = value ?? string.Empty;
        }

        public long SessionTimestamp
        {
            get => sessionTimestamp;
            set => sessionTimestamp = value;
        }

        public bool HasOverallScore
        {
            get => hasOverallScore;
            set => hasOverallScore = value;
        }

        public bool HasEyeContactScore
        {
            get => hasEyeContactScore;
            set => hasEyeContactScore = value;
        }

        public bool HasSpeechPaceScore
        {
            get => hasSpeechPaceScore;
            set => hasSpeechPaceScore = value;
        }

        public bool HasPostureScore
        {
            get => hasPostureScore;
            set => hasPostureScore = value;
        }

        public bool HasWpm
        {
            get => hasWpm;
            set => hasWpm = value;
        }

        public bool HasFillerWordsPerMinute
        {
            get => hasFillerWordsPerMinute;
            set => hasFillerWordsPerMinute = value;
        }

        public bool HasAveragePauseDuration
        {
            get => hasAveragePauseDuration;
            set => hasAveragePauseDuration = value;
        }

        public bool HasToneVariationScore
        {
            get => hasToneVariationScore;
            set => hasToneVariationScore = value;
        }

        public bool HasHeadMovementPercent
        {
            get => hasHeadMovementPercent;
            set => hasHeadMovementPercent = value;
        }

        public bool HasHeadSpeedEventsPerMinute
        {
            get => hasHeadSpeedEventsPerMinute;
            set => hasHeadSpeedEventsPerMinute = value;
        }

        public bool HasCrossedArmsPercent
        {
            get => hasCrossedArmsPercent;
            set => hasCrossedArmsPercent = value;
        }

        public FeedbackReport DetailedReport => detailedReport;
        public PresentationQaResult QaResult => qaResult;
        public bool HasQaResult => qaResult != null && qaResult.HasAnswers;

        public IReadOnlyList<string> Recommendations => recommendations;

        public void Reset()
        {
            totalScore = 0f;
            eyeContactScore = 0f;
            speechPaceScore = 0f;
            postureScore = 0f;
            fillerWordCount = 0f;
            durationSeconds = 0f;
            wpm = 0f;
            fillerWordsPerMinute = 0f;
            averagePauseDuration = 0f;
            toneVariationScore = 0f;
            headMovementPercent = 0f;
            headSpeedEventsPerMinute = 0f;
            crossedArmsPercent = 0f;
            sessionId = string.Empty;
            strongestArea = string.Empty;
            weakestArea = string.Empty;
            performanceBand = string.Empty;
            sessionTimestamp = 0;
            hasOverallScore = false;
            hasEyeContactScore = false;
            hasSpeechPaceScore = false;
            hasPostureScore = false;
            hasWpm = false;
            hasFillerWordsPerMinute = false;
            hasAveragePauseDuration = false;
            hasToneVariationScore = false;
            hasHeadMovementPercent = false;
            hasHeadSpeedEventsPerMinute = false;
            hasCrossedArmsPercent = false;
            detailedReport = null;
            qaResult = null;
            recommendations.Clear();
        }

        public SessionResultSummary Clone()
        {
            var clone = (SessionResultSummary)MemberwiseClone();
            clone.recommendations = new List<string>(recommendations);
            clone.detailedReport = CloneFeedbackReport(detailedReport);
            clone.qaResult = CloneQaResult(qaResult);
            return clone;
        }

        public void SetDetailedReport(FeedbackReport report)
        {
            detailedReport = CloneFeedbackReport(report);
        }

        public void SetQaResult(PresentationQaResult result)
        {
            qaResult = CloneQaResult(result);
        }

        public void SetRecommendations(IEnumerable<string> values)
        {
            recommendations.Clear();
            if (values == null)
            {
                return;
            }

            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    recommendations.Add(value);
                }
            }
        }

        private static FeedbackReport CloneFeedbackReport(FeedbackReport report)
        {
            if (report == null)
            {
                return null;
            }

            var clone = new FeedbackReport
            {
                totalScore = report.totalScore,
                speechScore = report.speechScore,
                eyeScore = report.eyeScore,
                postureScore = report.postureScore,
                performanceBand = report.performanceBand,
                strongestArea = report.strongestArea,
                weakestArea = report.weakestArea,
                sessionTimestamp = report.sessionTimestamp
            };

            if (report.items != null)
            {
                for (int index = 0; index < report.items.Count; index++)
                {
                    FeedbackItem item = report.items[index];
                    if (item != null)
                    {
                        clone.items.Add(new FeedbackItem(item.category, item.metric, item.severity, item.message, item.score));
                    }
                }
            }

            if (report.strengths != null)
            {
                clone.strengths.AddRange(report.strengths);
            }

            if (report.improvements != null)
            {
                clone.improvements.AddRange(report.improvements);
            }

            return clone;
        }

        private static PresentationQaResult CloneQaResult(PresentationQaResult result)
        {
            if (result == null)
            {
                return null;
            }

            var clone = new PresentationQaResult
            {
                deckId = result.deckId,
                deckName = result.deckName,
                status = result.status,
                summary = result.summary,
                completedUnixTime = result.completedUnixTime
            };

            if (result.answers != null)
            {
                for (int index = 0; index < result.answers.Count; index++)
                {
                    PresentationQaAnswer answer = result.answers[index];
                    if (answer == null)
                    {
                        continue;
                    }

                    clone.answers.Add(new PresentationQaAnswer
                    {
                        questionId = answer.questionId,
                        question = answer.question,
                        expectedAnswer = answer.expectedAnswer,
                        answerTranscript = answer.answerTranscript,
                        skipped = answer.skipped,
                        feedback = CloneAnswerFeedback(answer.feedback)
                    });
                }
            }

            return clone;
        }

        private static PresentationAnswerFeedback CloneAnswerFeedback(PresentationAnswerFeedback feedback)
        {
            if (feedback == null)
            {
                return null;
            }

            return new PresentationAnswerFeedback
            {
                accuracy = feedback.accuracy,
                coverage = feedback.coverage,
                clarity = feedback.clarity,
                summary = feedback.summary,
                betterAnswer = feedback.betterAnswer,
                status = feedback.status
            };
        }
    }
}
