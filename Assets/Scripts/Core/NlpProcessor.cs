using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PresentationAnalyzer.Core
{
    /// <summary>
    /// STTBridge'den gelen metni biriktirir ve kelime bazlı analiz yapar.
    /// Cümle tahmini kaldırıldı — STT çıktısında noktalama olmadığı için
    /// cümle sayımı güvenilir değil. Tüm metrikler kelime düzeyinde.
    /// </summary>
    public class NlpProcessor
    {
        // ─── Türkçe dolgu kelimeleri ──────────────────────────────────────────
        private static readonly HashSet<string> FillerWords =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "eee", "ıı", "iii", "mmm", "şey", "yani", "falan", "filan",
                "hani", "mesela", "aynen", "işte", "ya", "ha", "he", "be",
                "efendim", "aslında", "şimdi", "gibi", "sanki"
            };

        // ─── Türkçe bağlaçlar ─────────────────────────────────────────────────
        private static readonly HashSet<string> Conjunctions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ve", "veya", "ya", "yahut", "ile", "ama", "fakat", "lakin",
                "ancak", "oysa", "oysaki", "halbuki", "çünkü", "zira", "ki",
                "eğer", "ise", "ne", "hem", "bile", "dahi",
                "rağmen", "karşın", "dolayı", "için"
            };

        // ─── İç durum ─────────────────────────────────────────────────────────
        private readonly StringBuilder _fullTranscript = new StringBuilder();
        private int _totalWords;
        private int _fillerCount;
        private int _conjunctionCount;

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// STTBridge.OnFinalResult event'ine bağlanır.
        /// Her yeni metin parçası geldiğinde çağrılır.
        /// </summary>
        public void AppendText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            if (_fullTranscript.Length > 0)
                _fullTranscript.Append(" ");
            _fullTranscript.Append(text.Trim());

            AnalyzeChunk(text);

            Debug.Log($"[NlpProcessor] Kelime: {_totalWords} " +
                      $"| Dolgu: {_fillerCount} | Bağlaç: {_conjunctionCount}");
        }

        /// <summary>
        /// Konuşma süresi verilerek WPM hesaplar ve sonuç nesnesini döner.
        /// </summary>
        public NlpResult GetResult(float speechDurationSeconds)
        {
            float minutes = speechDurationSeconds > 0
                ? speechDurationSeconds / 60f
                : 1f;

            float wpm = _totalWords / minutes;

            float fillerRatio = _totalWords > 0
                ? (float)_fillerCount / _totalWords * 100f
                : 0f;

            float conjunctionRatio = _totalWords > 0
                ? (float)_conjunctionCount / _totalWords * 100f
                : 0f;

            return new NlpResult
            {
                FullTranscript   = _fullTranscript.ToString(),
                TotalWords       = _totalWords,
                FillerCount      = _fillerCount,
                FillerRatio      = MathF.Round(fillerRatio, 1),
                ConjunctionCount = _conjunctionCount,
                ConjunctionRatio = MathF.Round(conjunctionRatio, 1),
                WordsPerMinute   = MathF.Round(wpm, 1)
            };
        }

        /// <summary>
        /// Yeni kayıt için sıfırlar.
        /// </summary>
        public void Reset()
        {
            _fullTranscript.Clear();
            _totalWords       = 0;
            _fillerCount      = 0;
            _conjunctionCount = 0;
        }

        // ─── İç analiz ────────────────────────────────────────────────────────

        /// <summary>
        /// Metni kelimelerine ayırır, her kelimeyi sınıflandırır.
        /// </summary>
        private void AnalyzeChunk(string text)
        {
            // Noktalama temizle, küçük harfe çevir
            string cleaned = Regex.Replace(
                text.ToLowerInvariant(), @"[^\w\s]", " ");

            string[] words = cleaned.Split(
                new[] { ' ', '\t', '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0) return;

            _totalWords += words.Length;

            foreach (string word in words)
            {
                if (FillerWords.Contains(word))
                    _fillerCount++;

                if (Conjunctions.Contains(word))
                    _conjunctionCount++;
            }
        }
    }

    // ─── Sonuç modeli ─────────────────────────────────────────────────────────
    [Serializable]
    public class NlpResult
    {
        public string FullTranscript;
        public int    TotalWords;
        public int    FillerCount;
        public float  FillerRatio;        // toplam kelimelere yüzde
        public int    ConjunctionCount;
        public float  ConjunctionRatio;   // toplam kelimelere yüzde
        public float  WordsPerMinute;
    }
}