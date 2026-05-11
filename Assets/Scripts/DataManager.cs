using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System;
using VRPublicSpeaking.AppShell.Data;
using VRPublicSpeaking.AppShell.PresentationQuestioning;

[System.Serializable]
public class SessionData
{
    public string sessionId;
    public string date;
    public long sessionTimestamp;
    public float overallScore;
    public float eyeContact;
    public float pace;
    public float posture;
    public float durationSeconds;
    public float fillerWordCount;
    public float wpm;
    public float fillerWordsPerMinute;
    public float averagePauseDuration;
    public float toneVariationScore;
    public float headMovementPercent;
    public float headSpeedEventsPerMinute;
    public float crossedArmsPercent;
    public bool hasOverallScore;
    public bool hasEyeContactScore;
    public bool hasSpeechPaceScore;
    public bool hasPostureScore;
    public bool hasWpm;
    public bool hasFillerWordsPerMinute;
    public bool hasAveragePauseDuration;
    public bool hasToneVariationScore;
    public bool hasHeadMovementPercent;
    public bool hasHeadSpeedEventsPerMinute;
    public bool hasCrossedArmsPercent;
    public FeedbackReport detailedReport;
    public PresentationQaResult qaResult;
}

[System.Serializable]
public class SessionHistory
{
    public List<SessionData> allSessions = new List<SessionData>();
}

public class DataManager : MonoBehaviour
{
    public static DataManager Instance;
    public SessionHistory history = new SessionHistory();
    private string filePath;
    public string currentUser = "DefaultUser";

