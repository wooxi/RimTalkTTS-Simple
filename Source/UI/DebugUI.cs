using System;
using System.Linq;
using System.Threading.Tasks;
using RimTalkTTS.Simple.Data;
using RimTalkTTS.Simple.Patch;
using RimTalkTTS.Simple.Service;
using RimTalkTTS.Simple.Util;
using UnityEngine;
using Verse;

namespace RimTalkTTS.Simple.UI
{
    public static class DebugUI
    {
        private static bool _showDebug = false;
        private static bool _showEvents = false;
        private static bool _showEventDetail = false;
        private static int _selectedEventIndex = -1;
        private static Vector2 _eventScrollPos;
        private static string _testResult = "";
        private static bool _testRunning = false;

        public static void DrawDebugSection(Listing_Standard listing, TTSSettings settings)
        {
            listing.CheckboxLabeled("调试面板", ref _showDebug);

            if (!_showDebug) return;

            listing.Gap();

            if (listing.ButtonText("清除日志 / 重置统计"))
            {
                TTSEventHistory.Clear();
                TTSStats.Reset();
                _testResult = "";
            }

            listing.Gap();

            DrawStatus(listing, settings);
            listing.Gap();
            DrawTestButton(listing, settings);
            listing.Gap();
            DrawStats(listing);
            listing.Gap();
            DrawEventLog(listing);
        }

        private static void DrawStatus(Listing_Standard listing, TTSSettings settings)
        {
            Text.Font = GameFont.Medium;
            listing.Label("运行状态");
            Text.Font = GameFont.Small;

            string channel = settings.Provider == TTSSettings.TTSProvider.EdgeTTS
                ? "Edge TTS (免费)" : $"MiMo TTS ({settings.GetEffectiveModel()})";
            listing.Label($"渠道: {channel}");

            if (settings.UseCustomEndpoint)
                listing.Label($"端点: {settings.CustomEndpointUrl}");

            string keyStatus = settings.Provider != TTSSettings.TTSProvider.MiMoTTS
                || !string.IsNullOrWhiteSpace(settings.GetEffectiveApiKey()) ? "✅" : "⚠️";
            listing.Label($"API Key: {keyStatus}");

            listing.Label($"阻塞对话: {RimTalkPatches.blockedDialogues.Count}");
            listing.Label($"待播放音频: {(AudioPlaybackService.IsCurrentlyPlaying() ? "▶ 播放中" : "⏸ 空闲")}");

            listing.Gap();
            listing.Label("RimTalk 连接:");
            RimTalkPatches.ResolveRimTalkTypes();
            string asm = RimTalkPatches.RimTalkAssembly != null
                ? $"✅ {RimTalkPatches.RimTalkAssembly.GetName().Name}" : "❌ 未找到";
            listing.Label($"  程序集: {asm}");
            listing.Label($"  TalkResponse: {(RimTalkPatches.TalkResponseType != null ? "✅" : "❌")}  PawnState: {(RimTalkPatches.PawnStateType != null ? "✅" : "❌")}");
            listing.Label($"  TalkHistory: {(RimTalkPatches.TalkHistoryType != null ? "✅" : "❌")}  TalkService: {(RimTalkPatches.TalkServiceType != null ? "✅" : "❌")}");
            listing.Label($"  Overlay: {(RimTalkPatches.OverlayType != null ? "✅" : "❌")}  Cache: {(RimTalkPatches.CacheType != null ? "✅" : "❌")}");
            listing.Label($"  TalkRequest: {(RimTalkPatches.TalkRequestType != null ? "✅" : "❌")}");

            listing.Gap();
            listing.Label("Patch 状态:");
            try
            {
                var harmony = new HarmonyLib.Harmony("nitoritech.rimtalk.tts.simple");
                var methods = harmony.GetPatchedMethods().ToList();
                if (methods.Any())
                    foreach (var m in methods)
                        listing.Label($"  ✅ {m.DeclaringType?.Name}.{m.Name}");
                else
                    listing.Label("  ⚠️ 无已激活 patch");
            }
            catch { listing.Label("  ⚠️ 无法读取"); }
        }

