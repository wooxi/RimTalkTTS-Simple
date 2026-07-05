using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RimTalkTTS.Simple.Data;
using RimTalkTTS.Simple.Util;
using Verse;

namespace RimTalkTTS.Simple.Service
{
    public static class MiMoTTSClient
    {
        private const string DEFAULT_API_URL = "https://api.xiaomimimo.com/v1/chat/completions";
        private static readonly HttpClient _httpClient;

        static MiMoTTSClient()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(120);
        }

        public static async Task<byte[]> GenerateSpeechAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(request.ApiKey))
                {
                    TTSLogger.ErrorNotify("MiMo API Key 未配置，请在设置中填写 API Key", "MiMo");
                    return null;
                }

                string apiUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? DEFAULT_API_URL : request.BaseUrl;

                string personaPrompt = string.IsNullOrEmpty(request.Persona)
                    ? "Use a natural, clear speaking voice."
                    : request.Persona;

                bool streaming = request.EnableStreaming;
                bool isVoiceDesign = (request.Model ?? "").Contains("voicedesign");
                string audioFormat = streaming ? "pcm16" : "wav";

                var audioObj = new JObject
                {
                    ["format"] = audioFormat
                };
                if (!isVoiceDesign)
                {
                    audioObj["voice"] = request.Voice ?? "冰糖";
                }

                var payload = new JObject
                {
                    ["model"] = request.Model ?? "mimo-v2.5-tts",
                    ["messages"] = new JArray
                    {
                        new JObject { ["role"] = "user", ["content"] = personaPrompt },
                        new JObject { ["role"] = "assistant", ["content"] = request.Input }
                    },
                    ["audio"] = audioObj,
                    ["stream"] = streaming
                };

                string json = JsonConvert.SerializeObject(payload);

                TTSLogger.Info($"MiMo 请求: model={request.Model} isVoiceDesign={isVoiceDesign} streaming={streaming}", "MiMo");
                TTSLogger.Debug($"MiMo JSON: {json.Substring(0, Math.Min(500, json.Length))}", "MiMo");
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, apiUrl)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                httpRequest.Headers.Add("api-key", request.ApiKey);

                if (streaming)
                {
                    return await HandleStreamResponseAsync(httpRequest, cancellationToken);
                }
                else
                {
                    return await HandleNonStreamResponseAsync(httpRequest, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                TTSLogger.ErrorNotify($"MiMo TTS 异常: {ex.GetType().Name}: {ex.Message}", "MiMo");
                return null;
            }
        }

        private static async Task<byte[]> HandleNonStreamResponseAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = await _httpClient.SendAsync(request, ct);
            string responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                string summary = responseJson.Length > 300 ? responseJson.Substring(0, 300) : responseJson;
                TTSLogger.ErrorNotify($"MiMo HTTP {(int)response.StatusCode}: {summary}", "MiMo");
                return null;
            }

            var obj = JObject.Parse(responseJson);
            var choices = obj["choices"] as JArray;
            if (choices == null || choices.Count == 0)
            {
                TTSLogger.WarningNotify("MiMo 响应无 choices", "MiMo");
                return null;
            }

            var firstChoice = choices[0] as JObject;
            if (firstChoice == null)
            {
                TTSLogger.WarningNotify("MiMo 响应格式异常：choices[0] 不是对象", "MiMo");
                return null;
            }

            var message = firstChoice["message"] as JObject;
            if (message == null)
            {
                TTSLogger.WarningNotify("MiMo 响应无 message 字段", "MiMo");
                return null;
            }

            var audio = message["audio"] as JObject;
            var data = audio?["data"]?.ToString();

            if (string.IsNullOrEmpty(data))
            {
                TTSLogger.WarningNotify("MiMo 响应无音频数据", "MiMo");
                return null;
            }

            byte[] audioBytes = Convert.FromBase64String(data);

            if (Prefs.DevMode)
            {
                TTSLogger.Debug($"MiMo TTS generated {audioBytes.Length} bytes (non-stream)", "MiMo");
            }

            return audioBytes;
        }

        private static async Task<byte[]> HandleStreamResponseAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                string summary = errorBody.Length > 300 ? errorBody.Substring(0, 300) : errorBody;
                TTSLogger.ErrorNotify($"MiMo 流式 HTTP {(int)response.StatusCode}: {summary}", "MiMo");
                return null;
            }

            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var reader = new StreamReader(stream))
            {
                var pcmChunks = new System.Collections.Generic.List<byte[]>();
                int totalPcmBytes = 0;

                while (!reader.EndOfStream && !ct.IsCancellationRequested)
                {
                    string line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line)) continue;
                    if (!line.StartsWith("data: ")) continue;

                    string dataContent = line.Substring(6);
                    if (dataContent == "[DONE]") break;

                    try
                    {
                        var delta = JObject.Parse(dataContent);
                        var choices = delta["choices"] as JArray;
                        if (choices == null || choices.Count == 0) continue;

                        var firstChoice = choices[0] as JObject;
                        if (firstChoice == null) continue;

                        var deltaObj = firstChoice["delta"] as JObject;
                        if (deltaObj == null) continue;

                        var audio = deltaObj["audio"] as JObject;
                        if (audio == null) continue;

                        var audioData = audio["data"]?.ToString();

                        if (!string.IsNullOrEmpty(audioData))
                        {
                            byte[] chunk = Convert.FromBase64String(audioData);
                            pcmChunks.Add(chunk);
                            totalPcmBytes += chunk.Length;
                        }
                    }
                    catch (JsonException) { }
                }

                if (pcmChunks.Count == 0)
                {
                    TTSLogger.WarningNotify("MiMo 流式: 未收到 PCM 数据", "MiMo");
                    return null;
                }

                byte[] allPcm = new byte[totalPcmBytes];
                int offset = 0;
                foreach (var chunk in pcmChunks)
                {
                    Array.Copy(chunk, 0, allPcm, offset, chunk.Length);
                    offset += chunk.Length;
                }

                byte[] wavData = ConvertPcm16ToWav(allPcm, 24000, 1, 16);

                if (Prefs.DevMode)
                {
                    TTSLogger.Debug($"MiMo TTS stream: {pcmChunks.Count} chunks, {totalPcmBytes} bytes PCM, {wavData.Length} bytes WAV", "MiMo");
                }

                return wavData;
            }
        }

        private static byte[] ConvertPcm16ToWav(byte[] pcmData, int sampleRate, int channels, int bitsPerSample)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                int byteRate = sampleRate * channels * bitsPerSample / 8;
                int blockAlign = channels * bitsPerSample / 8;
                int dataSize = pcmData.Length;

                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1); // PCM format
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write((short)blockAlign);
                writer.Write((short)bitsPerSample);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);
                writer.Write(pcmData);

                return ms.ToArray();
            }
        }

        public static async Task<Data.EndpointTestResult> TestEndpointAsync(string url, string apiKey, string model, string voice)
        {
            var result = new Data.EndpointTestResult
            {
                RequestUrl = url,
                Success = false
            };

            try
            {
                var payload = new JObject
                {
                    ["model"] = string.IsNullOrWhiteSpace(model) ? "mimo-v2.5-tts" : model,
                    ["messages"] = new JArray
                    {
                        new JObject { ["role"] = "user", ["content"] = "Use a natural voice." },
                        new JObject { ["role"] = "assistant", ["content"] = "Endpoint test." }
                    },
                    ["audio"] = new JObject
                    {
                        ["format"] = "wav",
                        ["voice"] = string.IsNullOrWhiteSpace(voice) ? "冰糖" : voice
                    },
                    ["stream"] = false
                };

                string json = JsonConvert.SerializeObject(payload);
                result.RequestJson = json;

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                httpRequest.Headers.Add("api-key", apiKey);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var response = await _httpClient.SendAsync(httpRequest);
                sw.Stop();

                result.LatencyMs = sw.ElapsedMilliseconds;
                result.HttpStatusCode = (int)response.StatusCode;

                string responseJson = await response.Content.ReadAsStringAsync();
                result.ResponseJson = responseJson.Length > 2000
                    ? responseJson.Substring(0, 2000) + "...(truncated)"
                    : responseJson;

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var obj = JObject.Parse(responseJson);
                        var choices = obj["choices"] as JArray;
                        if (choices != null && choices.Count > 0)
                        {
                            var first = choices[0] as JObject;
                            var msg = first?["message"] as JObject;
                            var audio = msg?["audio"] as JObject;
                            var data = audio?["data"]?.ToString();
                            if (!string.IsNullOrEmpty(data))
                            {
                                byte[] audioBytes = Convert.FromBase64String(data);
                                result.AudioSizeBytes = audioBytes.Length;
                                result.Success = true;
                            }
                            else
                            {
                                result.ErrorMessage = "响应中无音频数据";
                            }
                        }
                        else
                        {
                            result.ErrorMessage = "响应中无 choices";
                        }
                    }
                    catch (Exception ex)
                    {
                        result.ErrorMessage = $"解析响应失败: {ex.Message}";
                    }
                }
                else
                {
                    result.ErrorMessage = $"HTTP {(int)response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"请求失败: {ex.Message}";
            }

            return result;
        }
    }
}
