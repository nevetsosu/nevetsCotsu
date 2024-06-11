using AudioPipeline;
using Discord;
using Discord.Audio;
using System.Diagnostics;
using System.Collections.Concurrent;

public class MP3Handler {

     public enum PlayerCommandStatus {
          EmptyQueue, Already, Good
     }
     private enum PlayerState {
          Paused, Playing, Idle
     }

     private struct PlayerStateData {
          public Process? CurrentFFMPEGSource;
          public MP3Entry CurrentEntry;
          public PlayerState CurrentState;
          public SemaphoreSlim StateLock;
          public CancellationTokenSource InterruptSource;
          public PlayerStateData() {
               CurrentState = PlayerState.Idle;
               CurrentFFMPEGSource = null;
               StateLock = new(1, 1);
               InterruptSource = new();
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
     private ILogger Logger;
     private PlayerStateData _PlayerStateData;

     public int QueueCount { get => SongQueue.Count; }
     public MP3Handler(VoiceStateManager voiceStateManager, ILogger logger) {
          _VoiceStateManager = voiceStateManager;
          SongQueue = new();
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
     public async Task<bool> Pause() {
          await _PlayerStateData.StateLock.WaitAsync();
          if (_PlayerStateData.CurrentState != PlayerState.Playing) {
               _PlayerStateData.StateLock.Release();
               return false; // return ALREADY
          }
          InterruptPlayer();
          _PlayerStateData.CurrentState = PlayerState.Paused;
          _PlayerStateData.StateLock.Release();
          return true; // return OK
     }

     public async Task<bool> TryResume(IVoiceChannel targetChannnel, string? song = null) {
          await _PlayerStateData.StateLock.WaitAsync();
          if (_PlayerStateData.CurrentState == PlayerState.Playing) {
               _PlayerStateData.StateLock.Release();
               return false; // return ALREADY
          }

          if (!string.IsNullOrEmpty(song)) {
               AddQueue(new MP3Entry(song));
          }

          if (_PlayerStateData.CurrentState != PlayerState.Paused && !await TryPopQueue()) {
               _PlayerStateData.StateLock.Release();
               return false; // return EMPTYQUEUE
          }
          InterruptPlayer();
          await StartPlayer(targetChannnel);
          return true; // return GOOD
     }

     public async Task<bool> SkipSong() {
          await _PlayerStateData.StateLock.WaitAsync();

          InterruptPlayer();
          if (_PlayerStateData.CurrentFFMPEGSource != null) _PlayerStateData.CurrentFFMPEGSource.Kill();

          if (_VoiceStateManager.ConnectedVoiceChannel == null) {
               _PlayerStateData.CurrentState = PlayerState.Idle;
               _PlayerStateData.StateLock.Release();
               return false;
          }

          if (!await TryPopQueue()) {
               _PlayerStateData.CurrentState = PlayerState.Idle;
               _PlayerStateData.StateLock.Release();
               return true;
          }

          await StartPlayer(_VoiceStateManager.ConnectedVoiceChannel);

          return true;
     }

     private void InterruptPlayer() {
          _PlayerStateData.InterruptSource.Cancel();
          _PlayerStateData.InterruptSource = new();
     }

     public async Task<bool> TryPopQueue() {
          MP3Entry entry;
          if (!SongQueue.TryDequeue(out entry)) return false;

          _PlayerStateData.CurrentFFMPEGSource = await new FFMPEGHandler(Logger).TrySpawnYoutubeFFMPEG(entry.URL, null, 1.0f);
          if (_PlayerStateData.CurrentFFMPEGSource == null) return false;
          return true;
     }

     public async Task<bool> TryPlay(IVoiceChannel targetChannnel) {
          await _PlayerStateData.StateLock.WaitAsync();
          if (!await TryPopQueue()) return false;

          InterruptPlayer();
          await StartPlayer(targetChannnel);
          return true;
     }

     // It is ASSUMED that the StateLock is Already acquired BEFORE a call to the StartPlayer function
     public async Task StartPlayer(IVoiceChannel targetChannnel) {
          var Log = async (string str) => await Logger.LogAsync("[Debug/StartPlayer] " + str);
          IAudioClient? AudioClient = await _VoiceStateManager.ConnectAsync(targetChannnel, OnDisconnectAsync);
          if (AudioClient == null) return;

          do {
               Process? FFMPEGSource = _PlayerStateData.CurrentFFMPEGSource;

               if (FFMPEGSource == null) {
                    await Log("_PlayerStateData.CurrentFFMPEGSource is null, stoppping");
                    return;
               }
               _PlayerStateData.CurrentState = PlayerState.Playing;
               _PlayerStateData.StateLock.Release();

               Stream input = FFMPEGSource.StandardOutput.BaseStream;
               using (Stream output = AudioClient.CreatePCMStream(AudioApplication.Mixed)) {
                    try {
                         await input.CopyToAsync(output, _PlayerStateData.InterruptSource.Token);
                         await output.FlushAsync();
                         FFMPEGSource.Kill();
                    } catch (OperationCanceledException) {
                         return;
                    } catch (Exception e) {
                         await Log("generic exception: " + e.Message);
                    }
               }; // point of potential Error
               await _PlayerStateData.StateLock.WaitAsync();
          } while (await TryPopQueue());

          // natural player exit (the queue has become empty)
          _PlayerStateData.CurrentState = PlayerState.Idle;
          _PlayerStateData.StateLock.Release();
     }

     private async Task OnDisconnectAsync(ulong id) {
          await Logger.LogAsync("[Debug/MP3Handler/OnDisconnectAsync] Triggered");
          // await _PlayerStateData.StateLock.WaitAsync();
          // switch (_PlayerStateData.CurrentState) {
          //      case PlayerState.Paused:
                    
          //           break;
          // }
          // if ( == PlayerState.Paused)
          // _PlayerStateData.StateLock.Release();
     }
}