using System;

namespace VRPublicSpeaking.AppShell.Data
{
    [Serializable]
    public enum PracticeMode
    {
        GuidedPractice = 0,
        FreePractice = 1,
        EvaluationMode = 2,
        ChallengeMode = 3
    }

    [Serializable]
    public enum SessionDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
        Expert = 3
    }

    [Serializable]
    public enum AudiencePreset
    {
        Supportive = 0,
        Neutral = 1,
        Distracted = 2,
        Challenging = 3
    }

    [Serializable]
    public enum FeedbackLevel
    {
        Minimal = 0,
        Standard = 1,
        Detailed = 2
    }

    [Serializable]
    public enum AppPanelType
    {
        Login = 0,
        Home = 1,
        PracticeMode = 2,
        EnvironmentSelection = 3,
        SessionSetup = 4,
        Ready = 5,
        ResultsSummary = 6,
        Progress = 7,
        Settings = 8,
        PauseOverlay = 9,
        Dashboard = 10,
        AudienceQa = 11
    }
}
