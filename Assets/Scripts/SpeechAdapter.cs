using UnityEngine;
public class SpeechAdapter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PerformanceScoringEngine scoringEngine;

    private void Awake()
    {
        AutoWireIfNeeded();
    }

    public void SetScoringEngine(PerformanceScoringEngine engine)
    {
        scoringEngine = engine;
    }

    public bool AutoWireIfNeeded()
    {
        if (scoringEngine == null)
        {
            scoringEngine = FindFirstObjectByType<PerformanceScoringEngine>(FindObjectsInactive.Include);
        }

        return scoringEngine != null;
    }

   
    public void OnSpeechAnalysisComplete(float wpm, float fillerPerMin,
                                          float avgPause, float toneScore)
    {
        wpm          = float.IsNaN(wpm)          ? 0f : Mathf.Max(0f, wpm);
        fillerPerMin = float.IsNaN(fillerPerMin) ? 0f : Mathf.Clamp(fillerPerMin, 0f, 60f);
        avgPause     = float.IsNaN(avgPause)     ? 0f : Mathf.Max(0f, avgPause);
        toneScore    = float.IsNaN(toneScore)    ? 0f : Mathf.Clamp(toneScore, 0f, 100f);

        AutoWireIfNeeded();

        if (scoringEngine == null)
        {
            Debug.LogWarning("[SpeechAdapter] Scoring Engine bağlı değil!");
            return;
        }

        scoringEngine.SetSpeechMetrics(wpm, fillerPerMin, avgPause, toneScore);
        scoringEngine.CalculateSessionScore();

        Debug.Log($"[SpeechAdapter] Veriler alındı → " +
                  $"WPM={wpm:F1}, Filler={fillerPerMin:F1}/min, " +
                  $"Pause={avgPause:F2}s, Tone={toneScore:F1}");
    }
}
