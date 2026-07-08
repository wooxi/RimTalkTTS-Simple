using System.Collections.Generic;
using Verse;

namespace RimTalkTTS.Simple.Data
{
    public class TTSSettings : ModSettings
    {
        public enum TTSProvider
        {
            EdgeTTS,
            MiMoTTS
        }

        public TTSProvider Provider = TTSProvider.EdgeTTS;
        public bool EnableTTS = false;
        public bool EnableStreaming = false;
        public bool EnableNotifications = true;

        public string MiMoApiKey = "";
        public string MiMoModel = "mimo-v2.5-tts";
        public string MiMoVoice = "冰糖";
        public string EdgeVoice = "zh-CN-XiaoxiaoNeural";

        public bool UseCustomEndpoint = false;
        public string CustomEndpointUrl = "";
        public string CustomApiKey = "";
        public string CustomModel = "";

        public float Volume = 0.8f;
        public float Speed = 1.0f;

        public bool EnablePawnTypeModelRouting = false;
        public string ColonistModel = "mimo-v2.5-tts";
        public string NonColonistModel = "mimo-v2.5-tts-voicedesign";

        public static List<VoiceModel> GetMiMoVoices()
        {
            return new List<VoiceModel>
            {
                new VoiceModel("冰糖", "冰糖 (中文, 女)"),
                new VoiceModel("茉莉", "茉莉 (中文, 女)"),
                new VoiceModel("苏打", "苏打 (中文, 男)"),
                new VoiceModel("白桦", "白桦 (中文, 男)"),
                new VoiceModel("Mia", "Mia (英文, 女)"),
                new VoiceModel("Chloe", "Chloe (英文, 女)"),
                new VoiceModel("Milo", "Milo (英文, 男)"),
                new VoiceModel("Dean", "Dean (英文, 男)"),
                new VoiceModel("mimo_default", "MiMo 默认")
            };
        }

        public static List<VoiceModel> GetMiMoModels()
        {
            return new List<VoiceModel>
            {
                new VoiceModel("mimo-v2.5-tts", "MiMo V2.5 TTS (预置音色)"),
                new VoiceModel("mimo-v2.5-tts-voicedesign", "MiMo V2.5 TTS Voice Design (文本设计音色)")
            };
        }

        public static List<VoiceModel> GetEdgeVoices()
        {
            return new List<VoiceModel>
            {
                new VoiceModel("zh-CN-XiaoxiaoNeural", "Xiaoxiao (CN, Female)"),
                new VoiceModel("zh-CN-YunxiNeural", "Yunxi (CN, Male)"),
                new VoiceModel("zh-CN-XiaoyiNeural", "Xiaoyi (CN, Female)"),
                new VoiceModel("zh-CN-YunjianNeural", "Yunjian (CN, Male)"),
                new VoiceModel("en-US-JennyNeural", "Jenny (US, Female)"),
                new VoiceModel("en-US-GuyNeural", "Guy (US, Male)"),
                new VoiceModel("en-US-AriaNeural", "Aria (US, Female)"),
                new VoiceModel("en-US-DavisNeural", "Davis (US, Male)"),
                new VoiceModel("en-GB-SoniaNeural", "Sonia (UK, Female)"),
                new VoiceModel("en-GB-RyanNeural", "Ryan (UK, Male)")
            };
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref EnableTTS, "enableTTS", false);
            Scribe_Values.Look(ref EnableStreaming, "enableStreaming", false);
            Scribe_Values.Look(ref EnableNotifications, "enableNotifications", true);
            Scribe_Values.Look(ref MiMoApiKey, "miMoApiKey", "");
            Scribe_Values.Look(ref MiMoModel, "miMoModel", "mimo-v2.5-tts");
            Scribe_Values.Look(ref MiMoVoice, "miMoVoice", "冰糖");
            Scribe_Values.Look(ref EdgeVoice, "edgeVoice", "zh-CN-XiaoxiaoNeural");
            Scribe_Values.Look(ref Volume, "volume", 0.8f);
            Scribe_Values.Look(ref Speed, "speed", 1.0f);
            Scribe_Values.Look(ref Provider, "provider", TTSProvider.EdgeTTS);
            Scribe_Values.Look(ref UseCustomEndpoint, "useCustomEndpoint", false);
            Scribe_Values.Look(ref CustomEndpointUrl, "customEndpointUrl", "");
            Scribe_Values.Look(ref CustomApiKey, "customApiKey", "");
            Scribe_Values.Look(ref CustomModel, "customModel", "");
            Scribe_Values.Look(ref EnablePawnTypeModelRouting, "enablePawnTypeModelRouting", false);
            Scribe_Values.Look(ref ColonistModel, "colonistModel", "mimo-v2.5-tts");
            Scribe_Values.Look(ref NonColonistModel, "nonColonistModel", "mimo-v2.5-tts-voicedesign");
        }

        public string GetEffectiveApiKey()
        {
            if (Provider == TTSProvider.MiMoTTS && UseCustomEndpoint && !string.IsNullOrWhiteSpace(CustomApiKey))
                return CustomApiKey;
            if (Provider == TTSProvider.MiMoTTS)
                return MiMoApiKey;
            return "";
        }

        public string GetEffectiveEndpointUrl()
        {
            if (Provider == TTSProvider.MiMoTTS && UseCustomEndpoint && !string.IsNullOrWhiteSpace(CustomEndpointUrl))
                return CustomEndpointUrl;
            return "https://api.xiaomimimo.com/v1/chat/completions";
        }

        public string GetModelForPawn(Pawn pawn)
        {
            if (pawn == null) return GetEffectiveModel();
            if (!EnablePawnTypeModelRouting) return GetEffectiveModel();
            if (UseCustomEndpoint && !string.IsNullOrWhiteSpace(CustomModel))
                return CustomModel;
            return pawn.IsColonist ? ColonistModel : NonColonistModel;
        }

        public string GetEffectiveModel()
        {
            if (Provider == TTSProvider.MiMoTTS && UseCustomEndpoint && !string.IsNullOrWhiteSpace(CustomModel))
                return CustomModel;
            return MiMoModel;
        }
    }
}
