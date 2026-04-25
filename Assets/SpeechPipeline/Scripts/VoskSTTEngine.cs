using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;
using Vosk;

namespace SpeechPipeline
{
    /// <summary>
    /// Wraps Vosk on a background thread so the Unity main thread is never blocked.
    /// The constructor returns immediately — model loading happens in the background.
    /// Poll IsReady before sending audio; check LoadError if it stays false.
    /// </summary>
    public sealed class VoskSTTEngine : IDisposable
    {
        public readonly struct PartialResult
        {
            public readonly string Text;
            public PartialResult(string t) => Text = t;
        }

        public readonly struct FinalResult
        {
            public readonly string Text;
            public FinalResult(string t) => Text = t;
        }

        public bool   IsReady   => _isReady;
        public string LoadError => _loadError;

        private readonly ConcurrentQueue<float[]> _audioQueue  = new ConcurrentQueue<float[]>();
        private readonly ConcurrentQueue<object>  _resultQueue = new ConcurrentQueue<object>();

        private volatile bool   _isReady;
        private volatile bool   _running = true;
        private volatile string _loadError;

        // Use full qualifier to avoid name clash with this class
        private global::Vosk.VoskRecognizer _recognizer;
        private readonly Thread _worker;
        private readonly string _modelPath;
        private readonly int    _sampleRate;

        private const string KeyText    = "\"text\" : \"";
        private const string KeyPartial = "\"partial\" : \"";

        public VoskSTTEngine(string modelPath, int sampleRate = 16000)
        {
            _modelPath  = modelPath;
            _sampleRate = sampleRate;
            _worker     = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name         = "VoskSTTWorker",
            };
            _worker.Start();
        }

        /// <summary>Enqueue a mono float[] chunk from the main thread.</summary>
        public void EnqueueAudio(float[] samples)
        {
            if (!_isReady || !_running || samples == null || samples.Length == 0) return;
            var copy = new float[samples.Length];
            Array.Copy(samples, copy, samples.Length);
            _audioQueue.Enqueue(copy);
        }

        /// <summary>Drain one result per call. Returns false when queue is empty.</summary>
        public bool TryDequeueResult(out object result) =>
            _resultQueue.TryDequeue(out result);

        public void Dispose()
        {
            _running = false;
            _worker?.Join(1000);
            _recognizer?.Dispose();
        }

        // ── Worker thread ────────────────────────────────────────────────

        private void WorkerLoop()
        {
            // Phase 1: load model (heavy — off main thread)
            try
            {
                var model   = new Model(_modelPath);
                _recognizer = new global::Vosk.VoskRecognizer(model, _sampleRate);
                _recognizer.SetMaxAlternatives(0);
                _recognizer.SetWords(false);
                _isReady = true;
            }
            catch (Exception e)
            {
                _loadError = e.Message;
                _running   = false;
                return;
            }

            // Phase 2: process audio
            while (_running)
            {
                if (_audioQueue.TryDequeue(out float[] samples))
                {
                    byte[] pcm     = ToPCM16(samples);
                    bool   isFinal = _recognizer.AcceptWaveform(pcm, pcm.Length);

                    if (isFinal)
                    {
                        string text = ParseJson(_recognizer.Result(), KeyText);
                        if (!string.IsNullOrWhiteSpace(text))
                            _resultQueue.Enqueue(new FinalResult(text));
                    }
                    else
                    {
                        string text = ParseJson(_recognizer.PartialResult(), KeyPartial);
                        if (!string.IsNullOrWhiteSpace(text))
                            _resultQueue.Enqueue(new PartialResult(text));
                    }
                }
                else
                {
                    Thread.Sleep(5);
                }
            }

            // Flush on shutdown
            if (_recognizer != null)
            {
                string text = ParseJson(_recognizer.FinalResult(), KeyText);
                if (!string.IsNullOrWhiteSpace(text))
                    _resultQueue.Enqueue(new FinalResult(text));
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static byte[] ToPCM16(float[] samples)
        {
            var bytes = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                float clamped    = Mathf.Clamp(samples[i], -1f, 1f);
                short s          = (short)(clamped * short.MaxValue);
                bytes[i * 2]     = (byte)(s & 0xFF);
                bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }
            return bytes;
        }

        private static string ParseJson(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int start = json.IndexOf(key, StringComparison.Ordinal);
            if (start < 0) return null;
            start += key.Length;
            int end = json.IndexOf('"', start);
            return end < 0 ? null : json.Substring(start, end - start).Trim();
        }
    }
}
