using System;
using UnityEngine;

namespace SpeechPipeline
{
    /// <summary>
    /// Classifies audio chunks as voiced / silent using RMS energy.
    /// Fires OnPauseDetected when continuous silence exceeds PauseThreshold seconds.
    /// </summary>
    public sealed class RMSPauseDetector
    {
        // Tune NoiseFloor per environment; 0.015 suits a typical quiet room.
        public float NoiseFloor     = 0.015f;
        // Silence longer than this is reported as a pause.
        public float PauseThreshold = 1.5f;
        // Ignore sub-hysteresis gaps so brief unvoiced bursts don't flicker IsSpeaking.
        public float Hysteresis     = 0.08f;

        /// <summary>Fires when silence first exceeds PauseThreshold. Arg = silence duration so far.</summary>
        public event Action<float> OnPauseDetected;

        public bool  IsSpeaking   { get; private set; }
        public bool  IsInPause    { get; private set; }
        public float CurrentRMS   { get; private set; }
        public float PauseElapsed { get; private set; }
        public float SilenceTimer { get; private set; }

        private float _gapTimer;

        /// <summary>
        /// Feed a mono float[] chunk and its real-world duration in seconds.
        /// Returns true if the chunk is voiced (caller should forward to STT).
        /// </summary>
        public bool ProcessChunk(float[] samples, float durationSec)
        {
            CurrentRMS = ComputeRMS(samples);
            bool voiced = CurrentRMS >= NoiseFloor;

            if (voiced)
            {
                // Exit pause state once gap exceeds hysteresis threshold
                if (_gapTimer >= Hysteresis && IsInPause)
                {
                    IsInPause = false;
                }
                _gapTimer    = 0f;
                SilenceTimer = 0f;
                IsSpeaking   = true;
            }
            else
            {
                _gapTimer    += durationSec;
                SilenceTimer += durationSec;

                if (SilenceTimer >= PauseThreshold && !IsInPause)
                {
                    IsInPause    = true;
                    IsSpeaking   = false;
                    PauseElapsed = SilenceTimer;
                    OnPauseDetected?.Invoke(SilenceTimer);
                }
                else if (IsInPause)
                {
                    PauseElapsed = SilenceTimer;
                }
            }

            return voiced;
        }

        public static float ComputeRMS(float[] samples)
        {
            if (samples == null || samples.Length == 0) return 0f;
            float sum = 0f;
            foreach (float s in samples) sum += s * s;
            return Mathf.Sqrt(sum / samples.Length);
        }
    }
}
