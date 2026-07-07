using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using RimTalkTTS.Simple.Data;
using RimTalkTTS.Simple.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalkTTS.Simple.Patch
{
    public static class OverlayButtonPatch
    {
        private const float BtnPadding = 5f;
        private const float BtnHeight = 22f;
        private const float BtnTextPadding = 8f;

        private static readonly string[] BtnLabels = { "TTS 重置", "TTS 测试", "强制对话", "忽略全部", "TTS 调试" };
        private static readonly Color[] BtnColors = {
            new Color(0.6f, 0.3f, 0.3f),
            new Color(0.25f, 0.45f, 0.25f),
            new Color(0.3f, 0.35f, 0.5f),
            new Color(0.45f, 0.35f, 0.25f),
            new Color(0.35f, 0.3f, 0.5f),
        };

        private static Rect[] _btnScreenRects = new Rect[0];

        [HarmonyPatch]
        public static class OverlayMapComponentOnGUI_Patch
        {
            static bool Prepare()
            {
                RimTalkPatches.ResolveRimTalkTypes();
                bool ok = RimTalkPatches.OverlayType != null;
                if (!ok) TTSLogger.Warning("OverlayButtonPatch SKIPPED: Overlay type not found", "Overlay");
                return ok;
            }

            static MethodBase TargetMethod()
            {
                return AccessTools.Method(RimTalkPatches.OverlayType, "MapComponentOnGUI");
            }

            static void Postfix(object __instance)
            {
                try
                {
                    if (__instance == null) return;
                    if (!TTSConfig.IsEnabled) return;

                    var field = AccessTools.Field(RimTalkPatches.OverlayType, "_gearIconScreenRect");
                    if (field == null) return;

                    Rect gearRect = (Rect)field.GetValue(__instance);
                    if (gearRect.width <= 0 || gearRect.height <= 0) return;

                    if (TTSConfig.Settings?.EnableTTS != true) return;

                    DrawOverlayButtons(gearRect);
                }
                catch (Exception ex)
                {
                    TTSLogger.Error($"Overlay: {ex.Message}", "Overlay");
                }
            }
        }

        [HarmonyPatch]
        public static class OverlayHandleInput_Patch
        {
            static bool Prepare()
            {
                RimTalkPatches.ResolveRimTalkTypes();
                return RimTalkPatches.OverlayType != null;
            }

            static MethodBase TargetMethod()
            {
                return AccessTools.Method(RimTalkPatches.OverlayType, "HandleInput");
            }

            static bool Prefix(object __instance)
            {
                if (!TTSConfig.IsEnabled) return true;
                if (__instance == null) return true;
                if (TTSConfig.Settings?.EnableTTS != true) return true;

                Event currentEvent = Event.current;
                if (currentEvent == null) return true;
                if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0) return true;

                for (int i = 0; i < _btnScreenRects.Length && i < BtnLabels.Length; i++)
                {
                    if (_btnScreenRects[i].Contains(currentEvent.mousePosition))
                    {
                        currentEvent.Use();
                        ExecuteBtnAction(i);
                        return false;
                    }
                }

                return true;
            }
        }

        private static void DrawOverlayButtons(Rect gearRect)
        {
            float totalWidth = 0;
            float[] widths = new float[BtnLabels.Length];
            for (int i = 0; i < BtnLabels.Length; i++)
            {
                widths[i] = Text.CalcSize(BtnLabels[i]).x + BtnTextPadding * 2;
                totalWidth += widths[i];
            }
            totalWidth += (BtnLabels.Length - 1) * BtnPadding;

            float x = gearRect.x - totalWidth;
            float y = gearRect.y;

            _btnScreenRects = new Rect[BtnLabels.Length];

            var prevColor = GUI.color;
            Text.Font = GameFont.Tiny;

            for (int i = 0; i < BtnLabels.Length; i++)
            {
                Rect btnRect = new Rect(x, y, widths[i], BtnHeight);
                _btnScreenRects[i] = btnRect;
                GUI.color = BtnColors[i];
                Widgets.ButtonText(btnRect, BtnLabels[i]);
                x += widths[i] + BtnPadding;
            }

            GUI.color = prevColor;
            Text.Font = GameFont.Small;
        }

        private static void ExecuteBtnAction(int idx)
        {
            switch (idx)
            {
                case 0:
                    Service.TTSService.StopAll(false);
                    Messages.Message("TTS 已重置", MessageTypeDefOf.TaskCompletion, false);
                    break;
                case 1: RunTestTTS(); break;
                case 2: GenerateTalkForce(); break;
                case 3: IgnoreAllTalks(); break;
                case 4:
                    Find.WindowStack.Add(new RimTalkTTS.Simple.UI.TTSDebugWindow());
                    break;
            }
        }

        private static void RunTestTTS()
        {
            Task.Run(async () =>
            {
                try
                {
                    string text = "你好，这是 RimTalk TTS Simple 的测试语音。";
                    var settings = TTSConfig.Settings;
                    byte[] audio = await Service.TTSService.GenerateSpeechAsync(text,
                        "Use a natural, clear speaking voice.", null, settings);

                    if (audio != null && audio.Length > 0)
                    {
                        var testId = Guid.NewGuid();
                        Service.AudioPlaybackService.SetAudioResult(testId, audio);
                        Service.AudioPlaybackService.PlayAudio(testId, null);
                    }
                    else
                    {
                        Messages.Message("TTS 测试失败：未生成音频", MessageTypeDefOf.NegativeEvent, false);
                    }
                }
                catch (Exception ex)
                {
                    Messages.Message($"TTS 测试失败：{ex.Message}", MessageTypeDefOf.NegativeEvent, false);
                }
            });
        }

        private static void GenerateTalkForce()
        {
            try
            {
                RimTalkPatches.ResolveRimTalkTypes();
                if (RimTalkPatches.TalkServiceType == null || RimTalkPatches.TalkRequestType == null)
                {
                    Messages.Message("无法生成对话：RimTalk 类型未找到", MessageTypeDefOf.NegativeEvent, false);
                    return;
                }

                var pawn = Find.CurrentMap?.mapPawns?.FreeColonists.FirstOrDefault();
                if (pawn == null)
                {
                    Messages.Message("无法生成对话：没有可用的殖民者", MessageTypeDefOf.NegativeEvent, false);
                    return;
                }

                var talkTypeDefault = Enum.ToObject(RimTalkPatches.TalkTypeType, 0);
                var ctor = AccessTools.Constructor(RimTalkPatches.TalkRequestType,
                    new[] { typeof(string), typeof(Pawn), typeof(Pawn), RimTalkPatches.TalkTypeType });

                object talkRequest;
                if (ctor != null)
                    talkRequest = ctor.Invoke(new object[] { null, pawn, null, talkTypeDefault });
                else
                    talkRequest = Activator.CreateInstance(RimTalkPatches.TalkRequestType, new object[] { null, pawn });

                var generateTalkMethod = AccessTools.Method(RimTalkPatches.TalkServiceType, "GenerateTalk",
                    new[] { RimTalkPatches.TalkRequestType });

                bool result = generateTalkMethod != null &&
                    (bool)generateTalkMethod.Invoke(null, new[] { talkRequest });

                Messages.Message(result ? "正在生成对话..." : "生成对话失败",
                    result ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.NegativeEvent, false);
            }
            catch (Exception ex)
            {
                Messages.Message($"强制对话失败：{ex.Message}", MessageTypeDefOf.NegativeEvent, false);
            }
        }

        private static void IgnoreAllTalks()
        {
            try
            {
                RimTalkPatches.ResolveRimTalkTypes();
                if (RimTalkPatches.CacheType == null) return;

                var getAllMethod = AccessTools.Method(RimTalkPatches.CacheType, "GetAll");
                if (getAllMethod == null) return;

                var all = getAllMethod.Invoke(null, null) as IEnumerable;
                if (all == null) return;

                int count = 0;
                foreach (var pawnState in all)
                {
                    var ignoreMethod = AccessTools.Method(RimTalkPatches.PawnStateType, "IgnoreAllTalkResponses");
                    if (ignoreMethod != null)
                    {
                        ignoreMethod.Invoke(pawnState, new object[] { null });
                        count++;
                    }
                }

                Messages.Message($"已忽略 {count} 个角色的待显示对话", MessageTypeDefOf.TaskCompletion, false);
            }
            catch (Exception ex)
            {
                Messages.Message($"忽略对话失败：{ex.Message}", MessageTypeDefOf.NegativeEvent, false);
            }
        }
    }
}
