using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using VRPublicSpeaking.AppShell.Data;

[System.Serializable]
public class SessionData
{
    public string date;
    public float overallScore;
    public float eyeContact;
    public float pace;
    public float posture;
    public float durationSeconds;
    public FeedbackReport detailedReport;
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
        // 1. ÖLÜMSÜZLÜK VE TEKÝL KOPYA (SINGLETON) KONTROLÜ
        if (Instance != null && Instance != this)
        {
            Debug.Log($"[DataManager] Sahnede zaten {Instance.currentUser} isimli bir yönetici var. Bu kopya ({gameObject.name}) yok ediliyor.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null); // Obje baþka bir þeyin içindeyse zorla dýþarý çýkar
        DontDestroyOnLoad(gameObject); // Sahne deðiþse bile silinmesini engelle

        Debug.Log("[DataManager] Ölümsüz DataManager baþarýyla oluþturuldu!");

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

        Debug.Log($"<color=green>[DataManager] GÝRÝÞ BAÞARILI! Aktif kullanýcý: {currentUser}</color>");
    }

    public bool SaveSession(FeedbackReport report)
    {
        if (report == null)
        {
            Debug.LogWarning("[DataManager] Kayýt iptal: Rapor boþ (null) geldi.");
            return false;
        }

        SessionData newSession = new SessionData
        {
            date = System.DateTime.Now.ToString("MMM dd | HH:mm", CultureInfo.InvariantCulture),
            overallScore = report.totalScore,
            eyeContact = report.eyeScore,
            pace = report.speechScore,
            posture = report.postureScore,
            detailedReport = report
        };

        return SaveSessionData(newSession);
    }

    public bool SaveSession(SessionResultSummary summary)
    {
        // KAYIT NEDEN ÝPTAL OLUYOR KONTROLÜ
        if (summary == null)
        {
            Debug.LogWarning("[DataManager] Kayýt iptal: Gelen SessionResultSummary tamamen boþ (null).");
            return false;
        }

        if (!summary.HasOverallScore && !summary.HasEyeContactScore && !summary.HasSpeechPaceScore && !summary.HasPostureScore)
        {
            Debug.LogWarning("[DataManager] Kayýt iptal: Konuþma metriklerinin hepsi 0 veya algýlanmamýþ! (Sensörler veri üretmemiþ olabilir)");
            return false;
        }

        FeedbackReport report = BuildFeedbackReport(summary);
        SessionData newSession = new SessionData
        {
            date = System.DateTime.Now.ToString("MMM dd | HH:mm", CultureInfo.InvariantCulture),
            overallScore = summary.HasOverallScore ? summary.TotalScore : report.totalScore,
            eyeContact = summary.HasEyeContactScore ? summary.EyeContactScore : report.eyeScore,
            pace = summary.HasSpeechPaceScore ? summary.SpeechPaceScore : report.speechScore,
            posture = summary.HasPostureScore ? summary.PostureScore : report.postureScore,
            durationSeconds = summary.DurationSeconds,
            detailedReport = report
        };

        return SaveSessionData(newSession);
    }

    private bool SaveSessionData(SessionData newSession)
    {
        if (IsDuplicateLatestSession(newSession))
        {
            Debug.LogWarning("[DataManager] Kayýt iptal: Bu oturum zaten daha önce kaydedilmiþ (Kopya veri korumasý).");
            return false;
        }

        history.allSessions.Add(newSession);

        string json = JsonUtility.ToJson(history, true);
        File.WriteAllText(filePath, json);
        Debug.Log($"<color=cyan>[DataManager] MÜKEMMEL! Veri baþarýyla '{currentUser}' adýna kaydedildi.</color>");
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
            Debug.Log($"[DataManager] {currentUser} için geçmiþ veriler yüklendi. (Toplam {history.allSessions.Count} oturum)");
        }
        else
        {
            history = new SessionHistory();
            history.allSessions = new List<SessionData>();
            Debug.Log($"[DataManager] {currentUser} için yeni, temiz bir kayýt profili oluþturuldu.");
        }
    }

    public void DeleteAllData()
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            history = new SessionHistory();
            Debug.Log($"[DataManager] {currentUser} adlý kullanýcýnýn tüm verileri silindi!");
        }
    }

    private bool IsDuplicateLatestSession(SessionData candidate)
    {
        if (candidate == null || history == null || history.allSessions == null || history.allSessions.Count == 0) return false;

        SessionData latest = history.allSessions[history.allSessions.Count - 1];
        return Mathf.Abs(latest.overallScore - candidate.overallScore) < 0.01f &&
            Mathf.Abs(latest.eyeContact - candidate.eyeContact) < 0.01f &&
            Mathf.Abs(latest.pace - candidate.pace) < 0.01f &&
            Mathf.Abs(latest.posture - candidate.posture) < 0.01f &&
            Mathf.Abs(latest.durationSeconds - candidate.durationSeconds) < 0.1f;
    }

    // --- AÞAÐIDAKÝ METOTLAR ESKÝSÝYLE BÝREBÝR AYNI (HATA YOK) ---
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

        AddFeedbackItem(report, "Speech", "WPM", speechScore, "Speech pace supported the delivery.");
        AddFeedbackItem(report, "Eye Contact", "Gaze Ratio", eyeScore, "Eye contact supported audience connection.");
        AddFeedbackItem(report, "Posture", "Slouching", postureScore, "Posture supported a confident delivery.");

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
}