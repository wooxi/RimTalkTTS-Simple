using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using RimTalkTTS.Simple.Data;
using RimTalkTTS.Simple.Patch;
using RimTalkTTS.Simple.Service;
using RimTalkTTS.Simple.Util;
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
                LogInitStatus();
            }
            catch (Exception ex)
            {
                TTSLogger.Error($"Failed to apply patches: {ex.Message}");
            }
        }

        private void LogInitStatus()
        {
            TTSLogger.Info("========== TTS Module Initialized ==========");

            var harmony = new Harmony("nitoritech.rimtalk.tts.simple");
            var patched = harmony.GetPatchedMethods();
            TTSLogger.Info($"Harmony patches applied: {patched.Count()}");
            foreach (var m in patched)
            {
                TTSLogger.Info($"  Patched: {m.DeclaringType?.FullName}.{m.Name}");
            }

            RimTalkPatches.ResolveRimTalkTypes();
            TTSLogger.Info($"RimTalk assembly: {(RimTalkPatches.RimTalkAssembly != null ? RimTalkPatches.RimTalkAssembly.GetName().Name : "NOT FOUND")}");

            var personaDef = DefDatabase<HediffDef>.GetNamedSilentFail("RimTalk_PersonaData");
            TTSLogger.Info($"PersonaData hediff: {(personaDef != null ? "YES" : "NO")}");

            TTSLogger.Info($"Provider: {_settings.Provider}, Enabled: {_settings.EnableTTS}");
            TTSLogger.Info("=============================================");
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

    [HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
    public static class StatsTick_Patch
    {
        private static int _counter = 0;
        static void Postfix()
        {
            _counter++;
            if (_counter % 60 == 0)
            {
                TTSStats.Update();
            }
            TTSLogger.FlushNotifications();
        }
    }
}
