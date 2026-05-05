using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Text;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;

public class DashboardController : MonoBehaviour
{
    [Header("Overview Page (Genel Bakýþ)")]
    public TextMeshProUGUI overviewScoreText;
    public TextMeshProUGUI overviewDateText;

    [Header("Performance Page (Eski Ana Barlar)")]
    public Image[] performanceBars;
    public TextMeshProUGUI[] performanceTexts;
    public TextMeshProUGUI performanceDateText;

    [Header("Performance Page (Yeni Detaylý Sütunlar)")]
    public TextMeshProUGUI wpmValueText;
    public TextMeshProUGUI fillerValueText;
    public TextMeshProUGUI pauseValueText;
    public TextMeshProUGUI toneValueText;
    public TextMeshProUGUI eyeRatioValueText;
    public TextMeshProUGUI slouchValueText;
    public TextMeshProUGUI swayValueText;
    public TextMeshProUGUI crossedArmsValueText;

    [Header("AI Coach Page Elements")]
    public TextMeshProUGUI performanceBandText;
    public TextMeshProUGUI strongestAreaText;
    public TextMeshProUGUI weakestAreaText;
    public TextMeshProUGUI speechFeedbackSummary;
    public TextMeshProUGUI eyeFeedbackSummary;
    public TextMeshProUGUI postureFeedbackSummary;
    public TextMeshProUGUI fullCoachNotes;

    [Header("History Page (Geçmiþ ve Grafik)")]
    public LineRenderer chartLine;
    public TextMeshProUGUI[] cardScores;
    public TextMeshProUGUI[] cardDates;
    public TextMeshProUGUI historyDateText;
    public TextMeshProUGUI[] chartLabels;

    void Start()
    {
        EnsureDataManager();

        if (PerformanceScoringEngine.Instance != null)
        {
            PerformanceScoringEngine.Instance.OnScoreCalculated += HandleNewSessionData;
        }
    }

    // YENÝ EKLENDÝ: Ekran her aktif olduðunda son veriyi çeker ve UI'ý günceller
    void OnEnable()
    {
        ImportLatestAppShellResult();
        RefreshAllUI();
    }

    private void OnDestroy()
    {
        if (PerformanceScoringEngine.Instance != null)
        {
            PerformanceScoringEngine.Instance.OnScoreCalculated -= HandleNewSessionData;
        }
    }

    public void HandleNewSessionData(FeedbackReport report)
    {
        if (DataManager.Instance != null)
        {
            DataManager.Instance.SaveSession(report);
            RefreshAllUI();
        }
    }

    public void RefreshAllUI()
    {
        if (DataManager.Instance == null) return;

        var all = DataManager.Instance.history.allSessions;
        if (all.Count > 0)
        {
            DisplaySession(all[all.Count - 1]);
        }
        RefreshHistoryCards();
        UpdateChart();
    }

    private void EnsureDataManager()
    {
        if (DataManager.Instance != null) return;

        GameObject dataManagerRoot = new GameObject("DataManager_Auto");
        dataManagerRoot.AddComponent<DataManager>();
    }

    private void ImportLatestAppShellResult()
    {
        Debug.Log("[DashboardController] Veri çekme (Import) iþlemi baþladý...");

        if (DataManager.Instance == null)
        {
            Debug.LogError("[DashboardController] HATA: DataManager bulunamadý!");
            return;
        }

        if (!AppRuntimeState.HasInstance)
        {
            Debug.LogError("[DashboardController] HATA: AppRuntimeState bulunamadý! Motor veriyi tutmuyor.");
            return;
        }

        SessionResultSummary latestSummary = AppRuntimeState.Instance.GetLastSessionResultCopy();

        if (latestSummary == null)
        {
            Debug.LogWarning("[DashboardController] AppRuntimeState bulundu ama oturum verisi BOÞ (null).");
            return;
        }

        bool basarili = DataManager.Instance.SaveSession(latestSummary);

        if (basarili)
        {
            Debug.Log("<color=green>[DashboardController] MÜKEMMEL! Sahne geçiþinde konuþma verisi baþarýyla yakalandý ve kaydedildi!</color>");
        }
        else
        {
            Debug.LogWarning("[DashboardController] Veri bulundu ancak DataManager kaydetmeyi reddetti (veri 0 veya ayný veri az önce kaydedildi).");
        }
    }

    public void DisplaySession(SessionData data)
    {
        if (data == null) return;

        if (overviewScoreText != null) overviewScoreText.text = data.overallScore.ToString("F0");
        if (overviewDateText != null) overviewDateText.text = data.date;

        if (performanceBars != null)
        {
            for (int i = 0; i < performanceBars.Length; i++)
            {
                float val = 0;
                if (i == 0) val = data.eyeContact;
                else if (i == 1) val = data.pace;
                else if (i == 2) val = data.posture;
                else if (i == 3) val = (data.eyeContact + data.pace + data.posture) / 3f;

                if (performanceBars[i] != null) performanceBars[i].fillAmount = val / 100f;
                if (performanceTexts != null && i < performanceTexts.Length && performanceTexts[i] != null)
                    performanceTexts[i].text = "%" + val.ToString("F0");
            }
        }
        if (performanceDateText != null) performanceDateText.text = data.date;

        if (data.detailedReport != null)
        {
            UpdateDetailedColumns(data.detailedReport, data.eyeContact);
            UpdateAICoachPage(data.detailedReport);
        }

        if (historyDateText != null) historyDateText.text = data.date;
    }

