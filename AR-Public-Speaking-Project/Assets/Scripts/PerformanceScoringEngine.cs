using UnityEngine;
using System; // Event (Action) kullanmak için eklendi
using System.Collections.Generic;

// --- Veri Sýnýflarý (Ayný Kaldý) ---
[System.Serializable]
public class SpeechMetrics
{
    public float wpm = 140f;
    public float fillerWordsPerMinute = 2f;
    public float averagePauseDuration = 0.8f;
    [Range(0f, 100f)] public float toneVariationScore = 75f;
}

[System.Serializable]
public class EyeMetrics
{
    [Range(0f, 1f)] public float eyeContactRatio = 0.65f;
}

[System.Serializable]
public class PostureMetrics
{
    public float slouchEventsPerMinute = 1f;
    [Range(0f, 100f)] public float swayDurationPercent = 10f;
    [Range(0f, 100f)] public float crossedArmsPercent = 5f;
}

[System.Serializable]
public class SpeechSubScores
{
    [Range(0f, 100f)] public float wpmScore;
    [Range(0f, 100f)] public float fillerScore;
    [Range(0f, 100f)] public float pauseScore;
    [Range(0f, 100f)] public float toneScore;
}

[System.Serializable]
public class ScoreBreakdown
{
    [Range(0f, 100f)] public float speechScore;
    [Range(0f, 100f)] public float eyeScore;
    [Range(0f, 100f)] public float postureScore;
    public SpeechSubScores speechSubScores = new SpeechSubScores();
    [Range(0f, 100f)] public float totalScore;
    public float slouchScore;
    public float swayScore;
    public float crossedArmsScore;
}

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

public class PerformanceScoringEngine : MonoBehaviour
{
    // --- SINGLETON YAPISI (Senin için eklendi) ---
    public static PerformanceScoringEngine Instance { get; private set; }

    // --- EVENT (SÝNYAL) SÝSTEMÝ (Senin için eklendi) ---
    // Hesaplama bittiðinde bu event fýrlatýlacak ve FeedbackReport paketini taþýyacak
    public event Action<FeedbackReport> OnScoreCalculated;

    [Header("Input Metrics")]
    public SpeechMetrics speechMetrics = new SpeechMetrics();
    public EyeMetrics eyeMetrics = new EyeMetrics();
    public PostureMetrics postureMetrics = new PostureMetrics();

    [Header("Output Scores")]
    public ScoreBreakdown scoreBreakdown = new ScoreBreakdown();

    [Header("Feedback")]
    public string speechFeedback;
    public string eyeFeedback;
    public string postureFeedback;
    public string strongestArea;
    public string weakestArea;

    [Header("Weights & Thresholds")]
    [Range(0f, 1f)] public float wpmWeight = 0.35f;
    [Range(0f, 1f)] public float fillerWeight = 0.35f;
    [Range(0f, 1f)] public float pauseWeight = 0.15f;
    [Range(0f, 1f)] public float toneWeight = 0.15f;
    [Range(0f, 1f)] public float speechFinalWeight = 0.40f;
    [Range(0f, 1f)] public float eyeFinalWeight = 0.35f;
    [Range(0f, 1f)] public float postureFinalWeight = 0.25f;

    public float idealWpmMin = 120f; public float idealWpmMax = 160f;
    public float minAcceptableWpm = 80f; public float maxAcceptableWpm = 220f;
    public float idealFillerPerMin = 0f; public float maxFillerPerMin = 10f;
    public float idealPauseMin = 0.5f; public float idealPauseMax = 1.5f;
    public float minAcceptablePause = 0.1f; public float maxAcceptablePause = 3.0f;

    public float slouchPenalty = 8f;
    public float swayPenalty = 0.5f;
    public float crossedArmsPenalty = 0.4f;

    [Range(50f, 90f)] public float strengthThreshold = 75f;
    [Range(20f, 60f)] public float majorWeaknessThreshold = 50f;

    [Header("Auto Recalculate")]
    public bool calculateEveryFrame = false;

    private FeedbackReport lastReport;

