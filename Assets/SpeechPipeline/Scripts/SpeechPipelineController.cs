using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
#if UNITY_ANDROID && !UNITY_EDITOR
using System.IO.Compression;
#endif
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpeechPipeline
{
    /// <summary>
    /// Attach to any persistent GameObject.
    /// Set ModelFolder in the Inspector to the StreamingAssets subfolder name.
    ///
    /// Flow:
    ///   Play  → model loads in background, logs ready once done.
    ///   SPACE → start recording.
    ///   SPACE → stop, print session summary.
    ///   SPACE → start a new session.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("Speech Pipeline/Controller")]
    public sealed class SpeechPipelineController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("STT")]
        [Tooltip("Subfolder name inside StreamingAssets")]
        public string ModelFolder   = "vosk-model-small-en-us-0.15";
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

        [Header("Scoring Integration")]
        public PerformanceScoringEngine ScoringEngine;
        public SpeechAdapter SpeechAdapter;
        public bool UpdateScoringOnSessionEnd = true;

        [Header("Debug Input")]
        [Tooltip("Allow SPACE key to start/stop recording without MainController. Disable in VR builds.")]
        public bool EnableSpaceKeyInput = false;

        // ── State machine ─────────────────────────────────────────────────────

        private enum PipelineState { Loading, Ready, Recording }
        private PipelineState _state = PipelineState.Loading;
        private bool _startWhenReadyRequested;
        private bool _modelUnavailable;
        private bool _loadErrorLogged;
        private bool _isPaused;

        public bool IsReady => _state == PipelineState.Ready;
        public bool IsRecording => _state == PipelineState.Recording;
        public bool IsPaused => _isPaused;
        public event Action<string> FinalTranscriptReceived;

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
        private float _totalPausedSec;
        private float _pauseStartedAt = -1f;
        private const float ChunkSec = 0.1f;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private IEnumerator Start()
        {
            AutoWireScoringTargets();

            string modelPath = ResolveModelPath();
#if UNITY_ANDROID && !UNITY_EDITOR
            yield return ExtractAndroidStreamingModelIfNeeded();
            modelPath = ResolveModelPath();
#endif
            if (!IsUsableModelFolder(modelPath))
            {
                _modelUnavailable = true;
                Debug.LogError(
                    $"[SpeechPipeline] Vosk model folder missing or empty: {modelPath}. " +
                    $"Place the model folder in Assets/StreamingAssets/{ModelFolder}/ before enabling voice analysis.");
                yield break;
            }

            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                _modelUnavailable = true;
                Debug.LogError("[SpeechPipeline] Microphone permission denied.");
                yield break;
            }

            // AudioSource is guaranteed by [RequireComponent] — used only for GetSpectrumData
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

            ConsoleDisplay.LoadingModel();
            _stt = new VoskSTTEngine(modelPath, SampleRate);
        }

        private void Update()
        {
            if (_stt == null) return;

            // ── Wait for model ────────────────────────────────────────────────
            if (!_stt.IsReady)
            {
                if (!_loadErrorLogged && _stt.LoadError != null)
                {
                    _loadErrorLogged = true;
                    _modelUnavailable = true;
                    _startWhenReadyRequested = false;
                    Debug.LogError($"[SpeechPipeline] STT load failed: {_stt.LoadError}");
                }

                return;
            }
            if (!_modelReadyLogged)
            {
                _modelReadyLogged = true;
                ConsoleDisplay.ModelReady();
                _state = PipelineState.Ready;

                if (_startWhenReadyRequested)
                {
                    _startWhenReadyRequested = false;
                    BeginSession();
                }
            }

            // ── Space key (debug only — disabled by default in VR) ────────────
            if (EnableSpaceKeyInput && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                switch (_state)
                {
                    case PipelineState.Ready:     BeginRecordingFromShell(); break;
                    case PipelineState.Recording: EndRecordingFromShell();   break;
                }
                return;
            }

            if (_state != PipelineState.Recording || _isPaused) return;

            // ── Drain STT results ─────────────────────────────────────────────
            DrainSTT();

            // ── Audio chunk timer ─────────────────────────────────────────────
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

        public bool BeginRecordingFromShell()
        {
            if (_state == PipelineState.Recording)
            {
                return true;
            }

            if (_modelUnavailable)
            {
                Debug.LogWarning("[SpeechPipeline] Cannot start voice recording because the speech model or microphone is unavailable.");
                return false;
            }

            if (_state == PipelineState.Loading)
            {
                _startWhenReadyRequested = true;
                Debug.Log("[SpeechPipeline] Session start requested before STT was ready. Recording will start when the model finishes loading.");
                return false;
            }

            BeginSession();
            return true;
        }

        public bool EndRecordingFromShell()
        {
            _startWhenReadyRequested = false;

            if (_state != PipelineState.Recording)
            {
                return false;
            }

            _isPaused = false;
            EndSession();
            return true;
        }

        public void PauseRecordingFromShell()
        {
            if (_state != PipelineState.Recording || _isPaused)
            {
                return;
            }

            _isPaused = true;
            _pauseStartedAt = Time.realtimeSinceStartup;
            _capture?.Poll(); // flush buffered audio so we don't process dead air on resume
            if (_wasSpeaking)
            {
                _pace?.CancelUtterance(); // avoid orphaned StartUtterance inflating WPM on resume
                _wasSpeaking   = false;
                _speakingTimer = 0f;
                _tickTimer     = 0f;
            }
        }

        public void ResumeRecordingFromShell()
        {
            if (_state != PipelineState.Recording || !_isPaused)
            {
                return;
            }

            if (_pauseStartedAt >= 0f)
            {
                _totalPausedSec += Time.realtimeSinceStartup - _pauseStartedAt;
                _pauseStartedAt  = -1f;
            }

            _isPaused = false;
            _capture?.Poll(); // discard audio captured while paused
            _pause?.Reset();  // clear stale SilenceTimer so resume doesn't fire a spurious OnPauseDetected
        }

        private void BeginSession()
        {
            _state = PipelineState.Recording;
            _isPaused       = false;
            _pauseStartedAt = -1f;
            _totalPausedSec = 0f;
            _capture.Poll(); // flush stale audio

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
            DrainSTT();

            if (!string.IsNullOrWhiteSpace(_currentPartial))
            {
                FinaliseUtterance(_currentPartial);
            }

            float avgWpm      = _sessionWpmCount   > 0 ? _sessionWpmSum      / _sessionWpmCount   : EstimateSessionWpm();
            float avgPitchStd = _sessionPitchCount > 0 ? _sessionPitchStdSum / _sessionPitchCount : 0f;
            float totalSec    = Mathf.Max(0f, Time.realtimeSinceStartup - _sessionStart - _totalPausedSec);
            float totalMin    = totalSec / 60f;
            float fillerPerMin = totalMin > 0f ? _sessionFillers / totalMin : 0f;
            float avgPause = _sessionPauses > 0 ? _sessionPauseTotal / _sessionPauses : 0f;
            float toneScore = NormalizePitchStd(avgPitchStd);

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

            PushSpeechMetricsToScoring(avgWpm, fillerPerMin, avgPause, toneScore);
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
            FinalTranscriptReceived?.Invoke(text);
            if (wpm > 0f) { _sessionWpmSum      += wpm; _sessionWpmCount++;   }
            if (sd  > 0f) { _sessionPitchStdSum += sd;  _sessionPitchCount++; }

            ConsoleDisplay.Utterance(m);
            ResetUtteranceAccumulators();
        }

        private void AutoWireScoringTargets()
        {
            if (SpeechAdapter == null)
            {
                SpeechAdapter = FindFirstObjectByType<SpeechAdapter>(FindObjectsInactive.Include);
            }

            if (ScoringEngine == null)
            {
                ScoringEngine = FindFirstObjectByType<PerformanceScoringEngine>(FindObjectsInactive.Include);
            }

            if (SpeechAdapter != null && ScoringEngine != null)
            {
                SpeechAdapter.SetScoringEngine(ScoringEngine);
            }
        }

        private void PushSpeechMetricsToScoring(float wpm, float fillerPerMin, float avgPause, float toneScore)
        {
            if (!UpdateScoringOnSessionEnd)
            {
                return;
            }

            AutoWireScoringTargets();

            if (SpeechAdapter != null)
            {
                SpeechAdapter.OnSpeechAnalysisComplete(wpm, fillerPerMin, avgPause, toneScore);
                return;
            }

            if (ScoringEngine != null)
            {
                ScoringEngine.SetSpeechMetrics(wpm, fillerPerMin, avgPause, toneScore);
                ScoringEngine.CalculateSessionScore();
                return;
            }

            Debug.LogWarning("[SpeechPipeline] No SpeechAdapter or PerformanceScoringEngine found for speech metrics.");
        }

        private float EstimateSessionWpm()
        {
            if (_sessionSpeaking <= 0f || _sessionWords <= 0)
            {
                return 0f;
            }

            return (_sessionWords / _sessionSpeaking) * 60f;
        }

        private static float NormalizePitchStd(float pitchStdHz)
        {
            if (pitchStdHz <= 10f)
            {
                return 0f;
            }

            if (pitchStdHz >= 55f)
            {
                return 100f;
            }

            return Mathf.InverseLerp(10f, 55f, pitchStdHz) * 100f;
        }

        private string ResolveModelPath()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return Path.Combine(Path.Combine(Application.persistentDataPath, "VoskModels"), ModelFolder);
#else
            return Path.Combine(Application.streamingAssetsPath, ModelFolder);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private IEnumerator ExtractAndroidStreamingModelIfNeeded()
        {
            string targetPath = ResolveModelPath();
            if (IsUsableModelFolder(targetPath))
            {
                yield break;
            }

            string sourcePrefix = $"assets/{ModelFolder.Trim('/', '\\')}/";
            try
            {
                Directory.CreateDirectory(targetPath);
                using FileStream apkStream = File.OpenRead(Application.dataPath);
                using ZipArchive archive = new ZipArchive(apkStream, ZipArchiveMode.Read);
                int extractedFiles = 0;

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string entryName = entry.FullName.Replace('\\', '/');
                    if (!entryName.StartsWith(sourcePrefix) || entryName.EndsWith("/"))
                    {
                        continue;
                    }

                    string relativePath = entryName.Substring(sourcePrefix.Length);
                    if (string.IsNullOrWhiteSpace(relativePath))
                    {
                        continue;
                    }

                    string destinationPath = Path.Combine(
                        targetPath,
                        relativePath.Replace('/', Path.DirectorySeparatorChar));
                    string destinationDirectory = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(destinationDirectory))
                    {
                        Directory.CreateDirectory(destinationDirectory);
                    }

                    using Stream sourceStream = entry.Open();
                    using FileStream destinationStream = File.Create(destinationPath);
                    sourceStream.CopyTo(destinationStream);
                    extractedFiles++;
                    if (extractedFiles % 12 == 0)
                    {
                        yield return null;
                    }
                }

                if (extractedFiles == 0)
                {
                    Debug.LogError($"[SpeechPipeline] No Vosk model files were found in APK StreamingAssets under {sourcePrefix}.");
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[SpeechPipeline] Failed to extract Vosk model from Android StreamingAssets: {exception.Message}");
            }
        }
#endif

        private static bool IsUsableModelFolder(string modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
            {
                return false;
            }

            if (!Directory.Exists(modelPath))
            {
                return false;
            }

            return Directory.GetFiles(modelPath).Length > 0 || Directory.GetDirectories(modelPath).Length > 0;
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
