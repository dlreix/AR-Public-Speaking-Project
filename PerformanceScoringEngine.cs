// selin
using TMPro;
using UnityEngine;

[System.Serializable]
public class SpeechMetrics
{
    [Header("Raw Speech Inputs")]
    public float wpm = 140f;
    public float fillerWordsPerMinute = 2f;
    public float averagePauseDuration = 0.8f;   // seconds
    [Range(0f, 100f)] public float toneVariationScore = 75f; // already normalized
}

[System.Serializable]
public class EyeMetrics
{
    [Header("Raw Eye Contact Inputs")]
    [Range(0f, 1f)] public float eyeContactRatio = 0.65f; // time looking at audience / total session time
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
    [Header("UI - Skor Metinleri")]
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI speechScoreText;
    public TextMeshProUGUI eyeScoreText;
    public TextMeshProUGUI postureScoreText;

    [Header("UI - Feedback Paneli")]
    public TextMeshProUGUI strongestText;
    public TextMeshProUGUI weakestText;
    public TextMeshProUGUI feedbackSummaryText;

    [Header("Eye Tracking (Gelismis)")]
    [Tooltip("GazeScoringSystem bagliysa onun skorunu kullanir, degilse eyeContactRatio'ya duser.")]
    public GazeScoringSystem gazeScoringSystem;

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
    public float slouchPenalty = 8f;
    public float swayPenalty = 0.5f;
    public float crossedArmsPenalty = 0.4f;

    [Header("Auto Recalculate")]
    public bool calculateEveryFrame = false;

    private void Start()
    {
        if (speechScoreText != null)     speechScoreText.text     = "Speech: -";
        if (eyeScoreText != null)        eyeScoreText.text        = "Eye: -";
        if (postureScoreText != null)    postureScoreText.text    = "Posture: -";
        if (finalScoreText != null)      finalScoreText.text      = "Press SPACE to start speaking...";
        if (strongestText != null)       strongestText.text       = "Strongest: -";
        if (weakestText != null)         weakestText.text         = "Weakest: -";
        if (feedbackSummaryText != null) feedbackSummaryText.text = "";
    }

    private void Update()
    {
        if (calculateEveryFrame)
            CalculateSessionScore();
    }

    [ContextMenu("Calculate Session Score")]
    public void CalculateSessionScore()
    {
        scoreBreakdown.speechScore  = CalculateSpeechScore();
        scoreBreakdown.eyeScore     = CalculateEyeScore();
        scoreBreakdown.postureScore = CalculatePostureScore();

        scoreBreakdown.totalScore =
            (speechFinalWeight * scoreBreakdown.speechScore) +
            (eyeFinalWeight    * scoreBreakdown.eyeScore)    +
            (postureFinalWeight * scoreBreakdown.postureScore);

        scoreBreakdown.totalScore = ClampScore(scoreBreakdown.totalScore);

        Debug.Log(
            "[PerformanceScoringEngine] Speech=" + scoreBreakdown.speechScore.ToString("F1") +
            ", Eye=" + scoreBreakdown.eyeScore.ToString("F1") +
            ", Posture=" + scoreBreakdown.postureScore.ToString("F1") +
            ", Total=" + scoreBreakdown.totalScore.ToString("F1")
        );

        if (speechScoreText != null)
            speechScoreText.text = "Speech: " + scoreBreakdown.speechScore.ToString("F0");

        if (eyeScoreText != null)
            eyeScoreText.text = "Eye: " + scoreBreakdown.eyeScore.ToString("F0");

        if (postureScoreText != null)
            postureScoreText.text = "Posture: " + scoreBreakdown.postureScore.ToString("F0");

        if (finalScoreText != null)
            finalScoreText.text = "Final Performance Score: " + scoreBreakdown.totalScore.ToString("F0");

        // Strongest / Weakest
        string strongest = GetStrongestCategory();
        string weakest   = GetWeakestCategory();

        if (strongestText != null)
            strongestText.text = "Strongest: " + strongest;

        if (weakestText != null)
            weakestText.text = "Weakest: " + weakest;

        // Feedback summary
        if (feedbackSummaryText != null)
        {
            string band       = GetPerformanceBand();
            string paceLine   = GetPaceFeedback();
            string fillerLine = GetFillerFeedback();
            string toneLine   = GetToneFeedback();

            feedbackSummaryText.text =
                "-- " + band.ToUpper() + " (" + scoreBreakdown.totalScore.ToString("F0") + "/100) --\n" +
                "Speech: " + scoreBreakdown.speechScore.ToString("F0") + "  |  " +
                "Eye: " + scoreBreakdown.eyeScore.ToString("F0") + "  |  " +
                "Posture: " + scoreBreakdown.postureScore.ToString("F0") + "\n" +
                "Strongest: " + strongest + "    Weakest: " + weakest + "\n\n" +
                paceLine + "\n" +
                fillerLine + "\n" +
                toneLine;
        }
    }

    // ---- Speech feedback helpers ----