    private void Awake() 
    {
        // Singleton Kurulumu
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
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

        GenerateFeedback();
        DetermineStrengths();

        lastReport = BuildFeedbackReport();

        // UI FONKSÝYONU SÝLÝNDÝ. YERÝNE EVENT FIRLATILIYOR:
        OnScoreCalculated?.Invoke(lastReport);

        Debug.Log($"[Scoring Engine] Hesaplama tamamlandý. Total Score: {scoreBreakdown.totalScore:F1}");
    }

    private float CalculateSpeechScore()
    {
        float wpmScore = NormalizeWpm(speechMetrics.wpm);
        float fillerScore = NormalizeInverse(speechMetrics.fillerWordsPerMinute, idealFillerPerMin, maxFillerPerMin);
        float pauseScore = NormalizePauseDuration(speechMetrics.averagePauseDuration);
        float toneScore = ClampScore(speechMetrics.toneVariationScore);

        scoreBreakdown.speechSubScores.wpmScore = wpmScore;
        scoreBreakdown.speechSubScores.fillerScore = fillerScore;
        scoreBreakdown.speechSubScores.pauseScore = pauseScore;
        scoreBreakdown.speechSubScores.toneScore = toneScore;

        return ClampScore(
            (wpmWeight * wpmScore) + (fillerWeight * fillerScore) +
            (pauseWeight * pauseScore) + (toneWeight * toneScore)
        );
    }

    private float CalculateEyeScore()
    {
        return ClampScore(eyeMetrics.eyeContactRatio * 100f);
    }

    private float CalculatePostureScore()
    {
        scoreBreakdown.slouchScore = ClampScore(100f - slouchPenalty * postureMetrics.slouchEventsPerMinute);
        scoreBreakdown.swayScore = ClampScore(100f - swayPenalty * postureMetrics.swayDurationPercent);
        scoreBreakdown.crossedArmsScore = ClampScore(100f - crossedArmsPenalty * postureMetrics.crossedArmsPercent);

        return ClampScore(
            (scoreBreakdown.slouchScore + scoreBreakdown.swayScore + scoreBreakdown.crossedArmsScore) / 3f
        );
    }

    private void GenerateFeedback()
    {
        if (scoreBreakdown.speechScore >= 80)
            speechFeedback = "Strong speech performance.";
        else if (scoreBreakdown.speechScore >= 60)
            speechFeedback = $"Speech is acceptable. WPM: {speechMetrics.wpm:F0} (ideal {idealWpmMin}-{idealWpmMax}), " +
                             $"Filler: {speechMetrics.fillerWordsPerMinute:F1}/min.";
        else
            speechFeedback = $"Speech needs improvement. WPM: {speechMetrics.wpm:F0} (ideal {idealWpmMin}-{idealWpmMax}), " +
                             $"Filler: {speechMetrics.fillerWordsPerMinute:F1}/min, Pause: {speechMetrics.averagePauseDuration:F1}s.";

        float eyePct = eyeMetrics.eyeContactRatio * 100f;
        if (scoreBreakdown.eyeScore >= 80)
            eyeFeedback = $"Good eye contact with the audience ({eyePct:F0}% of session).";
        else if (scoreBreakdown.eyeScore >= 60)
            eyeFeedback = $"Eye contact is moderate ({eyePct:F0}%). Try to look at the audience more.";
        else
            eyeFeedback = $"Not enough eye contact ({eyePct:F0}%). Face the audience, not your notes or screen.";

        if (scoreBreakdown.postureScore >= 80)
            postureFeedback = "Confident posture throughout the session.";
        else if (scoreBreakdown.postureScore >= 60)
            postureFeedback = $"Posture is acceptable. Slouch: {postureMetrics.slouchEventsPerMinute:F1}/min, Sway: {postureMetrics.swayDurationPercent:F0}%.";
        else
            postureFeedback = $"Posture appears unstable. Slouch: {postureMetrics.slouchEventsPerMinute:F1}/min, " +
                              $"Sway: {postureMetrics.swayDurationPercent:F0}%, Crossed arms: {postureMetrics.crossedArmsPercent:F0}%.";
    }

