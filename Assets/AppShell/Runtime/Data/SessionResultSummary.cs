using System;
using System.Collections.Generic;
using UnityEngine;

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
        [SerializeField] private string strongestArea = string.Empty;
        [SerializeField] private string weakestArea = string.Empty;
        [SerializeField] private string performanceBand = string.Empty;
        [SerializeField] private bool hasOverallScore;
        [SerializeField] private bool hasEyeContactScore;
        [SerializeField] private bool hasSpeechPaceScore;
        [SerializeField] private bool hasPostureScore;
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

        public IReadOnlyList<string> Recommendations => recommendations;

        public void Reset()
        {
            totalScore = 0f;
            eyeContactScore = 0f;
            speechPaceScore = 0f;
            postureScore = 0f;
            fillerWordCount = 0f;
            durationSeconds = 0f;
            strongestArea = string.Empty;
            weakestArea = string.Empty;
            performanceBand = string.Empty;
            hasOverallScore = false;
            hasEyeContactScore = false;
            hasSpeechPaceScore = false;
            hasPostureScore = false;
            recommendations.Clear();
        }

        public SessionResultSummary Clone()
        {
            var clone = (SessionResultSummary)MemberwiseClone();
            clone.recommendations = new List<string>(recommendations);
            return clone;
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
    }
}
