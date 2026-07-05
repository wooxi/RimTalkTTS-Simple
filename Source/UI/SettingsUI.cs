using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RimTalkTTS.Simple.Data;
using RimTalkTTS.Simple.Service;
using UnityEngine;
using Verse;

namespace RimTalkTTS.Simple.UI
{
    public static class SettingsUI
    {
        private static string _miMoApiKeyBuffer = "";
        private static string _customEndpointBuffer = "";
        private static string _customApiKeyBuffer = "";
        private static string _customModelBuffer = "";
        private static bool _initialized = false;
        private static EndpointTestResult _lastTestResult;
        private static bool _testRunning = false;

        public static void DrawSettings(Rect inRect, TTSSettings settings)
        {
            if (!_initialized)
            {
                _miMoApiKeyBuffer = settings.MiMoApiKey;
                _customEndpointBuffer = settings.CustomEndpointUrl;
                _customApiKeyBuffer = settings.CustomApiKey;
                _customModelBuffer = settings.CustomModel;
                _initialized = true;
            }

            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.CheckboxLabeled("启用 TTS", ref settings.EnableTTS);

            if (!settings.EnableTTS)
            {
                listing.End();
                return;
            }

            listing.Gap();
            DrawSectionHeader(listing, "TTS 渠道");
            DrawProviderSelector(listing, settings);

            listing.Gap();
            listing.GapLine();

            if (settings.Provider == TTSSettings.TTSProvider.MiMoTTS)
            {
                DrawSectionHeader(listing, "MiMo 配置");
                DrawMiMoSettings(listing, settings);

                listing.Gap();
                listing.GapLine();

                DrawSectionHeader(listing, "自定义端点");
                DrawCustomEndpointSection(listing, settings);
            }
            else
            {
                DrawSectionHeader(listing, "Edge TTS 配置");
                DrawEdgeSettings(listing, settings);
            }

            listing.Gap();
            listing.GapLine();

            DrawSectionHeader(listing, "音频设置");
            listing.CheckboxLabeled("流式输出 (MiMo)", ref settings.EnableStreaming);
            listing.CheckboxLabeled("游戏内通知 (错误/警告)", ref settings.EnableNotifications);

            float volume = settings.Volume;
            listing.Label($"音量: {volume:F1}");
            settings.Volume = listing.Slider(volume, 0f, 1f);

            float speed = settings.Speed;
            listing.Label($"语速: {speed:F1}");
            settings.Speed = listing.Slider(speed, 0.25f, 2f);

            listing.Gap();
            listing.GapLine();

            DebugUI.DrawDebugSection(listing, settings);

            listing.End();
        }

        private static void DrawSectionHeader(Listing_Standard listing, string title)
        {
            Text.Font = GameFont.Medium;
            listing.Label(title);
            Text.Font = GameFont.Small;
        }

        private static void DrawProviderSelector(Listing_Standard listing, TTSSettings settings)
        {
            bool isEdge = settings.Provider == TTSSettings.TTSProvider.EdgeTTS;
            bool isMiMo = settings.Provider == TTSSettings.TTSProvider.MiMoTTS;

            if (listing.RadioButton("Edge TTS (免费，无需 API Key)", isEdge))
                settings.Provider = TTSSettings.TTSProvider.EdgeTTS;

            if (listing.RadioButton("MiMo TTS (需要 API Key)", isMiMo))
                settings.Provider = TTSSettings.TTSProvider.MiMoTTS;
        }

        private static void DrawMiMoSettings(Listing_Standard listing, TTSSettings settings)
        {
            listing.Label("API Key (从 https://mimo.mi.com 获取)");
            string newKey = listing.TextEntry(_miMoApiKeyBuffer, 3);
            if (newKey != _miMoApiKeyBuffer)
            {
                _miMoApiKeyBuffer = newKey;
                settings.MiMoApiKey = newKey;
            }

            listing.Gap();
            DrawDropdown(listing, "模型", settings.MiMoModel, TTSSettings.GetMiMoModels(),
                v => settings.MiMoModel = v);

            bool isVoiceDesign = (settings.MiMoModel ?? "").Contains("voicedesign");
            if (!isVoiceDesign)
                DrawDropdown(listing, "音色", settings.MiMoVoice, TTSSettings.GetMiMoVoices(),
                    v => settings.MiMoVoice = v);
            else
                listing.Label("音色: 由人格描述自动设计 (Voice Design 模式)");

            listing.Gap();
            listing.Label("提示：模组会自动读取 RimTalk 分配的角色人格作为音色描述");
        }

        private static void DrawEdgeSettings(Listing_Standard listing, TTSSettings settings)
        {
            DrawDropdown(listing, "音色", settings.EdgeVoice, TTSSettings.GetEdgeVoices(),
                v => settings.EdgeVoice = v);
        }

