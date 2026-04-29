using UnityEngine;

/// <summary>
/// Göz Teması Puanlama Sistemi
///
/// EyeTrackingSystem'den gelen ham verileri okur, işler ve
/// 0–100 arası bir göz teması puanı üretir.
///
///   0   = Berbat  (izleyiciye hiç bakılmamış veya veri çok bozuk)
///   100 = Kusursuz (sürekli izleyiciye bakılmış, sabit ve güvenilir veri)
///
/// ─────────────────────────────────────────────────────────────
///  Genel Performans Puanı:
///
///  Bu script'in TEK çıktısı:
///     GazeScoringSystem.GazeScore   (float, 0–100, readonly)
///
///  Bu, scoring script'inde şöyle kullanılabilir:
///
///     [SerializeField] GazeScoringSystem gazeScoring;
///
///     void CalculateOverallScore()
///     {
///         float eyeContactPart = gazeScoring.GazeScore;
///         // ... kendi ağırlığını uygula, diğer puanlarla birleştir
///     }
///
///  Bu değişkeni değiştirme — sadece oku.
///  Sistemde başka hiçbir yerde kullanılmıyor, sana özel.
/// ─────────────────────────────────────────────────────────────
///
/// TASARIM:
///   • EyeTrackingSystem → (her frame veri sağlar) → GazeScoringSystem
///   • İleride CircleEventSystem veya başka kaynaklar da bonus/ceza
///     verebilir: ReportBonus() / ReportPenalty() metotlarını kullan.
///   • Ring buffer tabanlı: son N saniyelik pencerede hesaplar.
///   • GC-friendly: sıfır allocation (fixed-size array).
///
/// Hierarchy'de boş bir GameObject'e atanır (örn. "ScoringManager").
/// </summary>
public class GazeScoringSystem : MonoBehaviour
{
    // ──────────────────────────────────────────────
    //  REFERANS
    // ──────────────────────────────────────────────
    [Header("Veri Kaynağı")]
    [Tooltip("Main Camera üzerindeki EyeTrackingSystem scripti")]
    public EyeTrackingSystem eyeTracking;

    // ──────────────────────────────────────────────
    //  PENCERE AYARLARI
    // ──────────────────────────────────────────────
    [Header("Skor Penceresi")]
    [Tooltip("Son kaç saniyelik veriyle puan hesaplansın.\n" +
             "Kısa pencere = anlık tepki, uzun pencere = kararlı ama yavaş")]
    [Range(2f, 30f)]
    public float scoreWindowSeconds = 5f;

    // ──────────────────────────────────────────────
    //  AĞIRLIK AYARLARI
    // ──────────────────────────────────────────────
    [Header("Puan Bileşen Ağırlıkları")]
    [Tooltip("İzleyiciye bakma oranının toplam puana etkisi (ana bileşen)")]
    [Range(0f, 1f)]
    public float audienceLookWeight = 0.60f;

    [Tooltip("Veri güvenilirliğinin (geçerli frame oranı) toplam puana etkisi")]
    [Range(0f, 1f)]
    public float confidenceWeight = 0.15f;

    [Tooltip("Uzun bakış cezasının toplam puana etkisi")]
    [Range(0f, 1f)]
    public float starePenaltyWeight = 0.10f;

    [Tooltip("Hızlı kafa hareketi cezasının toplam puana etkisi")]
    [Range(0f, 1f)]
    public float headSpeedPenaltyWeight = 0.15f;

    // ──────────────────────────────────────────────
    //  EŞİK AYARLARI
    // ──────────────────────────────────────────────
    [Header("Ceza Eşikleri")]
    [Tooltip("Bu hız (°/s) üzerinde kafa hareketi ceza almaya başlar.\n" +
             "EyeTrackingSystem'deki headWarningSpeed'den düşük olmalı " +
             "çünkü puan, uyarıdan ÖNCE düşmeye başlamalı.")]
    [Range(30f, 200f)]
    public float headSpeedPenaltyStart = 80f;

