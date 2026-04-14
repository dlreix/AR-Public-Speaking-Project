using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using UnityEngine;
using System.Threading.Tasks;

namespace PresentationAnalyzer.Core
{
    /// <summary>
    /// Vosk STT motorunu saran köprü sınıfı.
    /// AudioRecorder'ın Channel'ından byte[] okur (Consumer),
    /// Vosk'a besler ve tanınan metni event ile dışarı iletir.
    /// </summary>
    public class STTBridge : IDisposable
    {
        // ─── Vosk Native API (libvosk.dylib) ──────────────────────────────────
        // P/Invoke: C# → native C kütüphanesi köprüsü.
        // Her fonksiyon Vosk'un C header dosyasındaki imzayla birebir eşleşmeli.

        [DllImport("vosk")]
        private static extern IntPtr vosk_model_new(string modelPath);

        [DllImport("vosk")]
        private static extern void vosk_model_free(IntPtr model);

        [DllImport("vosk")]
        private static extern IntPtr vosk_recognizer_new(IntPtr model, float sampleRate);

        [DllImport("vosk")]
        private static extern void vosk_recognizer_free(IntPtr recognizer);

        /// <summary>
        /// PCM byte dizisini recognizer'a besler.
        /// Dönüş: 1 = cümle tamamlandı (final), 0 = devam ediyor (partial)
        /// </summary>
        [DllImport("vosk")]
        private static extern int vosk_recognizer_accept_waveform(
            IntPtr recognizer, byte[] data, int length);

        /// <summary>
        /// Kısmi (partial) sonucu JSON string olarak döner.
        /// Örn: {"partial": "merhaba nasıl"}
        /// </summary>
        [DllImport("vosk")]
        private static extern IntPtr vosk_recognizer_partial_result(IntPtr recognizer);

        /// <summary>
        /// Final sonucu JSON string olarak döner.
        /// Örn: {"text": "merhaba nasılsın"}
        /// </summary>
        [DllImport("vosk")]
        private static extern IntPtr vosk_recognizer_result(IntPtr recognizer);

        /// <summary>
        /// Kalan ses verisini sıfırlar ve son sonucu alır.
        /// StopRecording sırasında çağrılır.
        /// </summary>
        [DllImport("vosk")]
        private static extern IntPtr vosk_recognizer_final_result(IntPtr recognizer);

        // ─── Event'ler ────────────────────────────────────────────────────────

        /// <summary>
        /// Vosk kısmi sonuç ürettiğinde tetiklenir.
        /// UI'da "yazıyor..." efekti için kullanılır.
        /// </summary>
        public event Action<string> OnPartialResult;

        /// <summary>
        /// Vosk bir cümleyi tamamladığında tetiklenir.
        /// NlpProcessor bu metni işleyecek.
        /// </summary>
        public event Action<string> OnFinalResult;

        // ─── Özel alanlar ─────────────────────────────────────────────────────

        private IntPtr _model;
        private IntPtr _recognizer;
        private bool   _disposed;

        // ─── Başlatma ─────────────────────────────────────────────────────────

        /// <summary>
        /// Vosk modelini diskten yükler ve recognizer oluşturur.
        /// StreamingAssets içindeki model klasörünü kullanır.
        /// </summary>
        public void Initialize()
        {
            // Zaten başlatılmışsa tekrar yükleme
            if (_recognizer != IntPtr.Zero)
            {
                Debug.Log("[STTBridge] Zaten başlatılmış, atlanıyor.");
                return;
            }
            // StreamingAssets yolu platform bağımsız çalışır
            string modelPath = Path.Combine(
                Application.streamingAssetsPath, "vosk-model-tr");

            if (!Directory.Exists(modelPath))
            {
                Debug.LogError(
                    $"[STTBridge] Model klasörü bulunamadı: {modelPath}\n" +
                    "StreamingAssets/vosk-model-tr klasörünü kontrol et.");
                return;
            }

            // Native model nesnesini oluştur (yavaş — ana thread'de bir kez çağrılır)
            _model = vosk_model_new(modelPath);

            if (_model == IntPtr.Zero)
            {
                Debug.LogError("[STTBridge] Vosk modeli yüklenemedi.");
                return;
            }

            // Recognizer: modeli ve örnekleme hızını bağla
            _recognizer = vosk_recognizer_new(_model, AudioRecorder.SampleRate);

            if (_recognizer == IntPtr.Zero)
            {
                Debug.LogError("[STTBridge] Vosk recognizer oluşturulamadı.");
                return;
            }

            Debug.Log("[STTBridge] Vosk başarıyla yüklendi.");
        }

