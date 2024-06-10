using AudioPipeline;
using Discord;
using Discord.Audio;
using System.Diagnostics;
using System.Collections.Concurrent;

public class MP3Handler {
     private enum PlayerState {
          Play, Pause, Paused, Skip, None, Resume, Playing, Idle, Skipping
     }

     private struct PlayerStateData {
          public Process? CurrentFFMPEGSource;
          public PlayerState CurrentState;
          public ReaderWriterLockSlim Lock;

          public PlayerStateData() {
               CurrentState = PlayerState.None;
               CurrentFFMPEGSource = null;
               Lock = new();
          }
     }

     public struct MP3Entry {
          public string URL;

          public MP3Entry(string url) {
               URL = url;
          }
     }

     private VoiceStateManager _VoiceStateManager;
     private ConcurrentQueue<MP3Entry> SongQueue;
     // add in a ReadySongs Queue that always tries to pop the Song Queue when below a capacity of (x)
     private CancellationTokenSource InterruptSource;
     private ILogger Logger;
     private PlayerStateData _PlayerStateData;

     public int QueueCount { get => SongQueue.Count; }
     public MP3Handler(VoiceStateManager voiceStateManager, ILogger logger) {
          _VoiceStateManager = voiceStateManager;
          SongQueue = new();
          InterruptSource = new();
          _PlayerStateData = new();

          Logger = logger;
     }

     public void ClearQueue() {
          SongQueue.Clear();
     }

     public void AddQueue(MP3Entry entry) {
          SongQueue.Enqueue(entry);
     }

     // Pause will usually always succeed, but will return false if the player wasnt already playing something. other wise returns true
     public bool Pause() {
          if (_PlayerStateData.CurrentState != PlayerState.Playing) return false;
          _PlayerStateData.CurrentState = PlayerState.Paused;
          InterruptPlayer();
          return true;
     }

     public async Task<bool> TryResume(IVoiceChannel targetChannnel) {
          if (_PlayerStateData.CurrentState == PlayerState.Playing) return false;

          if (_PlayerStateData.CurrentState != PlayerState.Paused)
               if (!await TryPopQueue()) return false;
          await StartPlayer(targetChannnel).ConfigureAwait(true);
          return true;
     }

     public async Task<bool> SkipSong() {
          InterruptPlayer();
          if (!await TryPopQueue()) return true;

          if (_VoiceStateManager.ConnectedVoiceChannel == null) return false;
          _PlayerStateData.CurrentState = PlayerState.Skipping;
          await StartPlayer(_VoiceStateManager.ConnectedVoiceChannel).ConfigureAwait(true);

          if (_PlayerStateData.CurrentFFMPEGSource != null)
          _PlayerStateData.CurrentFFMPEGSource.Kill();

          return true;
     }

     private void InterruptPlayer() {
          InterruptSource.Cancel();
          InterruptSource = new CancellationTokenSource();
     }

     public async Task<bool> TryPopQueue() {
          MP3Entry entry;
          if (!SongQueue.TryDequeue(out entry)) return false;

          _PlayerStateData.CurrentFFMPEGSource = await new FFMPEGHandler(Logger).TrySpawnYoutubeFFMPEG(entry.URL, null, 1.0f);
          if (_PlayerStateData.CurrentFFMPEGSource == null) return false;
          return true;
     }

     public async Task<bool> TryPlay(IVoiceChannel targetChannnel) {
          if (!await TryPopQueue()) return false;
          await StartPlayer(targetChannnel);
          return true;
     }

     public async Task StartPlayer(IVoiceChannel targetChannnel) {
          var Log = async (string str) => await Logger.LogAsync("[Debug/StartPlayer] " + str);
          IAudioClient? AudioClient = await _VoiceStateManager.ConnectAsync(targetChannnel);
          if (AudioClient == null) return;

          do {
               if (_PlayerStateData.CurrentFFMPEGSource == null) {
                    await Log("_PlayerStateData.CurrentFFMPEGSource is null, stoppping");
                    return;
               }
               _PlayerStateData.CurrentState = PlayerState.Playing;
               Stream input = _PlayerStateData.CurrentFFMPEGSource.StandardOutput.BaseStream;
               using (Stream output = AudioClient.CreatePCMStream(AudioApplication.Mixed)) {
                    try {
                         await input.CopyToAsync(output, InterruptSource.Token);
                    } catch (OperationCanceledException) {
                         return;
                    } catch (Exception e) {
                         await Log("generic exception: " + e.Message);
                    }
                    _PlayerStateData.CurrentFFMPEGSource.Kill();
               }; // point of potential Error
          } while (SongQueue.Count > 0);

          // if (_PlayerStateData.CurrentState != PlayerState.Paused && _PlayerStateData.CurrentState != PlayerState.Skipping)
          //      _PlayerStateData.CurrentState = PlayerState.Idle;
     }
}