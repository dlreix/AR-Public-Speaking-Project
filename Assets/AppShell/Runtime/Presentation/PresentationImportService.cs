using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VRPublicSpeaking.AppShell.Presentation
{
    public static class PresentationImportService
    {
        private const string PresentationsFolderName = "Presentations";
        private const string ManifestFileName = "manifest.json";

        public static bool TrySelectAndImportPresentation(
            out PresentationDeckReference deck,
            out string statusMessage)
        {
            deck = null;
            statusMessage = string.Empty;

            string sourcePath = OpenPresentationFilePicker();
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                statusMessage = "Selection canceled.";
                return false;
            }

            return TryImportPresentation(sourcePath, out deck, out statusMessage);
        }

        public static bool TryImportPresentation(
            string sourcePath,
            out PresentationDeckReference deck,
            out string statusMessage)
        {
            deck = null;
            statusMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                statusMessage = "Presentation file could not be found.";
                return false;
            }

            string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (extension != ".pdf" && extension != ".pptx")
            {
                statusMessage = "Only PDF and PPTX files are supported.";
                return false;
            }

            string sourceHash = ComputeFileHash(sourcePath);
            string deckId = BuildDeckId(sourceHash);
            string displayName = Path.GetFileNameWithoutExtension(sourcePath);
            string importFolder = Path.Combine(
                Application.persistentDataPath,
                PresentationsFolderName,
                deckId);
            string importedFilePath = Path.Combine(importFolder, $"source{extension}");
            string manifestPath = Path.Combine(importFolder, ManifestFileName);

            try
            {
                if (Directory.Exists(importFolder))
                {
                    Directory.Delete(importFolder, true);
                }

                Directory.CreateDirectory(importFolder);
                File.Copy(sourcePath, importedFilePath, true);

                if (!PresentationConversionService.TryConvertToPageImages(
                        importedFilePath,
                        importFolder,
                        extension,
                        out int pageCount,
                        out string conversionError))
                {
                    statusMessage = conversionError;
                    TryDeleteImportFolder(importFolder);
                    return false;
                }

                deck = new PresentationDeckReference
                {
                    DeckId = deckId,
                    DisplayName = displayName,
                    SourceExtension = extension,
                    SourceHash = sourceHash,
                    SourceFilePath = sourcePath,
                    ImportedFilePath = importedFilePath,
                    ImportFolderPath = importFolder,
                    ManifestPath = manifestPath,
                    PageCount = pageCount,
                    IsReady = true,
                    ErrorMessage = string.Empty,
                    QuestionStatus = "Not generated"
                };

                if (PresentationQuestioning.PresentationTextExtractionService.TryExtractSlideText(
                        deck,
                        out string slideTextPath,
                        out string extractionMessage))
                {
                    deck.SlideTextPath = slideTextPath;
                    deck.QuestionStatus = PresentationQuestioning.OpenAiRuntimeConfig.HasUsableConfiguration()
                        ? "Ready to generate"
                        : "Missing API key";
                }
                else
                {
                    deck.QuestionStatus = extractionMessage;
                }

                WriteManifest(deck, sourceHash);
                statusMessage = $"Imported {displayName} ({pageCount} slide{(pageCount == 1 ? string.Empty : "s")}).";
                Debug.Log($"[PresentationImportService] {statusMessage} Folder: {importFolder}");
                return true;
            }
            catch (Exception exception)
            {
                statusMessage = $"Presentation import failed. {exception.Message}";
                TryDeleteImportFolder(importFolder);
                return false;
            }
        }

        private static void WriteManifest(PresentationDeckReference deck, string sourceHash)
        {
            string[] pages = new string[deck.PageCount];
            for (int index = 0; index < deck.PageCount; index++)
            {
                pages[index] = Path.GetFileName(deck.GetPageImagePath(index));
            }

            var manifest = new PresentationDeckManifest
            {
                deckId = deck.DeckId,
                displayName = deck.DisplayName,
                sourceExtension = deck.SourceExtension,
                sourceFileName = Path.GetFileName(deck.SourceFilePath),
                sourceHash = sourceHash,
                importedFileName = Path.GetFileName(deck.ImportedFilePath),
                pageCount = deck.PageCount,
                createdUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                pages = pages
            };

            File.WriteAllText(deck.ManifestPath, JsonUtility.ToJson(manifest, true));
        }

        private static string OpenPresentationFilePicker()
        {
#if UNITY_EDITOR
            return EditorUtility.OpenFilePanel("Select Presentation", string.Empty, string.Empty);
#elif UNITY_STANDALONE_WIN
            return OpenWindowsFileDialog();
#else
            return string.Empty;
#endif
        }

        private static string ComputeFileHash(string filePath)
        {
            using (FileStream stream = File.OpenRead(filePath))
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static string BuildDeckId(string sourceHash)
        {
            string hashPrefix = string.IsNullOrWhiteSpace(sourceHash)
                ? Guid.NewGuid().ToString("N").Substring(0, 10)
                : sourceHash.Substring(0, Math.Min(10, sourceHash.Length));
            return $"{DateTime.UtcNow:yyyyMMddHHmmss}_{hashPrefix}";
        }

        private static void TryDeleteImportFolder(string importFolder)
        {
            try
            {
                if (Directory.Exists(importFolder))
                {
                    Directory.Delete(importFolder, true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static string OpenWindowsFileDialog()
        {
            var ofn = new OpenFileName();
            ofn.structSize = Marshal.SizeOf(typeof(OpenFileName));
            ofn.filter = "Presentation Files\0*.pdf;*.pptx\0PDF Files\0*.pdf\0PowerPoint Files\0*.pptx\0All Files\0*.*\0\0";
            ofn.file = new string(new char[4096]);
            ofn.maxFile = ofn.file.Length;
            ofn.fileTitle = new string(new char[256]);
            ofn.maxFileTitle = ofn.fileTitle.Length;
            ofn.title = "Select Presentation";
            ofn.defExt = "pdf";
            ofn.flags = 0x00080000 | 0x00001000 | 0x00000800;

            if (!GetOpenFileName(ofn))
            {
                return string.Empty;
            }

            int nullIndex = ofn.file.IndexOf('\0');
            return nullIndex >= 0 ? ofn.file.Substring(0, nullIndex) : ofn.file;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private sealed class OpenFileName
        {
            public int structSize;
            public IntPtr dlgOwner = IntPtr.Zero;
            public IntPtr instance = IntPtr.Zero;
            public string filter;
            public string customFilter;
            public int maxCustFilter;
            public int filterIndex = 1;
            public string file;
            public int maxFile;
            public string fileTitle;
            public int maxFileTitle;
            public string initialDir;
            public string title;
            public int flags;
            public short fileOffset;
            public short fileExtension;
            public string defExt;
            public IntPtr custData = IntPtr.Zero;
            public IntPtr hook = IntPtr.Zero;
            public string templateName;
            public IntPtr reservedPtr = IntPtr.Zero;
            public int reservedInt;
            public int flagsEx;
        }

        [DllImport("Comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);
#endif
    }
}
