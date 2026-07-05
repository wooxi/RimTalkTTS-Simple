using HarmonyLib;
using System;
using System.Collections.Generic;
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

        private static bool TTSActive()
        {
            return Data.TTSConfig.IsEnabled;
        }

        public static void RequestBlock(Guid dialogueId)
        {
            lock (_blockLock) { blockedDialogues.Add(dialogueId); }
            TTSLogger.Debug($"Blocked dialogue {dialogueId}", "Block");
        }

        public static void ReleaseBlock(Guid dialogueId)
        {
            lock (_blockLock) { blockedDialogues.Remove(dialogueId); }
            TTSLogger.Debug($"Released dialogue {dialogueId}", "Block");
        }

        public static bool IsBlocked(Guid dialogueId)
        {
            lock (_blockLock) { return blockedDialogues.Contains(dialogueId); }
        }

        public static void ClearAllBlocks()
        {
            lock (_blockLock) { blockedDialogues.Clear(); }
        }

        [HarmonyPatch]
        public static class CreateInteraction_Patch
        {
            static bool Prepare()
            {
                var method = AccessTools.Method("RimTalk.Service.TalkService:CreateInteraction", new[] { typeof(Pawn), Type.GetType("RimTalk.Data.TalkResponse, RimTalk") });
                bool ok = method != null;
                TTSLogger.Info($"CreateInteraction patch prepare: {(ok ? "OK" : "FAIL (method not found)")}", "Patch");
                return ok;
            }

            static MethodBase TargetMethod()
            {
                return AccessTools.Method("RimTalk.Service.TalkService:CreateInteraction", new[] { typeof(Pawn), Type.GetType("RimTalk.Data.TalkResponse, RimTalk") });
            }

            static bool Prefix(Pawn pawn, object talk)
            {
                try
                {
                    if (!TTSActive()) return true;
                    if (pawn == null || talk == null) return true;

                    var idField = talk.GetType().GetField("Id");
                    if (idField == null) return true;
                    var dialogueId = (Guid)idField.GetValue(talk);

                    if (Service.AudioPlaybackService.IsCurrentlyPlaying())
                    {
                        TTSLogger.Debug($"CreateInteraction blocked (audio playing): {pawn.LabelShort}", "Patch");
                        return false;
                    }

                    if (IsBlocked(dialogueId))
                    {
                        TTSLogger.Debug($"CreateInteraction blocked (still generating): {pawn.LabelShort}", "Patch");
                        return false;
                    }

                    var settings = Data.TTSConfig.Settings;
                    float volume = settings?.Volume ?? 0.8f;
                    Service.AudioPlaybackService.PlayAudio(dialogueId, pawn, volume);
                    TTSLogger.Debug($"CreateInteraction playing audio: {pawn.LabelShort}, id={dialogueId}", "Patch");
                    return true;
                }
                catch (Exception ex)
                {
                    TTSLogger.Error($"CreateInteraction Prefix: {ex}", "Patch");
                    return true;
                }
            }
        }

        [HarmonyPatch]
        public static class TalkResponsesAdd_Patch
        {
            static bool Prepare()
            {
                bool ok = Type.GetType("RimTalk.Data.TalkResponse, RimTalk") != null;
                TTSLogger.Info($"TalkResponsesAdd patch prepare: {(ok ? "OK" : "FAIL (RimTalk type not found)")}", "Patch");
                return ok;
            }

            static MethodBase TargetMethod()
            {
                var listType = typeof(List<>).MakeGenericType(Type.GetType("RimTalk.Data.TalkResponse, RimTalk"));
                return AccessTools.Method(listType, "Add", new[] { Type.GetType("RimTalk.Data.TalkResponse, RimTalk") });
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

                    var idField = item.GetType().GetField("Id");
                    var textField = item.GetType().GetField("Text");
                    if (idField == null || textField == null) return;

                    var dialogueId = (Guid)idField.GetValue(item);
                    var text = textField.GetValue(item) as string;

                    if (string.IsNullOrEmpty(text)) return;

                    var settings = Data.TTSConfig.Settings;
                    string persona = Service.TTSService.GetPersonaOrDefault(pawn);
                    string providerName = settings.Provider.ToString();
                    string voice = settings.Provider == Data.TTSSettings.TTSProvider.EdgeTTS
                        ? settings.EdgeVoice : settings.MiMoVoice;

                    TTSLogger.Info($"Dialogue intercepted: {pawn.LabelShort}, text={text.Substring(0, Math.Min(40, text.Length))}..., provider={providerName}", "Dialogue");

                    var evtLog = new TTSEventLog
                    {
                        PawnName = pawn.LabelShort,
                        Channel = providerName,
                        Voice = voice,
                        InputText = text,
                        Persona = persona,
                        Model = settings.Provider == Data.TTSSettings.TTSProvider.MiMoTTS ? settings.MiMoModel : "",
                        IsStreaming = settings.EnableStreaming,
                        EventState = TTSEventLog.State.Pending
                    };
                    TTSEventHistory.AddForDialogue(evtLog, dialogueId);

                    RequestBlock(dialogueId);

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
                                TTSLogger.Info($"TTS success: {pawn.LabelShort}, {audio.Length / 1024}KB, {sw.ElapsedMilliseconds}ms", "TTS");

                                if (RimTalkPatches.IsBlocked(dialogueId))
                                {
                                    Service.AudioPlaybackService.SetAudioResult(dialogueId, audio);
                                    ReleaseBlock(dialogueId);
                                }
                            }
                            else
                            {
                                TTSStats.RecordFailure("API returned empty data");
                                evtLog.ElapsedMs = sw.ElapsedMilliseconds;
                                evtLog.EventState = TTSEventLog.State.Failed;
                                evtLog.ErrorMessage = "API返回空数据";
                                TTSLogger.Warning($"TTS returned empty: {pawn.LabelShort}", "TTS");
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
                            TTSLogger.Error($"TTS generation failed: {pawn.LabelShort} - {ex.Message}", "TTS");
                            ReleaseBlock(dialogueId);
                        }
                    });
                }
                catch (Exception ex)
                {
                    TTSLogger.Error($"TalkResponsesAdd Postfix: {ex}", "Patch");
                }
            }
        }

        [HarmonyPatch]
        public static class PawnStateCtor_Patch
        {
            static bool Prepare()
            {
                var type = Type.GetType("RimTalk.Data.PawnState, RimTalk");
                bool ok = type != null;
                TTSLogger.Info($"PawnStateCtor patch prepare: {(ok ? "OK" : "FAIL (RimTalk type not found)")}", "Patch");
                return ok;
            }

            static MethodBase TargetMethod()
            {
                var type = Type.GetType("RimTalk.Data.PawnState, RimTalk");
                return AccessTools.Constructor(type, new[] { typeof(Pawn) });
            }

            static void Postfix(object __instance, Pawn pawn)
            {
                try
                {
                    if (pawn == null || __instance == null) return;
                    var prop = __instance.GetType().GetProperty("TalkResponses", BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null)
                    {
                        var list = prop.GetValue(__instance);
                        _listToPawnMap.Remove(list);
                        _listToPawnMap.Add(list, pawn);
                        TTSLogger.Debug($"Registered pawn dialogue list: {pawn.LabelShort}", "Patch");
                    }
                }
                catch (Exception ex)
                {
                    TTSLogger.Debug($"PawnStateCtor failed: {ex.Message}", "Patch");
                }
            }
        }

        [HarmonyPatch]
        public static class AddIgnored_Patch
        {
            static bool Prepare()
            {
                bool ok = Type.GetType("RimTalk.Data.TalkHistory, RimTalk") != null;
                TTSLogger.Info($"AddIgnored patch prepare: {(ok ? "OK" : "FAIL (RimTalk type not found)")}", "Patch");
                return ok;
            }

            static MethodBase TargetMethod()
            {
                return AccessTools.Method("RimTalk.Data.TalkHistory:AddIgnored", new[] { typeof(Guid) });
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
                    TTSLogger.Debug($"Dialogue ignored: {id}", "Dialogue");
                }
            }
        }

        [HarmonyPatch]
        public static class IgnoreTalkResponse_Patch
        {
            static bool Prepare()
            {
                var type = Type.GetType("RimTalk.Data.PawnState, RimTalk");
                bool ok = type != null;
                TTSLogger.Info($"IgnoreTalkResponse patch prepare: {(ok ? "OK" : "FAIL (RimTalk type not found)")}", "Patch");
                return ok;
            }

            static MethodBase TargetMethod()
            {
                return AccessTools.Method("RimTalk.Data.PawnState:IgnoreTalkResponse");
            }

            static void Prefix(object __instance)
            {
                try
                {
                    if (!TTSActive()) return;
                    if (__instance == null) return;

                    var prop = __instance.GetType().GetProperty("TalkResponses", BindingFlags.Public | BindingFlags.Instance);
                    if (prop == null) return;

                    var list = prop.GetValue(__instance) as System.Collections.IList;
                    if (list == null || list.Count == 0) return;

                    var item = list[0];
                    var idField = item.GetType().GetField("Id");
                    if (idField == null) return;

                    var dialogueId = (Guid)idField.GetValue(item);

                    if (IsBlocked(dialogueId))
                    {
                        ReleaseBlock(dialogueId);
                        Service.AudioPlaybackService.RemovePendingAudio(dialogueId);
                        TTSStats.RecordCancel();
                        var evt = TTSEventHistory.FindByDialogueId(dialogueId);
                        if (evt != null) evt.EventState = TTSEventLog.State.Cancelled;
                        TTSLogger.Debug($"TalkResponse ignored: {dialogueId}", "Dialogue");
                    }
                }
                catch (Exception ex)
                {
                    TTSLogger.Debug($"IgnoreTalkResponse failed: {ex.Message}", "Patch");
                }
            }
        }

        [HarmonyPatch]
        public static class StartedNewGame_Patch
        {
            static MethodBase TargetMethod()
            {
                return AccessTools.Method("RimTalk.RimTalk:StartedNewGame");
            }

            static void Postfix()
            {
                TTSLogger.Info("New game started, resetting TTS", "Lifecycle");
                Service.TTSService.StopAll(false);
                TTSStats.Reset();
                TTSEventHistory.Clear();
            }
        }

        [HarmonyPatch]
        public static class LoadedGame_Patch
        {
            static MethodBase TargetMethod()
            {
                return AccessTools.Method("RimTalk.RimTalk:LoadedGame");
            }

            static void Postfix()
            {
                TTSLogger.Info("Game loaded, resetting TTS", "Lifecycle");
                Service.TTSService.StopAll(false);
                TTSStats.Reset();
                TTSEventHistory.Clear();
            }
        }
    }
}
