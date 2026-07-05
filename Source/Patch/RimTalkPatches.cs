using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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

        [HarmonyPatch]
        public static class CreateInteraction_Patch
        {
            static bool Prepare()
            {
                var method = AccessTools.Method("RimTalk.Service.TalkService:CreateInteraction", new[] { typeof(Pawn), Type.GetType("RimTalk.Data.TalkResponse, RimTalk") });
                return method != null;
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
                    Log.Error($"[RimTalkTTS.Simple] CreateInteraction Prefix: {ex}");
                    return true;
                }
            }
        }

        [HarmonyPatch]
        public static class TalkResponsesAdd_Patch
        {
            static bool Prepare()
            {
                return Type.GetType("RimTalk.Data.TalkResponse, RimTalk") != null;
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

                    RequestBlock(dialogueId);

                    Task.Run(async () =>
                    {
                        try
                        {
                            var settings = Data.TTSConfig.Settings;
                            string persona = Service.TTSService.GetPersonaOrDefault(pawn);
                            byte[] audio = await Service.TTSService.GenerateSpeechAsync(text, persona, settings);

                            if (RimTalkPatches.IsBlocked(dialogueId))
                            {
                                Service.AudioPlaybackService.SetAudioResult(dialogueId, audio);
                                ReleaseBlock(dialogueId);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[RimTalkTTS.Simple] TTS generation: {ex.Message}");
                            ReleaseBlock(dialogueId);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalkTTS.Simple] TalkResponsesAdd: {ex}");
                }
            }
        }

        [HarmonyPatch]
        public static class PawnStateCtor_Patch
        {
            static bool Prepare()
            {
                var type = Type.GetType("RimTalk.Data.PawnState, RimTalk");
                return type != null;
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
                        _listToPawnMap.Remove(prop.GetValue(__instance));
                        _listToPawnMap.Add(prop.GetValue(__instance), pawn);
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
                return Type.GetType("RimTalk.Data.TalkHistory, RimTalk") != null;
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
                }
            }
        }

        [HarmonyPatch]
        public static class IgnoreTalkResponse_Patch
        {
            static bool Prepare()
            {
                var type = Type.GetType("RimTalk.Data.PawnState, RimTalk");
                return type != null;
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
                    }
                }
                catch { }
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
                Service.TTSService.StopAll(false);
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
                Service.TTSService.StopAll(false);
            }
        }
    }
}
