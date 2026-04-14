using System.Threading;
using System.Threading.Tasks;
using PresentationAnalyzer.Core;
using UnityEngine;

namespace PresentationAnalyzer
{
    /// <summary>
    /// Test versiyonu — UI yok, sonuçlar yalnızca konsola yazılır.
    /// Play Mode'da Space tuşu ile başlat/durdur.
    /// </summary>
    public class SessionController : MonoBehaviour
    {
        // ─── Core nesneleri ───────────────────────────────────────────────────
        private AudioRecorder    _recorder;
        private AcousticAnalyzer _acousticAnalyzer;
        private STTBridge        _sttBridge;
        private NlpProcessor     _nlpProcessor;
        private ReportGenerator  _reportGenerator;

        // ─── Oturum durumu ────────────────────────────────────────────────────
        private CancellationTokenSource _cts;
        private bool                    _isRecording;
        private float                   _sessionStartTime;

        // ─── Unity yaşam döngüsü ──────────────────────────────────────────────

        private void Awake()
        {
            _recorder         = new AudioRecorder();
            _acousticAnalyzer = new AcousticAnalyzer();
            _sttBridge        = new STTBridge();
            _nlpProcessor     = new NlpProcessor();
            _reportGenerator  = new ReportGenerator();
        }

        private void Start()
        {
            _sttBridge.OnFinalResult += _nlpProcessor.AppendText;
            Debug.Log("[SessionController] Hazır. SPACE = başlat / durdur.");
        }

        private void Update()
        {
            // Mikrofon verisini ana thread'de oku
            if (_isRecording)
                _recorder.Tick();

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (!_isRecording)
                    StartSession();
                else
                    _ = StopSessionAsync();
            }
        }

        private void OnDestroy()
        {
            if (_isRecording)
                _ = StopSessionAsync();

            _sttBridge.OnFinalResult -= _nlpProcessor.AppendText;
            _sttBridge.Dispose();
        }

        // ─── Oturum kontrolü ──────────────────────────────────────────────────

        private void StartSession()
        {
            // Önceki oturumdan kalan native kaynakları temizle
            _sttBridge.Dispose();

            // Nesneleri taze oluştur
            _acousticAnalyzer = new AcousticAnalyzer();
            _sttBridge        = new STTBridge();
            _nlpProcessor     = new NlpProcessor();

            // Event'i yeniden bağla
            _sttBridge.OnFinalResult += _nlpProcessor.AppendText;

            // Vosk'u başlat
            _sttBridge.Initialize();

            _cts              = new CancellationTokenSource();
            _isRecording      = true;
            _sessionStartTime = Time.realtimeSinceStartup;

            _recorder.StartRecording(_cts.Token);

            _ = Task.Run(async () =>
                await _acousticAnalyzer.StartConsumerLoop(
                    _recorder.AcousticChannel, _cts.Token));

            _ = Task.Run(async () =>
                await _sttBridge.StartConsumerLoop(
                    _recorder.SttChannel, _cts.Token));

            Debug.Log("[SessionController] Kayıt başladı. Durdurmak için SPACE.");
        }

        private async Task StopSessionAsync()
        {
            _isRecording = false;

            _cts.Cancel();
            _recorder.StopRecording();
            _sttBridge.FlushFinalResult();

            // AcousticAnalyzer'ın son chunk'ı işlemesi için bekle
            await Task.Delay(300);

            float totalSec  = Time.realtimeSinceStartup - _sessionStartTime;
            float speechSec = _acousticAnalyzer.GetSpeechDuration();

            AcousticResult acoustic = _acousticAnalyzer.GetResult(totalSec);
            NlpResult nlp = _nlpProcessor.GetResult(acoustic.TotalDurationSec);
            AnalysisReport report   = _reportGenerator.Generate(acoustic, nlp);
            
            _reportGenerator.CallSpeechAnalysis(report); 
            _reportGenerator.LogReport(report);
            _reportGenerator.SaveToJson(report);
        }
    }
}