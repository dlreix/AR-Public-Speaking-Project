using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics;
using UnityEngine;
using System.Threading.Tasks;

namespace PresentationAnalyzer.Core
{
    /// <summary>
    /// AudioRecorder kanalından PCM byte[] okur (Consumer),
    /// RMS ses şiddeti ve temel frekans (pitch) hesaplar.
    /// Sessizlik sürelerini ayrıca takip eder.
    /// </summary>
    public class AcousticAnalyzer
    {
        // ─── Sabitler ─────────────────────────────────────────────────────────

        // Bu eşiğin altındaki RMS değerleri sessizlik sayılır
        private const float SilenceThreshold = 0.01f;

        // FFT pencere boyutu — 2'nin kuvveti olmalı
        private const int FftWindowSize = 1024;

        // ─── Anlık değerler (UI için) ─────────────────────────────────────────
        public float CurrentRms   { get; private set; }
        public float CurrentPitch { get; private set; }

        // ─── Birikim değerleri ────────────────────────────────────────────────
        private float _totalRms;
        private int   _rmsCount;

        private float _speechDurationSec;
        private float _silenceDurationSec;

        // Pitch standart sapması için tüm değerleri biriktir
        private readonly List<float> _pitchSamples = new List<float>();

        // ─── Thread güvenliği ─────────────────────────────────────────────────
        private readonly object _lock = new object();

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// AudioRecorder kanalını dinleyen Consumer döngüsü.
        /// Task.Run ile arka plan thread'inde çalıştırılır.
        /// </summary>
        public async Task StartConsumerLoop(
            ChannelReader<byte[]> audioChannel,
            CancellationToken ct)
        {
            Debug.Log("[AcousticAnalyzer] Consumer döngüsü başladı.");

            // Her chunk yaklaşık 20ms ses verisi içerir
            // 16kHz / 320 sample = 50 chunk/saniye
            const float chunkDurationSec = 320f / AudioRecorder.SampleRate;

            try
            {
                await foreach (byte[] chunk in audioChannel.ReadAllAsync(ct))
                {
                    // byte[] → float[] dönüşümü
                    float[] samples = Pcm16ToFloat(chunk);

                    // RMS hesapla
                    float rms = CalculateRms(samples);

                    // Pitch hesapla (FFT tabanlı)
                    float pitch = CalculatePitchFFT(samples);

                    lock (_lock)
                    {
                        CurrentRms   = rms;
                        CurrentPitch = pitch;

                        // Ortalama RMS için biriktir
                        _totalRms += rms;
                        _rmsCount++;

                        // Pitch örneklerini biriktir (sıfır değerleri hariç)
                        if (pitch > 0f)
                            _pitchSamples.Add(pitch);

                        // Sessizlik / konuşma süresi
                        if (rms < SilenceThreshold)
                            _silenceDurationSec += chunkDurationSec;
                        else
                            _speechDurationSec  += chunkDurationSec;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[AcousticAnalyzer] Consumer döngüsü iptal edildi.");
            }
        }

        /// <summary>
        /// Toplam konuşma süresini döner (sessizlik hariç).
        /// SessionController WPM hesabı için bunu kullanır.
        /// </summary>
        public float GetSpeechDuration()
        {
            lock (_lock) { return _speechDurationSec; }
        }

        /// <summary>
        /// Tüm akustik metrikleri toplayıp AcousticResult olarak döner.
        /// </summary>
        public AcousticResult GetResult(float totalDurationSec)
        {
            lock (_lock)
            {
                float avgRms = _rmsCount > 0
                    ? _totalRms / _rmsCount
                    : 0f;

                float pitchStdDev = CalculateStdDev(_pitchSamples);

                return new AcousticResult
                {
                    TotalDurationSec   = MathF.Round(totalDurationSec, 1),
                    SpeechDurationSec  = MathF.Round(_speechDurationSec, 1),
                    SilenceDurationSec = MathF.Round(_silenceDurationSec, 1),
                    AverageRms         = MathF.Round(avgRms, 4),
                    PitchStdDev        = MathF.Round(pitchStdDev, 1)
                };
            }
        }

        /// <summary>
        /// Yeni kayıt için tüm değerleri sıfırlar.
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                CurrentRms          = 0f;
                CurrentPitch        = 0f;
                _totalRms           = 0f;
                _rmsCount           = 0;
                _speechDurationSec  = 0f;
                _silenceDurationSec = 0f;
                _pitchSamples.Clear();
            }
        }

