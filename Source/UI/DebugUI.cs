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
        private static Vector2 _eventScrollPos;
        private static string _testResult = "";
        private static bool _testRunning = false;

        public static void DrawDebugSection(Listing_Standard listing, TTSSettings settings)
        {
            listing.GapLine();

            listing.CheckboxLabeled("调试面板", ref _showDebug);

            if (!_showDebug) return;

            listing.Gap();

            DrawStatus(listing, settings);

            listing.Gap();

            DrawTestButton(listing, settings);

            listing.Gap();

            DrawStats(listing);

            listing.Gap();

            if (listing.ButtonText("清除日志"))
            {
                TTSEventHistory.Clear();
                TTSStats.Reset();
                _testResult = "";
            }

            listing.Gap();

            DrawEventLog(listing);
        }

        private static void DrawStatus(Listing_Standard listing, TTSSettings settings)
        {
            listing.Label("=== 状态 ===");

            string channel = settings.Provider == TTSSettings.TTSProvider.EdgeTTS
                ? "Edge TTS (免费)"
                : $"MiMo TTS ({settings.MiMoModel})";
            listing.Label($"渠道: {channel}");

            bool hasKey = settings.Provider != TTSSettings.TTSProvider.MiMoTTS
                || !string.IsNullOrWhiteSpace(settings.MiMoApiKey);
            string keyStatus = hasKey ? "✅ 已配置" : "⚠️ 未配置";
            listing.Label($"API Key: {keyStatus}");

            string voice = settings.Provider == TTSSettings.TTSProvider.EdgeTTS
                ? settings.EdgeVoice
                : settings.MiMoVoice;
            listing.Label($"音色: {voice}");

            listing.Gap();
            listing.Label("=== RimTalk 连接 ===");
            RimTalkPatches.ResolveRimTalkTypes();

            string asm = RimTalkPatches.RimTalkAssembly != null ?
                $"✅ {RimTalkPatches.RimTalkAssembly.GetName().Name}" : "❌ 未找到";

            listing.Label($"RimTalk 程序集: {asm}");
            listing.Label($"TalkResponse: {(RimTalkPatches.TalkResponseType != null ? "✅" : "❌")}");
            listing.Label($"PawnState:    {(RimTalkPatches.PawnStateType != null ? "✅" : "❌")}");
            listing.Label($"TalkHistory:  {(RimTalkPatches.TalkHistoryType != null ? "✅" : "❌")}");
            listing.Label($"TalkService:  {(RimTalkPatches.TalkServiceType != null ? "✅" : "❌")}");
            listing.Label($"RimTalk main: {(RimTalkPatches.RimTalkMainType != null ? "✅" : "❌")}");
            listing.Label($"Overlay:      {(RimTalkPatches.OverlayType != null ? "✅" : "❌")}");
            listing.Label($"Cache:        {(RimTalkPatches.CacheType != null ? "✅" : "❌")}");
            listing.Label($"TalkRequest:  {(RimTalkPatches.TalkRequestType != null ? "✅" : "❌")}");

            listing.Gap();
            listing.Label("=== Patch 状态 ===");
            try
            {
                var harmony = new HarmonyLib.Harmony("nitoritech.rimtalk.tts.simple");
                var methods = harmony.GetPatchedMethods().ToList();
                if (methods.Any())
                {
                    foreach (var m in methods)
                        listing.Label($"  ✅ {m.DeclaringType?.Name}.{m.Name}");
                }
                else
                {
                    listing.Label("  ⚠️ 没有已激活的 patch");
                }
            }
            catch
            {
                listing.Label("  ⚠️ 无法读取 Harmony 状态");
            }
        }

        private static void DrawTestButton(Listing_Standard listing, TTSSettings settings)
        {
            listing.Label($"测试 TTS ({settings.Provider}):");
            if (_testRunning)
            {
                listing.Label("正在生成...");
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
                        byte[] audio = await Service.TTSService.GenerateSpeechAsync(testText, persona, null, settings);
                        sw.Stop();

                        if (audio != null && audio.Length > 0)
                        {
                            _testResult = $"✅ 成功: {audio.Length / 1024}KB, {sw.ElapsedMilliseconds}ms (正在播放...)";
                            var testId = Guid.NewGuid();
                            Service.AudioPlaybackService.SetAudioResult(testId, audio);
                            Service.AudioPlaybackService.PlayAudio(testId, null);
                            _testResult = $"✅ 成功: {audio.Length / 1024}KB, {sw.ElapsedMilliseconds}ms";
                        }
                        else
                        {
                            _testResult = "❌ 失败: API 返回空数据";
                        }
                    }
                    catch (System.Exception ex)
                    {
                        _testResult = $"❌ 异常: {ex.Message}";
                    }
                    finally
                    {
                        _testRunning = false;
                    }
                });
            }

            if (!string.IsNullOrEmpty(_testResult))
            {
                listing.Label(_testResult);
            }
        }

        private static void DrawStats(Listing_Standard listing)
        {
            listing.Label("=== 统计 ===");

            listing.Label($"总请求: {TTSStats.TotalRequests}  " +
                         $"成功: {TTSStats.TotalSuccess}  " +
                         $"失败: {TTSStats.TotalFailed}  " +
                         $"取消: {TTSStats.TotalCancelled}");

            if (TTSStats.TotalSuccess > 0)
            {
                listing.Label($"平均生成耗时: {TTSStats.AvgElapsedMs:F0}ms");
                listing.Label($"平均音频: {TTSStats.AvgAudioBytes / 1024}KB");
            }

            if (!string.IsNullOrEmpty(TTSStats.LastError))
            {
                listing.Label($"最近错误: {TTSStats.LastError.Substring(0, System.Math.Min(100, TTSStats.LastError.Length))}");
            }
        }

        private static void DrawEventLog(Listing_Standard listing)
        {
            listing.CheckboxLabeled($"事件日志 (共{TTSEventHistory.GetAll().Count}条)", ref _showEvents);

            if (!_showEvents) return;

            var events = TTSEventHistory.GetRecent(50);
            float height = 200f;
            var outerRect = listing.GetRect(height);
            var innerRect = new Rect(0, 0, outerRect.width - 16f, events.Count * 22f + 10f);

            Widgets.BeginScrollView(outerRect, ref _eventScrollPos, innerRect);

            var innerListing = new Listing_Standard();
            innerListing.Begin(innerRect);

            foreach (var evt in events)
            {
                var color = evt.GetColor();
                GUI.color = color;
                string line = $"{evt.Timestamp:HH:mm:ss} {evt.GetStateLabel().Substring(0, 3)} ";
                GUI.color = Color.white;
                line += $"{evt.PawnName ?? "-"} | ";
                line += $"{evt.Voice ?? "-"} | ";
                line += $"{evt.ElapsedMs}ms | ";
                if (evt.AudioBytes > 0) line += $"{evt.AudioBytes / 1024}KB";
                else if (!string.IsNullOrEmpty(evt.ErrorMessage))
                    line += evt.ErrorMessage.Substring(0, System.Math.Min(30, evt.ErrorMessage.Length));

                innerListing.Label(line);
            }

            innerListing.End();
            Widgets.EndScrollView();
        }
    }
}
