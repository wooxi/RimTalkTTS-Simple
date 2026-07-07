using System;
using System.Linq;
using RimTalkTTS.Simple.Data;
using RimTalkTTS.Simple.Patch;
using RimTalkTTS.Simple.Service;
using RimTalkTTS.Simple.Util;
using UnityEngine;
using Verse;

namespace RimTalkTTS.Simple.UI
{
    public class TTSDebugWindow : Window
    {
        private int _selectedIndex = -1;
        private Vector2 _listScrollPos = Vector2.zero;
        private Vector2 _detailScrollPos = Vector2.zero;
        private bool _showStatus = true;
        private bool _showRimTalk = false;
        private bool _showStats = true;

        public TTSDebugWindow()
        {
            doCloseX = true;
            draggable = true;
            closeOnAccept = false;
            closeOnCancel = true;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
        }

        public override Vector2 InitialSize => new Vector2(780f, 560f);

        public override void DoWindowContents(Rect inRect)
        {
            float leftW = inRect.width * 0.4f;
            float rightW = inRect.width - leftW - 8f;

            Rect leftRect = new Rect(inRect.x, inRect.y, leftW, inRect.height);
            Rect rightRect = new Rect(leftRect.xMax + 8f, inRect.y, rightW, inRect.height);

            Widgets.DrawBoxSolid(leftRect, new Color(0.08f, 0.08f, 0.1f, 0.6f));
            Widgets.DrawBoxSolid(rightRect, new Color(0.08f, 0.08f, 0.1f, 0.6f));

            float rightY = 0;
            DrawTopInfo(rightRect, ref rightY);
            DrawEventList(leftRect.ContractedBy(4f));
            DrawEventDetail(new Rect(rightRect.x, rightRect.y + rightY, rightRect.width, rightRect.height - rightY).ContractedBy(4f));
        }

        private void DrawTopInfo(Rect rect, ref float totalY)
        {
            var inner = rect.ContractedBy(4f);
            float y = inner.y;
            float lineH = 14f;
            Text.Font = GameFont.Tiny;

            var settings = TTSConfig.Settings;

            if (CollapseHeader(inner, ref y, lineH, "运行状态", ref _showStatus) && _showStatus)
            {
                string channel = settings?.Provider == TTSSettings.TTSProvider.EdgeTTS
                    ? "Edge TTS" : $"MiMo TTS ({settings?.GetEffectiveModel()})";
                Widgets.Label(new Rect(inner.x + 8f, y, inner.width - 8f, lineH), $"渠道: {channel}");
                y += lineH;

                string endpoint = settings?.UseCustomEndpoint == true ? settings.CustomEndpointUrl : "默认";
                if (endpoint.Length > 50) endpoint = endpoint.Substring(0, 50) + "...";
                Widgets.Label(new Rect(inner.x + 8f, y, inner.width - 8f, lineH), $"端点: {endpoint}");
                y += lineH;

                string keyOk = settings?.Provider == TTSSettings.TTSProvider.MiMoTTS
                    && !string.IsNullOrWhiteSpace(settings?.GetEffectiveApiKey()) ? "✅" : "⚠️";
                string voice = settings?.Provider == TTSSettings.TTSProvider.EdgeTTS ? settings?.EdgeVoice : settings?.MiMoVoice;
                Widgets.Label(new Rect(inner.x + 8f, y, inner.width - 8f, lineH), $"API: {keyOk} | 音色: {voice} | 流式: {(settings?.EnableStreaming == true ? "是" : "否")}");
                y += lineH;

                int blocked = RimTalkPatches.blockedDialogues.Count;
                string playing = AudioPlaybackService.IsCurrentlyPlaying() ? "▶ 播放中" : "⏸ 空闲";
                string lastEvt = "";
                var recent = TTSEventHistory.GetRecent(1).FirstOrDefault();
                if (recent != null)
                    lastEvt = recent.EventState == TTSEventLog.State.Success
                        ? $"✓{recent.AudioBytes / 1024}K {recent.ElapsedMs}ms"
                        : recent.EventState == TTSEventLog.State.Failed ? "✗" : "…";
                Widgets.Label(new Rect(inner.x + 8f, y, inner.width - 8f, lineH), $"阻塞: {blocked} | 播放: {playing} | 最近: {lastEvt}");
                y += lineH;
            }

            y += 2f;

            if (CollapseHeader(inner, ref y, lineH, "RimTalk 连接", ref _showRimTalk) && _showRimTalk)
            {
                RimTalkPatches.ResolveRimTalkTypes();
                string asm = RimTalkPatches.RimTalkAssembly?.GetName().Name ?? "❌";
                Widgets.Label(new Rect(inner.x + 8f, y, inner.width - 8f, lineH),
                    $"程序集: {asm} | TR:{(RimTalkPatches.TalkResponseType != null ? "✓" : "✗")} PS:{(RimTalkPatches.PawnStateType != null ? "✓" : "✗")}");
                y += lineH;
                Widgets.Label(new Rect(inner.x + 8f, y, inner.width - 8f, lineH),
                    $"TH:{(RimTalkPatches.TalkHistoryType != null ? "✓" : "✗")} TS:{(RimTalkPatches.TalkServiceType != null ? "✓" : "✗")} OL:{(RimTalkPatches.OverlayType != null ? "✓" : "✗")}");
                y += lineH;
                Widgets.Label(new Rect(inner.x + 8f, y, inner.width - 8f, lineH),
                    $"Cache:{(RimTalkPatches.CacheType != null ? "✓" : "✗")} TReq:{(RimTalkPatches.TalkRequestType != null ? "✓" : "✗")}");

                try
                {
                    var harmony = new HarmonyLib.Harmony("nitoritech.rimtalk.tts.simple");
                    int cnt = harmony.GetPatchedMethods().Count();
                    Widgets.Label(new Rect(inner.x + 8f, y, inner.width - 8f, lineH), $"");
                    y += lineH;
                    Widgets.Label(new Rect(inner.x + 8f, y, inner.width - 8f, lineH), $"Patch: {cnt} active");
                }
                catch { }
                y += lineH;
            }

            y += 2f;

            if (CollapseHeader(inner, ref y, lineH, "统计", ref _showStats) && _showStats)
            {
                long total = TTSStats.TotalRequests;
                long succ = TTSStats.TotalSuccess;
                long fail = TTSStats.TotalFailed;
                long canc = TTSStats.TotalCancelled;
                string rate = total > 0 ? $"{(double)succ / total * 100:F1}%" : "-";
                Widgets.Label(new Rect(inner.x + 8f, y, inner.width - 8f, lineH),
                    $"请求: {total} | 成功: {succ} ({rate}) | 失败: {fail} | 取消: {canc}");
                y += lineH;

                if (succ > 0)
                    Widgets.Label(new Rect(inner.x + 8f, y, inner.width - 8f, lineH),
                        $"平均延迟: {TTSStats.AvgElapsedMs:F0}ms | 平均音频: {TTSStats.AvgAudioBytes / 1024}KB");
                y += lineH;

                if (!string.IsNullOrEmpty(TTSStats.LastError))
                {
                    string err = TTSStats.LastError.Length > 60 ? TTSStats.LastError.Substring(0, 60) + "..." : TTSStats.LastError;
                    Widgets.Label(new Rect(inner.x + 8f, y, inner.width - 8f, lineH), $"最近错误: {err}");
                    y += lineH;
                }
            }

            totalY = y - inner.y + 8f;
            Text.Font = GameFont.Small;
        }

