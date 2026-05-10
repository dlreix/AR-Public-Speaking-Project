using System;
using System.Collections;
using System.Text;
using UnityEngine;

namespace VRPublicSpeaking.AppShell.PresentationQuestioning
{
    public static class PresentationAnswerEvaluationService
    {
        public static IEnumerator EvaluateAnswer(
            PresentationQuestion question,
            string answerTranscript,
            Action<PresentationAnswerFeedback> completed)
        {
            if (question == null)
            {
                completed?.Invoke(BuildUnavailableFeedback("Missing question context."));
                yield break;
            }

            if (string.IsNullOrWhiteSpace(answerTranscript))
            {
                completed?.Invoke(new PresentationAnswerFeedback
                {
                    accuracy = 0f,
                    coverage = 0f,
                    clarity = 0f,
                    summary = "No answer was captured.",
                    betterAnswer = question.expectedAnswer ?? string.Empty,
                    status = "Skipped"
                });
                yield break;
            }

            if (!OpenAiRuntimeConfig.HasUsableConfiguration())
            {
                completed?.Invoke(BuildUnavailableFeedback("Evaluation unavailable: missing API key."));
                yield break;
            }

            bool requestCompleted = false;
            bool success = false;
            string responseText = string.Empty;

            yield return OpenAiResponsesClient.SendPrompt(
                BuildEvaluationPrompt(question, answerTranscript),
                1200,
                (ok, text) =>
                {
                    success = ok;
                    responseText = text;
                    requestCompleted = true;
                });

            if (!requestCompleted || !success)
            {
                completed?.Invoke(BuildUnavailableFeedback(
                    string.IsNullOrWhiteSpace(responseText) ? "Evaluation unavailable." : responseText));
                yield break;
            }

            if (!TryParseFeedback(responseText, out PresentationAnswerFeedback feedback))
            {
                completed?.Invoke(BuildUnavailableFeedback("Evaluation response could not be parsed."));
                yield break;
            }

            feedback.status = string.IsNullOrWhiteSpace(feedback.status) ? "Evaluated" : feedback.status;
            feedback.accuracy = Mathf.Clamp(feedback.accuracy, 0f, 100f);
            feedback.coverage = Mathf.Clamp(feedback.coverage, 0f, 100f);
            feedback.clarity = Mathf.Clamp(feedback.clarity, 0f, 100f);
            completed?.Invoke(feedback);
        }

        private static bool TryParseFeedback(string responseText, out PresentationAnswerFeedback feedback)
        {
            feedback = null;
            string json = OpenAiResponsesClient.ExtractJsonObject(responseText);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                AnswerEvaluationPayload payload = JsonUtility.FromJson<AnswerEvaluationPayload>(json);
                if (payload == null)
                {
                    return false;
                }

                feedback = new PresentationAnswerFeedback
                {
                    accuracy = payload.accuracy,
                    coverage = payload.coverage,
                    clarity = payload.clarity,
                    summary = payload.summary,
                    betterAnswer = payload.betterAnswer,
                    status = "Evaluated"
                };
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PresentationAnswerEvaluationService] Could not parse answer evaluation: {exception.Message}");
                return false;
            }
        }

        private static PresentationAnswerFeedback BuildUnavailableFeedback(string message)
        {
            return new PresentationAnswerFeedback
            {
                accuracy = 0f,
                coverage = 0f,
                clarity = 0f,
                summary = string.IsNullOrWhiteSpace(message) ? "Evaluation unavailable." : message,
                betterAnswer = string.Empty,
                status = "Evaluation unavailable"
            };
        }

        private static string BuildEvaluationPrompt(PresentationQuestion question, string answerTranscript)
        {
            var builder = new StringBuilder();
            builder.AppendLine("You evaluate a presenter's answer to an audience question.");
            builder.AppendLine("Return only valid JSON. No markdown, no code fence.");
            builder.AppendLine("JSON schema:");
            builder.AppendLine("{\"accuracy\":0-100,\"coverage\":0-100,\"clarity\":0-100,\"summary\":\"one or two concise sentences\",\"betterAnswer\":\"a stronger sample answer\"}");
            builder.AppendLine();
            builder.AppendLine($"Audience question: {question.question}");
            builder.AppendLine($"Expected answer: {question.expectedAnswer}");
            builder.AppendLine($"Rubric: {question.rubric}");
            builder.AppendLine($"Presenter answer transcript: {answerTranscript}");
            return builder.ToString();
        }
    }
}