        private static void DrawTestButton(Listing_Standard listing, TTSSettings settings)
        {
            listing.Label($"测试 TTS ({settings.Provider}):");
            if (_testRunning)
            {
                listing.Label("⏳ 正在生成...");
                return;
            }

            if (listing.ButtonText("发送测试文本"))
            {
                _testRunning = true;
                _testResult = "正在测试...";

                Task.Run(async () =>
                {
                    try
                    {
                        string testText = "你好，这是来自RimTalk TTS Simple的测试语音。";
                        string persona = "使用自然清晰的语音风格。";

                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        byte[] audio = await TTSService.GenerateSpeechAsync(testText, persona, null, settings);
                        sw.Stop();

                        if (audio != null && audio.Length > 0)
                        {
                            _testResult = $"✅ 成功: {audio.Length / 1024}KB, {sw.ElapsedMilliseconds}ms";
                            var testId = Guid.NewGuid();
                            AudioPlaybackService.SetAudioResult(testId, audio);
                            AudioPlaybackService.PlayAudio(testId, null);
                        }
                        else
                            _testResult = "❌ 失败: API 返回空数据";
                    }
                    catch (System.Exception ex)
                    {
                        _testResult = $"❌ 异常: {ex.Message}";
                    }
                    finally { _testRunning = false; }
                });
            }

            if (!string.IsNullOrEmpty(_testResult))
                listing.Label(_testResult);
        }

        private static void DrawStats(Listing_Standard listing)
        {
            Text.Font = GameFont.Medium;
            listing.Label("统计");
            Text.Font = GameFont.Small;

            long total = TTSStats.TotalRequests;
            long success = TTSStats.TotalSuccess;
            long failed = TTSStats.TotalFailed;
            long cancelled = TTSStats.TotalCancelled;
            string rate = total > 0 ? $"{(double)success / total * 100:F1}%" : "-";

            listing.Label($"请求: {total}  成功: {success} ({rate})  失败: {failed}  取消: {cancelled}");

            if (success > 0)
            {
                listing.Label($"平均延迟: {TTSStats.AvgElapsedMs:F0}ms");
                listing.Label($"平均音频: {TTSStats.AvgAudioBytes / 1024}KB");
            }

            if (!string.IsNullOrEmpty(TTSStats.LastError))
                listing.Label($"最近错误: {TTSStats.LastError.Substring(0, Math.Min(120, TTSStats.LastError.Length))}");
        }

        private static void DrawEventLog(Listing_Standard listing)
        {
            var events = TTSEventHistory.GetRecent(50);

            listing.CheckboxLabeled($"事件日志 (共{TTSEventHistory.GetAll().Count}条, 最近{events.Count})", ref _showEvents);
            if (!_showEvents) return;

            float height = 200f;
            Rect outerRect = listing.GetRect(height);
            float innerHeight = events.Count * 18f + 10f;
            Rect innerRect = new Rect(0, 0, outerRect.width - 16f, Mathf.Max(innerHeight, outerRect.height));

            Widgets.BeginScrollView(outerRect, ref _eventScrollPos, innerRect);

            float y = 0;
            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                string stateIcon = evt.EventState == TTSEventLog.State.Success ? "✓"
                    : evt.EventState == TTSEventLog.State.Failed ? "✗"
                    : evt.EventState == TTSEventLog.State.Cancelled ? "✕" : "…";

                string line = $"{evt.Timestamp:HH:mm:ss} {stateIcon} {evt.PawnName?.Substring(0, Math.Min(8, evt.PawnName?.Length ?? 0)) ?? "-"} | {evt.Voice?.Substring(0, Math.Min(8, evt.Voice?.Length ?? 0)) ?? "-"} | {evt.ElapsedMs}ms";
                if (evt.AudioBytes > 0) line += $" | {evt.AudioBytes / 1024}KB";
                if (!string.IsNullOrEmpty(evt.ErrorMessage))
                    line += $" | {evt.ErrorMessage.Substring(0, Math.Min(25, evt.ErrorMessage.Length))}";

                Rect rowRect = new Rect(0, y, innerRect.width, 16f);

                GUI.color = evt.GetColor();
                if (Widgets.ButtonInvisible(rowRect))
                {
                    _selectedEventIndex = i;
                    _showEventDetail = !_showEventDetail;
                }
                Widgets.Label(rowRect, line);
                GUI.color = Color.white;

                if (_showEventDetail && _selectedEventIndex == i)
                {
                    y += 16f;
                    string detail = $"  ▶ 全文: {evt.InputText?.Substring(0, Math.Min(200, evt.InputText?.Length ?? 0)) ?? "-"}\n"
                        + $"  ▶ 人格: {evt.Persona?.Substring(0, Math.Min(200, evt.Persona?.Length ?? 0)) ?? "-"}\n"
                        + $"  ▶ 模型: {evt.Model ?? "-"}  流式: {evt.IsStreaming}\n"
                        + $"  ▶ 错误: {evt.ErrorMessage ?? "(无)"}";
                    float detailHeight = 64f;
                    Rect detailRect = new Rect(4f, y, innerRect.width - 8f, detailHeight);
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(detailRect, detail);
                    Text.Font = GameFont.Small;
                    y += detailHeight;
                }

                y += 16f;
            }

            Widgets.EndScrollView();
        }
    }
}
