using System;
using System.Text;
using UnityEngine;

namespace RimTalkTTS.Simple.Util
{
    public class TTSEventLog
    {
        public enum State
        {
            None,
            Pending,
            Success,
            Failed,
            Cancelled
        }

        public Guid Id { get; } = Guid.NewGuid();
        public State EventState { get; set; } = State.Pending;
        public DateTime Timestamp { get; } = DateTime.Now;
        public string PawnName { get; set; }
        public string Channel { get; set; }
        public string Voice { get; set; }
        public string InputText { get; set; }
        public string Persona { get; set; }
        public string Model { get; set; }
        public long AudioBytes { get; set; }
        public long ElapsedMs { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsStreaming { get; set; }

        public string GetStateLabel()
        {
            return EventState switch
            {
                State.Pending => "⏳ 等待中",
                State.Success => "✅ 成功",
                State.Failed => "❌ 失败",
                State.Cancelled => "🔇 取消",
                _ => "未知"
            };
        }

        public Color GetColor()
        {
            return EventState switch
            {
                State.Failed => new Color(1f, 0.5f, 0.5f),
                State.Pending => Color.yellow,
                State.Cancelled => Color.gray,
                State.Success => new Color(0.5f, 1f, 0.5f),
                _ => Color.white
            };
        }

        public string GetSummary()
        {
            var sb = new StringBuilder();
            sb.Append($"{Timestamp:HH:mm:ss} ");
            sb.Append($"{GetStateLabel()} | ");
            if (!string.IsNullOrEmpty(PawnName)) sb.Append($"{PawnName} | ");
            if (!string.IsNullOrEmpty(Voice)) sb.Append($"{Voice} | ");
            sb.Append($"{ElapsedMs}ms | ");
            sb.Append($"{AudioBytes / 1024}KB");
            return sb.ToString();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== TTS Event: {Id} ===");
            sb.AppendLine($"Time: {Timestamp:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"State: {GetStateLabel()}");
            sb.AppendLine($"Channel: {Channel}");
            sb.AppendLine($"Pawn: {PawnName ?? "-"}");
            sb.AppendLine($"Model: {Model ?? "-"}");
            sb.AppendLine($"Voice: {Voice ?? "-"}");
            sb.AppendLine($"Streaming: {IsStreaming}");
            sb.AppendLine($"Audio: {AudioBytes} bytes ({AudioBytes / 1024} KB)");
            sb.AppendLine($"Elapsed: {ElapsedMs}ms");
            sb.AppendLine();
            sb.AppendLine($"Persona: {Persona ?? "-"}");
            sb.AppendLine();
            sb.AppendLine($"Input: {InputText ?? "-"}");
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                sb.AppendLine();
                sb.AppendLine($"Error: {ErrorMessage}");
            }
            return sb.ToString();
        }
    }
}
