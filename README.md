# RimTalk TTS Simple

轻量级 RimWorld TTS 语音合成模组，为 [RimTalk](https://github.com/jlibrary/RimTalk) 对话添加语音朗读。

## TTS 渠道

| 渠道 | 需要 API Key | 说明 |
|------|-------------|------|
| **MiMo TTS** | 需要 | 小米 MiMo-V2.5-TTS，支持预置音色 + AI人格音色 |
| **Edge TTS** | 不需要 | 微软 Edge 免费 TTS，开箱即用 |

## 前置要求

- [Harmony](https://github.com/pardeike/HarmonyRimWorld)
- [RimTalk](https://github.com/jlibrary/RimTalk)

## 配置说明

1. 在 Mod 选项中启用 TTS
2. 选择渠道 (Edge TTS 或 MiMo TTS)
3. 如选 MiMo TTS，填入 API Key（从 https://mimo.mi.com 获取）
4. 选择模型和音色
5. （可选）开启流式输出

## 人格音色 (MiMo)

Mod 会自动读取 RimTalk 为每个角色分配的 AI 人格描述，并将其作为 MiMo TTS 的音色描述参数。
这使得每个殖民者的语音都与其设定的人格相符。

## 构建

```bash
dotnet restore Source/RimTalkTTS.Simple.csproj
dotnet build Source/RimTalkTTS.Simple.csproj -c Release
```

## 许可

MIT License