    void Awake()
    {
        // 1. ÖLÜMSÜZLÜK VE TEKİL KOPYA (SINGLETON) KONTROLÜ
        if (Instance != null && Instance != this)
        {
            Debug.Log($"[DataManager] Sahnede zaten {Instance.currentUser} isimli bir yönetici var. Bu kopya ({gameObject.name}) yok ediliyor.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null); // Obje başka bir şeyin içindeyse zorla dışarı çıkar
        DontDestroyOnLoad(gameObject); // Sahne değişse bile silinmesini engelle

        Debug.Log("[DataManager] Ölümsüz DataManager başarıyla oluşturuldu!");

        UpdateFilePath();
        LoadData();
    }

    private void UpdateFilePath()
    {
        filePath = Application.persistentDataPath + $"/history_{currentUser}.json";
        Debug.Log($"[DataManager] Dosya yolu güncellendi: {filePath}");
    }

    public void SetUser(string username)
    {
        currentUser = username;
        UpdateFilePath();
        LoadData();

        DashboardController dc = FindFirstObjectByType<DashboardController>();
        if (dc != null)
        {
            dc.RefreshAllUI();
        }

        Debug.Log($"<color=green>[DataManager] GİRİŞ BAŞARILI! Aktif kullanıcı: {currentUser}</color>");
    }

    public bool SaveSession(FeedbackReport report)
    {
        if (report == null)
        {
            Debug.LogWarning("[DataManager] Kayıt iptal: Rapor boş (null) geldi.");
            return false;
        }

        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        SessionData newSession = new SessionData
        {
            sessionId = Guid.NewGuid().ToString("N"),
            date = System.DateTime.Now.ToString("MMM dd | HH:mm", CultureInfo.InvariantCulture),
            sessionTimestamp = timestamp,
            overallScore = report.totalScore,
            eyeContact = report.eyeScore,
            pace = report.speechScore,
            posture = report.postureScore,
            hasOverallScore = true,
            hasEyeContactScore = true,
            hasSpeechPaceScore = true,
            hasPostureScore = true,
            detailedReport = CloneFeedbackReport(report)
        };

        return SaveSessionData(newSession);
    }

    public bool SaveSession(SessionResultSummary summary)
    {
        // KAYIT NEDEN İPTAL OLUYOR KONTROLÜ
        if (summary == null)
        {
            Debug.LogWarning("[DataManager] Kayıt iptal: Gelen SessionResultSummary tamamen boş (null).");
            return false;
        }

        if (!summary.HasOverallScore &&
            !summary.HasEyeContactScore &&
            !summary.HasSpeechPaceScore &&
            !summary.HasPostureScore &&
            !summary.HasQaResult)
        {
            Debug.LogWarning("[DataManager] Kayıt iptal: Konuşma metriklerinin hepsi 0 veya algılanmamış! (Sensörler veri üretmemiş olabilir)");
            return false;
        }

        bool hasScoredMetrics =
            summary.HasOverallScore ||
            summary.HasEyeContactScore ||
            summary.HasSpeechPaceScore ||
            summary.HasPostureScore;
        FeedbackReport report = summary.DetailedReport != null
            ? CloneFeedbackReport(summary.DetailedReport)
            : hasScoredMetrics ? BuildFeedbackReport(summary) : null;
        long timestamp = summary.SessionTimestamp > 0
            ? summary.SessionTimestamp
            : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        SessionData newSession = new SessionData
        {
            sessionId = string.IsNullOrWhiteSpace(summary.SessionId) ? Guid.NewGuid().ToString("N") : summary.SessionId,
            date = System.DateTime.Now.ToString("MMM dd | HH:mm", CultureInfo.InvariantCulture),
            sessionTimestamp = timestamp,
            overallScore = summary.HasOverallScore ? summary.TotalScore : report != null ? report.totalScore : 0f,
            eyeContact = summary.HasEyeContactScore ? summary.EyeContactScore : report != null ? report.eyeScore : 0f,
            pace = summary.HasSpeechPaceScore ? summary.SpeechPaceScore : report != null ? report.speechScore : 0f,
            posture = summary.HasPostureScore ? summary.PostureScore : report != null ? report.postureScore : 0f,
            durationSeconds = summary.DurationSeconds,
            fillerWordCount = summary.FillerWordCount,
            wpm = summary.Wpm,
            fillerWordsPerMinute = summary.FillerWordsPerMinute,
            averagePauseDuration = summary.AveragePauseDuration,
            toneVariationScore = summary.ToneVariationScore,
            headMovementPercent = summary.HeadMovementPercent,
            headSpeedEventsPerMinute = summary.HeadSpeedEventsPerMinute,
            crossedArmsPercent = summary.CrossedArmsPercent,
            hasOverallScore = summary.HasOverallScore,
            hasEyeContactScore = summary.HasEyeContactScore,
            hasSpeechPaceScore = summary.HasSpeechPaceScore,
            hasPostureScore = summary.HasPostureScore,
            hasWpm = summary.HasWpm,
            hasFillerWordsPerMinute = summary.HasFillerWordsPerMinute,
            hasAveragePauseDuration = summary.HasAveragePauseDuration,
            hasToneVariationScore = summary.HasToneVariationScore,
            hasHeadMovementPercent = summary.HasHeadMovementPercent,
            hasHeadSpeedEventsPerMinute = summary.HasHeadSpeedEventsPerMinute,
            hasCrossedArmsPercent = summary.HasCrossedArmsPercent,
            detailedReport = report,
            qaResult = CloneQaResult(summary.QaResult)
        };

        if (newSession.detailedReport != null && newSession.detailedReport.sessionTimestamp <= 0)
        {
            newSession.detailedReport.sessionTimestamp = timestamp;
        }

        return SaveSessionData(newSession);
    }

    private bool SaveSessionData(SessionData newSession)
    {
        if (IsDuplicateLatestSession(newSession))
        {
            Debug.LogWarning("[DataManager] Kayıt iptal: Bu oturum zaten daha önce kaydedilmiş (Kopya veri koruması).");
            return false;
        }

        history ??= new SessionHistory();
        history.allSessions ??= new List<SessionData>();

        history.allSessions.Add(newSession);

        string json = JsonUtility.ToJson(history, true);
        File.WriteAllText(filePath, json);
        Debug.Log($"<color=cyan>[DataManager] MÜKEMMEL! Veri başarıyla '{currentUser}' adına kaydedildi.</color>");
        return true;
    }

    public void LoadData()
    {
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            history = JsonUtility.FromJson<SessionHistory>(json);
            history ??= new SessionHistory();
            history.allSessions ??= new List<SessionData>();
            Debug.Log($"[DataManager] {currentUser} için geçmiş veriler yüklendi. (Toplam {history.allSessions.Count} oturum)");
        }
        else
        {
            history = new SessionHistory();
            history.allSessions = new List<SessionData>();
            Debug.Log($"[DataManager] {currentUser} için yeni, temiz bir kayıt profili oluşturuldu.");
        }
    }

    public void DeleteAllData()
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        history = new SessionHistory();
        history.allSessions = new List<SessionData>();
        Debug.Log($"[DataManager] {currentUser} adlı kullanıcının tüm verileri silindi!");
    }

