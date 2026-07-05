using System.Collections.Generic;
using System.Linq;
using RimTalkTTS.Simple.Data;
using UnityEngine;
using Verse;

namespace RimTalkTTS.Simple.UI
{
    public static class SettingsUI
    {
        private static string _miMoApiKeyBuffer = "";
        private static bool _initialized = false;

        public static void DrawSettings(Rect inRect, TTSSettings settings)
        {
            if (!_initialized)
            {
                _miMoApiKeyBuffer = settings.MiMoApiKey;
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

            DrawProviderSelector(listing, settings);

            listing.Gap();

            if (settings.Provider == TTSSettings.TTSProvider.MiMoTTS)
            {
                DrawMiMoSettings(listing, settings);
            }
            else
            {
                DrawEdgeSettings(listing, settings);
            }

            listing.Gap();

            listing.CheckboxLabeled("流式输出 (MiMo)", ref settings.EnableStreaming);

            listing.Gap();

            float volume = settings.Volume;
            listing.Label($"音量: {volume:F1}");
            settings.Volume = listing.Slider(volume, 0f, 1f);

            float speed = settings.Speed;
            listing.Label($"语速: {speed:F1}");
            settings.Speed = listing.Slider(speed, 0.25f, 2f);

            listing.End();
        }

        private static void DrawProviderSelector(Listing_Standard listing, TTSSettings settings)
        {
            bool isEdge = settings.Provider == TTSSettings.TTSProvider.EdgeTTS;
            bool isMiMo = settings.Provider == TTSSettings.TTSProvider.MiMoTTS;

            listing.Label("TTS 渠道");

            if (listing.RadioButton("Edge TTS (免费，无需 API Key)", isEdge))
            {
                settings.Provider = TTSSettings.TTSProvider.EdgeTTS;
            }

            if (listing.RadioButton("MiMo TTS (需要 API Key)", isMiMo))
            {
                settings.Provider = TTSSettings.TTSProvider.MiMoTTS;
            }
        }

        private static void DrawMiMoSettings(Listing_Standard listing, TTSSettings settings)
        {
            listing.Label($"API Key (从 https://mimo.mi.com 获取)");

            string newKey = listing.TextEntry(_miMoApiKeyBuffer, 3);
            if (newKey != _miMoApiKeyBuffer)
            {
                _miMoApiKeyBuffer = newKey;
                settings.MiMoApiKey = newKey;
            }

            listing.Gap();

            DrawDropdown(listing, "模型", settings.MiMoModel, TTSSettings.GetMiMoModels(),
                v => settings.MiMoModel = v);

            listing.Gap();

            DrawDropdown(listing, "音色", settings.MiMoVoice, TTSSettings.GetMiMoVoices(),
                v => settings.MiMoVoice = v);

            listing.Gap();
            listing.Label("提示：模组会自动读取 RimTalk 分配的角色人格作为音色描述");
        }

        private static void DrawEdgeSettings(Listing_Standard listing, TTSSettings settings)
        {
            DrawDropdown(listing, "音色", settings.EdgeVoice, TTSSettings.GetEdgeVoices(),
                v => settings.EdgeVoice = v);
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