    private void DetermineStrengths()
    {
        float speech = scoreBreakdown.speechScore;
        float eye = scoreBreakdown.eyeScore;
        float posture = scoreBreakdown.postureScore;

        float max = Mathf.Max(speech, eye, posture);
        float min = Mathf.Min(speech, eye, posture);

        strongestArea = (max == speech) ? "Speech" : (max == eye) ? "Eye Contact" : "Posture";
        weakestArea = (min == speech) ? "Speech" : (min == eye) ? "Eye Contact" : "Posture";
    }

    private FeedbackReport BuildFeedbackReport()
    {
        FeedbackReport report = new FeedbackReport
        {
            totalScore = scoreBreakdown.totalScore,
            speechScore = scoreBreakdown.speechScore,
            eyeScore = scoreBreakdown.eyeScore,
            postureScore = scoreBreakdown.postureScore,
            performanceBand = GetPerformanceBand(),
            strongestArea = strongestArea,
            weakestArea = weakestArea,
            sessionTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        AddFeedbackItem(report, "Speech", "WPM", scoreBreakdown.speechSubScores.wpmScore,
            $"Speaking pace is ideal ({speechMetrics.wpm:F0} wpm).",
            speechMetrics.wpm < idealWpmMin ? $"Slightly too slow ({speechMetrics.wpm:F0} wpm). Target: {idealWpmMin}-{idealWpmMax}." : $"Slightly too fast ({speechMetrics.wpm:F0} wpm). Target: {idealWpmMin}-{idealWpmMax}.",
            speechMetrics.wpm < idealWpmMin ? $"Too slow ({speechMetrics.wpm:F0} wpm). Presentation sounds monotone." : $"Too fast ({speechMetrics.wpm:F0} wpm). Audience may struggle to follow."
        );

        AddFeedbackItem(report, "Speech", "Filler Words", scoreBreakdown.speechSubScores.fillerScore,
            "Minimal filler words — fluent and professional.",
            $"Some filler words detected ({speechMetrics.fillerWordsPerMinute:F1}/min). Try to reduce them.",
            $"High filler word usage ({speechMetrics.fillerWordsPerMinute:F1}/min). Pause instead of filling silence."
        );

        AddFeedbackItem(report, "Speech", "Pause Duration", scoreBreakdown.speechSubScores.pauseScore,
            $"Pauses are well-timed ({speechMetrics.averagePauseDuration:F1}s avg).",
            speechMetrics.averagePauseDuration < idealPauseMin ? $"Pauses too short ({speechMetrics.averagePauseDuration:F1}s). Give the audience time to absorb." : $"Pauses too long ({speechMetrics.averagePauseDuration:F1}s). Shorten them to maintain flow.",
            speechMetrics.averagePauseDuration < idealPauseMin ? $"Almost no pausing ({speechMetrics.averagePauseDuration:F1}s). Slow down and breathe." : $"Pauses far too long ({speechMetrics.averagePauseDuration:F1}s). Session flow is breaking."
        );

        AddFeedbackItem(report, "Speech", "Tone Variation", scoreBreakdown.speechSubScores.toneScore,
            "Voice tone is varied and engaging.",
            "Add more tone variation to emphasise key points.",
            "Voice is flat and monotone — audience attention may drop."
        );

        float eyePct = eyeMetrics.eyeContactRatio * 100f;
        AddFeedbackItem(report, "Eye Contact", "Gaze Ratio", scoreBreakdown.eyeScore,
            $"Eye contact ratio is excellent ({eyePct:F0}%).",
            $"Eye contact is moderate ({eyePct:F0}%). Try to look at the audience more often.",
            $"Eye contact is low ({eyePct:F0}%). Face the audience, not your notes or screen."
        );

        float slouchScore = scoreBreakdown.slouchScore;
        AddFeedbackItem(report, "Posture", "Slouching", slouchScore,
            "Upright and confident posture throughout.",
            $"Some slouching detected ({postureMetrics.slouchEventsPerMinute:F1}/min). Keep your shoulders back.",
            $"Frequent slouching ({postureMetrics.slouchEventsPerMinute:F1}/min). This signals low confidence."
        );

        float swayScore = scoreBreakdown.swayScore;
        AddFeedbackItem(report, "Posture", "Swaying", swayScore,
            "Stable and grounded stance.",
            $"Some swaying detected ({postureMetrics.swayDurationPercent:F0}% of session). Try to stand still.",
            $"Excessive swaying ({postureMetrics.swayDurationPercent:F0}%). Can appear nervous or distracting."
        );

        float crossedScore = scoreBreakdown.crossedArmsScore;
        AddFeedbackItem(report, "Posture", "Crossed Arms", crossedScore,
            "Open body language — approachable and confident.",
            $"Arms crossed {postureMetrics.crossedArmsPercent:F0}% of the time. Try to keep them at your sides.",
            $"Arms crossed frequently ({postureMetrics.crossedArmsPercent:F0}%). Looks closed-off and defensive."
        );

        foreach (var item in report.items)
        {
            if (item.severity == FeedbackItem.Severity.Strength)
                report.strengths.Add($"[{item.category}] {item.metric}");
            else if (item.severity == FeedbackItem.Severity.Major)
                report.improvements.Add($"[{item.category}] {item.metric}");
        }

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

    private float NormalizeWpm(float wpm)
    {
        if (wpm >= idealWpmMin && wpm <= idealWpmMax) return 100f;
        if (wpm < idealWpmMin) { if (wpm <= minAcceptableWpm) return 0f; return Mathf.InverseLerp(minAcceptableWpm, idealWpmMin, wpm) * 100f; }
        if (wpm >= maxAcceptableWpm) return 0f;
        return (1f - Mathf.InverseLerp(idealWpmMax, maxAcceptableWpm, wpm)) * 100f;
    }

    private float NormalizePauseDuration(float pause)
    {
        if (pause >= idealPauseMin && pause <= idealPauseMax) return 100f;
        if (pause < idealPauseMin) { if (pause <= minAcceptablePause) return 0f; return Mathf.InverseLerp(minAcceptablePause, idealPauseMin, pause) * 100f; }
        if (pause >= maxAcceptablePause) return 0f;
        return (1f - Mathf.InverseLerp(idealPauseMax, maxAcceptablePause, pause)) * 100f;
    }

    private float NormalizeInverse(float value, float idealMin, float maxValue)
    {
        if (value <= idealMin) return 100f;
        if (value >= maxValue) return 0f;
        return (1f - Mathf.InverseLerp(idealMin, maxValue, value)) * 100f;
    }

    private float ClampScore(float value) => Mathf.Clamp(value, 0f, 100f);

    public void SetSpeechMetrics(float wpm, float fillerPerMin, float avgPause, float toneScore)
    {
        speechMetrics.wpm = wpm;
        speechMetrics.fillerWordsPerMinute = fillerPerMin;
        speechMetrics.averagePauseDuration = avgPause;
        speechMetrics.toneVariationScore = toneScore;
    }

    public void SetEyeContactRatio(float ratio) { eyeMetrics.eyeContactRatio = Mathf.Clamp01(ratio); }

    public void SetPostureMetrics(float slouchPerMin, float swayPercent, float crossedArmsPercent)
    {
        postureMetrics.slouchEventsPerMinute = slouchPerMin;
        postureMetrics.swayDurationPercent = Mathf.Clamp(swayPercent, 0f, 100f);
        postureMetrics.crossedArmsPercent = Mathf.Clamp(crossedArmsPercent, 0f, 100f);
    }

    public float GetFinalScore() => scoreBreakdown.totalScore;

    public string GetPerformanceBand()
    {
        float s = scoreBreakdown.totalScore;
        if (s >= 90f) return "Excellent";
        if (s >= 75f) return "Good";
        if (s >= 60f) return "Needs Improvement";
        return "Weak Performance";
    }

    public FeedbackReport GetFeedbackReport() => lastReport;

    public string GetFeedbackReportJson()
    {
        if (lastReport == null) return "{}";
        return JsonUtility.ToJson(lastReport, prettyPrint: true);
    }

    public List<FeedbackItem> GetFeedbackByCategory(string category)
    {
        var result = new List<FeedbackItem>();
        if (lastReport == null) return result;
        foreach (var item in lastReport.items)
            if (item.category == category) result.Add(item);
        return result;
    }
}


