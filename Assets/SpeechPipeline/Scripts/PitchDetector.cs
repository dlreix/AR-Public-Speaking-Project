using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpeechPipeline
{
    // Estimates fundamental frequency (F0) each voiced frame via Harmonic Product Spectrum.
    // HPS is more robust than naive peak-picking because harmonics reinforce the fundamental.
    public sealed class PitchDetector
    {
        public float MinHz = 70f;
        public float MaxHz = 500f;

        private readonly int         _specSize;
        private readonly float[]     _specBuf;
        private readonly List<float> _history = new List<float>(512);
        private readonly AudioSource _src;

        public PitchDetector(AudioSource src, int specSize = 1024)
        {
            _src      = src;
            _specSize = specSize;
            _specBuf  = new float[specSize];
        }

        // Call while the speaker is voiced. Returns F0 in Hz (0 if no clear pitch).
        public float AnalyzeFrame()
        {
            _src.GetSpectrumData(_specBuf, 0, FFTWindow.BlackmanHarris);
            float f0 = HPS(_specBuf);
            if (f0 > 0f) _history.Add(f0);
            return f0;
        }

        // Computes avg/stdDev/min/max from collected samples and clears history.
        // Returns all zeros if no voiced frames were recorded.
        public (float avg, float stdDev, float min, float max) FlushStats()
        {
            if (_history.Count == 0) return (0f, 0f, 0f, 0f);

            float sum = 0f, mn = float.MaxValue, mx = float.MinValue;
            foreach (float p in _history)
            {
                sum += p;
                if (p < mn) mn = p;
                if (p > mx) mx = p;
            }
            float avg  = sum / _history.Count;
            float vsum = 0f;
            foreach (float p in _history) vsum += (p - avg) * (p - avg);
            float sd = Mathf.Sqrt(vsum / _history.Count);

            _history.Clear();
            return (avg, sd, mn, mx);
        }

        public void Reset() => _history.Clear();

        private float HPS(float[] spec)
        {
            int n = spec.Length;
            const int order = 5;

            var hps = new float[n];
            Array.Copy(spec, hps, n);
            for (int h = 2; h <= order; h++)
                for (int i = 0; i < n / h; i++)
                    hps[i] *= spec[i * h];

            float hzPerBin = (float)AudioSettings.outputSampleRate / (2f * n);
            int   lo       = Mathf.RoundToInt(MinHz / hzPerBin);
            int   hi       = Mathf.Min(Mathf.RoundToInt(MaxHz / hzPerBin), n / order - 1);

            float peak = 0f;
            int   bin  = -1;
            for (int i = lo; i <= hi; i++)
                if (hps[i] > peak) { peak = hps[i]; bin = i; }

            if (bin < 0 || peak < 1e-8f) return 0f;

            // Parabolic interpolation for sub-bin frequency accuracy.
            float f0 = Parabolic(hps, bin) * hzPerBin;
            return f0 >= MinHz && f0 <= MaxHz ? f0 : 0f;
        }

        private static float Parabolic(float[] d, int p)
        {
            if (p <= 0 || p >= d.Length - 1) return p;
            float a = d[p - 1], b = d[p], c = d[p + 1];
            float denom = a - 2f * b + c;
            if (Mathf.Abs(denom) < 1e-10f) return p;
            return p + 0.5f * (a - c) / denom;
        }
    }
}
