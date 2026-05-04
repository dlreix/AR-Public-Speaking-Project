using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum StressLevel { Easy, Medium, Hard }

public enum AudienceState
{
    Idle,
    Attentive,
    Neutral,
    Distracted,
    Bored,
    Applauding,
    Nodding,
    Stretching,
    NoteTaking,
    ChinResting
}

[System.Serializable]
public class StressScenario
{
    public StressLevel level;
    public int audienceSize;
    public float negativeReactionMultiplier;
    public float positiveReactionMultiplier;
    [TextArea] public string description;
}

public class AudienceBehaviorController : MonoBehaviour
{
    [Header("Scenario Settings")]
    public StressLevel currentStressLevel = StressLevel.Medium;
    public List<StressScenario> scenarios = new List<StressScenario>();

    [Header("Core Engines")]
    public AudienceReactionEngine reactionEngine;

    [Header("Audience Members")]
    public List<AudienceMember> audienceMembers = new List<AudienceMember>();

    private StressScenario _activeScenario;

    [Header("Live State")]
    public bool sessionEnded = false;

    void Start()
    {
        ApplyScenario(currentStressLevel);
        foreach (var member in audienceMembers)
            if (member != null) member.SetState(AudienceState.Neutral);
    }

    void Update()
    {
        if (_activeScenario == null || _activeScenario.level != currentStressLevel)
            ApplyScenario(currentStressLevel);

        if (sessionEnded)
        {
            foreach (var member in audienceMembers)
                if (member != null) member.SetState(AudienceState.Applauding);
            return;
        }

        EvaluateAndSetStatePerMember();
    }

    private void EvaluateAndSetStatePerMember()
    {
        if (reactionEngine == null || reactionEngine.scoringEngine == null)
            return; // Wait for engines

        // Get the frame from Engine
        ReactionFrame frame = reactionEngine.currentReaction;
        float negMult = _activeScenario != null ? _activeScenario.negativeReactionMultiplier : 1f;
        float finalScore = reactionEngine.scoringEngine.GetFinalScore();

        foreach (var member in audienceMembers)
        {
            if (member == null) continue;

            // ---- BİREYSEL SKOR HESABI ----
            // Her öğrencinin kişilik toleransı, genel skoru kendi perspektifinden kaydırır.
            float personalScore = finalScore 
                + (member.personalEyeContactTolerance * 40f)
                + (member.personalWpmTolerance * 0.3f);
            personalScore = Mathf.Clamp(personalScore, 0f, 100f);

            // Stress seviyesine göre negatif tepkileri güçlendir
            personalScore = Mathf.Lerp(personalScore, personalScore * (2f - negMult), 0.5f);

            AudienceState targetState;

            // ---- BİREYSEL STATE SEÇİMİ YENİ (3 BAND - SCORE SCALING) ----
            if (personalScore < 35f)
            {
                // CRITICAL LOW SCORE (< 35)
                if (member.CurrentState == AudienceState.Stretching || member.CurrentState == AudienceState.Distracted)
                {
                    targetState = member.CurrentState;
                }
                else
                {
                    float r = Random.value;
                    if (r > 0.6f) targetState = AudienceState.Stretching; 
                    else targetState = AudienceState.Distracted;
                }

                if (member.proceduralAnimator != null)
                    member.proceduralAnimator.externalBoredomLevel = 1.0f;
            }
            else if (personalScore < 55f)
            {
                // LOW/NEUTRAL SCORE (35 - 55 Arası)
                // Daha fazla tepki çeşitliliği
                if (member.CurrentState == AudienceState.Distracted || 
                    member.CurrentState == AudienceState.Stretching || 
                    member.CurrentState == AudienceState.NoteTaking || 
                    member.CurrentState == AudienceState.Neutral)
                {
                    targetState = member.CurrentState;
                }
                else
                {
                    float rLow = Random.value;
                    if (rLow > 0.7f) targetState = AudienceState.Distracted; // Telefon/Tablet
                    else if (rLow > 0.45f) targetState = AudienceState.Stretching; // Esneme
                    else if (rLow > 0.25f) targetState = AudienceState.NoteTaking; // Not Tutma
                    else targetState = AudienceState.Neutral; // Sakin bekleme
                }
                
                if (member.proceduralAnimator != null)
                    member.proceduralAnimator.externalBoredomLevel = Mathf.InverseLerp(55f, 35f, personalScore);
            }
            else if (personalScore <= 75f)
            {
                // NORMAL/GOOD SCORE (55 - 75 Arası)
                targetState = AudienceState.Neutral;
                
                if (member.proceduralAnimator != null)
                    member.proceduralAnimator.externalBoredomLevel = 0f;
            }
            else
            {
                // HIGH SCORE (> 75)
                bool hasGoodEyeContact = frame != null && frame.dominant_factors != null && frame.dominant_factors.Contains("good_eye_contact");

                if (member.CurrentState == AudienceState.Nodding ||
                    member.CurrentState == AudienceState.NoteTaking ||
                    member.CurrentState == AudienceState.Attentive)
                {
                    targetState = member.CurrentState;
                    // Force Nodding if eye contact is exceptionally good
                    if (hasGoodEyeContact && targetState != AudienceState.Nodding)
                    {
                        targetState = AudienceState.Nodding;
                    }
                }
                else
                {
                    float r = Random.value;
                    if (r > 0.6f || hasGoodEyeContact)
                        targetState = AudienceState.Nodding;
                    else if (r > 0.3f)
                        targetState = AudienceState.NoteTaking;
                    else
                        targetState = AudienceState.Attentive;
                }
                
                if (member.proceduralAnimator != null)
                    member.proceduralAnimator.externalBoredomLevel = 0f;
            }

            if (targetState == AudienceState.Stretching)
            {
                Debug.Log($"[Behavior] Member {member.name} - Score: {personalScore:F1} - Target State: {targetState}");
            }

            member.SetState(targetState);
        }
    }

    public void ApplyScenario(StressLevel level)
    {
        _activeScenario = scenarios.Find(s => s.level == level);
        if (_activeScenario == null)
        {
            _activeScenario = CreateDefaultScenario(level);
        }
        currentStressLevel = level;
    }

    private StressScenario CreateDefaultScenario(StressLevel level)
    {
        StressScenario scenario = new StressScenario();
        scenario.level = level;

        switch (level)
        {
            case StressLevel.Easy:
                scenario.audienceSize = 20;
                scenario.negativeReactionMultiplier = 0.85f;
                scenario.positiveReactionMultiplier = 1.1f;
                scenario.description = "Default easy scenario";
                break;
            case StressLevel.Hard:
                scenario.audienceSize = 50;
                scenario.negativeReactionMultiplier = 1.2f;
                scenario.positiveReactionMultiplier = 0.9f;
                scenario.description = "Default hard scenario";
                break;
            case StressLevel.Medium:
            default:
                scenario.audienceSize = 35;
                scenario.negativeReactionMultiplier = 1f;
                scenario.positiveReactionMultiplier = 1f;
                scenario.description = "Default medium scenario";
                break;
        }

        return scenario;
    }

    public void TriggerSessionEnd() => sessionEnded = true;
    public void ChangeStressLevel(StressLevel level) => ApplyScenario(level);
}