    private bool IsDuplicateLatestSession(SessionData candidate)
    {
        if (candidate == null || history == null || history.allSessions == null || history.allSessions.Count == 0) return false;

        SessionData latest = history.allSessions[history.allSessions.Count - 1];

        if (!string.IsNullOrWhiteSpace(candidate.sessionId))
        {
            for (int index = 0; index < history.allSessions.Count; index++)
            {
                SessionData existing = history.allSessions[index];
                if (existing != null && existing.sessionId == candidate.sessionId)
                {
                    return true;
                }
            }

            return false;
        }

        return Mathf.Abs(latest.overallScore - candidate.overallScore) < 0.01f &&
            Mathf.Abs(latest.eyeContact - candidate.eyeContact) < 0.01f &&
            Mathf.Abs(latest.pace - candidate.pace) < 0.01f &&
            Mathf.Abs(latest.posture - candidate.posture) < 0.01f &&
            Mathf.Abs(latest.durationSeconds - candidate.durationSeconds) < 0.1f;
    }

    // --- AŞAĞIDAKİ METOTLAR ESKİSİYLE BİREBİR AYNI (HATA YOK) ---
    private static FeedbackReport BuildFeedbackReport(SessionResultSummary summary)
    { /* ... Orijinal kodunuz ... */
        float speechScore = summary.HasSpeechPaceScore ? summary.SpeechPaceScore : 0f;
        float eyeScore = summary.HasEyeContactScore ? summary.EyeContactScore : 0f;
        float postureScore = summary.HasPostureScore ? summary.PostureScore : 0f;
        float totalScore = summary.HasOverallScore ? summary.TotalScore : Mathf.Max(speechScore, eyeScore, postureScore);

        FeedbackReport report = new FeedbackReport
        {
            totalScore = totalScore,
            speechScore = speechScore,
            eyeScore = eyeScore,
            postureScore = postureScore,
            performanceBand = string.IsNullOrWhiteSpace(summary.PerformanceBand) ? ResolvePerformanceBand(totalScore) : summary.PerformanceBand,
            strongestArea = string.IsNullOrWhiteSpace(summary.StrongestArea) ? ResolveStrongestArea(speechScore, eyeScore, postureScore) : summary.StrongestArea,
            weakestArea = string.IsNullOrWhiteSpace(summary.WeakestArea) ? ResolveWeakestArea(speechScore, eyeScore, postureScore) : summary.WeakestArea,
            sessionTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        if (summary.HasWpm)
        {
            AddFeedbackItem(report, "Speech", "WPM", summary.Wpm, "Speech pace supported the delivery.");
        }
        if (summary.HasFillerWordsPerMinute)
        {
            AddFeedbackItem(report, "Speech", "Filler Words", summary.FillerWordsPerMinute, "Filler usage supported the delivery.");
        }
        if (summary.HasAveragePauseDuration)
        {
            AddFeedbackItem(report, "Speech", "Pause Duration", summary.AveragePauseDuration, "Pauses supported the delivery.");
        }
        if (summary.HasToneVariationScore)
        {
            AddFeedbackItem(report, "Speech", "Tone Variation", summary.ToneVariationScore, "Tone variation supported the delivery.");
        }
        if (summary.HasEyeContactScore)
        {
            AddFeedbackItem(report, "Eye Contact", "Gaze Ratio", eyeScore, "Eye contact supported audience connection.");
        }
        if (summary.HasHeadSpeedEventsPerMinute)
        {
            AddFeedbackItem(report, "Posture", "Head Speed", summary.HeadSpeedEventsPerMinute, "Posture supported a confident delivery.");
        }
        if (summary.HasHeadMovementPercent)
        {
            AddFeedbackItem(report, "Posture", "Head Movement", summary.HeadMovementPercent, "Head movement supported the delivery.");
        }
        if (summary.HasCrossedArmsPercent)
        {
            AddFeedbackItem(report, "Posture", "Crossed Arms", summary.CrossedArmsPercent, "Open posture supported the delivery.");
        }

        foreach (string recommendation in summary.Recommendations)
        {
            if (!string.IsNullOrWhiteSpace(recommendation)) report.improvements.Add(recommendation);
        }
        return report;
    }
    private static void AddFeedbackItem(FeedbackReport report, string category, string metric, float score, string strengthMessage)
    {
        FeedbackItem.Severity severity = score >= 75f ? FeedbackItem.Severity.Strength : score >= 50f ? FeedbackItem.Severity.Minor : FeedbackItem.Severity.Major;
        string message = severity == FeedbackItem.Severity.Strength ? strengthMessage : $"{category} needs attention ({score:F0}/100).";
        report.items.Add(new FeedbackItem(category, metric, severity, message, score));
        if (severity == FeedbackItem.Severity.Strength) report.strengths.Add($"[{category}] {metric}");
        else if (severity == FeedbackItem.Severity.Major) report.improvements.Add($"[{category}] {metric}");
    }
    private static string ResolveStrongestArea(float s, float e, float p) { float m = Mathf.Max(s, e, p); return m == s ? "Speech" : m == e ? "Eye Contact" : "Posture"; }
    private static string ResolveWeakestArea(float s, float e, float p) { float m = Mathf.Min(s, e, p); return m == s ? "Speech" : m == e ? "Eye Contact" : "Posture"; }
    private static string ResolvePerformanceBand(float score) { if (score >= 90f) return "Excellent"; if (score >= 75f) return "Good"; if (score >= 60f) return "Needs Improvement"; return "Weak Performance"; }

    private static FeedbackReport CloneFeedbackReport(FeedbackReport report)
    {
        if (report == null)
        {
            return null;
        }

        FeedbackReport clone = new FeedbackReport
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
            foreach (FeedbackItem item in report.items)
            {
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

        PresentationQaResult clone = new PresentationQaResult
        {
            deckId = result.deckId,
            deckName = result.deckName,
            status = result.status,
            summary = result.summary,
            completedUnixTime = result.completedUnixTime,
            qaScore = result.qaScore,
            averageAccuracy = result.averageAccuracy,
            averageCoverage = result.averageCoverage,
            averageClarity = result.averageClarity
        };

        if (result.answers != null)
        {
            foreach (PresentationQaAnswer answer in result.answers)
            {
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
                    feedback = answer.feedback == null ? null : new PresentationAnswerFeedback
                    {
                        accuracy = answer.feedback.accuracy,
                        coverage = answer.feedback.coverage,
                        clarity = answer.feedback.clarity,
                        summary = answer.feedback.summary,
                        betterAnswer = answer.feedback.betterAnswer,
                        status = answer.feedback.status
                    }
                });
            }
        }

        return clone;
    }
}