        // ─── Consumer döngüsü ─────────────────────────────────────────────────

        /// <summary>
        /// AudioRecorder'ın kanalını okur ve her chunk'ı Vosk'a besler.
        /// Ayrı bir thread'de çalışır (Task.Run ile SessionController çağırır).
        /// </summary>
        public async Task StartConsumerLoop(
            ChannelReader<byte[]> audioChannel,
            CancellationToken ct)
        {
            if (_recognizer == IntPtr.Zero)
            {
                Debug.LogError("[STTBridge] Initialize() çağrılmadan döngü başlatılamaz.");
                return; // Task döndürdüğü için bu satır artık sorunsuz çalışır
            }

            Debug.Log("[STTBridge] Consumer döngüsü başladı.");

            try
            {
                // Kanal kapanana veya iptal edilene kadar oku
                await foreach (byte[] chunk in audioChannel.ReadAllAsync(ct))
                {
                    // Vosk'a PCM chunk'ı besle
                    int isFinal = vosk_recognizer_accept_waveform(
                        _recognizer, chunk, chunk.Length);

                    if (isFinal == 1)
                    {
                        // Cümle tamamlandı → final sonucu al
                        string json = PtrToString(
                            vosk_recognizer_result(_recognizer));
                        string text = ParseVoskJson(json, "text");

                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            Debug.Log($"[STTBridge] Final: {text}");
                            OnFinalResult?.Invoke(text);
                        }
                    }
                    else
                    {
                        // Cümle devam ediyor → kısmi sonucu al
                        string json = PtrToString(
                            vosk_recognizer_partial_result(_recognizer));
                        string partial = ParseVoskJson(json, "partial");

                        if (!string.IsNullOrWhiteSpace(partial))
                            OnPartialResult?.Invoke(partial);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal iptal — hata değil
                Debug.Log("[STTBridge] Consumer döngüsü iptal edildi.");
            }

            // Kanalda kalan son parçayı işle
            FlushFinalResult();
        }

        /// <summary>
        /// Kayıt bittiğinde tampondaki son metni zorla alır.
        /// </summary>
        public void FlushFinalResult()
        {
            if (_recognizer == IntPtr.Zero) return;

            string json = PtrToString(
                vosk_recognizer_final_result(_recognizer));
            string text = ParseVoskJson(json, "text");

            if (!string.IsNullOrWhiteSpace(text))
            {
                Debug.Log($"[STTBridge] Flush final: {text}");
                OnFinalResult?.Invoke(text);
            }
        }

        // ─── Yardımcı metodlar ────────────────────────────────────────────────

        /// <summary>
        /// Native IntPtr (C string) → C# string dönüşümü.
        /// Vosk'un döndürdüğü pointer'ı UTF-8 string'e çevirir.
        /// </summary>
        private static string PtrToString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return string.Empty;
            return Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
        }

        /// <summary>
        /// Vosk JSON çıktısından belirli bir alanı çeker.
        /// Örn: {"text": "merhaba"} → "merhaba"
        /// Harici JSON kütüphanesi gerektirmez.
        /// </summary>
        private static string ParseVoskJson(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return string.Empty;

            // Örn: "text" : "merhaba nasılsın"
            string search = $"\"{key}\"";
            int keyIndex = json.IndexOf(search, StringComparison.Ordinal);
            if (keyIndex < 0) return string.Empty;

            int colon = json.IndexOf(':', keyIndex);
            if (colon < 0) return string.Empty;

            int quoteStart = json.IndexOf('"', colon + 1);
            if (quoteStart < 0) return string.Empty;

            int quoteEnd = json.IndexOf('"', quoteStart + 1);
            if (quoteEnd < 0) return string.Empty;

            return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1).Trim();
        }

        // ─── Bellek yönetimi ──────────────────────────────────────────────────

        /// <summary>
        /// Native kaynakları serbest bırakır.
        /// SessionController OnDestroy'da bu metodu çağırmalı.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_recognizer != IntPtr.Zero)
            {
                vosk_recognizer_free(_recognizer);
                _recognizer = IntPtr.Zero;
            }

            if (_model != IntPtr.Zero)
            {
                vosk_model_free(_model);
                _model = IntPtr.Zero;
            }

            Debug.Log("[STTBridge] Vosk kaynakları serbest bırakıldı.");
        }
    }
}