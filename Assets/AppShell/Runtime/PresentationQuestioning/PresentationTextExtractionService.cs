using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using VRPublicSpeaking.AppShell.Presentation;
using Debug = UnityEngine.Debug;

namespace VRPublicSpeaking.AppShell.PresentationQuestioning
{
    public static class PresentationTextExtractionService
    {
        private const string SlideTextFileName = "slide_text.json";
        private const int TimeoutMs = 60000;

        public static bool TryExtractSlideText(PresentationDeckReference deck, out string slideTextPath, out string message)
        {
            slideTextPath = string.Empty;
            message = string.Empty;

            if (deck == null || string.IsNullOrWhiteSpace(deck.ImportFolderPath))
            {
                message = "No presentation deck selected.";
                return false;
            }

            string pdfPath = ResolvePdfPath(deck);
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            {
                message = "No readable PDF source found for slide text.";
                return false;
            }

            string rawTextPath = Path.Combine(deck.ImportFolderPath, "slide_text_raw.txt");
            slideTextPath = Path.Combine(deck.ImportFolderPath, SlideTextFileName);
            string pdftotextPath = ResolvePdfToTextPath();
            string arguments = $"-layout -enc UTF-8 {Quote(pdfPath)} {Quote(rawTextPath)}";

            if (!RunProcess(pdftotextPath, arguments, deck.ImportFolderPath, TimeoutMs, out string processError))
            {
                message = $"Slide text extraction failed. {processError}";
                return false;
            }

            if (!File.Exists(rawTextPath))
            {
                message = "No readable slide text found.";
                return false;
            }

            string rawText = File.ReadAllText(rawTextPath);
            if (string.IsNullOrWhiteSpace(rawText))
            {
                message = "No readable slide text found.";
                return false;
            }

            var document = new PresentationSlideTextDocument
            {
                deckId = deck.DeckId,
                displayName = deck.DisplayName,
                sourceExtension = deck.SourceExtension,
                createdUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            string[] pages = rawText.Split('\f');
            for (int index = 0; index < pages.Length; index++)
            {
                string pageText = CleanText(pages[index]);
                if (string.IsNullOrWhiteSpace(pageText))
                {
                    continue;
                }

                document.pages.Add(new PresentationSlideTextPage
                {
                    pageNumber = index + 1,
                    text = pageText
                });
            }

            if (document.pages.Count == 0)
            {
                message = "No readable slide text found.";
                return false;
            }

            File.WriteAllText(slideTextPath, JsonUtility.ToJson(document, true));
            message = $"Extracted text from {document.pages.Count} slide(s).";
            Debug.Log($"[PresentationTextExtractionService] {message}");
            return true;
        }

        public static PresentationSlideTextDocument LoadSlideText(PresentationDeckReference deck)
        {
            string path = deck != null && !string.IsNullOrWhiteSpace(deck.SlideTextPath)
                ? deck.SlideTextPath
                : Path.Combine(deck?.ImportFolderPath ?? string.Empty, SlideTextFileName);

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            return JsonUtility.FromJson<PresentationSlideTextDocument>(File.ReadAllText(path));
        }

        private static string ResolvePdfPath(PresentationDeckReference deck)
        {
            if (deck.SourceExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(deck.ImportedFilePath))
            {
                return deck.ImportedFilePath;
            }

            string expected = Path.Combine(deck.ImportFolderPath, $"{Path.GetFileNameWithoutExtension(deck.ImportedFilePath)}.pdf");
            if (File.Exists(expected))
            {
                return expected;
            }

            string[] pdfs = Directory.GetFiles(deck.ImportFolderPath, "*.pdf", SearchOption.TopDirectoryOnly);
            return pdfs.Length > 0 ? pdfs[0] : string.Empty;
        }

        private static string ResolvePdfToTextPath()
        {
            string bundledPath = Path.Combine(
                Application.streamingAssetsPath,
                "PresentationConverters",
                "win-x64",
                "poppler",
                "bin",
                "pdftotext.exe");
            return File.Exists(bundledPath) ? bundledPath : "pdftotext.exe";
        }

        private static string CleanText(string text)
        {
            string normalized = Regex.Replace(text ?? string.Empty, "[ \t]+", " ");
            normalized = Regex.Replace(normalized, "\\n{3,}", "\n\n");
            return normalized.Trim();
        }

        private static bool RunProcess(string executable, string arguments, string workingDirectory, int timeoutMs, out string error)
        {
            error = string.Empty;
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        error = "Process could not be started.";
                        return false;
                    }

                    if (!process.WaitForExit(timeoutMs))
                    {
                        process.Kill();
                        error = "pdftotext timed out.";
                        return false;
                    }

                    error = process.StandardError.ReadToEnd();
                    return process.ExitCode == 0;
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static string Quote(string value)
        {
            return $"\"{value}\"";
        }
    }
}
