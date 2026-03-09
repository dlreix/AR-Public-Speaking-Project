using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class DashboardController : MonoBehaviour
{
    [Header("Main Overview")]
    public TextMeshProUGUI mainScoreText;

    [Header("Performance Stats")]
    public Image[] progressBars; // 4 tane barý buraya sürükle
    public TextMeshProUGUI[] progressTexts; // %74, 142 WPM gibi yazýlar

    [Header("History Page - Chart")]
    public LineRenderer chartLine;

    [Header("History Page - Cards")]
    public TextMeshProUGUI[] cardScores; // Kartlardaki 82, 70, 70 yazýlarý
    public TextMeshProUGUI[] cardDates;  // Today, Tuesday vb. yazýlarý

    public void UpdateAllData()
    {
        // 1. Ana Skoru Güncelle
        if (mainScoreText) mainScoreText.text = Random.Range(75, 98).ToString();

        // 2. Performans Barlarýný ve Yazýlarýný Güncelle
        for (int i = 0; i < progressBars.Length; i++)
        {
            if (progressBars[i] != null)
            {
                float randomFill = Random.Range(0.1f, 0.9f);
                progressBars[i].fillAmount = randomFill;

                // HATA BURADA OLABÝLÝR: 'i' harfinin bu süslü parantezler içinde olduðundan emin ol!
                if (i < progressTexts.Length && progressTexts[i] != null)
                {
                    progressTexts[i].text = "%" + (randomFill * 100).ToString("F0");
                }
            }
        }

        // 3. Grafiði Canlandýr (Zikzak Çizgi)
        if (chartLine != null)
        {
            for (int i = 0; i < chartLine.positionCount; i++)
            {
                float randomY = Random.Range(-30f, 120f); // Senin koordinatlarýna göre ayarlandý
                Vector3 currentPos = chartLine.GetPosition(i);
                chartLine.SetPosition(i, new Vector3(currentPos.x, randomY, 0));
            }
        }

        // 4. Geçmiþ Kartlarýný (History Cards) Güncelle
        string[] days = { "Today", "Yesterday", "Friday", "Thursday", "Tuesday" };
        for (int i = 0; i < cardScores.Length; i++)
        {
            cardScores[i].text = Random.Range(65, 90).ToString();
            if (i < cardDates.Length)
                cardDates[i].text = days[i % days.Length];
        }

        Debug.Log("Tüm Dashboard Verileri Baþarýyla Simüle Edildi!");
    }
}