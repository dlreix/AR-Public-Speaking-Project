using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using VRPublicSpeaking.AppShell.Presentation;

namespace VRPublicSpeaking.AppShell.PresentationQuestioning
{
    public static class PresentationQuestionGenerationService
    {
        private const string QuestionSetFileName = "question_set.json";

        public static IEnumerator GenerateQuestionSet(
            PresentationDeckReference deck,
            Action<bool, string, PresentationQuestionSet> completed)
        {
            if (deck == null || !deck.HasPages)
            {
                completed?.Invoke(false, "No presentation deck selected.", null);
                yield break;
            }

            PresentationSlideTextDocument slideText = PresentationTextExtractionService.LoadSlideText(deck);
            if (slideText == null || slideText.pages == null || slideText.pages.Count == 0)
            {
                completed?.Invoke(false, "No readable slide text found.", null);
                yield break;
            }

            OpenAiRuntimeConfig config = OpenAiRuntimeConfig.Load();
            if (config.TryGetConfigurationError(out string configError))
            {
                completed?.Invoke(false, configError, null);
                yield break;
            }

            string prompt = BuildQuestionPrompt(slideText, config.maxQuestions);
            bool requestCompleted = false;
            bool success = false;
            string message = string.Empty;
            PresentationQuestionSet generatedSet = null;

            yield return OpenAiResponsesClient.SendPrompt(
                prompt,
                1800,
                (ok, responseText) =>
                {
                    success = ok;
                    message = responseText;
                    requestCompleted = true;
                });

            if (!requestCompleted || !success)
            {
                if (TryLoadQuestionSet(deck, out PresentationQuestionSet cachedSet))
                {
                    deck.QuestionSetPath = ResolveQuestionSetPath(deck);
                    deck.QuestionStatus = "Generated (cached)";
                    completed?.Invoke(true, "Using cached question set.", cachedSet);
                    yield break;
                }

                completed?.Invoke(false, string.IsNullOrWhiteSpace(message) ? "Question generation failed." : message, null);
                yield break;
            }

            if (!TryParseQuestionSet(message, deck, slideText, config.maxQuestions, out generatedSet, out string parseError))
            {
                completed?.Invoke(false, parseError, null);
                yield break;
            }

            string questionSetPath = ResolveQuestionSetPath(deck);
            try
            {
                File.WriteAllText(questionSetPath, JsonUtility.ToJson(generatedSet, true));
                deck.QuestionSetPath = questionSetPath;
                deck.QuestionStatus = "Generated";
                completed?.Invoke(true, $"Generated {generatedSet.questions.Count} question(s).", generatedSet);
            }
            catch (Exception exception)
            {
                completed?.Invoke(false, $"Could not save question set. {exception.Message}", null);
            }
        }

