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
    public FeedbackReport detailedReport; // Takým arkadaþýnýn yazdýðý AI Raporu
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

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            filePath = Application.persistentDataPath + "/history_v3.json";
            LoadData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Engine'den gelen raporu eski sistemle birlestirip kaydeder
    public bool SaveSession(FeedbackReport report)
    {
        if (report == null)
        {
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
        if (summary == null ||
            (!summary.HasOverallScore &&
             !summary.HasEyeContactScore &&
             !summary.HasSpeechPaceScore &&
             !summary.HasPostureScore))
        {
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
            return false;
        }

        history.allSessions.Add(newSession);

        string json = JsonUtility.ToJson(history, true);
        File.WriteAllText(filePath, json);
        Debug.Log("Dosya Konumu: <color=yellow>" + Application.persistentDataPath + "</color>");
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
        }
    }

    public void DeleteAllData()
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            history = new SessionHistory();
            Debug.Log("Tüm geçmiþ veriler temizlendi!");
        }
    }
    private bool IsDuplicateLatestSession(SessionData candidate)
    {
        if (candidate == null || history == null || history.allSessions == null || history.allSessions.Count == 0)
        {
            return false;
        }

        SessionData latest = history.allSessions[history.allSessions.Count - 1];
        return Mathf.Abs(latest.overallScore - candidate.overallScore) < 0.01f &&
            Mathf.Abs(latest.eyeContact - candidate.eyeContact) < 0.01f &&
            Mathf.Abs(latest.pace - candidate.pace) < 0.01f &&
            Mathf.Abs(latest.posture - candidate.posture) < 0.01f &&
            Mathf.Abs(latest.durationSeconds - candidate.durationSeconds) < 0.1f;
    }

    private static FeedbackReport BuildFeedbackReport(SessionResultSummary summary)
    {
        float speechScore = summary.HasSpeechPaceScore ? summary.SpeechPaceScore : 0f;
        float eyeScore = summary.HasEyeContactScore ? summary.EyeContactScore : 0f;
        float postureScore = summary.HasPostureScore ? summary.PostureScore : 0f;
        float totalScore = summary.HasOverallScore
            ? summary.TotalScore
            : Mathf.Max(speechScore, eyeScore, postureScore);

        FeedbackReport report = new FeedbackReport
        {
            totalScore = totalScore,
            speechScore = speechScore,
            eyeScore = eyeScore,
            postureScore = postureScore,
            performanceBand = string.IsNullOrWhiteSpace(summary.PerformanceBand)
                ? ResolvePerformanceBand(totalScore)
                : summary.PerformanceBand,
            strongestArea = string.IsNullOrWhiteSpace(summary.StrongestArea)
                ? ResolveStrongestArea(speechScore, eyeScore, postureScore)
                : summary.StrongestArea,
            weakestArea = string.IsNullOrWhiteSpace(summary.WeakestArea)
                ? ResolveWeakestArea(speechScore, eyeScore, postureScore)
                : summary.WeakestArea,
            sessionTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        AddFeedbackItem(report, "Speech", "WPM", speechScore, "Speech pace supported the delivery.");
        AddFeedbackItem(report, "Eye Contact", "Gaze Ratio", eyeScore, "Eye contact supported audience connection.");
        AddFeedbackItem(report, "Posture", "Slouching", postureScore, "Posture supported a confident delivery.");

        foreach (string recommendation in summary.Recommendations)
        {
            if (!string.IsNullOrWhiteSpace(recommendation))
            {
                report.improvements.Add(recommendation);
            }
        }

        return report;
    }

    private static void AddFeedbackItem(FeedbackReport report, string category, string metric, float score, string strengthMessage)
    {
        FeedbackItem.Severity severity = score >= 75f
            ? FeedbackItem.Severity.Strength
            : score >= 50f
                ? FeedbackItem.Severity.Minor
                : FeedbackItem.Severity.Major;

        string message = severity == FeedbackItem.Severity.Strength
            ? strengthMessage
            : $"{category} needs attention ({score:F0}/100).";

        report.items.Add(new FeedbackItem(category, metric, severity, message, score));
        if (severity == FeedbackItem.Severity.Strength)
        {
            report.strengths.Add($"[{category}] {metric}");
        }
        else if (severity == FeedbackItem.Severity.Major)
        {
            report.improvements.Add($"[{category}] {metric}");
        }
    }

    private static string ResolveStrongestArea(float speechScore, float eyeScore, float postureScore)
    {
        float max = Mathf.Max(speechScore, eyeScore, postureScore);
        return max == speechScore ? "Speech" : max == eyeScore ? "Eye Contact" : "Posture";
    }

    private static string ResolveWeakestArea(float speechScore, float eyeScore, float postureScore)
    {
        float min = Mathf.Min(speechScore, eyeScore, postureScore);
        return min == speechScore ? "Speech" : min == eyeScore ? "Eye Contact" : "Posture";
    }

    private static string ResolvePerformanceBand(float score)
    {
        if (score >= 90f) return "Excellent";
        if (score >= 75f) return "Good";
        if (score >= 60f) return "Needs Improvement";
        return "Weak Performance";
    }

}