    [Tooltip("Bu hız (°/s) üzerinde kafa hareketi tam ceza alır")]
    [Range(100f, 400f)]
    public float headSpeedPenaltyFull = 200f;

    // ──────────────────────────────────────────────
    //  EVENT BONUS SINIRLARI
    // ──────────────────────────────────────────────
    [Header("Event Bonus/Ceza Sınırları")]
    [Tooltip("Oturum boyunca birikmiş event bonus puanlarının üst sınırı.\n" +
             "Çok sayıda başarılı event skoru 100'e sabitlemesin diye makul bir tavan.")]
    [Range(0f, 100f)]
    public float maxEventBonusTotal = 40f;

    [Tooltip("Oturum boyunca birikmiş event ceza puanlarının üst sınırı")]
    [Range(0f, 100f)]
    public float maxEventPenaltyTotal = 40f;

    // ──────────────────────────────────────────────
    //  ÇIKTI  —  DİĞER SİSTEMLER BU DEĞERİ OKUR
    // ──────────────────────────────────────────────

    /// <summary>
    /// Göz teması puanı (0–100). Salt okunur.
    ///   0   = Berbat
    ///   50  = Ortalama
    ///   100 = Kusursuz
    ///
    /// Bu değer her frame güncellenir ve son scoreWindowSeconds
    /// içindeki verinin ağırlıklı ortalamasıdır.
    ///
    /// Genel performans puanı hesaplayan arkadaşın bu property'yi
    /// okuyup kendi formülüne dahil edebilir.
    /// </summary>
    public float GazeScore => gazeScore;

    // ──────────────────────────────────────────────
    //  ÖZEL DEĞİŞKENLER
    // ──────────────────────────────────────────────

    // Çıktı
    private float gazeScore;

    // Ring buffer — her sample bir frame'in verisini tutar
    private const int BUFFER_SIZE = 1024;
    private readonly FrameSample[] samples = new FrameSample[BUFFER_SIZE];
    private int bufferHead;
    private int bufferCount;

    // Oturum boyunca biriken event bonus/cezaları (her frame SIFIRLANMAZ).
    // ReportBonus / ReportPenalty çağrıları buraya ekler; ResetBuffer sıfırlar.
    // Bu sayede tek-seferlik event bonusları sadece 1 frame değil, tüm oturum boyunca
    // skora katkıda bulunur.
    private float eventBonusTotal;
    private float eventPenaltyTotal;

    // Aktiflik (EyeTrackingSystem aktifken çalışır)
    private bool wasActive;
    private bool wasPaused;

    // Periyodik log zamanlayıcısı
    private float nextLogTime;
    private const float LOG_INTERVAL = 5f;

    // ══════════════════════════════════════════════
    //  YAŞAM DÖNGÜSÜ
    // ══════════════════════════════════════════════

    void LateUpdate()
    {
        // EyeTrackingSystem'den SONRA çalışması için LateUpdate kullanıyoruz.
        // Böylece EyeTrackingSystem.Update() bu frame'in verilerini hazırladıktan
        // sonra biz okuruz.

        if (eyeTracking == null) return;

        // Oturum başladığında buffer'ı sıfırla
        if (eyeTracking.IsActive && !wasActive)
        {
            ResetBuffer();
            nextLogTime = Time.time + LOG_INTERVAL;
            Debug.Log("[GazeScoringSystem] Session detected — buffer reset, scoring started.");
        }
        wasActive = eyeTracking.IsActive;

        if (!eyeTracking.IsActive)
        {
            // Oturum bitmişse skoru koru (son değer kalır)
            wasPaused = false;
            return;
        }

        if (eyeTracking.IsPaused)
        {
            wasPaused = true;
            return;
        }

        if (wasPaused)
        {
            ResetSamplesPreservingScore();
            nextLogTime = Time.time + LOG_INTERVAL;
            wasPaused = false;
            return;
        }

        // Bu frame'in verisini topla
        CollectSample();

        // Pencere içindeki sample'lardan puan hesapla
        ComputeScore();

        // Her LOG_INTERVAL saniyede bir skoru logla
        if (Time.time >= nextLogTime)
        {
            nextLogTime = Time.time + LOG_INTERVAL;
            Debug.Log(string.Format("[GazeScoringSystem] Live score: {0:F1}/100", gazeScore));
        }
    }

