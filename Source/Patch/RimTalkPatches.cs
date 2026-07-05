using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using RimTalkTTS.Simple.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalkTTS.Simple.Patch
{
    public static class RimTalkPatches
    {
        public static readonly HashSet<Guid> blockedDialogues = new HashSet<Guid>();
        private static readonly object _blockLock = new object();
        private static readonly ConditionalWeakTable<object, Pawn> _listToPawnMap = new ConditionalWeakTable<object, Pawn>();

        public static Assembly RimTalkAssembly;
        public static Type TalkResponseType;
        public static Type PawnStateType;
        public static Type TalkHistoryType;
        public static Type TalkServiceType;
        public static Type RimTalkMainType;
        public static bool TypesResolved;

        public static void ResolveRimTalkTypes()
        {
            if (TypesResolved) return;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "RimTalk" || asm.GetName().Name.StartsWith("RimTalk"))
                {
                    RimTalkAssembly = asm;
                    TTSLogger.Info($"Found RimTalk assembly: {asm.FullName}", "Patch");
                    break;
                }
            }

            if (RimTalkAssembly == null)
            {
                RimTalkAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.FullName.Contains("RimTalk"));
                if (RimTalkAssembly != null)
                    TTSLogger.Info($"Found RimTalk assembly (fuzzy): {RimTalkAssembly.FullName}", "Patch");
                else
                    TTSLogger.ErrorNotify("未找到 RimTalk 程序集，请确认 RimTalk 已加载", "Patch");
            }

            if (RimTalkAssembly != null)
            {
                TalkResponseType = RimTalkAssembly.GetType("RimTalk.Data.TalkResponse");
                PawnStateType = RimTalkAssembly.GetType("RimTalk.Data.PawnState");
                TalkHistoryType = RimTalkAssembly.GetType("RimTalk.Data.TalkHistory");
                TalkServiceType = RimTalkAssembly.GetType("RimTalk.Service.TalkService");
                RimTalkMainType = RimTalkAssembly.GetType("RimTalk.RimTalk");

                TTSLogger.Info($"TalkResponse: {(TalkResponseType != null ? "OK" : "MISSING")}", "Patch");
                TTSLogger.Info($"PawnState: {(PawnStateType != null ? "OK" : "MISSING")}", "Patch");
                TTSLogger.Info($"TalkHistory: {(TalkHistoryType != null ? "OK" : "MISSING")}", "Patch");
                TTSLogger.Info($"TalkService: {(TalkServiceType != null ? "OK" : "MISSING")}", "Patch");
                TTSLogger.Info($"RimTalk: {(RimTalkMainType != null ? "OK" : "MISSING")}", "Patch");
            }
            else
            {
                TTSLogger.Warning("RimTalk assembly NOT FOUND", "Patch");
            }

            TypesResolved = true;
        }

        public static bool IsPatchReady()
        {
            return TypesResolved && TalkResponseType != null && PawnStateType != null;
        }

        private static bool TTSActive()
        {
            return Data.TTSConfig.IsEnabled;
        }

        public static void RequestBlock(Guid dialogueId)
        {
            lock (_blockLock) { blockedDialogues.Add(dialogueId); }
        }

        public static void ReleaseBlock(Guid dialogueId)
        {
            lock (_blockLock) { blockedDialogues.Remove(dialogueId); }
        }

        public static bool IsBlocked(Guid dialogueId)
        {
            lock (_blockLock) { return blockedDialogues.Contains(dialogueId); }
        }

        public static void ClearAllBlocks()
        {
            lock (_blockLock) { blockedDialogues.Clear(); }
        }

        private static object GetMemberValue(object obj, string name)
        {
            if (obj == null) return null;
            var type = obj.GetType();
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null) return field.GetValue(obj);
            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null) return prop.GetValue(obj, null);
            return null;
        }

        private static T GetMemberValue<T>(object obj, string name)
        {
            var value = GetMemberValue(obj, name);
            if (value is T t) return t;
            return default;
        }

        [HarmonyPatch]
        public static class CreateInteraction_Patch
        {
            static bool Prepare()
            {
                ResolveRimTalkTypes();
                if (TalkServiceType == null || TalkResponseType == null)
                {
                    TTSLogger.Warning("CreateInteraction patch SKIPPED: RimTalk types not found", "Patch");
                    return false;
                }
                var method = AccessTools.Method(TalkServiceType, "CreateInteraction", new[] { typeof(Pawn), TalkResponseType });
                bool ok = method != null;
                TTSLogger.Info($"CreateInteraction patch: {(ok ? "OK" : "FAIL")}", "Patch");
                return ok;
            }

            static MethodBase TargetMethod()
            {
                return AccessTools.Method(TalkServiceType, "CreateInteraction", new[] { typeof(Pawn), TalkResponseType });
            }

            static bool Prefix(Pawn pawn, object talk)
            {
                try
                {
                    if (!TTSActive()) return true;
                    if (pawn == null || talk == null) return true;

                    var dialogueId = GetMemberValue<Guid>(talk, "Id");

                    if (Service.AudioPlaybackService.IsCurrentlyPlaying())
                        return false;

                    if (IsBlocked(dialogueId))
                        return false;

                    var settings = Data.TTSConfig.Settings;
                    float volume = settings?.Volume ?? 0.8f;
                    Service.AudioPlaybackService.PlayAudio(dialogueId, pawn, volume);
                    return true;
                }
                catch (Exception ex)
                {
                    TTSLogger.Error($"CreateInteraction: {ex}", "Patch");
                    return true;
                }
            }
        }

        [HarmonyPatch]
        public static class TalkResponsesAdd_Patch
        {
            static bool Prepare()
            {
                ResolveRimTalkTypes();
                if (TalkResponseType == null)
                {
                    TTSLogger.Warning("TalkResponsesAdd patch SKIPPED: TalkResponse type not found", "Patch");
                    return false;
                }
                TTSLogger.Info("TalkResponsesAdd patch: OK", "Patch");
                return true;
            }

            static MethodBase TargetMethod()
            {
                var listType = typeof(List<>).MakeGenericType(TalkResponseType);
                return AccessTools.Method(listType, "Add", new[] { TalkResponseType });
            }

            static void Postfix(object __instance)
            {
                try
                {
                    if (!TTSActive()) return;
                    if (__instance == null) return;

                    if (!_listToPawnMap.TryGetValue(__instance, out Pawn pawn)) return;

                    var list = __instance as System.Collections.IList;
                    if (list == null || list.Count == 0) return;

                    var item = list[list.Count - 1];
                    if (item == null) return;

                    var dialogueId = GetMemberValue<Guid>(item, "Id");
                    var text = GetMemberValue(item, "Text") as string;
                    if (string.IsNullOrEmpty(text)) return;

                    RequestBlock(dialogueId);

                    var settings = Data.TTSConfig.Settings;
                    string persona = Service.TTSService.GetPersonaOrDefault(pawn);
                    string providerName = settings.Provider.ToString();

                    var evtLog = new TTSEventLog
                    {
                        PawnName = pawn.LabelShort,
                        Channel = providerName,
                        Voice = settings.Provider == Data.TTSSettings.TTSProvider.EdgeTTS ? settings.EdgeVoice : settings.MiMoVoice,
                        InputText = text,
                        Persona = persona,
                        Model = settings.Provider == Data.TTSSettings.TTSProvider.MiMoTTS ? settings.MiMoModel : "",
                        IsStreaming = settings.EnableStreaming,
                        EventState = TTSEventLog.State.Pending
                    };
                    TTSEventHistory.AddForDialogue(evtLog, dialogueId);

                    TTSLogger.Info($"Dialogue: {pawn.LabelShort} text={text.Substring(0, Math.Min(50, text.Length))}...", "Dialogue");

                    Task.Run(async () =>
                    {
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        try
                        {
                            TTSStats.RecordRequest();
                            byte[] audio = await Service.TTSService.GenerateSpeechAsync(text, persona, settings);
                            sw.Stop();

                            if (audio != null && audio.Length > 0)
                            {
                                TTSStats.RecordSuccess(audio.Length, sw.ElapsedMilliseconds);
                                evtLog.AudioBytes = audio.Length;
                                evtLog.ElapsedMs = sw.ElapsedMilliseconds;
                                evtLog.EventState = TTSEventLog.State.Success;
                                TTSLogger.Info($"TTS OK: {pawn.LabelShort} {audio.Length / 1024}KB {sw.ElapsedMilliseconds}ms", "TTS");

                                if (IsBlocked(dialogueId))
                                {
                                    Service.AudioPlaybackService.SetAudioResult(dialogueId, audio);
                                    ReleaseBlock(dialogueId);
                                }
                            }
                            else
                            {
                                TTSStats.RecordFailure("no audio");
                                evtLog.ElapsedMs = sw.ElapsedMilliseconds;
                                evtLog.EventState = TTSEventLog.State.Failed;
                                evtLog.ErrorMessage = "no audio data";
                                TTSLogger.Warning($"TTS empty: {pawn.LabelShort}", "TTS");
                                ReleaseBlock(dialogueId);
                            }
                        }
                        catch (Exception ex)
                        {
                            sw.Stop();
                            TTSStats.RecordFailure(ex.Message);
                            evtLog.ElapsedMs = sw.ElapsedMilliseconds;
                            evtLog.EventState = TTSEventLog.State.Failed;
                            evtLog.ErrorMessage = ex.Message;
                            TTSLogger.Error($"TTS failed: {pawn.LabelShort} {ex.Message}", "TTS");
                            ReleaseBlock(dialogueId);
                        }
                    });
                }
                catch (Exception ex)
                {
                    TTSLogger.Error($"TalkResponsesAdd: {ex}", "Patch");
                    TTSLogger.WarningNotify($"对话拦截异常: {ex.Message}", "Patch");
                }
            }
        }

        [HarmonyPatch]
        public static class PawnStateCtor_Patch
        {
            static bool Prepare()
            {
                ResolveRimTalkTypes();
                if (PawnStateType == null)
                {
                    TTSLogger.Warning("PawnStateCtor patch SKIPPED: PawnState not found", "Patch");
                    return false;
                }
                var ctor = PawnStateType.GetConstructor(new[] { typeof(Pawn) });
                bool ok = ctor != null;
                TTSLogger.Info($"PawnStateCtor patch: {(ok ? "OK" : "FAIL (no Pawn ctor)")}", "Patch");
                return ok;
            }

            static MethodBase TargetMethod()
            {
                return PawnStateType.GetConstructor(new[] { typeof(Pawn) });
            }

            static void Postfix(object __instance, Pawn pawn)
            {
                try
                {
                    if (pawn == null || __instance == null) return;
                    var list = GetMemberValue(__instance, "TalkResponses");
                    if (list != null)
                    {
                        _listToPawnMap.Remove(list);
                        _listToPawnMap.Add(list, pawn);
                    }
                }
                catch { }
            }
        }

        [HarmonyPatch]
        public static class AddIgnored_Patch
        {
            static bool Prepare()
            {
                ResolveRimTalkTypes();
                if (TalkHistoryType == null)
                {
                    TTSLogger.Warning("AddIgnored patch SKIPPED: TalkHistory not found", "Patch");
                    return false;
                }
                var method = AccessTools.Method(TalkHistoryType, "AddIgnored", new[] { typeof(Guid) });
                bool ok = method != null;
                TTSLogger.Info($"AddIgnored patch: {(ok ? "OK" : "FAIL")}", "Patch");
                return ok;
            }

            static MethodBase TargetMethod()
            {
                return AccessTools.Method(TalkHistoryType, "AddIgnored", new[] { typeof(Guid) });
            }

            static void Prefix(Guid id)
            {
                if (!TTSActive()) return;
                if (id == Guid.Empty) return;

                if (IsBlocked(id))
                {
                    ReleaseBlock(id);
                    Service.AudioPlaybackService.RemovePendingAudio(id);
                    TTSStats.RecordCancel();
                    var evt = TTSEventHistory.FindByDialogueId(id);
                    if (evt != null) evt.EventState = TTSEventLog.State.Cancelled;
                }
            }
        }

        [HarmonyPatch]
        public static class IgnoreTalkResponse_Patch
        {
            static bool Prepare()
            {
                ResolveRimTalkTypes();
                if (PawnStateType == null)
                {
                    TTSLogger.Warning("IgnoreTalkResponse patch SKIPPED: PawnState not found", "Patch");
                    return false;
                }
                var method = AccessTools.Method(PawnStateType, "IgnoreTalkResponse");
                bool ok = method != null;
                TTSLogger.Info($"IgnoreTalkResponse patch: {(ok ? "OK" : "FAIL")}", "Patch");
                return ok;
            }

            static MethodBase TargetMethod()
            {
                return AccessTools.Method(PawnStateType, "IgnoreTalkResponse");
            }

            static void Prefix(object __instance)
            {
                try
                {
                    if (!TTSActive()) return;
                    if (__instance == null) return;

                    var list = GetMemberValue(__instance, "TalkResponses") as System.Collections.IList;
                    if (list == null || list.Count == 0) return;

                    var item = list[0];
                    var dialogueId = GetMemberValue<Guid>(item, "Id");

                    if (IsBlocked(dialogueId))
                    {
                        ReleaseBlock(dialogueId);
                        Service.AudioPlaybackService.RemovePendingAudio(dialogueId);
                        TTSStats.RecordCancel();
                        var evt = TTSEventHistory.FindByDialogueId(dialogueId);
                        if (evt != null) evt.EventState = TTSEventLog.State.Cancelled;
                    }
                }
                catch { }
            }
        }

        [HarmonyPatch]
        public static class StartedNewGame_Patch
        {
            static bool Prepare()
            {
                ResolveRimTalkTypes();
                if (RimTalkMainType == null)
                {
                    TTSLogger.Warning("StartedNewGame patch SKIPPED: RimTalk type not found", "Patch");
                    return false;
                }
                var method = RimTalkMainType.GetMethod("StartedNewGame", BindingFlags.Public | BindingFlags.Instance);
                if (method != null)
                {
                    TTSLogger.Info("StartedNewGame patch: OK", "Patch");
                    return true;
                }
                method = RimTalkMainType.GetMethod("StartedNewGame", BindingFlags.NonPublic | BindingFlags.Instance);
                if (method != null)
                {
                    TTSLogger.Info("StartedNewGame patch (non-public): OK", "Patch");
                    return true;
                }
                TTSLogger.Warning("StartedNewGame patch: FAIL (method not found)", "Patch");
                return false;
            }

            static MethodBase TargetMethod()
            {
                return AccessTools.Method(RimTalkMainType, "StartedNewGame");
            }

            static void Postfix()
            {
                TTSLogger.Info("New game, resetting TTS", "Lifecycle");
                Service.TTSService.StopAll(false);
                TTSStats.Reset();
                TTSEventHistory.Clear();
                TTSLogger.ClearNotificationDedup();
            }
        }

        [HarmonyPatch]
        public static class LoadedGame_Patch
        {
            static bool Prepare()
            {
                ResolveRimTalkTypes();
                if (RimTalkMainType == null)
                {
                    TTSLogger.Warning("LoadedGame patch SKIPPED: RimTalk type not found", "Patch");
                    return false;
                }
                var method = AccessTools.Method(RimTalkMainType, "LoadedGame");
                bool ok = method != null;
                TTSLogger.Info($"LoadedGame patch: {(ok ? "OK" : "FAIL")}", "Patch");
                return ok;
            }

            static MethodBase TargetMethod()
            {
                return AccessTools.Method(RimTalkMainType, "LoadedGame");
            }

            static void Postfix()
            {
                TTSLogger.Info("Game loaded, resetting TTS", "Lifecycle");
                Service.TTSService.StopAll(false);
                TTSStats.Reset();
                TTSEventHistory.Clear();
                TTSLogger.ClearNotificationDedup();
            }
        }

        [HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
        public static class TogglePatch
        {
            private static readonly Texture2D ToggleIcon = ContentFinder<Texture2D>.Get("UI/ToggleButton");

            public static void Postfix(WidgetRow row, bool worldView)
            {
                if (worldView || row == null) return;

                var settings = Data.TTSConfig.Settings;
                if (settings == null) return;

                bool onOff = settings.EnableTTS;
                row.ToggleableIcon(ref onOff, ToggleIcon, "RimTalk TTS Simple",
                    SoundDefOf.Mouseover_ButtonToggle);

                if (onOff != settings.EnableTTS)
                {
                    settings.EnableTTS = onOff;
                    settings.Write();

                    string msg = onOff ? "RimTalk TTS 已开启" : "RimTalk TTS 已关闭";
                    Messages.Message(msg, MessageTypeDefOf.TaskCompletion, false);

                    if (!onOff)
                    {
                        Service.TTSService.StopAll(false);
                    }
                }
            }
        }
    }
}