        private bool CollapseHeader(Rect inner, ref float y, float lineH, string title, ref bool state)
        {
            Rect headerRect = new Rect(inner.x, y, inner.width, lineH + 4f);
            GUI.color = new Color(0.3f, 0.5f, 0.3f, 0.6f);
            Widgets.DrawBoxSolid(headerRect, new Color(0.15f, 0.25f, 0.15f, 0.5f));
            GUI.color = Color.white;

            string label = $"{(state ? "▼" : "▶")} {title}";
            Widgets.Label(headerRect.ContractedBy(2f), label);

            if (Widgets.ButtonInvisible(headerRect))
                state = !state;

            y += lineH + 5f;
            return true;
        }

        private void DrawEventList(Rect rect)
        {
            Text.Font = GameFont.Small;
            var events = TTSEventHistory.GetRecent(20);
            string title = events.Count > 0 ? $"最近 {events.Count} 条事件" : "事件日志";
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 20f), title);
            float listTop = rect.y + 22f;

            float contentH = events.Count * 30f + 10f;
            Rect listOuter = new Rect(rect.x, listTop, rect.width, rect.height - 48f);
            Rect listInner = new Rect(0, 0, listOuter.width - 16f, Mathf.Max(contentH, listOuter.height));

            Widgets.BeginScrollView(listOuter, ref _listScrollPos, listInner);

            float y = 0;
            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                bool selected = _selectedIndex == i;
                Rect rowRect = new Rect(0, y, listInner.width, 28f);

                if (selected)
                    Widgets.DrawBoxSolid(rowRect, new Color(0.25f, 0.45f, 0.25f, 0.4f));
                else
                    Widgets.DrawBoxSolid(rowRect, new Color(0.12f, 0.12f, 0.14f, 0.5f));

                Widgets.DrawHighlightIfMouseover(rowRect);

                string icon = evt.EventState == TTSEventLog.State.Success ? "✓"
                    : evt.EventState == TTSEventLog.State.Failed ? "✗"
                    : evt.EventState == TTSEventLog.State.Cancelled ? "✕" : "…";

