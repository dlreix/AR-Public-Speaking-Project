using System;
using System.IO;
using UnityEngine;

namespace PresentationAnalyzer.Core
{
    /// <summary>
    /// AcousticAnalyzer ve NlpProcessor'dan gelen tüm metrikleri
    /// tek bir AnalysisReport nesnesinde birleştirir ve JSON olarak kaydeder.
    /// </summary>
    public class ReportGenerator
    {
        /// <summary>
        /// Tüm analiz metriklerini birleştirerek rapor üretir.
        /// SessionController kayıt bitince bu metodu çağırır.
        /// </summary>
        public AnalysisReport Generate(
            AcousticResult acoustic,
            NlpResult      nlp)
        {
            return new AnalysisReport
            {
                // ── Meta ──────────────────────────────────────────────────────
                GeneratedAt         = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),

                // ── Akustik metrikler ─────────────────────────────────────────
                TotalDurationSec    = acoustic.TotalDurationSec,
                SpeechDurationSec   = acoustic.SpeechDurationSec,
                SilenceDurationSec  = acoustic.SilenceDurationSec,
                AverageRms          = acoustic.AverageRms,
                PitchStdDev         = acoustic.PitchStdDev,

                // ── NLP metrikler ─────────────────────────────────────────────
                TotalWords          = nlp.TotalWords,
                FillerCount         = nlp.FillerCount,
                FillerRatio         = nlp.FillerRatio,
                ConjunctionCount    = nlp.ConjunctionCount,
                ConjunctionRatio    = nlp.ConjunctionRatio,
                WordsPerMinute      = nlp.WordsPerMinute,

                // ── Ham veri ──────────────────────────────────────────────────
                FullTranscript      = nlp.FullTranscript
            };
        }
        
        public void CallSpeechAnalysis(AnalysisReport r)
        {
            float wpm = r.WordsPerMinute;

            // Dakika başına dolgu kelime sayısı
            float fillerPerMin = r.TotalDurationSec > 0
                ? r.FillerCount / (r.TotalDurationSec / 60f)
                : 0f;

            // Kelime başına ortalama sessizlik süresi (saniye)
            // Konuşmacının kelimeler arası ortalama duraklamasını temsil eder
            float avgPause = r.TotalWords > 0
                ? r.SilenceDurationSec / r.TotalWords
                : r.SilenceDurationSec;

            // Ton skoru: RMS dinamikliğini 0-100 skalasına normalize et
            // PitchStdDev düzelince burası güncellenecek
            // 0.05f RMS üzeri = tam dinamik konuşma = 100 puan
            float toneScore = Mathf.Clamp01(r.AverageRms / 0.05f) * 100f;

            Debug.Log($"[ReportGenerator] OnSpeechAnalysisComplete → " +
                      $"wpm:{wpm:F1}, fillerPerMin:{fillerPerMin:F1}, " +
                      $"avgPause:{avgPause:F2}, toneScore:{toneScore:F1}");

            // Arkadaşının script'ine referans eklenince aktif hale gelecek:
            // hedefObje.OnSpeechAnalysisComplete(wpm, fillerPerMin, avgPause, toneScore);
        }

        /// <summary>
        /// Raporu JSON dosyası olarak kaydeder.
        /// Dosya: Application.persistentDataPath/reports/report_[timestamp].json
        /// </summary>
        public string SaveToJson(AnalysisReport report)
        {
            string dir = Path.Combine(
                Application.persistentDataPath, "reports");

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filePath  = Path.Combine(dir, $"report_{timestamp}.json");

            string json = JsonUtility.ToJson(report, prettyPrint: true);
            File.WriteAllText(filePath, json);

            Debug.Log($"[ReportGenerator] Rapor kaydedildi: {filePath}");
            return filePath;
        }

        /// <summary>
        /// Raporu Unity konsoluna okunabilir formatta basar.
        /// Geliştirme sırasında hızlı kontrol için kullanılır.
        /// </summary>
        public void LogReport(AnalysisReport r)
        {
            string line = new string('─', 48);

            Debug.Log(
                $"\n{line}\n" +
                $"  SUNUM ANALİZ RAPORU — {r.GeneratedAt}\n" +
                $"{line}\n" +
                $"  ZAMANLAMA\n" +
                $"    Toplam süre      : {FormatDuration(r.TotalDurationSec)}\n" +
                $"    Konuşma süresi   : {FormatDuration(r.SpeechDurationSec)}\n" +
                $"    Sessizlik süresi : {FormatDuration(r.SilenceDurationSec)}\n" +
                $"{line}\n" +
                $"  AKUSTİK\n" +
                $"    Ort. ses şiddeti : {r.AverageRms:F4} RMS\n" +
                $"    Ton dalgalanması : {r.PitchStdDev:F1} Hz std sapma\n" +
                $"{line}\n" +
                $"  DİL ANALİZİ\n" +
                $"    Toplam kelime    : {r.TotalWords}\n" +
                $"    Konuşma hızı     : {r.WordsPerMinute:F1} KDK\n" +
                $"    Dolgu kelime     : {r.FillerCount} ({r.FillerRatio:F1}%)\n" +
                $"    Bağlaç           : {r.ConjunctionCount} ({r.ConjunctionRatio:F1}%)\n" +
                $"{line}\n" +
                $"  TRANSKRİPT\n" +
                $"    {r.FullTranscript}\n" +
                $"{line}"
            );
        }

        /// <summary>
        /// Saniyeyi GG:DD:SS formatına çevirir.
        /// </summary>
        private static string FormatDuration(float seconds)
        {
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            return $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
        }
    }

    // ─── Rapor modeli ──────────────────────────────────────────────────────────

    /// <summary>
    /// Tüm analiz metriklerini taşıyan veri sınıfı.
    /// [Serializable] → JsonUtility.ToJson ile JSON'a dönüştürülebilir.
    /// </summary>
    [Serializable]
    public class AnalysisReport
    {
        // Meta
        public string GeneratedAt;

        // Akustik
        public float  TotalDurationSec;
        public float  SpeechDurationSec;
        public float  SilenceDurationSec;
        public float  AverageRms;
        public float  PitchStdDev;

        // NLP
        public int    TotalWords;
        public int    FillerCount;
        public float  FillerRatio;
        public int    ConjunctionCount;
        public float  ConjunctionRatio;
        public float  WordsPerMinute;

        // Ham veri
        public string FullTranscript;
    }
}