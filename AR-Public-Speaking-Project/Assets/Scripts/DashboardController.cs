using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DashboardController : MonoBehaviour
{
    [Header("Overview Page (Genel Bakýþ)")]
    public TextMeshProUGUI overviewScoreText;  // Ortadaki büyük skor (82 vb.)
    public TextMeshProUGUI overviewDateText;   // Overview sayfasýndaki tarih yazýsý

    [Header("Performance Page (Detaylar)")]
    public Image[] performanceBars;            // 4 adet bar (Eye, Pace, Posture, Filler)
    public TextMeshProUGUI[] performanceTexts; // Barlarýn yanýndaki % yazýlarý
    public TextMeshProUGUI performanceDateText; // Performance sayfasýndaki tarih (opsiyonel)

    [Header("History Page (Geçmiþ)")]
    public LineRenderer chartLine;
    public TextMeshProUGUI[] cardScores;       // Kartlarýn içindeki küçük skorlar
    public TextMeshProUGUI[] cardDates;        // Kartlarýn içindeki tarih yazýlarý
    public TextMeshProUGUI historyDateText;    // Sol üstteki büyük tarih (dateText yerine)

    [Header("Grafik Etiketleri (s1, s2...)")]
    public TextMeshProUGUI[] chartLabels;

    void Start()
    {
        // Uygulama açýldýðýnda varsa en son veriyi tüm sayfalara yükle
        var all = DataManager.Instance.history.allSessions;
        if (all.Count > 0)
        {
            DisplaySession(all[all.Count - 1]);
        }
        RefreshHistoryCards();
        UpdateChart();
    }

    // --- TÜM SAYFALARI GÜNCELLEYEN ANA FONKSÝYON ---
    public void DisplaySession(SessionData data)
    {
        // 1. Overview Güncelle
        if (overviewScoreText != null) overviewScoreText.text = data.overallScore.ToString("F0");
        if (overviewDateText != null) overviewDateText.text = data.date;

        // 2. Performance Güncelle
        for (int i = 0; i < performanceBars.Length; i++)
        {
            float val = 0;
            if (i == 0) val = data.eyeContact;
            else if (i == 1) val = data.pace;
            else if (i == 2) val = data.posture;
            else if (i == 3) val = (data.eyeContact + data.pace + data.posture) / 3f;

            performanceBars[i].fillAmount = val / 100f;
            if (i < performanceTexts.Length)
                performanceTexts[i].text = "%" + val.ToString("F0");
        }
        if (performanceDateText != null) performanceDateText.text = data.date;

        // 3. History Üst Tarih Güncelle
        if (historyDateText != null) historyDateText.text = data.date;
    }

    // --- VERÝ KAYDETME FONKSÝYONU ---
    public void FinishAndSaveSession()
    {
        // --- TEST MODU: Her kayýtta farklý sayýlar üretir ---
        // Gerçek sistemde burasý sensör verilerini alacak, þimdilik rastgele:
        float randomScore = Random.Range(60f, 100f);
        float randomEye = Random.Range(50f, 100f);
        float randomPace = Random.Range(40f, 95f);
        float randomPost = Random.Range(60f, 90f);

        // 1. Önce bu rastgele verileri DataManager'a gönder ve kaydet
        DataManager.Instance.SaveSession(randomScore, randomEye, randomPace, randomPost);

        // 2. Kaydedilen bu yeni veriyi hemen ekrana (tüm sayfalara) yansýt
        // Son eklenen veriyi alýyoruz
        var all = DataManager.Instance.history.allSessions;
        if (all.Count > 0)
        {
            DisplaySession(all[all.Count - 1]);
        }

        // 3. Grafik ve Kartlarý tazele
        RefreshHistoryCards();
        UpdateChart();

        Debug.Log("Yeni rastgele veriler kaydedildi ve ekran güncellendi!");
    }

    // --- GEÇMÝÞ KARTLARINI TAZELEME ---
    public void RefreshHistoryCards()
    {
        var all = DataManager.Instance.history.allSessions;
        for (int i = 0; i < cardScores.Length; i++)
        {
            int dataIndex = all.Count - 1 - i;
            if (dataIndex >= 0)
            {
                cardScores[i].text = all[dataIndex].overallScore.ToString("F0");
                cardDates[i].text = all[dataIndex].date;
            }
            else
            {
                cardScores[i].text = "-";
                cardDates[i].text = "--/--/----";
            }
        }
    }

    // --- GRAFÝK GÜNCELLEME (KAYAR PENCERE) ---
    public void UpdateChart()
    {
        var all = DataManager.Instance.history.allSessions;
        int count = Mathf.Min(all.Count, 10);
        chartLine.positionCount = count;

        for (int i = 0; i < count; i++)
        {
            int dataIndex = all.Count - count + i;
            float score = all[dataIndex].overallScore;

            float xPos = (i * 95f) - 400f; // Geniþlik ve hizalama ayarý
            float yPos = (score * 2.5f) - 120f; // Yükseklik ayarý

            chartLine.SetPosition(i, new Vector3(xPos, yPos, 0));

            // s1, s2 etiketlerini kaydýr
            if (i < chartLabels.Length)
            {
                int sessionNum = all.Count - count + i + 1;
                chartLabels[i].text = "s" + sessionNum;
            }
        }
    }

    // --- SÝSTEMÝ SIFIRLAMA ---
    public void ResetSystem()
    {
        DataManager.Instance.DeleteAllData();
        chartLine.positionCount = 0;
        RefreshHistoryCards();

        if (overviewScoreText != null) overviewScoreText.text = "0";
        if (overviewDateText != null) overviewDateText.text = "--/--/----";
        if (historyDateText != null) historyDateText.text = "--/--/----";

        Debug.Log("Sistem Sýfýrlandý!");
    }
}