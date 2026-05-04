using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ReactionFrame
{
    public string performance_level; // LOW, MEDIUM, HIGH
    public AudienceState overall_audience_state; // General state
    public List<string> dominant_factors;
    public List<string> reactions;
    [Range(0f, 100f)]
    public float engagement_level;
    public string real_time_adjustment;
}

public enum EnvironmentType { meeting_room, conference_hall, classroom }

public class AudienceReactionEngine : MonoBehaviour
{
    public PerformanceScoringEngine scoringEngine;
    public EnvironmentType environmentType = EnvironmentType.classroom;

    public ReactionFrame currentReaction = new ReactionFrame();

    void Update()
    {
        if (scoringEngine != null)
        {
            GenerateReactionFrame();
        }
    }

    private float smoothedScore = 50f;
    [SerializeField] private float smoothingSpeed = 1.5f;

    public void GenerateReactionFrame()
    {
        float rawScore = scoringEngine.GetFinalScore();
        smoothedScore = Mathf.Lerp(smoothedScore, rawScore, Time.deltaTime * smoothingSpeed);
        
        string performanceLevel = "MEDIUM";
        if (smoothedScore < 38f) performanceLevel = "LOW";
        else if (smoothedScore < 65f) performanceLevel = "MEDIUM";
        else performanceLevel = "HIGH";

        currentReaction.performance_level = performanceLevel;
        currentReaction.dominant_factors = new List<string>();
        currentReaction.reactions = new List<string>();

        // Analyze Speech
        if (scoringEngine.speechMetrics.wpm < scoringEngine.idealWpmMin)
            currentReaction.dominant_factors.Add("wpm_too_slow"); // Sıkıcı
        else if (scoringEngine.speechMetrics.wpm > scoringEngine.idealWpmMax)
            currentReaction.dominant_factors.Add("wpm_too_fast");

        if (scoringEngine.speechMetrics.fillerWordsPerMinute > 5f)
            currentReaction.dominant_factors.Add("high_filler_words"); // Güven kaybı

        if (scoringEngine.speechMetrics.toneVariationScore < 40f)
            currentReaction.dominant_factors.Add("monotone_voice"); // Monotonluk

        // Analyze Eye Contact
        if (scoringEngine.eyeMetrics.eyeContactRatio < 0.4f)
            currentReaction.dominant_factors.Add("eye_contact_low"); // Kopukluk
        else if (scoringEngine.eyeMetrics.eyeContactRatio > 0.7f)
            currentReaction.dominant_factors.Add("good_eye_contact"); // Bağ kurulmuş

        // Analyze Posture
        if (scoringEngine.postureMetrics.slouchEventsPerMinute > 2f)
            currentReaction.dominant_factors.Add("bad_posture_slouch"); // Özgüven düşer
        if (scoringEngine.postureMetrics.swayDurationPercent > 15f)
            currentReaction.dominant_factors.Add("bad_posture_sway"); // Huzursuzluk

        // Determine Audience State and Enagagement Level
        if (performanceLevel == "LOW")
        {
            currentReaction.engagement_level = Random.Range(10f, 35f);
            
            if (currentReaction.dominant_factors.Contains("eye_contact_low") || currentReaction.dominant_factors.Contains("monotone_voice"))
            {
                currentReaction.overall_audience_state = AudienceState.Distracted;
                currentReaction.real_time_adjustment = "Audience fully disconnected due to low eye contact/monotone voice.";
                currentReaction.reactions.Add("phone_usage");
            }
            else
            {
                currentReaction.overall_audience_state = AudienceState.Bored;
                currentReaction.real_time_adjustment = "General loss of interest, weak performance vibes.";
            }

            // Environment specifics
            if (environmentType == EnvironmentType.classroom)
                currentReaction.reactions.Add("talking_amongst_themselves");
            else if (environmentType == EnvironmentType.conference_hall)
                currentReaction.reactions.Add("murmuring");
            else if (environmentType == EnvironmentType.meeting_room)
                currentReaction.reactions.Add("silent_disinterest");
        }
        else if (performanceLevel == "MEDIUM")
        {
            currentReaction.engagement_level = Random.Range(45f, 75f);
            currentReaction.overall_audience_state = AudienceState.Neutral;
            currentReaction.real_time_adjustment = "Selective attention. Mixed engagement levels.";

            if (currentReaction.dominant_factors.Contains("high_filler_words"))
            {
                currentReaction.real_time_adjustment += " Filler words causing mild annoyance.";
            }
            if (currentReaction.dominant_factors.Contains("good_eye_contact"))
            {
                currentReaction.overall_audience_state = AudienceState.Attentive;
                currentReaction.reactions.Add("nodding"); // Baş sallama
            }
            if (currentReaction.dominant_factors.Contains("bad_posture_slouch"))
            {
                currentReaction.real_time_adjustment += " Poor posture reducing speaker authority.";
            }
        }
        else // HIGH
        {
            currentReaction.engagement_level = Random.Range(80f, 100f);
            currentReaction.overall_audience_state = AudienceState.Attentive;
            currentReaction.real_time_adjustment = "Audience is highly engaged.";

            if (environmentType == EnvironmentType.classroom)
                currentReaction.reactions.Add("taking_notes");
            else if (environmentType == EnvironmentType.conference_hall)
                currentReaction.reactions.Add("applause_and_laughs"); // Alkış/Gülme
            else if (environmentType == EnvironmentType.meeting_room)
                currentReaction.reactions.Add("professional_approval");

            if (currentReaction.dominant_factors.Contains("good_eye_contact"))
                currentReaction.real_time_adjustment += " Strong connection established via eye contact.";
        }
    }
}
