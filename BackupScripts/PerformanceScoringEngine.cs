using TMPro;
using UnityEngine;
using System;
using System.Collections.Generic;

[System.Serializable]
public class SpeechMetrics
{
    [Header("Raw Speech Inputs")]
    public float wpm;
    public float fillerWordsPerMinute;
    public float averagePauseDuration;
    [Range(0f, 100f)] public float toneVariationScore;
}

[System.Serializable]
public class EyeMetrics
{
    [Header("Raw Eye Contact Inputs")]
    [Range(0f, 1f)] public float eyeContactRatio;
}

[System.Serializable]
public class PostureMetrics
{
    [Header("Raw Posture Inputs")]
    public float slouchEventsPerMinute;
    [Range(0f, 100f)] public float swayDurationPercent;
    [Range(0f, 100f)] public float crossedArmsPercent;
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
    [Header("Sub Scores")]
    [Range(0f, 100f)] public float speechScore;
    [Range(0f, 100f)] public float eyeScore;
    [Range(0f, 100f)] public float postureScore;

    [Header("Speech Sub-Scores")]
    public SpeechSubScores speechSubScores = new SpeechSubScores();

    [Header("Final Score")]
    [Range(0f, 100f)] public float totalScore;

    [Header("Posture Sub-Scores")]
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
    public List<FeedbackItem> items        = new List<FeedbackItem>();
    public List<string>       strengths    = new List<string>();
    public List<string>       improvements = new List<string>();
    public long sessionTimestamp;
}

public class PerformanceScoringEngine : MonoBehaviour
{
    public static PerformanceScoringEngine Instance { get; private set; }
    public event Action<FeedbackReport> OnScoreCalculated;

    // ── UI ────────────────────────────────────────────────────────────────────

    [Header("UI — Skor Metinleri")]
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI speechScoreText;
    public TextMeshProUGUI eyeScoreText;
    public TextMeshProUGUI postureScoreText;
    public TextMeshProUGUI strongestText;
    public TextMeshProUGUI weakestText;

    [Header("UI — Feedback Paneli")]
    public TextMeshProUGUI feedbackSummaryText;

    // ── Metrics ───────────────────────────────────────────────────────────────

    [Header("Input Metrics")]
    public SpeechMetrics  speechMetrics  = new SpeechMetrics();
    public EyeMetrics     eyeMetrics     = new EyeMetrics();
    public PostureMetrics postureMetrics = new PostureMetrics();

    [Header("Output Scores")]
    public ScoreBreakdown scoreBreakdown = new ScoreBreakdown();

    [Header("Feedback")]
    public string speechFeedback;
    public string eyeFeedback;
    public string postureFeedback;
    public string strongestArea;
    public string weakestArea;

    // ── Posture — Head Tracking ───────────────────────────────────────────────

    [Header("Posture — Head Tracking (EyeTrackingSystem)")]
    [Tooltip("Main Camera üzerindeki EyeTrackingSystem. Bağlanırsa baş hareketinden posture tahmini yapılır.")]
    [SerializeField] private EyeTrackingSystem eyeTrackingSystem;

    [Tooltip("Sway hesabı için baş hız eşiği (°/s). Bu değerin üzeri sway sayılır.")]
    [SerializeField] private float swaySpeedThreshold = 40f;

    [Tooltip("Sway yüzdesi yumuşatma faktörü (0=anlık, 1=hiç değişmez).")]
    [Range(0f, 0.99f)]
    [SerializeField] private float swaySmoothing = 0.95f;

    // Posture tracking iç değişkenleri
    private float _postureSessionStart;
    private float _swayAccumSec;
    private float _totalActiveSec;
    private float _smoothedSwayPercent;
    private int   _headWarningCount;
    private bool  _postureTrackingActive;

    // ── Weights ───────────────────────────────────────────────────────────────

    [Header("Speech Score Weights")]
    [Range(0f, 1f)] public float wpmWeight    = 0.35f;
    [Range(0f, 1f)] public float fillerWeight = 0.35f;
    [Range(0f, 1f)] public float pauseWeight  = 0.15f;
    [Range(0f, 1f)] public float toneWeight   = 0.15f;

    [Header("Final Score Weights")]
    [Range(0f, 1f)] public float speechFinalWeight  = 0.40f;
    [Range(0f, 1f)] public float eyeFinalWeight     = 0.35f;
    [Range(0f, 1f)] public float postureFinalWeight = 0.25f;

