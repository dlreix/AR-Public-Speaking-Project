using System.Threading;
using System.Threading.Channels;
using UnityEngine;

namespace PresentationAnalyzer.Core
{
    /// <summary>
    /// Mikrofon okuması ana thread'de (Update ile) yapılır.
    /// AudioClip.GetData yalnızca ana thread'den çağrılabildiği için
    /// ProducerLoop thread'den kaldırıldı.
    /// Her StartRecording çağrısında kanallar taze oluşturulur.
    /// </summary>
    public class AudioRecorder
    {
        public const int SampleRate    = 16000;
        public const int Channels      = 1;
        private const int ClipSeconds  = 10;

        private AudioClip _clip;
        private string    _deviceName;
        private int       _lastSamplePos;
        private bool      _isRecording;

        private Channel<byte[]> _acousticChannel;
        private Channel<byte[]> _sttChannel;

        public ChannelReader<byte[]> AcousticChannel => _acousticChannel.Reader;
        public ChannelReader<byte[]> SttChannel      => _sttChannel.Reader;

        public void StartRecording(CancellationToken ct)
        {
            // Her kayıtta kanalları taze oluştur — önceki Complete() sonrası
            // tekrar kullanılamayacağı için yeniden yaratmak gerekir
            _acousticChannel = Channel.CreateUnbounded<byte[]>();
            _sttChannel      = Channel.CreateUnbounded<byte[]>();

            _deviceName = Microphone.devices.Length > 0
                ? Microphone.devices[0] : null;

            if (_deviceName == null)
            {
                Debug.LogError("[AudioRecorder] Mikrofon bulunamadı.");
                return;
            }

            _clip          = Microphone.Start(_deviceName, loop: true, ClipSeconds, SampleRate);
            _lastSamplePos = 0;
            _isRecording   = true;

            Debug.Log($"[AudioRecorder] Kayıt başladı: {_deviceName}");
        }

        /// <summary>
        /// Ana thread'den her frame çağrılır (SessionController.Update).
        /// AudioClip.GetData burada çağrılır — thread güvenli.
        /// </summary>
        public void Tick()
        {
            if (!_isRecording || _clip == null) return;

            const int chunkSamples = 320;
            int currentPos = Microphone.GetPosition(_deviceName);

            int available = currentPos >= _lastSamplePos
                ? currentPos - _lastSamplePos
                : (SampleRate * ClipSeconds) - _lastSamplePos + currentPos;

            while (available >= chunkSamples)
            {
                float[] floatBuf = new float[chunkSamples];
                _clip.GetData(floatBuf, _lastSamplePos);

                byte[] pcm = FloatToPcm16(floatBuf);
                _acousticChannel.Writer.TryWrite(pcm);
                _sttChannel.Writer.TryWrite(pcm);

                _lastSamplePos = (_lastSamplePos + chunkSamples)
                    % (SampleRate * ClipSeconds);
                available -= chunkSamples;
            }
        }

        public void StopRecording()
        {
            _isRecording = false;

            _acousticChannel.Writer.Complete();
            _sttChannel.Writer.Complete();

            if (_deviceName != null)
                Microphone.End(_deviceName);

            Debug.Log("[AudioRecorder] Kayıt durduruldu.");
        }

        private static byte[] FloatToPcm16(float[] samples)
        {
            byte[] bytes = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                float clamped  = Mathf.Clamp(samples[i], -1f, 1f);
                short pcmValue = (short)(clamped * short.MaxValue);
                bytes[i * 2]     = (byte)(pcmValue & 0xFF);
                bytes[i * 2 + 1] = (byte)((pcmValue >> 8) & 0xFF);
            }
            return bytes;
        }
    }
}