    // ══════════════════════════════════════════════
    //  DIŞ KAYNAK API (CircleEvent vb. için)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Dış bir sistemden tek seferlik bonus puan bildir.
    /// Örneğin CircleEvent başarıyla tamamlandığında çağrılabilir.
    /// Bonus değeri 0–15 arası clamp edilir, toplam birikim ise maxEventBonusTotal ile sınırlıdır.
    /// Birikim oturum boyunca kalıcıdır; her ComputeScore çağrısında skora eklenir.
    ///
    /// Kullanım:
    ///   gazeScoringSystem.ReportBonus(10f);  // circle event başarısı
    /// </summary>
    public void ReportBonus(float amount)
    {
        float clamped = Mathf.Clamp(amount, 0f, 15f);
        eventBonusTotal = Mathf.Min(eventBonusTotal + clamped, maxEventBonusTotal);
        Debug.Log(string.Format(
            "[GazeScoringSystem] Bonus reported: +{0:F1} pts (session total: {1:F1}/{2:F0})",
            clamped, eventBonusTotal, maxEventBonusTotal));
    }

    /// <summary>
    /// Dış bir sistemden ceza puan bildir.
    /// Örneğin circle event kaçırıldığında çağrılabilir.
    /// Ceza 0–15 arası clamp edilir, toplam birikim maxEventPenaltyTotal ile sınırlıdır.
    ///
    /// Kullanım:
    ///   gazeScoringSystem.ReportPenalty(5f);  // circle event kaçırıldı
    /// </summary>
    public void ReportPenalty(float amount)
    {
        float clamped = Mathf.Clamp(amount, 0f, 15f);
        eventPenaltyTotal = Mathf.Min(eventPenaltyTotal + clamped, maxEventPenaltyTotal);
        Debug.Log(string.Format(
            "[GazeScoringSystem] Penalty reported: -{0:F1} pts (session total: {1:F1}/{2:F0})",
            clamped, eventPenaltyTotal, maxEventPenaltyTotal));
    }

    /// <summary>
    /// Oturum sonu: nihai puanı döner ve dahili state'i sıfırlar.
    /// Arkadaşın bunu oturum bittiğinde çağırarak son puanı alabilir.
    /// </summary>
    public float FinalizeSession()
    {
        float finalScore = gazeScore;
        Debug.Log(string.Format("[GazeScoringSystem] Session finalized. Final gaze score: {0:F1}/100", finalScore));
        ResetBuffer();
        return finalScore;
    }

    // ══════════════════════════════════════════════
    //  VERİ TOPLAMA
    // ══════════════════════════════════════════════

    void CollectSample()
    {
        samples[bufferHead] = new FrameSample
        {
            time             = Time.time,
            isValid          = eyeTracking.IsGazeValid,
            isLookingAtAud   = eyeTracking.IsLookingAtAudience,
            isStaring        = eyeTracking.IsStareWarning,
            headSpeed        = eyeTracking.SmoothedHeadSpeed
        };

        bufferHead = (bufferHead + 1) % BUFFER_SIZE;
        if (bufferCount < BUFFER_SIZE) bufferCount++;
    }

    // ══════════════════════════════════════════════
    //  PUAN HESAPLAMA
    // ══════════════════════════════════════════════

