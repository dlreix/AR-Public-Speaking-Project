using UnityEngine;

public class HistoryCard : MonoBehaviour
{
    [Header("Ayarlar")]
    public int sessionIndex; // Müfettiþten buna 0, 1 veya 2 yazacaksýn

    public void OnCardClick()
    {
        // 1. DataManager'dan tüm geçmiþi alýyoruz
        var allData = DataManager.Instance.history.allSessions;

        // 2. Eðer týkladýðýmýz index'te bir veri varsa (yani boþ deðilse)
        if (sessionIndex < allData.Count)
        {
            // Listeyi tersten okuyoruz (0 her zaman en yeni session olsun diye)
            SessionData selectedSession = allData[allData.Count - 1 - sessionIndex];

            // 3. Dashboard'u bu veriye göre güncelle diyoruz
            FindObjectOfType<DashboardController>().DisplaySession(selectedSession);
        }
        else
        {
            Debug.Log("Bu kutu için henüz bir geçmiþ veri yok!");
        }
    }
}