        private static void DrawCustomEndpointSection(Listing_Standard listing, TTSSettings settings)
        {
            listing.CheckboxLabeled("使用自定义端点", ref settings.UseCustomEndpoint);

            if (!settings.UseCustomEndpoint)
            {
                listing.Label("关闭时使用默认 MiMo API 地址");
                return;
            }

            listing.Label("端点地址 (完整 URL)");
            string newUrl = listing.TextEntry(_customEndpointBuffer, 2);
            if (newUrl != _customEndpointBuffer)
            {
                _customEndpointBuffer = newUrl;
                settings.CustomEndpointUrl = newUrl;
            }

            listing.Label("API Key (留空则使用上方 MiMo Key)");
            string newCustomKey = listing.TextEntry(_customApiKeyBuffer, 3);
            if (newCustomKey != _customApiKeyBuffer)
            {
                _customApiKeyBuffer = newCustomKey;
                settings.CustomApiKey = newCustomKey;
            }

            listing.Label("模型名 (留空则使用上方 MiMo 模型)");
            string newCustomModel = listing.TextEntry(_customModelBuffer, 2);
            if (newCustomModel != _customModelBuffer)
            {
                _customModelBuffer = newCustomModel;
                settings.CustomModel = newCustomModel;
            }

            listing.Gap();

            DrawEndpointTestButton(listing, settings);
        }

        private static void DrawEndpointTestButton(Listing_Standard listing, TTSSettings settings)
        {
            if (_testRunning)
            {
                listing.Label("正在测试端点...");
                return;
            }

            if (listing.ButtonText("测试端点"))
            {
                _testRunning = true;
                _lastTestResult = null;

                Task.Run(async () =>
                {
                    try
                    {
                        string url = settings.CustomEndpointUrl;
                        string key = settings.GetEffectiveApiKey();
                        string model = settings.GetEffectiveModel();
                        string voice = settings.MiMoVoice;

                        _lastTestResult = await MiMoTTSClient.TestEndpointAsync(url, key, model, voice);
                    }
                    catch (System.Exception ex)
                    {
                        _lastTestResult = new EndpointTestResult
                        {
                            Success = false,
                            ErrorMessage = $"测试异常: {ex.Message}"
                        };
                    }
                    finally
                    {
                        _testRunning = false;
                    }
                });
            }

            if (_lastTestResult != null)
            {
                DrawTestResult(listing);
            }
        }

        private static bool _showRequestDetail = false;
        private static bool _showResponseDetail = false;

        private static void DrawTestResult(Listing_Standard listing)
        {
            var r = _lastTestResult;

            listing.Gap();
            listing.Label(r.Success
                ? $"✅ 端点可用 | HTTP {r.HttpStatusCode} | 延迟 {r.LatencyMs}ms | 音频 {r.AudioSizeBytes / 1024}KB"
                : $"❌ {r.ErrorMessage ?? "未知错误"}");

            if (!string.IsNullOrEmpty(r.RequestJson))
            {
                if (listing.ButtonText(_showRequestDetail ? "▾ 请求详情" : "▸ 请求详情"))
                    _showRequestDetail = !_showRequestDetail;

                if (_showRequestDetail)
                {
                    float reqH = Mathf.Min(120f, 14f * (r.RequestJson.Split('\n').Length + 2));
                    Rect reqRect = listing.GetRect(reqH);
                    Widgets.DrawBoxSolid(reqRect, new Color(0.05f, 0.05f, 0.05f, 0.8f));
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(reqRect.ContractedBy(4f), r.RequestJson);
                    Text.Font = GameFont.Small;
                }
            }

            if (!string.IsNullOrEmpty(r.ResponseJson))
            {
                if (listing.ButtonText(_showResponseDetail ? "▾ 响应详情" : "▸ 响应详情"))
                    _showResponseDetail = !_showResponseDetail;

                if (_showResponseDetail)
                {
                    float resH = Mathf.Min(120f, 14f * (r.ResponseJson.Split('\n').Length + 2));
                    Rect resRect = listing.GetRect(resH);
                    Widgets.DrawBoxSolid(resRect, new Color(0.05f, 0.05f, 0.05f, 0.8f));
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(resRect.ContractedBy(4f), r.ResponseJson);
                    Text.Font = GameFont.Small;
                }
            }
        }

        private static void DrawDropdown(Listing_Standard listing, string label, string current, List<VoiceModel> options, System.Action<string> onSelect)
        {
            string displayName = options.FirstOrDefault(v => v.ModelId == current)?.Name ?? current;
            Rect rect = listing.GetRect(30f);
            Widgets.Label(rect.LeftHalf(), label);

            if (Widgets.ButtonText(rect.RightHalf(), displayName))
            {
                var floatMenuOptions = new List<FloatMenuOption>();
                foreach (var option in options)
                {
                    var captured = option.ModelId;
                    floatMenuOptions.Add(new FloatMenuOption(option.Name, () => onSelect(captured)));
                }
                Find.WindowStack.Add(new FloatMenu(floatMenuOptions));
            }
        }
    }
}
