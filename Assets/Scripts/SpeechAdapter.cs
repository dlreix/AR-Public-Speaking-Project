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
