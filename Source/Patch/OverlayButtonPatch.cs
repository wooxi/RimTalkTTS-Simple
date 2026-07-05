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
        private static bool _statusBarExpanded = false;
        private const float BtnPadding = 5f;
        private const float BtnHeight = 22f;
        private const float BtnTextPadding = 8f;

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
                    DrawStatusBar(gearRect);
                }
                catch (Exception ex)
                {
                    TTSLogger.Error($"Overlay: {ex.Message}", "Overlay");
                }
            }
        }

        private static readonly (string label, Action action, Color color)[] Buttons = new[]
        {
            ("TTS 重置", () => {
                Service.TTSService.StopAll(false);
                Messages.Message("TTS 已重置", MessageTypeDefOf.TaskCompletion, false);
            }, new Color(0.6f, 0.3f, 0.3f)),

            ("TTS 测试", () => RunTestTTS(),
                new Color(0.25f, 0.45f, 0.25f)),

            ("强制对话", () => GenerateTalkForce(),
                new Color(0.3f, 0.35f, 0.5f)),

            ("忽略全部", () => IgnoreAllTalks(),
                new Color(0.45f, 0.35f, 0.25f)),
        };

        private static void DrawOverlayButtons(Rect gearRect)
        {
            float totalWidth = 0;
            float[] widths = new float[Buttons.Length];
            for (int i = 0; i < Buttons.Length; i++)
            {
                widths[i] = Text.CalcSize(Buttons[i].label).x + BtnTextPadding * 2;
                totalWidth += widths[i];
            }
            totalWidth += (Buttons.Length - 1) * BtnPadding;

            float x = gearRect.x - totalWidth;
            float y = gearRect.y;

            var prevColor = GUI.color;
            Text.Font = GameFont.Tiny;

            for (int i = 0; i < Buttons.Length; i++)
            {
                Rect btnRect = new Rect(x, y, widths[i], BtnHeight);
                GUI.color = Buttons[i].color;

                if (Widgets.ButtonText(btnRect, Buttons[i].label))
                    Buttons[i].action();

                x += widths[i] + BtnPadding;
            }

            GUI.color = prevColor;
            Text.Font = GameFont.Small;
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

        private static void DrawStatusBar(Rect gearRect)
        {
            float barHeight = 20f;
            float barWidth = 320f;
            float x = gearRect.xMax - barWidth;
            float y = gearRect.y - barHeight - 6f;

            Rect barRect = new Rect(x, y, barWidth, barHeight);

            var settings = TTSConfig.Settings;
            string endpointUrl = settings?.UseCustomEndpoint == true
                ? settings.CustomEndpointUrl : "";
            string providerName = settings?.Provider == TTSSettings.TTSProvider.EdgeTTS ? "Edge"
                : (!string.IsNullOrEmpty(endpointUrl) ? "Custom" : "MiMo");

            string status = (TTSConfig.IsEnabled && settings?.EnableTTS == true) ? "●" : "○";
            Color statusColor = (TTSConfig.IsEnabled && settings?.EnableTTS == true)
                ? new Color(0.3f, 0.7f, 0.3f) : new Color(0.5f, 0.5f, 0.5f);

            string lastEvent = "";
            var recent = TTSEventHistory.GetRecent(1).FirstOrDefault();
            if (recent != null)
            {
                lastEvent = recent.EventState == TTSEventLog.State.Success
                    ? $"✓{recent.AudioBytes / 1024}K"
                    : recent.EventState == TTSEventLog.State.Failed
                    ? "✗" : "...";
            }

            string stats = $"{TTSStats.TotalRequests}R/{TTSStats.TotalSuccess}S/{TTSStats.TotalFailed}F";

            Widgets.DrawBoxSolid(barRect, new Color(0.08f, 0.08f, 0.1f, 0.92f));

            var innerRect = barRect.ContractedBy(3f);
            Text.Font = GameFont.Tiny;

            Rect statusRect = new Rect(innerRect.x, innerRect.y, 12f, innerRect.height);
            GUI.color = statusColor;
            Widgets.Label(statusRect, status);
            GUI.color = Color.white;

            string text = $"{providerName} {lastEvent} | {stats}";
            if (_statusBarExpanded) text += " ▼";
            Rect textRect = new Rect(statusRect.xMax + 2f, innerRect.y, innerRect.width - 14f, innerRect.height);
            Widgets.Label(textRect, text);
            Text.Font = GameFont.Small;

            if (Widgets.ButtonInvisible(barRect))
                _statusBarExpanded = !_statusBarExpanded;

            if (_statusBarExpanded)
                DrawExpandedInfo(new Rect(x, y - 170f, barWidth, 160f));
        }

        private static Vector2 _expScrollPos;

        private static void DrawExpandedInfo(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.12f, 0.95f));

            var inner = rect.ContractedBy(4f);
            float lineH = 13f;
            float y = inner.y;
            Text.Font = GameFont.Tiny;

            var settings = TTSConfig.Settings;
            Widgets.Label(new Rect(inner.x, y, inner.width, lineH),
                $"渠道: {(settings?.Provider == TTSSettings.TTSProvider.EdgeTTS ? "Edge TTS" : "MiMo TTS")}  "
                + $"端点: {(settings?.UseCustomEndpoint == true ? settings.CustomEndpointUrl : "默认")}");
            y += lineH;

            string voice = settings?.Provider == TTSSettings.TTSProvider.EdgeTTS ? settings?.EdgeVoice : settings?.MiMoVoice;
            Widgets.Label(new Rect(inner.x, y, inner.width, lineH), $"音色: {voice}  模型: {settings?.GetEffectiveModel()}");
            y += lineH;

            Widgets.Label(new Rect(inner.x, y, inner.width, lineH),
                $"请求:{TTSStats.TotalRequests} 成功:{TTSStats.TotalSuccess} 失败:{TTSStats.TotalFailed} 取消:{TTSStats.TotalCancelled}");
            y += lineH;

            if (TTSStats.TotalSuccess > 0)
                Widgets.Label(new Rect(inner.x, y, inner.width, lineH),
                    $"平均延迟: {TTSStats.AvgElapsedMs:F0}ms 平均: {TTSStats.AvgAudioBytes / 1024}KB");
            y += lineH + 3f;

            Widgets.Label(new Rect(inner.x, y, inner.width, lineH), "最近事件:");
            y += lineH;

            var events = TTSEventHistory.GetRecent(6);
            float listH = rect.yMax - y - 4f;
            float contentH = events.Count * lineH + 4f;
            Rect listOuter = new Rect(inner.x, y, inner.width, listH);
            Rect listInner = new Rect(0, 0, listOuter.width - 10f, contentH);

            Widgets.BeginScrollView(listOuter, ref _expScrollPos, listInner);

            float ey = 0;
            foreach (var evt in events)
            {
                GUI.color = evt.GetColor();
                string line = $"{evt.Timestamp:HH:mm:ss} {(evt.EventState == TTSEventLog.State.Success ? "✓" : evt.EventState == TTSEventLog.State.Failed ? "✗" : "…")} ";
                GUI.color = Color.white;
                line += $"{evt.PawnName?.Substring(0, Math.Min(6, evt.PawnName?.Length ?? 0)) ?? "-"} | {evt.ElapsedMs}ms";
                if (evt.AudioBytes > 0) line += $" | {evt.AudioBytes / 1024}KB";
                Widgets.Label(new Rect(2f, ey, listInner.width - 4f, lineH), line);
                ey += lineH;
            }

            Widgets.EndScrollView();
            Text.Font = GameFont.Small;
        }
    }
}
