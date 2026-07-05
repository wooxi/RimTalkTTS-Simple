using HarmonyLib;
using System;
using System.Reflection;
using RimTalkTTS.Simple.Data;
using RimTalkTTS.Simple.Patch;
using RimTalkTTS.Simple.Service;
using Verse;

namespace RimTalkTTS.Simple
{
    public class TTSMod : Mod
    {
        private TTSSettings _settings;
        public static TTSMod Instance { get; private set; }

        public TTSMod(ModContentPack content) : base(content)
        {
            Instance = this;
            _settings = GetSettings<TTSSettings>();

            try
            {
                var harmony = new Harmony("nitoritech.rimtalk.tts.simple");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.Message("[RimTalkTTS.Simple] Harmony patches applied");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalkTTS.Simple] Failed to apply patches: {ex.Message}");
            }
        }

        public override string SettingsCategory()
        {
            return "RimTalk TTS Simple";
        }

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            UI.SettingsUI.DrawSettings(inRect, _settings);
        }

        public TTSSettings GetSettings()
        {
            return _settings;
        }

        public bool IsActive => _settings?.EnableTTS ?? false;
    }

    public static class TTSModule
    {
        private static TTSMod _mod;
        public static TTSMod Instance
        {
            get
            {
                if (_mod == null)
                    _mod = LoadedModManager.GetMod<TTSMod>();
                return _mod;
            }
        }

        public static TTSSettings GetSettings()
        {
            return Instance?.GetSettings();
        }

        public static bool IsActive => Instance?.IsActive ?? false;
    }
}
