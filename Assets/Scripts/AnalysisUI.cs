using PresentationAnalyzer.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PresentationAnalyzer
{
    /// <summary>
    /// Kayıt sırasında gerçek zamanlı ses şiddeti ve transkript gösterir.
    /// SessionController tarafından AcousticAnalyzer ve STTBridge referansları
    /// bu sınıfa aktarılır.
    /// </summary>
    public class AnalysisUI : MonoBehaviour
    {
        // ─── Inspector bağlantıları ───────────────────────────────────────────
        [Header("RMS Göstergesi")]
        [SerializeField] private Image  rmsBarFill;   // dolgu Image (Image Type: Filled)
        [SerializeField] private TMP_Text rmsLabel;   // "0.042 RMS"

        [Header("Transkript")]
        [SerializeField] private TMP_Text transcriptText;  // canlı yazı alanı
        [SerializeField] private int      maxTranscriptChars = 400;

        [Header("Pitch")]
        [SerializeField] private TMP_Text pitchLabel;  // "220 Hz"

        // ─── Referanslar (SessionController atar) ────────────────────────────
        private AcousticAnalyzer _acousticAnalyzer;
        private bool             _isActive;

        // ─── Transkript birikimi ──────────────────────────────────────────────
        private System.Text.StringBuilder _transcript
            = new System.Text.StringBuilder();

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// SessionController.StartSession() çağırır.
        /// </summary>
        public void StartDisplay(AcousticAnalyzer analyzer, STTBridge stt)
        {
            _acousticAnalyzer = analyzer;
            _isActive         = true;
            _transcript.Clear();

            // Kısmi sonuçları canlı göster
            stt.OnPartialResult += OnPartialResult;
            stt.OnFinalResult   += OnFinalResult;

            if (transcriptText != null)
                transcriptText.text = string.Empty;
        }

        /// <summary>
        /// SessionController.StopSession() çağırır.
        /// </summary>
        public void StopDisplay(STTBridge stt)
        {
            _isActive = false;
            stt.OnPartialResult -= OnPartialResult;
            stt.OnFinalResult   -= OnFinalResult;
        }

        // ─── Unity döngüsü ────────────────────────────────────────────────────

        private void Update()
        {
            if (!_isActive || _acousticAnalyzer == null) return;

            UpdateRmsBar();
            UpdatePitchLabel();
        }

        // ─── Güncelleme metodları ─────────────────────────────────────────────

        /// <summary>
        /// RMS değerini bar ve label olarak gösterir.
        /// Bar dolumu: 0.0–0.3 RMS aralığını 0–1'e normalize eder.
        /// Çoğu konuşma bu aralıkta kalır.
        /// </summary>
        private void UpdateRmsBar()
        {
            float rms = _acousticAnalyzer.CurrentRms;

            if (rmsBarFill != null)
                rmsBarFill.fillAmount = Mathf.Clamp01(rms / 0.3f);

            if (rmsLabel != null)
                rmsLabel.text = $"{rms:F3} RMS";
        }

        private void UpdatePitchLabel()
        {
            if (pitchLabel == null) return;
            float pitch = _acousticAnalyzer.CurrentPitch;
            pitchLabel.text = pitch > 0f ? $"{pitch:F0} Hz" : "— Hz";
        }

        /// <summary>
        /// Vosk kısmi sonucunu italik gri renkte gösterir.
        /// Ana thread kontrolü MainThreadDispatcher ile sağlanır.
        /// </summary>
        private void OnPartialResult(string partial)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                if (transcriptText == null) return;
                transcriptText.text = BuildTranscriptDisplay(partial, isPartial: true);
            });
        }

        /// <summary>
        /// Kesinleşen metni kalıcı olarak biriktirir.
        /// </summary>
        private void OnFinalResult(string final)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                _transcript.Append(final).Append(" ");

                // Çok uzarsa başını kes
                if (_transcript.Length > maxTranscriptChars)
                    _transcript.Remove(0,
                        _transcript.Length - maxTranscriptChars);

                if (transcriptText != null)
                    transcriptText.text = BuildTranscriptDisplay(
                        string.Empty, isPartial: false);
            });
        }

        /// <summary>
        /// Kesinleşmiş metin + kısmi metin birleşik gösterim.
        /// Kısmi metin italik ve soluk görünür.
        /// </summary>
        private string BuildTranscriptDisplay(string partial, bool isPartial)
        {
            string confirmed = _transcript.ToString();

            if (isPartial && !string.IsNullOrWhiteSpace(partial))
                return $"{confirmed}<color=#888888><i>{partial}</i></color>";

            return confirmed;
        }
    }
}