using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace VRPublicSpeaking.AppShell.Presentation
{
    public static class PresentationConversionService
    {
        private const int ConversionTimeoutMs = 180000;
        private const int RenderTimeoutMs = 180000;
        private const string OutputPrefix = "page";
        private const string PageFilePattern = "page*.png";

        public static bool TryConvertToPageImages(
            string sourcePath,
            string outputFolder,
            string sourceExtension,
            out int pageCount,
            out string errorMessage)
        {
            pageCount = 0;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                errorMessage = "Presentation file could not be found.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                errorMessage = "Presentation output folder is not configured.";
                return false;
            }

            if (!IsWindowsRuntime())
            {
                errorMessage = "Presentation conversion is supported only on Windows in V1.";
                return false;
            }

            Directory.CreateDirectory(outputFolder);
            ClearGeneratedImages(outputFolder);

            string extension = NormalizeExtension(sourceExtension);
            if (extension == ".pdf")
            {
                return TryRenderPdf(sourcePath, outputFolder, out pageCount, out errorMessage);
            }

            if (extension == ".pptx")
            {
                return TryConvertPptx(sourcePath, outputFolder, out pageCount, out errorMessage);
            }

            errorMessage = "Only PDF and PPTX presentations are supported.";
            return false;
        }

        private static bool TryConvertPptx(
            string sourcePath,
            string outputFolder,
            out int pageCount,
            out string errorMessage)
        {
            pageCount = 0;
            errorMessage = string.Empty;

            string sofficePath = ResolveLibreOfficePath();
            string profileUri = ToFileUri(Path.Combine(outputFolder, "lo-profile"));
            string arguments =
                "--headless --invisible --norestore --nofirststartwizard --nolockcheck " +
                $"{Quote($"-env:UserInstallation={profileUri}")} " +
                $"--convert-to pdf --outdir {Quote(outputFolder)} {Quote(sourcePath)}";

            if (!RunProcess(
                    sofficePath,
                    arguments,
                    outputFolder,
                    ConversionTimeoutMs,
                    out string standardOutput,
                    out string standardError,
                    out string processError))
            {
                errorMessage = BuildToolError(
                    "LibreOffice conversion failed. PPTX support requires bundled LibreOffice or soffice.exe on PATH.",
                    processError,
                    standardOutput,
                    standardError);
                return false;
            }

            string generatedPdfPath = ResolveConvertedPdfPath(sourcePath, outputFolder);
            if (string.IsNullOrWhiteSpace(generatedPdfPath) || !File.Exists(generatedPdfPath))
            {
                errorMessage = BuildToolError(
                    "LibreOffice did not produce a PDF for this PPTX.",
                    string.Empty,
                    standardOutput,
                    standardError);
                return false;
            }

            return TryRenderPdf(generatedPdfPath, outputFolder, out pageCount, out errorMessage);
        }

        private static bool TryRenderPdf(
            string pdfPath,
            string outputFolder,
            out int pageCount,
            out string errorMessage)
        {
            pageCount = 0;
            errorMessage = string.Empty;

            string pdftoppmPath = ResolvePdfToPngPath();
            string outputPrefix = Path.Combine(outputFolder, OutputPrefix);
            string scaledArguments = $"-png -scale-to 2048 {Quote(pdfPath)} {Quote(outputPrefix)}";

            if (!RunProcess(
                    pdftoppmPath,
                    scaledArguments,
                    outputFolder,
                    RenderTimeoutMs,
                    out string standardOutput,
                    out string standardError,
                    out string processError))
            {
                ClearGeneratedImages(outputFolder);
                string fallbackArguments = $"-png {Quote(pdfPath)} {Quote(outputPrefix)}";
                if (!RunProcess(
                        pdftoppmPath,
                        fallbackArguments,
                        outputFolder,
                        RenderTimeoutMs,
                        out standardOutput,
                        out standardError,
                        out processError))
                {
                    errorMessage = BuildToolError(
                        "PDF rendering failed. PDF support requires bundled Poppler pdftoppm.exe or pdftoppm.exe on PATH.",
                        processError,
                        standardOutput,
                        standardError);
                    return false;
                }
            }

            pageCount = NormalizeGeneratedPageNames(outputFolder);
            if (pageCount <= 0)
            {
                errorMessage = BuildToolError(
                    "The converter finished but no slide images were generated.",
                    string.Empty,
                    standardOutput,
                    standardError);
                return false;
            }

            Debug.Log($"[PresentationConversionService] Rendered {pageCount} page(s) from {Path.GetFileName(pdfPath)}.");
            return true;
        }

        private static string ResolveConvertedPdfPath(string sourcePath, string outputFolder)
        {
            string expectedPath = Path.Combine(
                outputFolder,
                $"{Path.GetFileNameWithoutExtension(sourcePath)}.pdf");
            if (File.Exists(expectedPath))
            {
                return expectedPath;
            }

            return Directory
                .GetFiles(outputFolder, "*.pdf", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        private static int NormalizeGeneratedPageNames(string outputFolder)
        {
            List<string> generatedFiles = Directory
                .GetFiles(outputFolder, PageFilePattern, SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetFileName(path).EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .OrderBy(ExtractPageNumber)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int outputIndex = 0;
            for (int index = 0; index < generatedFiles.Count; index++)
            {
                string currentPath = generatedFiles[index];
                string fileName = Path.GetFileName(currentPath);
                if (fileName.StartsWith("page_", StringComparison.OrdinalIgnoreCase))
                {
                    outputIndex++;
                    continue;
                }

                outputIndex++;
                string targetPath = Path.Combine(outputFolder, $"page_{outputIndex:0000}.png");
                if (Path.GetFullPath(currentPath).Equals(Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }

                File.Move(currentPath, targetPath);
            }

            return Directory.GetFiles(outputFolder, "page_*.png", SearchOption.TopDirectoryOnly).Length;
        }

        private static int ExtractPageNumber(string path)
        {
            string fileName = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
            int separatorIndex = fileName.LastIndexOfAny(new[] { '-', '_' });
            if (separatorIndex >= 0 && separatorIndex < fileName.Length - 1)
            {
                string suffix = fileName.Substring(separatorIndex + 1);
                if (int.TryParse(suffix, out int pageNumber))
                {
                    return pageNumber;
                }
            }

            return int.MaxValue;
        }

        private static void ClearGeneratedImages(string outputFolder)
        {
            if (!Directory.Exists(outputFolder))
            {
                return;
            }

            string[] generatedFiles = Directory.GetFiles(outputFolder, PageFilePattern, SearchOption.TopDirectoryOnly);
            for (int index = 0; index < generatedFiles.Length; index++)
            {
                File.Delete(generatedFiles[index]);
            }
        }

        private static bool RunProcess(
            string executablePath,
            string arguments,
            string workingDirectory,
            int timeoutMs,
            out string standardOutput,
            out string standardError,
            out string processError)
        {
            standardOutput = string.Empty;
            standardError = string.Empty;
            processError = string.Empty;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        processError = "Process could not be started.";
                        return false;
                    }

                    bool exited = process.WaitForExit(timeoutMs);
                    if (!exited)
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (InvalidOperationException)
                        {
                        }

                        processError = "Converter timed out.";
                        return false;
                    }

                    standardOutput = process.StandardOutput.ReadToEnd();
                    standardError = process.StandardError.ReadToEnd();
                    return process.ExitCode == 0;
                }
            }
            catch (Win32Exception exception)
            {
                processError = exception.Message;
                return false;
            }
            catch (Exception exception)
            {
                processError = exception.Message;
                return false;
            }
        }

        private static string ResolvePdfToPngPath()
        {
            return ResolveToolPath(
                "pdftoppm.exe",
                Path.Combine("poppler", "bin", "pdftoppm.exe"),
                Path.Combine("poppler", "pdftoppm.exe"));
        }

        private static string ResolveLibreOfficePath()
        {
            return ResolveToolPath(
                "soffice.exe",
                Path.Combine("LibreOffice", "program", "soffice.exe"),
                Path.Combine("libreoffice", "program", "soffice.exe"),
                Path.Combine("LibreOfficePortable", "App", "libreoffice", "program", "soffice.exe"));
        }

        private static string ResolveToolPath(string executableName, params string[] relativeCandidates)
        {
            string converterRoot = Path.Combine(
                Application.streamingAssetsPath,
                "PresentationConverters",
                "win-x64");

            for (int index = 0; index < relativeCandidates.Length; index++)
            {
                string candidatePath = Path.Combine(converterRoot, relativeCandidates[index]);
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            string pathCandidate = ResolveExecutableOnPath(executableName);
            return !string.IsNullOrWhiteSpace(pathCandidate) ? pathCandidate : executableName;
        }

        private static string ResolveExecutableOnPath(string executableName)
        {
            string pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            string[] pathEntries = pathVariable.Split(Path.PathSeparator);
            for (int index = 0; index < pathEntries.Length; index++)
            {
                string entry = pathEntries[index];
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                string candidate = Path.Combine(entry.Trim(), executableName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static string BuildToolError(
            string baseMessage,
            string processError,
            string standardOutput,
            string standardError)
        {
            var details = new List<string> { baseMessage };
            if (!string.IsNullOrWhiteSpace(processError))
            {
                details.Add(processError.Trim());
            }

            if (!string.IsNullOrWhiteSpace(standardError))
            {
                details.Add(standardError.Trim());
            }

            if (!string.IsNullOrWhiteSpace(standardOutput))
            {
                details.Add(standardOutput.Trim());
            }

            return string.Join(" ", details);
        }

        private static string NormalizeExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return string.Empty;
            }

            extension = extension.Trim().ToLowerInvariant();
            return extension.StartsWith(".") ? extension : $".{extension}";
        }

        private static string Quote(string value)
        {
            return $"\"{value}\"";
        }

        private static string ToFileUri(string path)
        {
            return new Uri(Path.GetFullPath(path)).AbsoluteUri;
        }

        private static bool IsWindowsRuntime()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return true;
#else
            return false;
#endif
        }
    }
}
