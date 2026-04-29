using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
[System.Serializable]
public class SessionData
{
    public string date;
    public float overallScore;
    public float eyeContact;
    public float pace;
    public float posture;
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

    // Engine'den gelen raporu eski sistemle birleþtirip kaydeder
    public void SaveSession(FeedbackReport report)
    {
        SessionData newSession = new SessionData
        {
            date = System.DateTime.Now.ToString("MMM dd | HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            overallScore = report.totalScore,
            eyeContact = report.eyeScore,
            pace = report.speechScore,
            posture = report.postureScore,
            detailedReport = report
        };

        history.allSessions.Add(newSession);

        string json = JsonUtility.ToJson(history, true);
        File.WriteAllText(filePath, json);
        Debug.Log("Dosya Konumu: <color=yellow>" + Application.persistentDataPath + "</color>");
    }

    public void LoadData()
    {
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            history = JsonUtility.FromJson<SessionHistory>(json);
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
}