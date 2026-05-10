using System.Text;
using UnityEngine;
using SpeechPipeline;

namespace VRPublicSpeaking.AppShell.PresentationQuestioning
{
    public class PresentationAnswerRecorder : MonoBehaviour
    {
        [SerializeField] private SpeechPipelineController speechPipelineController;

        private readonly StringBuilder transcriptBuilder = new StringBuilder();
        private bool previousScoringUpdate;
        private bool subscribed;

        public bool IsRecording { get; private set; }
        public bool UsedSpeechInput { get; private set; }
        public float LastTranscriptTime { get; private set; }

        public string CurrentTranscript => transcriptBuilder.ToString().Trim();
        public bool HasTranscript => transcriptBuilder.Length > 0;

        public void Configure(SpeechPipelineController controller)
        {
            speechPipelineController = controller;
        }

        public bool BeginRecording()
        {
            transcriptBuilder.Length = 0;
            LastTranscriptTime = Time.realtimeSinceStartup;
            UsedSpeechInput = false;

            if (speechPipelineController == null)
            {
                return false;
            }

            previousScoringUpdate = speechPipelineController.UpdateScoringOnSessionEnd;
            speechPipelineController.UpdateScoringOnSessionEnd = false;
            speechPipelineController.FinalTranscriptReceived += HandleFinalTranscript;
            subscribed = true;

            bool started = speechPipelineController.BeginRecordingFromShell();
            IsRecording = started || speechPipelineController.IsRecording;
            UsedSpeechInput = IsRecording;
            if (!IsRecording)
            {
                speechPipelineController.EndRecordingFromShell();
                RestoreSpeechPipeline();
            }

            return IsRecording;
        }

        public string EndRecording()
        {
            if (speechPipelineController != null && speechPipelineController.IsRecording)
            {
                speechPipelineController.EndRecordingFromShell();
            }

            RestoreSpeechPipeline();
            IsRecording = false;
            return CurrentTranscript;
        }

        public void CancelRecording()
        {
            EndRecording();
            transcriptBuilder.Length = 0;
        }

        private void HandleFinalTranscript(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (transcriptBuilder.Length > 0)
            {
                transcriptBuilder.Append(' ');
            }

            transcriptBuilder.Append(text.Trim());
            LastTranscriptTime = Time.realtimeSinceStartup;
        }

        private void RestoreSpeechPipeline()
        {
            if (speechPipelineController == null)
            {
                return;
            }

            if (subscribed)
            {
                speechPipelineController.FinalTranscriptReceived -= HandleFinalTranscript;
                subscribed = false;
            }

            speechPipelineController.UpdateScoringOnSessionEnd = previousScoringUpdate;
        }

        private void OnDestroy()
        {
            RestoreSpeechPipeline();
        }
    }
}