    private string GetPaceFeedback()
    {
        float wpm = speechMetrics.wpm;
        if (wpm <= 0f)   return "";
        if (wpm < 80f)   return "Pace: " + wpm.ToString("F0") + " WPM - too slow, try to speed up";
        if (wpm < 120f)  return "Pace: " + wpm.ToString("F0") + " WPM - a bit slow";
        if (wpm <= 160f) return "Pace: " + wpm.ToString("F0") + " WPM - good pace";
        if (wpm <= 200f) return "Pace: " + wpm.ToString("F0") + " WPM - a bit fast";
        return                  "Pace: " + wpm.ToString("F0") + " WPM - too fast, slow down";
    }

    private string GetFillerFeedback()
    {
        float f = speechMetrics.fillerWordsPerMinute;
        if (f <= 0f) return "Filler words: none";
        if (f < 2f)  return "Filler words: " + f.ToString("F1") + "/min - acceptable";
        if (f < 5f)  return "Filler words: " + f.ToString("F1") + "/min - try to reduce";
        return              "Filler words: " + f.ToString("F1") + "/min - avoid filler words!";
    }

    private string GetToneFeedback()
    {
        float t = speechMetrics.toneVariationScore;
        if (t <= 0f) return "";
        if (t < 30f) return "Tone: try varying your pitch more";
        if (t < 60f) return "Tone: natural vocal variety";
        return              "Tone: very expressive delivery";
    }

    // ---- Score calculation ----

    private float CalculateSpeechScore()
    {
        float wpmScore    = NormalizeWpm(speechMetrics.wpm);
        float fillerScore = NormalizeInverse(speechMetrics.fillerWordsPerMinute, idealFillerPerMin, maxFillerPerMin);
        float pauseScore  = NormalizePauseDuration(speechMetrics.averagePauseDuration);
        float toneScore   = ClampScore(speechMetrics.toneVariationScore);

        return ClampScore(
            (wpmWeight    * wpmScore)    +
            (fillerWeight * fillerScore) +
            (pauseWeight  * pauseScore)  +
            (toneWeight   * toneScore)
        );
    }

    private float CalculateEyeScore()
    {
        // GazeScoringSystem bagliysa onun gelismis skorunu kullan
        if (gazeScoringSystem != null)
            return ClampScore(gazeScoringSystem.GazeScore);

        // Bagli degilse eski yonteme dus (fallback)
        return ClampScore(eyeMetrics.eyeContactRatio * 100f);
    }

    private float CalculatePostureScore()
    {
        return ClampScore(
            100f -
            (slouchPenalty      * postureMetrics.slouchEventsPerMinute) -
            (swayPenalty        * postureMetrics.swayDurationPercent)   -
            (crossedArmsPenalty * postureMetrics.crossedArmsPercent)
        );
    }

    private float NormalizeWpm(float wpm)
    {
        if (wpm >= idealWpmMin && wpm <= idealWpmMax) return 100f;
        if (wpm < idealWpmMin)
        {
            if (wpm <= minAcceptableWpm) return 0f;
            return Mathf.InverseLerp(minAcceptableWpm, idealWpmMin, wpm) * 100f;
        }
        if (wpm >= maxAcceptableWpm) return 0f;
        return (1f - Mathf.InverseLerp(idealWpmMax, maxAcceptableWpm, wpm)) * 100f;
    }

    private float NormalizePauseDuration(float pause)
    {
        if (pause >= idealPauseMin && pause <= idealPauseMax) return 100f;
        if (pause < idealPauseMin)
        {
            if (pause <= minAcceptablePause) return 0f;
            return Mathf.InverseLerp(minAcceptablePause, idealPauseMin, pause) * 100f;
        }
        if (pause >= maxAcceptablePause) return 0f;
        return (1f - Mathf.InverseLerp(idealPauseMax, maxAcceptablePause, pause)) * 100f;
    }

    private float NormalizeInverse(float value, float idealMin, float maxValue)
    {
        if (value <= idealMin) return 100f;
        if (value >= maxValue) return 0f;
        return (1f - Mathf.InverseLerp(idealMin, maxValue, value)) * 100f;
    }

    private float ClampScore(float value)
    {
        return Mathf.Clamp(value, 0f, 100f);
    }

    // ---- Helpers ----

    private string GetStrongestCategory()
    {
        float s = scoreBreakdown.speechScore;
        float e = scoreBreakdown.eyeScore;
        float p = scoreBreakdown.postureScore;
        if (s >= e && s >= p) return "Speech";
        if (e >= s && e >= p) return "Eye Contact";
        return "Posture";
    }

    private string GetWeakestCategory()
    {
        float s = scoreBreakdown.speechScore;
        float e = scoreBreakdown.eyeScore;
        float p = scoreBreakdown.postureScore;
        if (s <= e && s <= p) return "Speech";
        if (e <= s && e <= p) return "Eye Contact";
        return "Posture";
    }

    // ---- Public setters for other modules ----

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

    public float GetFinalScore()
    {
        return scoreBreakdown.totalScore;
    }

    public string GetPerformanceBand()
    {
        float score = scoreBreakdown.totalScore;
        if (score >= 90f) return "Excellent";
        if (score >= 75f) return "Good";
        if (score >= 60f) return "Needs Improvement";
        return "Weak Performance";
    }
}