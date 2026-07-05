using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RimTalkTTS.Simple.Patch;
using UnityEngine;
using Verse;

namespace RimTalkTTS.Simple.Service
{
    [StaticConstructorOnStartup]
    public static class AudioPlaybackService
    {
        private static readonly GameObject _audioPlayerObject;
        private static readonly AudioSource _audioSource;
        private static readonly Dictionary<Guid, byte[]> _dialogueAudio = new Dictionary<Guid, byte[]>();
        private static bool _isPlaying = false;
        private static readonly object _lock = new object();

        static AudioPlaybackService()
        {
            _audioPlayerObject = new GameObject("RimTalkTTSAudioPlayer");
            UnityEngine.Object.DontDestroyOnLoad(_audioPlayerObject);
            _audioSource = _audioPlayerObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
            _audioSource.dopplerLevel = 0f;
        }

        public static void SetAudioResult(Guid dialogueId, byte[] audioData)
        {
            if (dialogueId == Guid.Empty) return;

            lock (_lock)
            {
                _dialogueAudio[dialogueId] = audioData;
            }
        }

        public static bool IsCurrentlyPlaying()
        {
            lock (_lock)
            {
                return _isPlaying;
            }
        }

        public static async void PlayAudio(Guid dialogueId, Pawn pawn, float volume = 1.0f)
        {
            if (dialogueId == Guid.Empty) return;

            try
            {
                while (IsCurrentlyPlaying())
                {
                    await Task.Delay(1000);
                }

                lock (_lock)
                {
                    _isPlaying = true;
                }

                int waitCycles = 0;
                const int maxWaitCycles = 30;

                while (RimTalkPatches.IsBlocked(dialogueId) && waitCycles < maxWaitCycles)
                {
                    await Task.Delay(1000);
                    waitCycles++;
                }

                if (waitCycles >= maxWaitCycles)
                {
                    lock (_lock) { _isPlaying = false; _dialogueAudio.Remove(dialogueId); }
                    return;
                }

                await Task.Delay(100);

                byte[] audioData;
                lock (_lock)
                {
                    if (!_dialogueAudio.TryGetValue(dialogueId, out audioData) || audioData == null || audioData.Length == 0)
                    {
                        _isPlaying = false;
                        _dialogueAudio.Remove(dialogueId);
                        return;
                    }
                }

                try
                {
                    AudioClip clip = await LoadAudioClipFromData(audioData, dialogueId.ToString());
                    if (clip != null && clip.length > 0)
                    {
                        _audioSource.clip = clip;
                        _audioSource.volume = Mathf.Clamp01(volume);
                        _audioSource.Play();
                        int playbackDelayMs = (int)(clip.length * 1000f);
                        await Task.Delay(playbackDelayMs);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimTalkTTS.Simple] PlayAudio exception: {ex}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalkTTS.Simple] PlayAudio outer exception: {ex}");
            }
            finally
            {
                lock (_lock)
                {
                    _isPlaying = false;
                    _dialogueAudio.Remove(dialogueId);
                }
            }
        }

        public static void StopAndClear()
        {
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
                _audioSource.clip = null;
            }

            lock (_lock)
            {
                _dialogueAudio.Clear();
                _isPlaying = false;
            }
        }

        public static void RemovePendingAudio(Guid dialogueId)
        {
            if (dialogueId == Guid.Empty) return;
            lock (_lock)
            {
                _dialogueAudio.Remove(dialogueId);
            }
        }

        private static async Task<AudioClip> LoadAudioClipFromData(byte[] audioData, string dialogueId)
        {
            try
            {
                bool isWav = audioData.Length > 12 &&
                             System.Text.Encoding.ASCII.GetString(audioData, 0, 4) == "RIFF" &&
                             System.Text.Encoding.ASCII.GetString(audioData, 8, 4) == "WAVE";

                if (isWav)
                {
                    return LoadAudioClipFromWav(audioData);
                }
                else
                {
                    return await LoadAudioClipFromMP3(audioData, dialogueId);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalkTTS.Simple] LoadAudioClip: {ex.Message}");
                return null;
            }
        }

        private static async Task<AudioClip> LoadAudioClipFromMP3(byte[] mp3Data, string dialogueId)
        {
            string tempFile = null;
            try
            {
                tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"rimtalk_tts_simple_{dialogueId}.mp3");
                await System.IO.File.WriteAllBytesAsync(tempFile, mp3Data);

                AudioClip clip = null;
                await Task.Run(async () =>
                {
                    using (var www = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip("file:///" + tempFile, UnityEngine.AudioType.MPEG))
                    {
                        var operation = www.SendWebRequest();
                        while (!operation.isDone) await Task.Delay(10);

                        if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                        {
                            clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
                        }
                    }
                });

                return clip;
            }
            finally
            {
                try
                {
                    if (tempFile != null && System.IO.File.Exists(tempFile))
                        System.IO.File.Delete(tempFile);
                }
                catch { }
            }
        }

        private static AudioClip LoadAudioClipFromWav(byte[] wavData)
        {
            try
            {
                int dataPos = 12;
                int channels = 1;
                int sampleRate = 24000;
                int bitsPerSample = 16;

                while (dataPos + 8 <= wavData.Length)
                {
                    string chunkId = System.Text.Encoding.ASCII.GetString(wavData, dataPos, 4);
                    int chunkSize = BitConverter.ToInt32(wavData, dataPos + 4);

                    if (chunkId == "fmt " && dataPos + 8 + 16 <= wavData.Length)
                    {
                        channels = BitConverter.ToInt16(wavData, dataPos + 10);
                        sampleRate = BitConverter.ToInt32(wavData, dataPos + 12);
                        bitsPerSample = BitConverter.ToInt16(wavData, dataPos + 22);
                    }

                    if (chunkId == "data")
                    {
                        dataPos += 8;
                        break;
                    }

                    long nextPos = (long)dataPos + 8 + chunkSize;
                    if (nextPos <= dataPos || nextPos > wavData.Length) return null;
                    dataPos = (int)nextPos;
                }

                if (dataPos >= wavData.Length) return null;

                int bytesPerSample = bitsPerSample / 8;
                int sampleCount = (wavData.Length - dataPos) / bytesPerSample;
                float[] audioData = new float[sampleCount];

                if (bitsPerSample == 16)
                {
                    for (int i = 0; i < sampleCount; i++)
                        audioData[i] = BitConverter.ToInt16(wavData, dataPos + i * 2) / 32768f;
                }
                else if (bitsPerSample == 8)
                {
                    for (int i = 0; i < sampleCount; i++)
                        audioData[i] = (wavData[dataPos + i] - 128) / 128f;
                }

                AudioClip clip = AudioClip.Create("RimTalkTTSSimple", sampleCount / channels, channels, sampleRate, false);
                clip.SetData(audioData, 0);
                return clip;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalkTTS.Simple] LoadWav: {ex.Message}");
                return null;
            }
        }
    }
}