    private void UpdateDetailedColumns(FeedbackReport report, float eyeContactRaw)
    {
        var items = report.items;

        if (wpmValueText != null) wpmValueText.text = GetMetricValue(items, "WPM") + " WPM";
        if (fillerValueText != null) fillerValueText.text = GetMetricValue(items, "Filler Words") + " /min";
        if (pauseValueText != null) pauseValueText.text = GetMetricValue(items, "Pause Duration") + "s";
        if (toneValueText != null) toneValueText.text = "%" + GetMetricValue(items, "Tone Variation");

        if (eyeRatioValueText != null) eyeRatioValueText.text = "%" + eyeContactRaw.ToString("F0");

        if (slouchValueText != null) slouchValueText.text = GetMetricValue(items, "Slouching") + " /min";
        if (swayValueText != null) swayValueText.text = "%" + GetMetricValue(items, "Swaying");
        if (crossedArmsValueText != null) crossedArmsValueText.text = "%" + GetMetricValue(items, "Crossed Arms");
    }

    private void UpdateAICoachPage(FeedbackReport report)
    {
        if (performanceBandText != null) performanceBandText.text = report.performanceBand.ToUpper();
        if (strongestAreaText != null) strongestAreaText.text = "Strongest Area: " + report.strongestArea;
        if (weakestAreaText != null) weakestAreaText.text = "Weakest Area: " + report.weakestArea;

        if (speechFeedbackSummary != null) speechFeedbackSummary.text = GetCategorySummary(report.items, "Speech");
        if (eyeFeedbackSummary != null) eyeFeedbackSummary.text = GetCategorySummary(report.items, "Eye Contact");
        if (postureFeedbackSummary != null) postureFeedbackSummary.text = GetCategorySummary(report.items, "Posture");

        if (fullCoachNotes != null)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<color=#00FF00><b>+ Strengths </b></color>");
            foreach (var item in report.items)
                if (item.severity == FeedbackItem.Severity.Strength)
                    sb.AppendLine("• " + item.message);

            sb.AppendLine("\n<color=#FF4444><b>- Weaknesses</b></color>");
            foreach (var item in report.items)
                if (item.severity == FeedbackItem.Severity.Major || item.severity == FeedbackItem.Severity.Minor)
                    sb.AppendLine("• " + item.message);

            fullCoachNotes.text = sb.ToString();
        }
    }

    private string GetMetricValue(List<FeedbackItem> items, string metricName)
    {
        var item = items.Find(x => x.metric == metricName);
        return item != null ? item.score.ToString("F1") : "--";
    }

    private string GetCategorySummary(List<FeedbackItem> items, string category)
    {
        var summary = items.Find(x => x.category == category && x.severity != FeedbackItem.Severity.Strength);
        if (summary == null) summary = items.Find(x => x.category == category);
        return summary != null ? summary.message : "Analysis complete.";
    }

    public void RefreshHistoryCards()
    {
        if (DataManager.Instance == null || cardScores == null || cardDates == null) return;

        var all = DataManager.Instance.history.allSessions;
        for (int i = 0; i < cardScores.Length; i++)
        {
            int dataIndex = all.Count - 1 - i;
            if (dataIndex >= 0 && dataIndex < all.Count)
            {
                if (cardScores[i] != null) cardScores[i].text = all[dataIndex].overallScore.ToString("F0");
                if (cardDates[i] != null) cardDates[i].text = all[dataIndex].date;
            }
            else
            {
                if (cardScores[i] != null) cardScores[i].text = "-";
                if (cardDates[i] != null) cardDates[i].text = "--/--/----";
            }
        }
    }

    public void UpdateChart()
    {
        if (DataManager.Instance == null || chartLine == null) return;

        var all = DataManager.Instance.history.allSessions;
        int count = Mathf.Min(all.Count, 10);
        chartLine.positionCount = count;

        for (int i = 0; i < count; i++)
        {
            int dataIndex = all.Count - count + i;
            float score = all[dataIndex].overallScore;

            float xPos = (i * 95f) - 400f;
            float yPos = (score * 2.5f) - 120f;

            chartLine.SetPosition(i, new Vector3(xPos, yPos, 0));

            if (chartLabels != null && i < chartLabels.Length && chartLabels[i] != null)
            {
                int sessionNum = all.Count - count + i + 1;
                chartLabels[i].text = "s" + sessionNum;
            }
        }
    }

    public void ResetSystem()
    {
        if (DataManager.Instance != null)
        {
            DataManager.Instance.DeleteAllData();
            if (chartLine != null) chartLine.positionCount = 0;

            if (chartLabels != null)
            {
                for (int i = 0; i < chartLabels.Length; i++)
                {
                    if (chartLabels[i] != null)
                        chartLabels[i].text = "s" + (i + 1);
                }
            }

            RefreshHistoryCards();

            if (overviewScoreText != null) overviewScoreText.text = "0";
            if (overviewDateText != null) overviewDateText.text = "--/--/----";
            if (historyDateText != null) historyDateText.text = "--/--/----";

            Debug.Log("Sistem Sýfýrlandý! Grafik temizlendi ve etiketler s1-s9 yapýldý.");
        }
    }
}