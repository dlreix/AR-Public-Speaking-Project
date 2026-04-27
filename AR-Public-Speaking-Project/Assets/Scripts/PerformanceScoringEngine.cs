using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;

// --- Dashboard için gereken Veri Sýnýflarý ---
[System.Serializable]
public class FeedbackItem
{
    public enum Severity { Strength, Minor, Major }
    public string category;
    public string metric;
    public Severity severity;
    public string message;
    public float score;
    public FeedbackItem(string cat, string met, Severity sev, string msg, float sc)
    {
        category = cat; metric = met; severity = sev; message = msg; score = sc;
    }
}

[System.Serializable]
public class FeedbackReport
{
    public float totalScore;
    public float speechScore;
    public float eyeScore;
    public float postureScore;
    public string performanceBand;
    public string strongestArea;
    public string weakestArea;
    public List<FeedbackItem> items = new List<FeedbackItem>();
    public List<string> strengths = new List<string>();
    public List<string> improvements = new List<string>();
    public long sessionTimestamp;
}

// ---  Raw Metrik Sýnýflarý ---
[System.Serializable]
public class SpeechMetrics
{
    [Header("Raw Speech Inputs")]
    public float wpm = 140f;
    public float fillerWordsPerMinute = 2f;
    public float averagePauseDuration = 0.8f;
    [Range(0f, 100f)] public float toneVariationScore = 75f;
}

[System.Serializable]
public class EyeMetrics
{
    [Header("Raw Eye Contact Inputs")]
    [Range(0f, 1f)] public float eyeContactRatio = 0.65f;
}

[System.Serializable]
public class PostureMetrics
{
    [Header("Raw Posture Inputs")]
    public float slouchEventsPerMinute = 1f;
    [Range(0f, 100f)] public float swayDurationPercent = 10f;
    [Range(0f, 100f)] public float crossedArmsPercent = 5f;
}

[System.Serializable]
public class ScoreBreakdown
{
    [Header("Sub Scores")]
    [Range(0f, 100f)] public float speechScore;
    [Range(0f, 100f)] public float eyeScore;
    [Range(0f, 100f)] public float postureScore;

    [Header("Final Score")]
    [Range(0f, 100f)] public float totalScore;
}

public class PerformanceScoringEngine : MonoBehaviour
{
    // --- SINGLETON VE EVENT ---
    public static PerformanceScoringEngine Instance { get; private set; }
    public event Action<FeedbackReport> OnScoreCalculated;

    [Header("UI - Skor Metinleri (Selin'in Sistemi)")]
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI speechScoreText;
    public TextMeshProUGUI eyeScoreText;
    public TextMeshProUGUI postureScoreText;

    [Header("UI - Feedback Paneli")]
    public TextMeshProUGUI strongestText;
    public TextMeshProUGUI weakestText;
    public TextMeshProUGUI feedbackSummaryText;

    //[Header("Eye Tracking (Gelismis)")]
    //public GazeScoringSystem gazeScoringSystem;

    [Header("Input Metrics")]
    public SpeechMetrics speechMetrics = new SpeechMetrics();
    public EyeMetrics eyeMetrics = new EyeMetrics();
    public PostureMetrics postureMetrics = new PostureMetrics();

    [Header("Output Scores")]
    public ScoreBreakdown scoreBreakdown = new ScoreBreakdown();

    [Header("Speech Score Weights")]
    [Range(0f, 1f)] public float wpmWeight = 0.35f;
    [Range(0f, 1f)] public float fillerWeight = 0.35f;
    [Range(0f, 1f)] public float pauseWeight = 0.15f;
    [Range(0f, 1f)] public float toneWeight = 0.15f;

    [Header("Final Score Weights")]
    [Range(0f, 1f)] public float speechFinalWeight = 0.40f;
    [Range(0f, 1f)] public float eyeFinalWeight = 0.35f;
    [Range(0f, 1f)] public float postureFinalWeight = 0.25f;

