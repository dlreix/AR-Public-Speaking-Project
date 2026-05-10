using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRPublicSpeaking.AppShell.PresentationQuestioning
{
    [Serializable]
    public class PresentationSlideTextDocument
    {
        public string deckId;
        public string displayName;
        public string sourceExtension;
        public long createdUnixTime;
        public List<PresentationSlideTextPage> pages = new List<PresentationSlideTextPage>();
    }

    [Serializable]
    public class PresentationSlideTextPage
    {
        public int pageNumber;
        [TextArea] public string text;
    }

    [Serializable]
    public class PresentationQuestionSet
    {
        public string deckId;
        public string sourceHash;
        public string displayName;
        public string language;
        public long createdUnixTime;
        public List<PresentationQuestion> questions = new List<PresentationQuestion>();

        public bool HasQuestions => questions != null && questions.Count > 0;
    }

    [Serializable]
    public class PresentationQuestion
    {
        public string id;
        public int slide;
        public string audiencePersona;
        [TextArea] public string question;
        [TextArea] public string expectedAnswer;
        [TextArea] public string rubric;
    }

    [Serializable]
    public class PresentationQaResult
    {
        public string deckId;
        public string deckName;
        public string status;
        public string summary;
        public long completedUnixTime;
        public List<PresentationQaAnswer> answers = new List<PresentationQaAnswer>();

        public bool HasAnswers => answers != null && answers.Count > 0;
    }

    [Serializable]
    public class PresentationQaAnswer
    {
        public string questionId;
        public string question;
        public string expectedAnswer;
        public string answerTranscript;
        public bool skipped;
        public PresentationAnswerFeedback feedback;
    }

    [Serializable]
    public class PresentationAnswerFeedback
    {
        public float accuracy;
        public float coverage;
        public float clarity;
        public string summary;
        public string betterAnswer;
        public string status;
    }

    [Serializable]
    internal class QuestionGenerationPayload
    {
        public List<PresentationQuestion> questions = new List<PresentationQuestion>();
    }

    [Serializable]
    internal class AnswerEvaluationPayload
    {
        public float accuracy;
        public float coverage;
        public float clarity;
        public string summary;
        public string betterAnswer;
    }
}
