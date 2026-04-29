using UnityEngine;

/// <summary>
/// SpeechAdapter — Arkadaş 4'ün ReportGenerator.cs ile
/// PerformanceScoringEngine arasındaki köprü.
///
/// Kullanım:
///   1. Bu scripti sahnedeki herhangi bir objeye ekle
///   2. Inspector'da Scoring Engine alanına ScoringSystem objesini bağla
///   3. Arkadaş 4'ün ReportGenerator.cs dosyasındaki yorum satırını kaldır:
///      // hedefObje.OnSpeechAnalysisComplete(wpm, fillerPerMin, avgPause, toneScore);
///      → speechAdapter.OnSpeechAnalysisComplete(wpm, fillerPerMin, avgPause, toneScore);
/// </summary>
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

    /// <summary>
    /// Arkadaş 4'ün ReportGenerator.cs bu metodu çağırır.
    /// Parametreler:
    ///   wpm          — konuşma hızı (kelime/dakika), tipik 100-150
    ///   fillerPerMin — dolgu kelime sayısı/dakika (düşük = iyi)
    ///   avgPause     — kelime başına ortalama sessizlik süresi (saniye)
    ///   toneScore    — ses dinamikliği skoru (0-100, yüksek = vurgulu)
    /// </summary>
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