    void ComputeScore()
    {
        float windowStart = Time.time - scoreWindowSeconds;

        int totalInWindow     = 0;
        int validCount        = 0;
        int audienceCount     = 0;
        int stareFrames       = 0;
        float headSpeedSum    = 0f;
        int headSpeedSamples  = 0;

        // Ring buffer'da geriye doğru tara
        for (int i = 0; i < bufferCount; i++)
        {
            int idx = (bufferHead - 1 - i + BUFFER_SIZE) % BUFFER_SIZE;
            if (samples[idx].time < windowStart) break;

            totalInWindow++;

            if (samples[idx].isValid)
            {
                validCount++;

                if (samples[idx].isLookingAtAud)
                    audienceCount++;

                if (samples[idx].isStaring)
                    stareFrames++;

                headSpeedSum += samples[idx].headSpeed;
                headSpeedSamples++;
            }
        }

        // Yeterli veri yoksa henüz puan verme
        if (totalInWindow < 10)
        {
            gazeScore = 0f;
            return;
        }

        // ── Bileşen 1: İzleyiciye bakma oranı (0–1) ──
        float audienceRatio = validCount > 0
            ? (float)audienceCount / validCount
            : 0f;

        // ── Bileşen 2: Veri güvenilirliği (0–1) ──
        float confidence = (float)validCount / totalInWindow;

        // ── Bileşen 3: Uzun bakış cezası (0–1, 1 = ceza yok) ──
        float stareRatio = validCount > 0
            ? (float)stareFrames / validCount
            : 0f;
        // stareRatio 0 → ceza yok (1.0), stareRatio 1 → tam ceza (0.0)
        float stareScore = 1f - stareRatio;

        // ── Bileşen 4: Kafa hızı cezası (0–1, 1 = ceza yok) ──
        float avgHeadSpeed = headSpeedSamples > 0
            ? headSpeedSum / headSpeedSamples
            : 0f;
        // headSpeedPenaltyStart altında → 1.0 (ceza yok)
        // headSpeedPenaltyFull üstünde  → 0.0 (tam ceza)
        float headScore = 1f - Mathf.Clamp01(
            (avgHeadSpeed - headSpeedPenaltyStart) /
            Mathf.Max(1f, headSpeedPenaltyFull - headSpeedPenaltyStart));

        // ── Ağırlıklı birleştirme ──
        // Ağırlıklar normalize edilir (toplamı 1 olması garanti)
        float totalWeight = audienceLookWeight + confidenceWeight
                          + starePenaltyWeight + headSpeedPenaltyWeight;

        // Sıfıra bölme koruması
        if (totalWeight < 0.001f) totalWeight = 1f;

        float rawScore =
            (audienceRatio * audienceLookWeight
           + confidence    * confidenceWeight
           + stareScore    * starePenaltyWeight
           + headScore     * headSpeedPenaltyWeight)
            / totalWeight;

        // 0–100 ölçeğine çevir
        float score = rawScore * 100f;

        // ── Oturum boyunca biriken event bonus/cezalarını uygula ──
        // DİKKAT: Sıfırlamıyoruz — bunlar tüm oturum boyunca skora katkıda bulunur.
        // Sadece ResetBuffer (oturum başı) sıfırlar.
        score += eventBonusTotal;
        score -= eventPenaltyTotal;

        // 0–100 clamp
        gazeScore = Mathf.Clamp(score, 0f, 100f);
    }

    // ══════════════════════════════════════════════
    //  YARDIMCI
    // ══════════════════════════════════════════════

    void ResetBuffer()
    {
        bufferHead   = 0;
        bufferCount  = 0;
        gazeScore    = 0f;
        eventBonusTotal   = 0f;
        eventPenaltyTotal = 0f;
    }

    void ResetSamplesPreservingScore()
    {
        bufferHead = 0;
        bufferCount = 0;
    }

    // ══════════════════════════════════════════════
    //  VERİ YAPISI
    // ══════════════════════════════════════════════

    /// <summary>Tek bir frame'in puanlama verileri.</summary>
    private struct FrameSample
    {
        public float time;
        public bool  isValid;
        public bool  isLookingAtAud;
        public bool  isStaring;
        public float headSpeed;
    }
}