        // ─── Hesaplama metodları ──────────────────────────────────────────────

        /// <summary>
        /// RMS (Root Mean Square) — ses şiddetinin en doğru ölçüsü.
        /// Her örneğin karesinin ortalamasının karekökü.
        /// 0.0 = sessizlik, 1.0 = maksimum şiddet.
        /// </summary>
        private static float CalculateRms(float[] samples)
        {
            if (samples.Length == 0) return 0f;

            float sum = 0f;
            foreach (float s in samples)
                sum += s * s;

            return MathF.Sqrt(sum / samples.Length);
        }

        /// <summary>
        /// FFT tabanlı temel frekans (F0) tahmini.
        /// En güçlü frekans bileşenini bulur → konuşma tonu (Hz).
        /// Tipik konuşma aralığı: 85–255 Hz.
        /// </summary>
        private static float CalculatePitchFFT(float[] samples)
        {
            if (samples.Length < FftWindowSize)
                return 0f;

            // FFT için Complex dizisi hazırla (ilk FftWindowSize örnek)
            var complex = new System.Numerics.Complex[FftWindowSize];
            for (int i = 0; i < FftWindowSize; i++)
                complex[i] = new System.Numerics.Complex(samples[i], 0);

            // MathNet FFT (in-place)
            Fourier.Forward(complex, FourierOptions.Matlab);

            // En güçlü frekans bileşenini bul (konuşma aralığında)
            float maxMagnitude = 0f;
            int   maxIndex     = 0;

            // Konuşma frekans aralığı: 85–400 Hz
            int minBin = (int)(85f  * FftWindowSize / AudioRecorder.SampleRate);
            int maxBin = (int)(400f * FftWindowSize / AudioRecorder.SampleRate);

            for (int i = minBin; i <= maxBin && i < FftWindowSize / 2; i++)
            {
                float magnitude = (float)complex[i].Magnitude;
                if (magnitude > maxMagnitude)
                {
                    maxMagnitude = magnitude;
                    maxIndex     = i;
                }
            }

            if (maxIndex == 0) return 0f;

            // Bin indeksini Hz'e çevir
            float hz = (float)maxIndex * AudioRecorder.SampleRate / FftWindowSize;
            Debug.Log($"[Pitch] maxIndex: {maxIndex}, hz: {hz}");
            return hz;
        }

        /// <summary>
        /// Pitch standart sapması — ton dalgalanmasının ölçüsü.
        /// Yüksek = vurgulu/dinamik konuşma, Düşük = monoton konuşma.
        /// </summary>
        private static float CalculateStdDev(List<float> values)
        {
            if (values.Count < 2) return 0f;

            float mean = 0f;
            foreach (float v in values) mean += v;
            mean /= values.Count;

            float variance = 0f;
            foreach (float v in values)
                variance += (v - mean) * (v - mean);
            variance /= values.Count;

            return MathF.Sqrt(variance);
        }

        /// <summary>
        /// 16-bit Little-Endian PCM byte[] → float[] [-1, 1]
        /// AudioRecorder'ın FloatToPcm16 metodunun tersi işlemi.
        /// </summary>
        private static float[] Pcm16ToFloat(byte[] bytes)
        {
            float[] samples = new float[bytes.Length / 2];

            for (int i = 0; i < samples.Length; i++)
            {
                short pcm = (short)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
                samples[i] = pcm / (float)short.MaxValue;
            }

            return samples;
        }
    }

    // ─── Sonuç modeli ─────────────────────────────────────────────────────────

    [Serializable]
    public class AcousticResult
    {
        public float TotalDurationSec;
        public float SpeechDurationSec;
        public float SilenceDurationSec;
        public float AverageRms;
        public float PitchStdDev;
    }
}