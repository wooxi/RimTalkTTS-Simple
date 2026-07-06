using System;
using System.Linq;
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

        public TTSDebugWindow()
        {
            doCloseX = true;
            draggable = true;
            closeOnAccept = false;
            closeOnCancel = true;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
        }

        public override Vector2 InitialSize => new Vector2(780f, 520f);

        public override void DoWindowContents(Rect inRect)
        {
            float leftW = inRect.width * 0.4f;
            float rightW = inRect.width - leftW - 8f;

            Rect leftRect = new Rect(inRect.x, inRect.y, leftW, inRect.height);
            Rect rightRect = new Rect(leftRect.xMax + 8f, inRect.y, rightW, inRect.height);

            Widgets.DrawBoxSolid(leftRect, new Color(0.08f, 0.08f, 0.1f, 0.6f));
            Widgets.DrawBoxSolid(rightRect, new Color(0.08f, 0.08f, 0.1f, 0.6f));

            DrawEventList(leftRect.ContractedBy(4f));
            DrawEventDetail(rightRect.ContractedBy(4f));
        }

        private void DrawEventList(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f), $"事件日志 ({TTSEventHistory.GetAll().Count})");
            Text.Font = GameFont.Small;

            var events = TTSEventHistory.GetRecent(20);
            float listTop = rect.y + 28f;
            float contentH = events.Count * 36f + 10f;
            Rect listOuter = new Rect(rect.x, listTop, rect.width, rect.height - 28f);
            Rect listInner = new Rect(0, 0, listOuter.width - 16f, Mathf.Max(contentH, listOuter.height));

            Widgets.BeginScrollView(listOuter, ref _listScrollPos, listInner);

            float y = 0;
            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                bool selected = _selectedIndex == i;
                Rect rowRect = new Rect(0, y, listInner.width, 32f);

                if (selected)
                    Widgets.DrawBoxSolid(rowRect, new Color(0.25f, 0.45f, 0.25f, 0.4f));
                else
                    Widgets.DrawBoxSolid(rowRect, new Color(0.12f, 0.12f, 0.14f, 0.5f));

                Widgets.DrawHighlightIfMouseover(rowRect);

                string stateIcon = evt.EventState == TTSEventLog.State.Success ? "✓"
                    : evt.EventState == TTSEventLog.State.Failed ? "✗"
                    : evt.EventState == TTSEventLog.State.Cancelled ? "✕" : "…";

                GUI.color = evt.GetColor();
                Rect iconRect = new Rect(rowRect.x + 4f, rowRect.y + 4f, 16f, 16f);
                Widgets.Label(iconRect, stateIcon);
                GUI.color = Color.white;

                string line1 = $"{evt.Timestamp:HH:mm:ss}  {evt.PawnName?.Substring(0, Math.Min(10, evt.PawnName?.Length ?? 0)) ?? "-"}";
                string line2 = $"{evt.Voice?.Substring(0, Math.Min(14, evt.Voice?.Length ?? 0)) ?? "-"}  {evt.ElapsedMs}ms  {(evt.AudioBytes > 0 ? $"{evt.AudioBytes / 1024}KB" : "")}";
                if (!string.IsNullOrEmpty(evt.ErrorMessage))
                    line2 += $"  {evt.ErrorMessage.Substring(0, Math.Min(20, evt.ErrorMessage.Length))}";

                Rect line1Rect = new Rect(iconRect.xMax + 4f, rowRect.y + 1f, listInner.width - 30f, 14f);
                Rect line2Rect = new Rect(iconRect.xMax + 4f, rowRect.y + 16f, listInner.width - 30f, 14f);

                Text.Font = GameFont.Tiny;
                Widgets.Label(line1Rect, line1);
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(line2Rect, line2);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;

                if (Widgets.ButtonInvisible(rowRect))
                    _selectedIndex = (_selectedIndex == i) ? -1 : i;

                y += 34f;
            }

            Widgets.EndScrollView();
        }

        private void DrawEventDetail(Rect rect)
        {
            var events = TTSEventHistory.GetRecent(20);
            if (_selectedIndex < 0 || _selectedIndex >= events.Count)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                Widgets.Label(rect, "选择左侧事件查看详情");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                return;
            }

            var evt = events[_selectedIndex];

            float detailH = 500f;
            Rect inner = new Rect(0, 0, rect.width - 16f, detailH);

            Widgets.BeginScrollView(rect, ref _detailScrollPos, inner);

            float y = 0;
            float lineH = 16f;
            Text.Font = GameFont.Tiny;

            DrawDetailLine(inner, ref y, lineH, $"时间: {evt.Timestamp:yyyy-MM-dd HH:mm:ss}");
            DrawDetailLine(inner, ref y, lineH, $"状态: {evt.GetStateLabel()}");
            DrawDetailLine(inner, ref y, lineH, $"角色: {evt.PawnName ?? "-"}");
            DrawDetailLine(inner, ref y, lineH, $"渠道: {evt.Channel ?? "-"}");
            DrawDetailLine(inner, ref y, lineH, $"模型: {evt.Model ?? "-"}");
            DrawDetailLine(inner, ref y, lineH, $"音色: {evt.Voice ?? "-"}");
            DrawDetailLine(inner, ref y, lineH, $"流式: {(evt.IsStreaming ? "是" : "否")}");
            DrawDetailLine(inner, ref y, lineH, $"延迟: {evt.ElapsedMs}ms");
            if (evt.AudioBytes > 0)
                DrawDetailLine(inner, ref y, lineH, $"音频: {evt.AudioBytes} bytes ({evt.AudioBytes / 1024}KB)");
            if (!string.IsNullOrEmpty(evt.ErrorMessage))
                DrawDetailLine(inner, ref y, lineH, $"错误: {evt.ErrorMessage}");

            y += 6f;

            if (!string.IsNullOrEmpty(evt.InputText))
            {
                DrawMultiLine(inner, ref y, lineH, "合成文本:", evt.InputText, 80f);
            }

            if (!string.IsNullOrEmpty(evt.Persona))
            {
                DrawMultiLine(inner, ref y, lineH, "人格描述:", evt.Persona, 60f);
            }

            if (!string.IsNullOrEmpty(evt.RequestJson))
            {
                DrawMultiLine(inner, ref y, lineH, "请求 JSON:", evt.RequestJson, 120f);
            }

            if (!string.IsNullOrEmpty(evt.ResponseJson))
            {
                DrawMultiLine(inner, ref y, lineH, "响应 JSON:", evt.ResponseJson, 180f);
            }

            Text.Font = GameFont.Small;
            Widgets.EndScrollView();
        }

        private void DrawDetailLine(Rect inner, ref float y, float lineH, string text)
        {
            Rect r = new Rect(inner.x, inner.y + y, inner.width, lineH);
            Widgets.Label(r, text);
            y += lineH + 1f;
        }

        private void DrawMultiLine(Rect inner, ref float y, float lineH, string label, string content, float maxH)
        {
            Rect labelRect = new Rect(inner.x, inner.y + y, inner.width, lineH);
            GUI.color = new Color(0.8f, 0.8f, 0.5f);
            Widgets.Label(labelRect, label);
            GUI.color = Color.white;
            y += lineH + 1f;

            float textH = Text.CalcHeight(content, inner.width - 4f);
            textH = Mathf.Min(textH, maxH);
            Rect contentRect = new Rect(inner.x + 4f, inner.y + y, inner.width - 4f, textH);
            Widgets.DrawBoxSolid(contentRect, new Color(0.03f, 0.03f, 0.05f, 0.9f));
            Widgets.Label(contentRect.ContractedBy(2f), content);
            y += textH + 4f;
        }
    }
}