        public static bool TryLoadQuestionSet(PresentationDeckReference deck, out PresentationQuestionSet questionSet)
        {
            questionSet = null;
            string path = !string.IsNullOrWhiteSpace(deck?.QuestionSetPath)
                ? deck.QuestionSetPath
                : ResolveQuestionSetPath(deck);

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                questionSet = JsonUtility.FromJson<PresentationQuestionSet>(File.ReadAllText(path));
                return questionSet != null && questionSet.HasQuestions;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PresentationQuestionGenerationService] Could not load question set: {exception.Message}");
                return false;
            }
        }

        public static string ResolveQuestionSetPath(PresentationDeckReference deck)
        {
            return string.IsNullOrWhiteSpace(deck?.ImportFolderPath)
                ? string.Empty
                : Path.Combine(deck.ImportFolderPath, QuestionSetFileName);
        }

        private static bool TryParseQuestionSet(
            string responseText,
            PresentationDeckReference deck,
            PresentationSlideTextDocument slideText,
            int maxQuestions,
            out PresentationQuestionSet questionSet,
            out string error)
        {
            questionSet = null;
            error = string.Empty;

            string json = OpenAiResponsesClient.ExtractJsonObject(responseText);
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Question generation did not return JSON.";
                return false;
            }

            QuestionGenerationPayload payload;
            try
            {
                payload = JsonUtility.FromJson<QuestionGenerationPayload>(json);
            }
            catch (Exception exception)
            {
                error = $"Question JSON could not be parsed. {exception.Message}";
                return false;
            }

            if (payload == null || payload.questions == null || payload.questions.Count == 0)
            {
                error = "Question generation returned no questions.";
                return false;
            }

            int questionCount = Mathf.Clamp(maxQuestions <= 0 ? 3 : maxQuestions, 1, 5);
            questionSet = new PresentationQuestionSet
            {
                deckId = deck.DeckId,
                sourceHash = deck.SourceHash,
                displayName = deck.DisplayName,
                language = ResolveLanguageFromResponse(json),
                createdUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            for (int index = 0; index < payload.questions.Count && questionSet.questions.Count < questionCount; index++)
            {
                PresentationQuestion question = payload.questions[index];
                if (question == null || string.IsNullOrWhiteSpace(question.question))
                {
                    continue;
                }

                question.id = string.IsNullOrWhiteSpace(question.id)
                    ? $"q{questionSet.questions.Count + 1:00}"
                    : question.id.Trim();
                question.slide = Mathf.Clamp(question.slide <= 0 ? 1 : question.slide, 1, Mathf.Max(1, slideText.pages.Count));
                question.audiencePersona = string.IsNullOrWhiteSpace(question.audiencePersona)
                    ? "Curious audience member"
                    : question.audiencePersona.Trim();
                question.question = question.question.Trim();
                questionSet.questions.Add(question);
            }

            if (questionSet.questions.Count == 0)
            {
                error = "Question generation returned only empty questions.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(questionSet.language))
            {
                questionSet.language = "auto";
            }

            return true;
        }

        private static string ResolveLanguageFromResponse(string json)
        {
            string language = ExtractStringProperty(json, "language");
            return string.IsNullOrWhiteSpace(language) ? "auto" : language.Trim();
        }

        private static string ExtractStringProperty(string json, string property)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(property))
            {
                return string.Empty;
            }

            string needle = $"\"{property}\"";
            int propertyIndex = json.IndexOf(needle, StringComparison.Ordinal);
            int colonIndex = propertyIndex >= 0 ? json.IndexOf(':', propertyIndex + needle.Length) : -1;
            int quoteIndex = colonIndex >= 0 ? json.IndexOf('"', colonIndex + 1) : -1;
            if (quoteIndex < 0)
            {
                return string.Empty;
            }

            int endIndex = json.IndexOf('"', quoteIndex + 1);
            return endIndex > quoteIndex ? json.Substring(quoteIndex + 1, endIndex - quoteIndex - 1) : string.Empty;
        }

        private static string BuildQuestionPrompt(PresentationSlideTextDocument document, int maxQuestions)
        {
            int questionCount = Mathf.Clamp(maxQuestions <= 0 ? 3 : maxQuestions, 1, 5);
            var builder = new StringBuilder();
            builder.AppendLine("You generate realistic audience questions for a public speaking practice app.");
            builder.AppendLine("Return only valid JSON. No markdown, no code fence.");
            builder.AppendLine("JSON schema:");
            builder.AppendLine("{\"language\":\"detected language or English\",\"questions\":[{\"id\":\"q01\",\"slide\":1,\"audiencePersona\":\"curious stakeholder\",\"question\":\"...\"}]}");
            builder.AppendLine($"Generate exactly {questionCount} questions, unless the slide text is too sparse.");
            builder.AppendLine("Questions should be asked after the presentation, should reference the slide content, and should invite the presenter to answer aloud.");
            builder.AppendLine("Keep every question short enough for a VR speech bubble: maximum 22 words or 160 characters.");
            builder.AppendLine("Do not generate expected answers, scoring rubrics, feedback, or any answer-evaluation fields.");
            builder.AppendLine("Use the slide language. If the language is unclear, use English.");
            builder.AppendLine();
            builder.AppendLine($"Deck: {document.displayName}");
            builder.AppendLine("Slides:");

            int budget = 9000;
            for (int index = 0; index < document.pages.Count && budget > 0; index++)
            {
                PresentationSlideTextPage page = document.pages[index];
                string text = Compact(page.text, Mathf.Min(1600, budget));
                budget -= text.Length;
                builder.AppendLine($"[Slide {page.pageNumber}]");
                builder.AppendLine(text);
                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static string Compact(string value, int maxLength)
        {
            string text = (value ?? string.Empty).Trim();
            if (text.Length <= maxLength)
            {
                return text;
            }

            return text.Substring(0, Mathf.Max(0, maxLength - 3)).TrimEnd() + "...";
        }
    }
}
