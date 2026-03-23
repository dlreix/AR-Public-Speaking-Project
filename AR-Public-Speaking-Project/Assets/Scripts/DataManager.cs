using UnityEngine;
using System.Collections.Generic;
using System.IO;

[System.Serializable]
public class SessionData
{
    public string date; // Sadece Tarih
    public float overallScore;
    public float eyeContact;
    public float pace;
    public float posture;
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
        if (Instance == null) Instance = this;
        filePath = Application.persistentDataPath + "/history_v2.json";
        LoadData();
    }

    public void SaveSession(float score, float eye, float pace, float post)
    {
        SessionData newSession = new SessionData
        {
            date = System.DateTime.Now.ToString("MMM dd"), // Örn: "Mar 23"
            overallScore = score,
            eyeContact = eye,
            pace = pace,
            posture = post
        };
        history.allSessions.Add(newSession);
        File.WriteAllText(filePath, JsonUtility.ToJson(history, true));
        // Bunu SaveSession fonksiyonunun en altýna, File.WriteAllText satýrýndan sonraya ekle:
        Debug.Log("Dosya Konumu: <color=yellow>" + Application.persistentDataPath + "</color>");
    }

    public void LoadData()
    {
        if (File.Exists(filePath))
        {
            history = JsonUtility.FromJson<SessionHistory>(File.ReadAllText(filePath));
        }
    }
    public void DeleteAllData()
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath); // Dosyayý sildik
            history = new SessionHistory(); // Hafýzadaki listeyi boþalttýk
            Debug.Log("Tüm geçmiþ veriler temizlendi!");
        }
    }
}