    [Header("Thresholds")]
    public float idealWpmMin = 120f; public float idealWpmMax = 160f;
    public float minAcceptableWpm = 80f; public float maxAcceptableWpm = 220f;
    public float idealFillerPerMin = 0f; public float maxFillerPerMin = 10f;
    public float idealPauseMin = 0.5f; public float idealPauseMax = 1.5f;
    public float minAcceptablePause = 0.1f; public float maxAcceptablePause = 3.0f;

    public float slouchPenalty = 8f; public float swayPenalty = 0.5f; public float crossedArmsPenalty = 0.4f;
    [Range(50f, 90f)] public float strengthThreshold = 75f;
    [Range(20f, 60f)] public float majorWeaknessThreshold = 50f;

    public bool calculateEveryFrame = false;
    private FeedbackReport lastReport;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (speechScoreText != null) speechScoreText.text = "Speech: -";
        if (eyeScoreText != null) eyeScoreText.text = "Eye: -";
        if (postureScoreText != null) postureScoreText.text = "Posture: -";
        if (finalScoreText != null) finalScoreText.text = "Press R/Space to start speaking...";
        if (strongestText != null) strongestText.text = "Strongest: -";
        if (weakestText != null) weakestText.text = "Weakest: -";
        if (feedbackSummaryText != null) feedbackSummaryText.text = "";
    }

    private void Update()
    {
        if (calculateEveryFrame) CalculateSessionScore();
    }

    [ContextMenu("Calculate Session Score")]
    public void CalculateSessionScore()
    {
        scoreBreakdown.speechScore = CalculateSpeechScore();
        scoreBreakdown.eyeScore = CalculateEyeScore();
        scoreBreakdown.postureScore = CalculatePostureScore();

        scoreBreakdown.totalScore = ClampScore(
            (speechFinalWeight * scoreBreakdown.speechScore) +
            (eyeFinalWeight * scoreBreakdown.eyeScore) +
            (postureFinalWeight * scoreBreakdown.postureScore)
        );

        UpdateSelinUI();

        //Dashboard için Raporu oluþtur ve gönder
        lastReport = BuildFeedbackReport();
        OnScoreCalculated?.Invoke(lastReport);

        Debug.Log($"[Scoring Engine] Final Score: {scoreBreakdown.totalScore:F1}");
    }

    private void UpdateSelinUI()
    {
        if (speechScoreText != null) speechScoreText.text = "Speech: " + scoreBreakdown.speechScore.ToString("F0");
        if (eyeScoreText != null) eyeScoreText.text = "Eye: " + scoreBreakdown.eyeScore.ToString("F0");
        if (postureScoreText != null) postureScoreText.text = "Posture: " + scoreBreakdown.postureScore.ToString("F0");
        if (finalScoreText != null) finalScoreText.text = "Final Performance Score: " + scoreBreakdown.totalScore.ToString("F0");

        string strongest = GetStrongestCategory();
        string weakest = GetWeakestCategory();

        if (strongestText != null) strongestText.text = "Strongest: " + strongest;
        if (weakestText != null) weakestText.text = "Weakest: " + weakest;

        if (feedbackSummaryText != null)
        {
            feedbackSummaryText.text =
                "-- " + GetPerformanceBand().ToUpper() + " (" + scoreBreakdown.totalScore.ToString("F0") + "/100) --\n" +
                "Speech: " + scoreBreakdown.speechScore.ToString("F0") + "  |  " +
                "Eye: " + scoreBreakdown.eyeScore.ToString("F0") + "  |  " +
                "Posture: " + scoreBreakdown.postureScore.ToString("F0") + "\n" +
                "Strongest: " + strongest + "    Weakest: " + weakest + "\n\n" +
                GetPaceFeedback() + "\n" + GetFillerFeedback() + "\n" + GetToneFeedback();
        }
    }

    // --- Dashboard Rapor Oluþturucusu ---
    private FeedbackReport BuildFeedbackReport()
    {
        FeedbackReport report = new FeedbackReport
        {
            totalScore = scoreBreakdown.totalScore,
            speechScore = scoreBreakdown.speechScore,
            eyeScore = scoreBreakdown.eyeScore,
            postureScore = scoreBreakdown.postureScore,
            performanceBand = GetPerformanceBand(),
            strongestArea = GetStrongestCategory(),
            weakestArea = GetWeakestCategory(),
            sessionTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // WPM Feedback
        AddFeedbackItem(report, "Speech", "WPM", NormalizeWpm(speechMetrics.wpm),
            $"Speaking pace is ideal ({speechMetrics.wpm:F0} wpm).",
            $"Pace is slightly off ({speechMetrics.wpm:F0} wpm). Target: {idealWpmMin}-{idealWpmMax}.",
            $"Pace needs major work ({speechMetrics.wpm:F0} wpm). Audience may struggle.");

        // Filler Feedback
        AddFeedbackItem(report, "Speech", "Filler Words", NormalizeInverse(speechMetrics.fillerWordsPerMinute, idealFillerPerMin, maxFillerPerMin),
            "Minimal filler words — fluent and professional.",
            $"Some filler words detected ({speechMetrics.fillerWordsPerMinute:F1}/min).",
            $"High filler word usage ({speechMetrics.fillerWordsPerMinute:F1}/min).");

        // Gaze Feedback
        float currentEyeScore = CalculateEyeScore();
        AddFeedbackItem(report, "Eye Contact", "Gaze Ratio", currentEyeScore,
            $"Eye contact is excellent ({currentEyeScore:F0}/100).",
            $"Eye contact is moderate ({currentEyeScore:F0}/100). Look at the audience more.",
            $"Eye contact is low ({currentEyeScore:F0}/100). Face the audience.");

        // Posture (Slouch)
        float slouchScore = ClampScore(100f - (slouchPenalty * postureMetrics.slouchEventsPerMinute));
        AddFeedbackItem(report, "Posture", "Slouching", slouchScore,
            "Upright and confident posture throughout.",
            $"Some slouching detected ({postureMetrics.slouchEventsPerMinute:F1}/min).",
            $"Frequent slouching ({postureMetrics.slouchEventsPerMinute:F1}/min).");

        return report;
    }

    private void AddFeedbackItem(FeedbackReport report, string category, string metric, float score, string strengthMsg, string minorMsg, string majorMsg)
    {
        FeedbackItem.Severity sev;
        string msg;
        if (score >= strengthThreshold) { sev = FeedbackItem.Severity.Strength; msg = strengthMsg; }
        else if (score >= majorWeaknessThreshold) { sev = FeedbackItem.Severity.Minor; msg = minorMsg; }
        else { sev = FeedbackItem.Severity.Major; msg = majorMsg; }
        report.items.Add(new FeedbackItem(category, metric, sev, msg, score));
    }

    // ---  Hesaplama Fonksiyonlarý ---
    private float CalculateSpeechScore() { return ClampScore((wpmWeight * NormalizeWpm(speechMetrics.wpm)) + (fillerWeight * NormalizeInverse(speechMetrics.fillerWordsPerMinute, idealFillerPerMin, maxFillerPerMin)) + (pauseWeight * NormalizePauseDuration(speechMetrics.averagePauseDuration)) + (toneWeight * ClampScore(speechMetrics.toneVariationScore))); }
    private float CalculateEyeScore() { //if (gazeScoringSystem != null) return ClampScore(gazeScoringSystem.GazeScore);
                                        return ClampScore(eyeMetrics.eyeContactRatio * 100f); }
    private float CalculatePostureScore() { return ClampScore(100f - (slouchPenalty * postureMetrics.slouchEventsPerMinute) - (swayPenalty * postureMetrics.swayDurationPercent) - (crossedArmsPenalty * postureMetrics.crossedArmsPercent)); }
    private float NormalizeWpm(float wpm) { if (wpm >= idealWpmMin && wpm <= idealWpmMax) return 100f; if (wpm < idealWpmMin) { if (wpm <= minAcceptableWpm) return 0f; return Mathf.InverseLerp(minAcceptableWpm, idealWpmMin, wpm) * 100f; } if (wpm >= maxAcceptableWpm) return 0f; return (1f - Mathf.InverseLerp(idealWpmMax, maxAcceptableWpm, wpm)) * 100f; }
    private float NormalizePauseDuration(float pause) { if (pause >= idealPauseMin && pause <= idealPauseMax) return 100f; if (pause < idealPauseMin) { if (pause <= minAcceptablePause) return 0f; return Mathf.InverseLerp(minAcceptablePause, idealPauseMin, pause) * 100f; } if (pause >= maxAcceptablePause) return 0f; return (1f - Mathf.InverseLerp(idealPauseMax, maxAcceptablePause, pause)) * 100f; }
    private float NormalizeInverse(float value, float idealMin, float maxValue) { if (value <= idealMin) return 100f; if (value >= maxValue) return 0f; return (1f - Mathf.InverseLerp(idealMin, maxValue, value)) * 100f; }
    private float ClampScore(float value) { return Mathf.Clamp(value, 0f, 100f); }

    // ---  Helper Fonksiyonlarý ---
    private string GetStrongestCategory() { float s = scoreBreakdown.speechScore; float e = scoreBreakdown.eyeScore; float p = scoreBreakdown.postureScore; if (s >= e && s >= p) return "Speech"; if (e >= s && e >= p) return "Eye Contact"; return "Posture"; }
    private string GetWeakestCategory() { float s = scoreBreakdown.speechScore; float e = scoreBreakdown.eyeScore; float p = scoreBreakdown.postureScore; if (s <= e && s <= p) return "Speech"; if (e <= s && e <= p) return "Eye Contact"; return "Posture"; }
    public string GetPerformanceBand() { float score = scoreBreakdown.totalScore; if (score >= 90f) return "Excellent"; if (score >= 75f) return "Good"; if (score >= 60f) return "Needs Improvement"; return "Weak Performance"; }
    private string GetPaceFeedback() { float wpm = speechMetrics.wpm; if (wpm <= 0f) return ""; if (wpm < 80f) return "Pace: " + wpm.ToString("F0") + " WPM - too slow"; if (wpm < 120f) return "Pace: " + wpm.ToString("F0") + " WPM - a bit slow"; if (wpm <= 160f) return "Pace: " + wpm.ToString("F0") + " WPM - good pace"; if (wpm <= 200f) return "Pace: " + wpm.ToString("F0") + " WPM - a bit fast"; return "Pace: " + wpm.ToString("F0") + " WPM - too fast"; }
    private string GetFillerFeedback() { float f = speechMetrics.fillerWordsPerMinute; if (f <= 0f) return "Filler words: none"; if (f < 2f) return "Filler words: " + f.ToString("F1") + "/min - acceptable"; if (f < 5f) return "Filler words: " + f.ToString("F1") + "/min - try to reduce"; return "Filler words: " + f.ToString("F1") + "/min - avoid filler words!"; }
    private string GetToneFeedback() { float t = speechMetrics.toneVariationScore; if (t <= 0f) return ""; if (t < 30f) return "Tone: try varying your pitch"; if (t < 60f) return "Tone: natural vocal variety"; return "Tone: very expressive delivery"; }

    // --- Veri Aktarým Fonksiyonlarý ---
    public void SetSpeechMetrics(float wpm, float fillerPerMin, float avgPause, float toneScore) { speechMetrics.wpm = wpm; speechMetrics.fillerWordsPerMinute = fillerPerMin; speechMetrics.averagePauseDuration = avgPause; speechMetrics.toneVariationScore = toneScore; }
    public void SetEyeContactRatio(float ratio) { eyeMetrics.eyeContactRatio = Mathf.Clamp01(ratio); }
    public void SetPostureMetrics(float slouchPerMin, float swayPercent, float crossedArmsPercent) { postureMetrics.slouchEventsPerMinute = slouchPerMin; postureMetrics.swayDurationPercent = Mathf.Clamp(swayPercent, 0f, 100f); postureMetrics.crossedArmsPercent = Mathf.Clamp(crossedArmsPercent, 0f, 100f); }
    public float GetFinalScore() { return scoreBreakdown.totalScore; }
    public FeedbackReport GetFeedbackReport() => lastReport;
}

