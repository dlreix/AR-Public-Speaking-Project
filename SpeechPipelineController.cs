using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpeechPipeline
{
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("Speech Pipeline/Controller")]
    public sealed class SpeechPipelineController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("STT")]
        [Tooltip("Subfolder name inside StreamingAssets")]
        public string ModelFolder   = "vosk-model-en-us-0.42-gigaspeech";
        public int    SampleRate    = 16000;

        [Header("Pause Detection")]
        [Range(0f, 0.1f)]
        public float NoiseFloor     = 0.015f;
        [Range(0.3f, 5f)]
        public float PauseThreshold = 1.5f;

        [Header("Pitch Analysis")]
        public int   SpectrumSize   = 1024;
        [Range(60f, 150f)]
        public float MinPitchHz     = 70f;
        [Range(300f, 800f)]
        public float MaxPitchHz     = 500f;

        [Header("Live Display")]
        [Tooltip("Seconds between live status lines while speaking")]
        [Range(0.5f, 3f)]
        public float TickInterval   = 1f;

        // ── Selin: Scoring Integration ────────────────────────────────────────
        [Header("Scoring Integration")]
        [Tooltip("Assign the PerformanceScoringEngine component from your scene.")]
        public PerformanceScoringEngine ScoringEngine;
        [Tooltip("Assign the SpeechAdapter component from your scene.")]
        public SpeechAdapter SpeechAdapter;

        // ── State machine ─────────────────────────────────────────────────────

        private enum PipelineState { Loading, Ready, Recording }
        private PipelineState _state = PipelineState.Loading;

        // ── Subsystems ────────────────────────────────────────────────────────

        private AudioCaptureBuffer _capture;
        private VoskSTTEngine      _stt;
        private RMSPauseDetector   _pause;
        private PitchDetector      _pitch;
        private PaceTracker        _pace;
        private FillerDetector     _filler;
        private AudioSource        _spectrumSrc;

        // ── Live tick accumulators ────────────────────────────────────────────

        private float  _speakingTimer;
        private float  _tickTimer;
        private bool   _wasSpeaking;
        private float  _lastPitchHz;
        private float  _lastRMS;
        private string _currentPartial;

        // ── Per-utterance accumulators ────────────────────────────────────────

        private int   _uttPauseCount;
        private float _uttLastPause;
        private float _uttRmsSum;
        private float _uttRmsMax;
        private int   _uttRmsCount;

        // ── Session accumulators ──────────────────────────────────────────────

        private float        _sessionStart;
        private float        _sessionSpeaking;
        private int          _sessionPauses;
        private float        _sessionPauseTotal;
        private int          _sessionWords;
        private int          _sessionFillers;
        private List<string> _sessionFillerList = new List<string>();
        private List<string> _sessionTranscript = new List<string>();
        private float        _sessionWpmSum;
        private int          _sessionWpmCount;
        private float        _sessionPitchStdSum;
        private int          _sessionPitchCount;

        // ── Misc ──────────────────────────────────────────────────────────────

        private bool  _modelReadyLogged;
        private float _chunkTimer;
        private const float ChunkSec = 0.1f;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private IEnumerator Start()
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                Debug.LogError("[SpeechPipeline] Microphone permission denied.");
                yield break;
            }

            _spectrumSrc = GetComponent<AudioSource>();
            _capture     = new AudioCaptureBuffer(10, SampleRate);
            _spectrumSrc.clip   = _capture.Clip;
            _spectrumSrc.mute   = true;
            _spectrumSrc.loop   = true;
            _spectrumSrc.Play();

            _pause = new RMSPauseDetector
            {
                NoiseFloor     = NoiseFloor,
                PauseThreshold = PauseThreshold,
            };
            _pause.OnPauseDetected += HandlePause;

            _pitch  = new PitchDetector(_spectrumSrc, SpectrumSize)
                      { MinHz = MinPitchHz, MaxHz = MaxPitchHz };
            _pace   = new PaceTracker();
            _filler = new FillerDetector();

            string modelPath = System.IO.Path.Combine(
                Application.streamingAssetsPath, ModelFolder);
            ConsoleDisplay.LoadingModel();
            _stt = new VoskSTTEngine(modelPath, SampleRate);
        }

        private void Update()
        {
            if (_stt == null) return;

            if (!_stt.IsReady)
            {
                if (_stt.LoadError != null)
                    Debug.LogError($"[SpeechPipeline] STT load failed: {_stt.LoadError}");
                return;
            }
            if (!_modelReadyLogged)
            {
                _modelReadyLogged = true;
                ConsoleDisplay.ModelReady();
                _state = PipelineState.Ready;
            }

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                switch (_state)
                {
                    case PipelineState.Ready:     BeginSession(); break;
                    case PipelineState.Recording: EndSession();   break;
                }
                return;
            }

            if (_state != PipelineState.Recording) return;

            DrainSTT();

            _chunkTimer += Time.unscaledDeltaTime;
            if (_chunkTimer < ChunkSec) return;
            _chunkTimer = 0f;

            float[] chunk = _capture.Poll();
            if (chunk == null || chunk.Length == 0) return;

            float chunkDur = (float)chunk.Length / SampleRate;
            bool  voiced   = _pause.ProcessChunk(chunk, chunkDur);

            _lastRMS = _pause.CurrentRMS;
            _uttRmsSum   += _lastRMS;
            _uttRmsCount++;
            if (_lastRMS > _uttRmsMax) _uttRmsMax = _lastRMS;

            if (voiced)
            {
                _lastPitchHz = _pitch.AnalyzeFrame();
                _stt.EnqueueAudio(chunk);

                if (!_wasSpeaking)
                {
                    _wasSpeaking  = true;
                    _pace.StartUtterance();
                    _tickTimer    = 0f;
                    _speakingTimer = 0f;
                }

                _speakingTimer   += chunkDur;
                _sessionSpeaking += chunkDur;
                _tickTimer       += chunkDur;

                if (_tickTimer >= TickInterval)
                {
                    _tickTimer -= TickInterval;
                    ConsoleDisplay.LiveTick(_speakingTimer, _lastPitchHz, _lastRMS, _currentPartial);
                }
            }
            else
            {
                if (_wasSpeaking)
                {
                    _wasSpeaking   = false;
                    _speakingTimer = 0f;
                    _tickTimer     = 0f;
                }
            }
        }

        // ── Session control ───────────────────────────────────────────────────

        private void BeginSession()
        {
            _state = PipelineState.Recording;
            _capture.Poll();

            _sessionStart       = Time.realtimeSinceStartup;
            _sessionSpeaking    = 0f;
            _sessionPauses      = 0;
            _sessionPauseTotal  = 0f;
            _sessionWords       = 0;
            _sessionFillers     = 0;
            _sessionFillerList.Clear();
            _sessionTranscript.Clear();
            _sessionWpmSum      = 0f; _sessionWpmCount   = 0;
            _sessionPitchStdSum = 0f; _sessionPitchCount = 0;

            ResetUtteranceAccumulators();
            ConsoleDisplay.RecordingStarted();
        }

        private void EndSession()
        {
            _state = PipelineState.Ready;

            // Son partial transcript varsa kelime/transcript bilgisini kaydet
            // ama WPM hesaplama — pace tracker baslatilmamis olabilir
            if (!string.IsNullOrWhiteSpace(_currentPartial))
            {
                var partialWords = _currentPartial.Split(
                    new[] { ' ', '\t', '\n' },
                    System.StringSplitOptions.RemoveEmptyEntries);
                if (partialWords.Length > 0)
                {
                    var partialFillers = _filler.Detect(_currentPartial);
                    _sessionWords   += partialWords.Length;
                    _sessionFillers += partialFillers.Count;
                    _sessionFillerList.AddRange(partialFillers);
                    _sessionTranscript.Add(_currentPartial);
                }
                _currentPartial = null;
            }

            // STT kuyruğunu son kez bosalt
            DrainSTT();

            // WPM: utterance bazli ortalama varsa onu kullan,
            // yoksa session toplam konusma suresinden hesapla
            float avgWpm;
            if (_sessionWpmCount > 0)
                avgWpm = _sessionWpmSum / _sessionWpmCount;
            else if (_sessionSpeaking > 0f && _sessionWords > 0)
                avgWpm = (_sessionWords / _sessionSpeaking) * 60f;
            else
                avgWpm = 0f;

            float avgPitchStd = _sessionPitchCount > 0 ? _sessionPitchStdSum / _sessionPitchCount : 0f;
            float totalSec    = Time.realtimeSinceStartup - _sessionStart;

            ConsoleDisplay.SessionSummary(
                totalSec,
                _sessionSpeaking,
                _sessionPauses,
                _sessionPauseTotal,
                _sessionWords,
                avgWpm,
                avgPitchStd,
                _sessionFillers,
                _sessionFillerList,
                _sessionTranscript);

            // ── Scoring Engine ve SpeechAdapter'a besle ───────────────────────
            float totalMinutes = totalSec / 60f;
            float fillerPerMin = totalMinutes > 0f ? _sessionFillers / totalMinutes : 0f;
            float avgPause     = _sessionPauses > 0 ? _sessionPauseTotal / _sessionPauses : 0f;
            float toneScore    = NormalizePitchStd(avgPitchStd);

            if (ScoringEngine != null)
            {
                ScoringEngine.SetSpeechMetrics(avgWpm, fillerPerMin, avgPause, toneScore);
                ScoringEngine.CalculateSessionScore();
            }

            if (SpeechAdapter != null)
            {
                SpeechAdapter.OnSpeechAnalysisComplete(avgWpm, fillerPerMin, avgPause, toneScore);
            }
        }

        private static float NormalizePitchStd(float stdHz)
        {
            if (stdHz <= 10f) return 0f;
            if (stdHz >= 55f) return 100f;
            return (stdHz - 10f) / (55f - 10f) * 100f;
        }

        private void ResetUtteranceAccumulators()
        {
            _uttPauseCount  = 0;
            _uttLastPause   = 0f;
            _uttRmsSum      = 0f;
            _uttRmsMax      = 0f;
            _uttRmsCount    = 0;
            _currentPartial = null;
            _wasSpeaking    = false;
            _speakingTimer  = 0f;
            _tickTimer      = 0f;
        }

        // ── STT drain ────────────────────────────────────────────────────────

        private void DrainSTT()
        {
            while (_stt.TryDequeueResult(out object result))
            {
                if (result is VoskSTTEngine.PartialResult p)
                {
                    bool isNew = p.Text != _currentPartial;
                    _currentPartial = p.Text;
                    if (isNew && !string.IsNullOrWhiteSpace(p.Text))
                        ConsoleDisplay.PartialTranscript(p.Text);
                }
                else if (result is VoskSTTEngine.FinalResult f)
                {
                    _currentPartial = null;
                    if (!string.IsNullOrWhiteSpace(f.Text))
                        FinaliseUtterance(f.Text);
                }
            }
        }

        // ── Pause event ───────────────────────────────────────────────────────

        private void HandlePause(float duration)
        {
            _uttLastPause       = duration;
            _uttPauseCount++;
            _sessionPauses++;
            _sessionPauseTotal += duration;
        }

        // ── Utterance finalisation ────────────────────────────────────────────

        private void FinaliseUtterance(string text)
        {
            var words = text.Split(
                new[] { ' ', '\t', '\n' },
                System.StringSplitOptions.RemoveEmptyEntries);

            var (wpm, dur)        = _pace.StopUtterance(words.Length);
            var (avg, sd, mn, mx) = _pitch.FlushStats();
            var fillers           = _filler.Detect(text);
            float avgRms          = _uttRmsCount > 0 ? _uttRmsSum / _uttRmsCount : 0f;

            var m = new UtteranceMetrics
            {
                Text          = text,
                WPM           = wpm,
                WordCount     = words.Length,
                DurationSec   = dur,
                AvgPitchHz    = avg,
                PitchStdDevHz = sd,
                PauseCount    = _uttPauseCount,
                LastPauseSec  = _uttLastPause,
                FillerWords   = fillers,
                FillerCount   = fillers.Count,
                AvgRMS        = avgRms,
                PeakRMS       = _uttRmsMax,
            };

            _sessionWords   += words.Length;
            _sessionFillers += fillers.Count;
            _sessionFillerList.AddRange(fillers);
            _sessionTranscript.Add(text);
            if (wpm > 0f) { _sessionWpmSum      += wpm; _sessionWpmCount++;   }
            if (sd  > 0f) { _sessionPitchStdSum += sd;  _sessionPitchCount++; }

            ConsoleDisplay.Utterance(m);
            ResetUtteranceAccumulators();
        }

        // ── Cleanup ───────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            if (_pause != null) _pause.OnPauseDetected -= HandlePause;
            _stt?.Dispose();
            _capture?.Dispose();
        }
    }
}
