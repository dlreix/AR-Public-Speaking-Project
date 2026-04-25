using System;
using UnityEngine;

namespace SpeechPipeline
{
    /// <summary>
    /// Wraps Unity's looping microphone AudioClip.
    /// Poll() drains newly recorded samples as mono float[].
    /// Exposes the AudioClip so an AudioSource can share it for spectrum data.
    /// </summary>
    public sealed class AudioCaptureBuffer : IDisposable
    {
        public readonly int SampleRate;

        // Expose clip so SpeechPipelineController can reuse it for GetSpectrumData
        public AudioClip Clip => _clip;

        private AudioClip _clip;
        private int       _lastPos;
        private readonly int _clipSamples;
        private float[]   _scratch;
        private bool      _disposed;

        public AudioCaptureBuffer(int durationSecs = 10, int sampleRate = 16000)
        {
            SampleRate  = sampleRate;
            _clip       = Microphone.Start(null, true, durationSecs, sampleRate);
            if (_clip == null)
                throw new InvalidOperationException(
                    "[AudioCaptureBuffer] Microphone.Start returned null. " +
                    "Check device permissions and that a microphone is connected.");
            _clipSamples = durationSecs * sampleRate;
        }

        /// <summary>
        /// Drains all samples recorded since last call. Returns null when nothing new.
        /// Always mono (stereo sources are averaged). Call from main thread only.
        /// </summary>
        public float[] Poll()
        {
            if (_disposed || _clip == null) return null;

            int pos       = Microphone.GetPosition(null);
            if (pos < 0) return null;

            int available = (pos - _lastPos + _clipSamples) % _clipSamples;
            if (available <= 0) return null;

            int channels = _clip.channels;
            int rawCount = available * channels;

            if (_scratch == null || _scratch.Length < rawCount)
                _scratch = new float[rawCount];

            _clip.GetData(_scratch, _lastPos);
            _lastPos = pos;

            if (channels == 1)
            {
                var mono = new float[available];
                Array.Copy(_scratch, mono, available);
                return mono;
            }

            var result = new float[available];
            for (int i = 0; i < available; i++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++)
                    sum += _scratch[i * channels + c];
                result[i] = sum / channels;
            }
            return result;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Microphone.End(null);
            if (_clip != null)
            {
                UnityEngine.Object.Destroy(_clip);
                _clip = null;
            }
        }
    }
}
