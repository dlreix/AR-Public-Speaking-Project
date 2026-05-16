using UnityEngine;
using VRPublicSpeaking.AppShell.Core;
using VRPublicSpeaking.AppShell.Data;

namespace VRPublicSpeaking.AppShell.Integration
{
    /// <summary>
    /// Bu adaptör, App Shell'in ayarlari (SessionConfig) ile 
    /// Arda'nin Audience Simulation (AudienceBehaviorController) motorunu birlestirir.
    /// Environment (Ortam) sahnelerinde GameManager, AudienceParent veya sahne yöneticisi ustune konulabilir.
    /// </summary>
    public class AudienceIntegrationAdapter : MonoBehaviour
    {
        [Header("Arda'nin Sistemi")]
        [Tooltip("Sahnedeki AudienceBehaviorController (Arda'nin yoneticisi)")]
        public AudienceBehaviorController behaviorController;

        [Header("Bizim Sistem (Scoring)")]
        [Tooltip("Anlik skor hesabi yapan motor (Gaze, vs) - Eger bos birakilirsa otomatik bulunur.")]
        public PerformanceScoringEngine scoringEngine;

        void Start()
        {
            // Eger Inspector uzerinden verilmediyse sahnede otomatik olarak bul.
            if (behaviorController == null)
            {
                behaviorController = Object.FindFirstObjectByType<AudienceBehaviorController>();
            }

            if (scoringEngine == null)
            {
                scoringEngine = Object.FindFirstObjectByType<PerformanceScoringEngine>();
            }

            // 1. App Shell'den ayarlari cek ve zorluk/preset ayarini Arda'nin motoruna aktar
            if (AppRuntimeState.HasInstance && behaviorController != null)
            {
                SessionConfig config = AppRuntimeState.Instance.CurrentSessionConfig;
                
                StressLevel stressLevel = ConvertDifficultyToStress(config);
                
                behaviorController.ApplyScenario(stressLevel);
                ApplyAudiencePresetTuning(config.AudiencePreset);
                Debug.Log($"[AudienceIntegrationAdapter] Arda'nin seyirci sistemi '{stressLevel}' (Difficulty: {config.DifficultyLevel}, Preset: {config.AudiencePreset}) modunda baslatildi.");
            }

            // 2. PerformanceScoringEngine'i bulup, Arda'nin Reaction Engine'ine bagla
            if (scoringEngine != null && behaviorController != null && behaviorController.reactionEngine != null)
            {
                behaviorController.reactionEngine.scoringEngine = scoringEngine;
                Debug.Log("[AudienceIntegrationAdapter] ScoringEngine basariyla AudienceReactionEngine'e baglandi. Seyirci tepkileri senin goz temasina gore calisacak.");
            }
            else
            {
                Debug.LogWarning("[AudienceIntegrationAdapter] ScoringEngine veya ReactionEngine bulunamadigi icin performans baglantisi kurulamadi.");
            }

            // 3. MainController'ın oturum bitiş event'ine abone ol (Alkışlatmak için)
            MainController mainController = Object.FindFirstObjectByType<MainController>();
            if (mainController != null)
            {
                mainController.SessionEnded += (duration, score) => TriggerSessionEnd();
                Debug.Log("[AudienceIntegrationAdapter] MainController.SessionEnded event'ine abone olundu.");
            }
        }

        private StressLevel ConvertDifficultyToStress(SessionConfig config)
        {
            if (config.DifficultyLevel == SessionDifficulty.Easy)
                return StressLevel.Easy;
            else if (config.DifficultyLevel == SessionDifficulty.Normal)
                return StressLevel.Medium;
            else if (config.DifficultyLevel == SessionDifficulty.Hard || config.DifficultyLevel == SessionDifficulty.Expert)
                return StressLevel.Hard;

            return StressLevel.Medium;
        }

        private void ApplyAudiencePresetTuning(AudiencePreset preset)
        {
            if (behaviorController == null)
            {
                return;
            }

            switch (preset)
            {
                case AudiencePreset.Supportive:
                    behaviorController.ConfigureAudienceTuning(8f, 0.75f, 1.25f);
                    break;
                case AudiencePreset.Challenging:
                    behaviorController.ConfigureAudienceTuning(-8f, 1.25f, 0.85f);
                    break;
                case AudiencePreset.Neutral:
                default:
                    behaviorController.ConfigureAudienceTuning(0f, 1f, 1f);
                    break;
            }
        }

        // --- PAUSE & RESUME ---
        // (Opsiyonel) Shell'in Pause Menu'sune kolayca hooklanabilir.
        
        public void PauseAudience()
        {
            if (behaviorController != null)
            {
                // İzleyicileri dondurabilir veya idle hale getirebiliriz
                // Time.timeScale = 0 yapildiginda animatörler zaten duracaktir.
                Debug.Log("[AudienceIntegrationAdapter] Seyirci simulasyonu duraklatildi.");
            }
        }

        public void ResumeAudience()
        {
            if (behaviorController != null)
            {
                Debug.Log("[AudienceIntegrationAdapter] Seyirci simulasyonu devam ediyor.");
            }
        }

        // --- SESSION END ---
        // Oturum bittiginde alkislama veya sonuc durumuna gecis
        public void TriggerSessionEnd()
        {
            if (behaviorController != null)
            {
                behaviorController.TriggerSessionEnd();
                Debug.Log("[AudienceIntegrationAdapter] Oturum bitti! Seyirciler alkisliyor.");
            }
        }
    }
}