    [Header("WPM Target Range")]
    public float idealWpmMin = 120f;
    public float idealWpmMax = 160f;
    public float minAcceptableWpm = 80f;
    public float maxAcceptableWpm = 220f;

    [Header("Filler Word Thresholds")]
    public float idealFillerPerMin = 0f;
    public float maxFillerPerMin = 10f;

    [Header("Pause Duration Thresholds (seconds)")]
    public float idealPauseMin = 0.5f;
    public float idealPauseMax = 1.5f;
    public float minAcceptablePause = 0.1f;
    public float maxAcceptablePause = 3.0f;

    [Header("Posture Penalty Constants")]
    public float slouchPenalty      = 8f;
    public float swayPenalty        = 0.5f;
    public float crossedArmsPenalty = 0.4f;

    [Header("Feedback Thresholds")]
    [Range(50f, 90f)] public float strengthThreshold      = 75f;
    [Range(20f, 60f)] public float majorWeaknessThreshold = 50f;

    [Header("Auto Recalculate")]
    public bool calculateEveryFrame = false;

    private FeedbackReport lastReport;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null || Instance == this)
            Instance = this;

        // Inspector'da atanmamışsa otomatik bul
        if (eyeTrackingSystem == null)
            eyeTrackingSystem = FindObjectOfType<EyeTrackingSystem>(true);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        CalculateSessionScore(false);
    }

    private void Update()
    {
        UpdatePostureFromHeadTracking();

        if (calculateEveryFrame)
            CalculateSessionScore();
    }

    // ── Posture Head Tracking ─────────────────────────────────────────────────

    /// <summary>
    /// Her frame çağrılır. EyeTrackingSystem aktifse baş hızından
    /// sway ve slouch tahminlerini günceller.
    /// </summary>
    private void UpdatePostureFromHeadTracking()
    {
        if (eyeTrackingSystem == null || !eyeTrackingSystem.IsActive || eyeTrackingSystem.IsPaused)
        {
            _postureTrackingActive = false;
            return;
        }

        float dt = Time.deltaTime;
        _totalActiveSec += dt;

        // İlk aktif frame'de sıfırla
        if (!_postureTrackingActive)
        {
            _postureTrackingActive = true;
            _postureSessionStart   = Time.time;
            _swayAccumSec          = 0f;
            _totalActiveSec        = 0f;
            _headWarningCount      = 0;
            _smoothedSwayPercent   = 0f;
        }

        float headSpeed = eyeTrackingSystem.SmoothedHeadSpeed;

        // Baş hızı eşiği aşıyorsa sway zamanı biriktir
        if (headSpeed > swaySpeedThreshold)
            _swayAccumSec += dt;

        // Sway yüzdesi = sway süresi / toplam aktif süre
        float rawSwayPercent = _totalActiveSec > 0.5f
            ? Mathf.Clamp(_swayAccumSec / _totalActiveSec * 100f, 0f, 100f)
            : 0f;

        // Yumuşat — ani spike'ları önler
        _smoothedSwayPercent = Mathf.Lerp(rawSwayPercent, _smoothedSwayPercent, swaySmoothing);

        // Baş uyarısından slouch tahmini (dakika başına uyarı sayısı)
        if (eyeTrackingSystem.IsHeadWarning)
            _headWarningCount++;

        float sessionMinutes = _totalActiveSec / 60f;
        float slouchPerMin   = sessionMinutes > 0f ? _headWarningCount / sessionMinutes : 0f;

        // PostureMetrics'e yaz — SetPostureMetrics üzerinden
        postureMetrics.swayDurationPercent   = _smoothedSwayPercent;
        postureMetrics.slouchEventsPerMinute = slouchPerMin;
        postureMetrics.crossedArmsPercent    = 0f; // VR'da ölçülemiyor
    }

    /// <summary>
    /// Posture tracking iç sayaçlarını sıfırlar. Session başlangıcında çağrılabilir.
    /// </summary>
    public void ResetPostureTracking()
    {
        _swayAccumSec        = 0f;
        _totalActiveSec      = 0f;
        _headWarningCount    = 0;
        _smoothedSwayPercent = 0f;
        _postureTrackingActive = false;
    }

    // ── Score Calculation ─────────────────────────────────────────────────────

    [ContextMenu("Calculate Session Score")]
    public void CalculateSessionScore()
    {
        CalculateSessionScore(true);
    }

    public void RefreshScoreSilently()
    {
        CalculateSessionScore(false);
    }

    private void CalculateSessionScore(bool notifyListeners)
    {
        scoreBreakdown.speechScore  = CalculateSpeechScore();
        scoreBreakdown.eyeScore     = CalculateEyeScore();
        scoreBreakdown.postureScore = CalculatePostureScore();

        scoreBreakdown.totalScore = ClampScore(
            (speechFinalWeight  * scoreBreakdown.speechScore)  +
            (eyeFinalWeight     * scoreBreakdown.eyeScore)     +
            (postureFinalWeight * scoreBreakdown.postureScore)
        );

        GenerateFeedback();
        DetermineStrengths();

        lastReport = BuildFeedbackReport();

        UpdateUI();
        if (notifyListeners)
            OnScoreCalculated?.Invoke(lastReport);

        Debug.Log(
            $"[PerformanceScoringEngine] Speech={scoreBreakdown.speechScore:F1} " +
            $"(WPM={scoreBreakdown.speechSubScores.wpmScore:F1} " +
            $"Filler={scoreBreakdown.speechSubScores.fillerScore:F1} " +
            $"Pause={scoreBreakdown.speechSubScores.pauseScore:F1} " +
            $"Tone={scoreBreakdown.speechSubScores.toneScore:F1}), " +
            $"Eye={scoreBreakdown.eyeScore:F1}, " +
            $"Posture={scoreBreakdown.postureScore:F1} " +
            $"(Sway={postureMetrics.swayDurationPercent:F1}% Slouch={postureMetrics.slouchEventsPerMinute:F1}/min), " +
            $"Total={scoreBreakdown.totalScore:F1} [{GetPerformanceBand()}]"
        );
    }

    private float CalculateSpeechScore()
    {
        if (speechMetrics.wpm <= 0f)
        {
            scoreBreakdown.speechSubScores.wpmScore    = 0f;
            scoreBreakdown.speechSubScores.fillerScore = 0f;
            scoreBreakdown.speechSubScores.pauseScore  = 0f;
            scoreBreakdown.speechSubScores.toneScore   = 0f;
            return 0f;
        }

        float wpmScore    = NormalizeWpm(speechMetrics.wpm);
        float fillerScore = NormalizeInverse(speechMetrics.fillerWordsPerMinute, idealFillerPerMin, maxFillerPerMin);
        float pauseScore  = NormalizePauseDuration(speechMetrics.averagePauseDuration);
        float toneScore   = ClampScore(speechMetrics.toneVariationScore);

        scoreBreakdown.speechSubScores.wpmScore    = wpmScore;
        scoreBreakdown.speechSubScores.fillerScore = fillerScore;
        scoreBreakdown.speechSubScores.pauseScore  = pauseScore;
        scoreBreakdown.speechSubScores.toneScore   = toneScore;

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
        // Henüz posture tracking başlamadıysa skor 0
        if (!_postureTrackingActive)
        {
            scoreBreakdown.slouchScore      = 0f;
            scoreBreakdown.swayScore        = 0f;
            scoreBreakdown.crossedArmsScore = 0f;
            return 0f;
        }

        scoreBreakdown.slouchScore      = ClampScore(100f - slouchPenalty * postureMetrics.slouchEventsPerMinute);
        scoreBreakdown.swayScore        = ClampScore(100f - swayPenalty * postureMetrics.swayDurationPercent);
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
            postureFeedback = $"Posture is acceptable. Sway: {postureMetrics.swayDurationPercent:F0}% of session.";
        else
            postureFeedback = $"Posture appears unstable. Sway: {postureMetrics.swayDurationPercent:F0}%, " +
                              $"Head warnings: {postureMetrics.slouchEventsPerMinute:F1}/min.";
    }

    private void DetermineStrengths()
    {
        float speech  = scoreBreakdown.speechScore;
        float eye     = scoreBreakdown.eyeScore;
        float posture = scoreBreakdown.postureScore;

        float max = Mathf.Max(speech, eye, posture);
        float min = Mathf.Min(speech, eye, posture);

        strongestArea = (max == speech) ? "Speech" : (max == eye) ? "Eye Contact" : "Posture";
        weakestArea   = (min == speech) ? "Speech" : (min == eye) ? "Eye Contact" : "Posture";
    }

    private FeedbackReport BuildFeedbackReport()
    {
        FeedbackReport report = new FeedbackReport
        {
            totalScore       = scoreBreakdown.totalScore,
            speechScore      = scoreBreakdown.speechScore,
            eyeScore         = scoreBreakdown.eyeScore,
            postureScore     = scoreBreakdown.postureScore,
            performanceBand  = GetPerformanceBand(),
            strongestArea    = strongestArea,
            weakestArea      = weakestArea,
            sessionTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // ── WPM ─────────────────────────────────────────────
        AddFeedbackItem(report, "Speech", "WPM",
            scoreBreakdown.speechSubScores.wpmScore,
            $"Speaking pace is ideal ({speechMetrics.wpm:F0} wpm).",
            speechMetrics.wpm < idealWpmMin
                ? $"Slightly too slow ({speechMetrics.wpm:F0} wpm). Target: {idealWpmMin}-{idealWpmMax}."
                : $"Slightly too fast ({speechMetrics.wpm:F0} wpm). Target: {idealWpmMin}-{idealWpmMax}.",
            speechMetrics.wpm < idealWpmMin
                ? $"Too slow ({speechMetrics.wpm:F0} wpm). Presentation sounds monotone."
                : $"Too fast ({speechMetrics.wpm:F0} wpm). Audience may struggle to follow."
        );

        // ── Filler Words ─────────────────────────────────────
        AddFeedbackItem(report, "Speech", "Filler Words",
            scoreBreakdown.speechSubScores.fillerScore,
            "Minimal filler words — fluent and professional.",
            $"Some filler words detected ({speechMetrics.fillerWordsPerMinute:F1}/min). Try to reduce them.",
            $"High filler word usage ({speechMetrics.fillerWordsPerMinute:F1}/min). Pause instead of filling silence."
        );

        // ── Pause Duration ───────────────────────────────────
        AddFeedbackItem(report, "Speech", "Pause Duration",
            scoreBreakdown.speechSubScores.pauseScore,
            $"Pauses are well-timed ({speechMetrics.averagePauseDuration:F1}s avg).",
            speechMetrics.averagePauseDuration < idealPauseMin
                ? $"Pauses too short ({speechMetrics.averagePauseDuration:F1}s). Give the audience time to absorb."
                : $"Pauses too long ({speechMetrics.averagePauseDuration:F1}s). Shorten them to maintain flow.",
            speechMetrics.averagePauseDuration < idealPauseMin
                ? $"Almost no pausing ({speechMetrics.averagePauseDuration:F1}s). Slow down and breathe."
                : $"Pauses far too long ({speechMetrics.averagePauseDuration:F1}s). Session flow is breaking."
        );

        // ── Tone Variation ───────────────────────────────────
        AddFeedbackItem(report, "Speech", "Tone Variation",
            scoreBreakdown.speechSubScores.toneScore,
            "Voice tone is varied and engaging.",
            "Add more tone variation to emphasise key points.",
            "Voice is flat and monotone — audience attention may drop."
        );

        // ── Eye Contact ──────────────────────────────────────
        float eyePct = eyeMetrics.eyeContactRatio * 100f;
        AddFeedbackItem(report, "Eye Contact", "Gaze Ratio",
            scoreBreakdown.eyeScore,
            $"Eye contact ratio is excellent ({eyePct:F0}%).",
            $"Eye contact is moderate ({eyePct:F0}%). Try to look at the audience more often.",
            $"Eye contact is low ({eyePct:F0}%). Face the audience, not your notes or screen."
        );

        // ── Posture: Swaying ─────────────────────────────────
        AddFeedbackItem(report, "Posture", "Head Movement",
            scoreBreakdown.swayScore,
            "Stable and grounded head movement throughout.",
            $"Some excessive head movement detected ({postureMetrics.swayDurationPercent:F0}% of session). Try to stay still.",
            $"Excessive head movement ({postureMetrics.swayDurationPercent:F0}%). Can appear nervous or distracting."
        );

        // ── Posture: Head Warnings ───────────────────────────
        AddFeedbackItem(report, "Posture", "Head Speed",
            scoreBreakdown.slouchScore,
            "Head movement speed is well controlled.",
            $"Some rapid head movements detected ({postureMetrics.slouchEventsPerMinute:F1}/min). Move more deliberately.",
            $"Frequent rapid head movements ({postureMetrics.slouchEventsPerMinute:F1}/min). Slow down your movements."
        );

        // ── Strengths / Improvements ─────────────────────────
        foreach (var item in report.items)
        {
            if (item.severity == FeedbackItem.Severity.Strength)
                report.strengths.Add($"[{item.category}] {item.metric}");
            else if (item.severity == FeedbackItem.Severity.Major)
                report.improvements.Add($"[{item.category}] {item.metric}");
        }

        return report;
    }

    private void AddFeedbackItem(FeedbackReport report,
                                  string category, string metric, float score,
                                  string strengthMsg, string minorMsg, string majorMsg)
    {
        FeedbackItem.Severity sev;
        string msg;

        if (score >= strengthThreshold)           { sev = FeedbackItem.Severity.Strength; msg = strengthMsg; }
        else if (score >= majorWeaknessThreshold) { sev = FeedbackItem.Severity.Minor;    msg = minorMsg;    }
        else                                      { sev = FeedbackItem.Severity.Major;    msg = majorMsg;    }

        report.items.Add(new FeedbackItem(category, metric, sev, msg, score));
    }

    private void UpdateUI()
    {
        if (speechScoreText  != null) speechScoreText.text  = "Speech: "  + scoreBreakdown.speechScore.ToString("F0");
        if (eyeScoreText     != null) eyeScoreText.text     = "Eye: "     + scoreBreakdown.eyeScore.ToString("F0");
        if (postureScoreText != null) postureScoreText.text = "Posture: " + scoreBreakdown.postureScore.ToString("F0");
        if (finalScoreText   != null) finalScoreText.text   = "Final Performance Score: " + scoreBreakdown.totalScore.ToString("F0");
        if (strongestText    != null) strongestText.text    = "Strongest: " + strongestArea;
        if (weakestText      != null) weakestText.text      = "Weakest: "   + weakestArea;

        if (feedbackSummaryText != null && lastReport != null)
            feedbackSummaryText.text = BuildFeedbackSummaryString(lastReport);
    }

    private string BuildFeedbackSummaryString(FeedbackReport report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"── {report.performanceBand.ToUpper()} ({report.totalScore:F0}/100) ──");
        sb.AppendLine($"Speech: {report.speechScore:F0}  |  Eye: {report.eyeScore:F0}  |  Posture: {report.postureScore:F0}");
        sb.AppendLine($"Strongest: {report.strongestArea}   Weakest: {report.weakestArea}");
        sb.AppendLine();
        sb.AppendLine("STRENGTHS");
        foreach (var it in report.items)
            if (it.severity == FeedbackItem.Severity.Strength)
                sb.AppendLine($"  • {it.message}");
        sb.AppendLine();
        sb.AppendLine("AREAS TO IMPROVE");
        foreach (var it in report.items)
        {
            if (it.severity == FeedbackItem.Severity.Major)       sb.AppendLine($"  ✗ {it.message}");
            else if (it.severity == FeedbackItem.Severity.Minor)  sb.AppendLine($"  △ {it.message}");
        }
        return sb.ToString();
    }

    // ── Normalization Helpers ─────────────────────────────────────────────────

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

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetSpeechMetrics(float wpm, float fillerPerMin, float avgPause, float toneScore)
    {
        speechMetrics.wpm                  = wpm;
        speechMetrics.fillerWordsPerMinute = fillerPerMin;
        speechMetrics.averagePauseDuration = avgPause;
        speechMetrics.toneVariationScore   = toneScore;
    }

    public void SetEyeContactRatio(float ratio)
    {
        eyeMetrics.eyeContactRatio = Mathf.Clamp01(ratio);
    }

    public void SetPostureMetrics(float slouchPerMin, float swayPercent, float crossedArmsPercent)
    {
        postureMetrics.slouchEventsPerMinute = slouchPerMin;
        postureMetrics.swayDurationPercent   = Mathf.Clamp(swayPercent, 0f, 100f);
        postureMetrics.crossedArmsPercent    = Mathf.Clamp(crossedArmsPercent, 0f, 100f);
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
