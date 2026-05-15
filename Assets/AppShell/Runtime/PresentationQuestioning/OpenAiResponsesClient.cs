using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace VRPublicSpeaking.AppShell.PresentationQuestioning
{
        public static class OpenAiResponsesClient
        {
        private const int MaxNetworkAttempts = 3;

        [Serializable]
        private class ResponsesRequest
        {
            public string model;
            public string input;
            public float temperature = 0.35f;
            public int max_output_tokens = 1200;
        }

        [Serializable]
        private class ResponsesResponse
        {
            public string output_text;
            public List<ResponsesOutput> output = new List<ResponsesOutput>();
        }

        [Serializable]
        private class ResponsesOutput
        {
            public List<ResponsesContent> content = new List<ResponsesContent>();
        }

        [Serializable]
        private class ResponsesContent
        {
            public string text;
        }

        [Serializable]
        private class GeminiRequest
        {
            public List<GeminiContent> contents = new List<GeminiContent>();
            public GeminiGenerationConfig generationConfig = new GeminiGenerationConfig();
        }

        [Serializable]
        private class GeminiResponse
        {
            public List<GeminiCandidate> candidates = new List<GeminiCandidate>();
        }

        [Serializable]
        private class GeminiCandidate
        {
            public GeminiContent content;
        }

        [Serializable]
        private class GeminiContent
        {
            public List<GeminiPart> parts = new List<GeminiPart>();
        }

        [Serializable]
        private class GeminiPart
        {
            public string text;
        }

        [Serializable]
        private class GeminiGenerationConfig
        {
            public float temperature = 0.35f;
            public int maxOutputTokens = 1200;
            public string responseMimeType = "application/json";
        }

        public static IEnumerator SendPrompt(
            string prompt,
            int maxOutputTokens,
            Action<bool, string> completed)
        {
            OpenAiRuntimeConfig config = OpenAiRuntimeConfig.Load();
            if (config.TryGetConfigurationError(out string configError))
            {
                completed?.Invoke(false, configError);
                yield break;
            }

            if (config.IsGemini)
            {
                yield return SendGeminiPrompt(config, prompt, maxOutputTokens, completed);
                yield break;
            }

            yield return SendOpenAiPrompt(config, prompt, maxOutputTokens, completed);
        }

        private static IEnumerator SendOpenAiPrompt(
            OpenAiRuntimeConfig config,
            string prompt,
            int maxOutputTokens,
            Action<bool, string> completed)
        {
            var requestBody = new ResponsesRequest
            {
                model = config.ResolvedModel,
                input = prompt ?? string.Empty,
                max_output_tokens = Mathf.Clamp(maxOutputTokens, 256, 4096)
            };

            string url = config.ResolvedBaseUrl;
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(requestBody));
            for (int attempt = 1; attempt <= MaxNetworkAttempts; attempt++)
            {
                using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    request.uploadHandler = new UploadHandlerRaw(body);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.timeout = config.requestTimeoutSeconds;
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("Authorization", $"Bearer {config.ResolvedApiKey}");

                    yield return request.SendWebRequest();

                    string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        if (ShouldRetryRequest(request, attempt))
                        {
                            Debug.LogWarning(BuildNetworkFailureMessage("OpenAI", url, request, responseText, attempt));
                            yield return new WaitForSecondsRealtime(0.75f * attempt);
                            continue;
                        }

                        completed?.Invoke(false, BuildNetworkFailureMessage("OpenAI", url, request, responseText, attempt));
                        yield break;
                    }

                    string output = ExtractOpenAiResponseText(responseText);
                    if (string.IsNullOrWhiteSpace(output))
                    {
                        completed?.Invoke(false, "LLM response did not include output text.");
                        yield break;
                    }

                    completed?.Invoke(true, output.Trim());
                    yield break;
                }
            }
        }

        private static IEnumerator SendGeminiPrompt(
            OpenAiRuntimeConfig config,
            string prompt,
            int maxOutputTokens,
            Action<bool, string> completed)
        {
            var requestBody = new GeminiRequest
            {
                generationConfig = new GeminiGenerationConfig
                {
                    maxOutputTokens = Mathf.Clamp(maxOutputTokens, 256, 4096),
                    responseMimeType = "application/json",
                    temperature = 0.35f
                }
            };
            requestBody.contents.Add(new GeminiContent
            {
                parts = new List<GeminiPart>
                {
                    new GeminiPart { text = prompt ?? string.Empty }
                }
            });

            string url = BuildGeminiGenerateContentUrl(config);
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(requestBody));
            for (int attempt = 1; attempt <= MaxNetworkAttempts; attempt++)
            {
                using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    request.uploadHandler = new UploadHandlerRaw(body);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.timeout = config.requestTimeoutSeconds;
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("x-goog-api-key", config.ResolvedApiKey);

                    yield return request.SendWebRequest();

                    string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        if (ShouldRetryRequest(request, attempt))
                        {
                            Debug.LogWarning(BuildNetworkFailureMessage("Gemini", url, request, responseText, attempt));
                            yield return new WaitForSecondsRealtime(0.75f * attempt);
                            continue;
                        }

                        completed?.Invoke(false, BuildNetworkFailureMessage("Gemini", url, request, responseText, attempt));
                        yield break;
                    }

                    string output = ExtractGeminiResponseText(responseText);
                    if (string.IsNullOrWhiteSpace(output))
                    {
                        completed?.Invoke(false, "Gemini response did not include output text.");
                        yield break;
                    }

                    completed?.Invoke(true, output.Trim());
                    yield break;
                }
            }
        }

        private static string BuildGeminiGenerateContentUrl(OpenAiRuntimeConfig config)
        {
            string baseUrl = (config.ResolvedBaseUrl ?? string.Empty).TrimEnd('/');
            if (baseUrl.IndexOf(":generateContent", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return baseUrl;
            }

            string model = (config.ResolvedModel ?? string.Empty).Trim().Trim('/');
            if (model.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                model.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return model;
            }

            const string modelsPrefix = "models/";
            if (model.StartsWith(modelsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                model = model.Substring(modelsPrefix.Length);
            }

            string escapedModel = UnityWebRequest.EscapeURL(model);
            if (baseUrl.EndsWith("/models", StringComparison.OrdinalIgnoreCase))
            {
                return $"{baseUrl}/{escapedModel}:generateContent";
            }

            if (baseUrl.IndexOf("/models/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return $"{baseUrl}:generateContent";
            }

            return $"{baseUrl}/models/{escapedModel}:generateContent";
        }

        public static string ExtractJsonObject(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                return string.Empty;
            }

            return text.Substring(start, end - start + 1);
        }

        private static string ExtractOpenAiResponseText(string json)
        {
            try
            {
                ResponsesResponse response = JsonUtility.FromJson<ResponsesResponse>(json);
                if (response != null)
                {
                    if (!string.IsNullOrWhiteSpace(response.output_text))
                    {
                        return response.output_text;
                    }

                    string nestedText = JoinResponsesText(response.output);
                    if (!string.IsNullOrWhiteSpace(nestedText))
                    {
                        return nestedText;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[OpenAiResponsesClient] Could not parse OpenAI response JSON: {exception.Message}");
            }

            return ExtractResponseTextFallback(json);
        }

        private static string ExtractGeminiResponseText(string json)
        {
            try
            {
                GeminiResponse response = JsonUtility.FromJson<GeminiResponse>(json);
                if (response != null && response.candidates != null)
                {
                    var builder = new StringBuilder();
                    for (int candidateIndex = 0; candidateIndex < response.candidates.Count; candidateIndex++)
                    {
                        GeminiContent content = response.candidates[candidateIndex]?.content;
                        if (content?.parts == null)
                        {
                            continue;
                        }

                        for (int partIndex = 0; partIndex < content.parts.Count; partIndex++)
                        {
                            string text = content.parts[partIndex]?.text;
                            if (string.IsNullOrWhiteSpace(text))
                            {
                                continue;
                            }

                            if (builder.Length > 0) builder.AppendLine();
                            builder.Append(text);
                        }
                    }

                    if (builder.Length > 0)
                    {
                        return builder.ToString();
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[OpenAiResponsesClient] Could not parse Gemini response JSON: {exception.Message}");
            }

            return ExtractResponseTextFallback(json);
        }

        private static string JoinResponsesText(List<ResponsesOutput> outputs)
        {
            if (outputs == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (int outputIndex = 0; outputIndex < outputs.Count; outputIndex++)
            {
                List<ResponsesContent> content = outputs[outputIndex]?.content;
                if (content == null)
                {
                    continue;
                }

                for (int contentIndex = 0; contentIndex < content.Count; contentIndex++)
                {
                    string text = content[contentIndex]?.text;
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    if (builder.Length > 0) builder.AppendLine();
                    builder.Append(text);
                }
            }

            return builder.ToString();
        }

        private static string ExtractResponseTextFallback(string json)
        {
            string outputText = ExtractJsonStringProperty(json, "output_text");
            if (!string.IsNullOrWhiteSpace(outputText))
            {
                return outputText;
            }

            var builder = new StringBuilder();
            int searchIndex = 0;
            while (TryExtractJsonStringProperty(json, "text", ref searchIndex, out string text))
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (builder.Length > 0) builder.AppendLine();
                    builder.Append(text);
                }
            }

            return builder.ToString();
        }

        private static string ExtractJsonStringProperty(string json, string property)
        {
            int index = 0;
            return TryExtractJsonStringProperty(json, property, ref index, out string value) ? value : string.Empty;
        }

        private static bool TryExtractJsonStringProperty(string json, string property, ref int searchIndex, out string value)
        {
            value = string.Empty;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(property))
            {
                return false;
            }

            string needle = $"\"{property}\"";
            int propertyIndex = json.IndexOf(needle, searchIndex, StringComparison.Ordinal);
            if (propertyIndex < 0)
            {
                return false;
            }

            int colonIndex = json.IndexOf(':', propertyIndex + needle.Length);
            int quoteIndex = colonIndex >= 0 ? json.IndexOf('"', colonIndex + 1) : -1;
            if (quoteIndex < 0)
            {
                searchIndex = propertyIndex + needle.Length;
                return false;
            }

            var builder = new StringBuilder();
            for (int index = quoteIndex + 1; index < json.Length; index++)
            {
                char c = json[index];
                if (c == '\\' && index + 1 < json.Length)
                {
                    char escaped = json[++index];
                    if (escaped == 'u' && TryDecodeUnicodeEscape(json, index + 1, out char unicode))
                    {
                        builder.Append(unicode);
                        index += 4;
                    }
                    else
                    {
                        builder.Append(DecodeEscapedChar(escaped));
                    }

                    continue;
                }

                if (c == '"')
                {
                    searchIndex = index + 1;
                    value = builder.ToString();
                    return true;
                }

                builder.Append(c);
            }

            searchIndex = json.Length;
            return false;
        }

        private static char DecodeEscapedChar(char c)
        {
            switch (c)
            {
                case 'n': return '\n';
                case 'r': return '\r';
                case 't': return '\t';
                case '"': return '"';
                case '\\': return '\\';
                case '/': return '/';
                default: return c;
            }
        }

        private static bool TryDecodeUnicodeEscape(string text, int startIndex, out char value)
        {
            value = '\0';
            if (string.IsNullOrEmpty(text) || startIndex < 0 || startIndex + 4 > text.Length)
            {
                return false;
            }

            int code = 0;
            for (int index = startIndex; index < startIndex + 4; index++)
            {
                int digit = HexValue(text[index]);
                if (digit < 0)
                {
                    return false;
                }

                code = (code << 4) + digit;
            }

            value = (char)code;
            return true;
        }

        private static int HexValue(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        private static bool ShouldRetryRequest(UnityWebRequest request, int attempt)
        {
            if (attempt >= MaxNetworkAttempts || request == null)
            {
                return false;
            }

            if (request.responseCode == 0)
            {
                return true;
            }

            return request.responseCode == 408 ||
                request.responseCode == 429 ||
                request.responseCode >= 500;
        }

        private static string BuildNetworkFailureMessage(
            string provider,
            string url,
            UnityWebRequest request,
            string responseText,
            int attempt)
        {
            string endpoint = string.IsNullOrWhiteSpace(url) ? "unknown endpoint" : url;
            string error = request != null && !string.IsNullOrWhiteSpace(request.error)
                ? request.error
                : "unknown network error";
            long responseCode = request != null ? request.responseCode : 0;
            string retrySuffix = attempt < MaxNetworkAttempts
                ? $" Retrying ({attempt + 1}/{MaxNetworkAttempts})..."
                : string.Empty;
            string responseSuffix = string.IsNullOrWhiteSpace(responseText)
                ? string.Empty
                : $" Response: {responseText}";
            return $"{provider} request failed at {endpoint}: {responseCode} {error}.{retrySuffix}{responseSuffix}";
        }
    }
}