                GUI.color = evt.GetColor();
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(rowRect.x + 4f, rowRect.y + 1f, 16f, rowRect.height), icon);
                GUI.color = Color.white;

                string info = $"{evt.Timestamp:HH:mm:ss}  {evt.PawnName?.Substring(0, Math.Min(8, evt.PawnName?.Length ?? 0)) ?? "-"}";
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(new Rect(rowRect.x + 22f, rowRect.y + 1f, listInner.width - 30f, 14f), info);
                GUI.color = Color.white;

                string info2 = $"{evt.Voice?.Substring(0, Math.Min(10, evt.Voice?.Length ?? 0)) ?? "-"}  {evt.ElapsedMs}ms";
                if (evt.AudioBytes > 0) info2 += $"  {evt.AudioBytes / 1024}KB";
                if (!string.IsNullOrEmpty(evt.ErrorMessage))
                    info2 += $"  {evt.ErrorMessage.Substring(0, Math.Min(18, evt.ErrorMessage.Length))}";
                Widgets.Label(new Rect(rowRect.x + 22f, rowRect.y + 15f, listInner.width - 30f, 12f), info2);

                if (Widgets.ButtonInvisible(rowRect))
                    _selectedIndex = (_selectedIndex == i) ? -1 : i;

                y += 30f;
            }

            Widgets.EndScrollView();

            float btnY = rect.yMax - 24f;
            if (Widgets.ButtonText(new Rect(rect.x, btnY, 60f, 22f), "清除"))
            {
                TTSEventHistory.Clear();
                TTSStats.Reset();
                _selectedIndex = -1;
            }
            if (Widgets.ButtonText(new Rect(rect.x + 66f, btnY, 60f, 22f), "重置"))
            {
                TTSStats.Reset();
            }
        }

        private void DrawEventDetail(Rect rect)
        {
            var events = TTSEventHistory.GetRecent(20);
            if (_selectedIndex < 0 || _selectedIndex >= events.Count)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                Widgets.Label(rect, "点击左侧事件查看详情");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                return;
            }

            var evt = events[_selectedIndex];
            float detailH = 500f;
            Rect inner = new Rect(0, 0, rect.width - 16f, detailH);
            Widgets.BeginScrollView(rect, ref _detailScrollPos, inner);

            float y = 0;
            float lineH = 15f;
            Text.Font = GameFont.Tiny;

            DetailLine(inner, ref y, lineH, $"时间: {evt.Timestamp:yyyy-MM-dd HH:mm:ss}");
            DetailLine(inner, ref y, lineH, $"状态: {evt.GetStateLabel()}");
            DetailLine(inner, ref y, lineH, $"角色: {evt.PawnName ?? "-"} | 渠道: {evt.Channel ?? "-"}");
            DetailLine(inner, ref y, lineH, $"模型: {evt.Model ?? "-"} | 音色: {evt.Voice ?? "-"}");
            DetailLine(inner, ref y, lineH, $"流式: {(evt.IsStreaming ? "是" : "否")} | 延迟: {evt.ElapsedMs}ms | 音频: {(evt.AudioBytes > 0 ? $"{evt.AudioBytes / 1024}KB" : "-")}");
            if (!string.IsNullOrEmpty(evt.ErrorMessage))
                DetailLine(inner, ref y, lineH, $"错误: {evt.ErrorMessage}");

            y += 4f;

            if (!string.IsNullOrEmpty(evt.InputText))
                DetailBlock(inner, ref y, "合成文本", evt.InputText, 60f);

            if (!string.IsNullOrEmpty(evt.Persona))
                DetailBlock(inner, ref y, "人格描述", evt.Persona, 50f);

            if (!string.IsNullOrEmpty(evt.RequestJson))
                DetailBlock(inner, ref y, "请求 JSON", evt.RequestJson, 100f);

            if (!string.IsNullOrEmpty(evt.ResponseJson))
                DetailBlock(inner, ref y, "响应 JSON", evt.ResponseJson, 140f);

            Text.Font = GameFont.Small;
            Widgets.EndScrollView();
        }

        private void DetailLine(Rect inner, ref float y, float h, string text)
        {
            Widgets.Label(new Rect(inner.x, inner.y + y, inner.width, h), text);
            y += h + 1f;
        }

        private void DetailBlock(Rect inner, ref float y, string label, string content, float maxH)
        {
            Rect lr = new Rect(inner.x, inner.y + y, inner.width, 14f);
            GUI.color = new Color(0.8f, 0.8f, 0.5f);
            Widgets.Label(lr, label);
            GUI.color = Color.white;
            y += 15f;

            float th = Text.CalcHeight(content, inner.width - 4f);
            th = Mathf.Min(th, maxH);
            Rect cr = new Rect(inner.x + 2f, inner.y + y, inner.width - 2f, th);
            Widgets.DrawBoxSolid(cr, new Color(0.03f, 0.03f, 0.05f, 0.9f));
            Widgets.Label(cr.ContractedBy(2f), content);
            y += th + 4f;
        }
    }
}
