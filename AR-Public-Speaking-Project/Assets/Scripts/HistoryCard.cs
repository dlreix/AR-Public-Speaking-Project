using UnityEngine;

public class HistoryCard : MonoBehaviour
{
    [Header("Ayarlar")]
    public int sessionIndex; 

    public void OnCardClick()
    {
        if (DataManager.Instance == null) return;

        var allData = DataManager.Instance.history.allSessions;

        if (sessionIndex < allData.Count)
        {
            // Listeyi tersten okuyoruz (0 her zaman en yeni session olsun diye)
            SessionData selectedSession = allData[allData.Count - 1 - sessionIndex];

            // Dashboard'u bu veriye göre güncelle diyoruz
            DashboardController dc = FindObjectOfType<DashboardController>();
            if (dc != null) dc.DisplaySession(selectedSession);
        }
        else
        {
            Debug.Log("Bu kutu için henüz bir geçmiþ veri yok!");
        }
    }
}