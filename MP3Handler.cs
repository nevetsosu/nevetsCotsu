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
          public ReaderWriterLockSlim StateLock;
          public CancellationTokenSource InterruptSource;
          public PlayerStateData() {
               CurrentState = PlayerState.None;
               CurrentFFMPEGSource = null;
               StateLock = new();
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
     public bool Pause() {
          InterruptPlayer();
          _PlayerStateData.StateLock.EnterUpgradeableReadLock();

          if (_PlayerStateData.CurrentState != PlayerState.Playing) {
               _PlayerStateData.StateLock.ExitUpgradeableReadLock();
               return false;
          }

          _PlayerStateData.StateLock.EnterWriteLock();
          _PlayerStateData.CurrentState = PlayerState.Paused;
          _PlayerStateData.StateLock.ExitWriteLock();

          _PlayerStateData.StateLock.ExitUpgradeableReadLock();

          return true;
     }

     public async Task<bool> TryResume(IVoiceChannel targetChannnel) {
          _PlayerStateData.StateLock.EnterReadLock();
          if (_PlayerStateData.CurrentState == PlayerState.Playing) {
               _PlayerStateData.StateLock.ExitReadLock();
               return false;
          }
          if (_PlayerStateData.CurrentState != PlayerState.Paused && !await TryPopQueue()) {
               _PlayerStateData.StateLock.ExitReadLock();
               return false;
          }
          InterruptPlayer();
          _PlayerStateData.StateLock.ExitReadLock();
          await StartPlayer(targetChannnel);
          return true;
     }

     public async Task<bool> SkipSong() {
          InterruptPlayer();
          if (!await TryPopQueue()) return true;

          if (_VoiceStateManager.ConnectedVoiceChannel == null) return false;

          ChangeState(PlayerState.Skipping);
          await StartPlayer(_VoiceStateManager.ConnectedVoiceChannel);

          if (_PlayerStateData.CurrentFFMPEGSource != null)
          _PlayerStateData.CurrentFFMPEGSource.Kill();

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
          if (!await TryPopQueue()) return false;
          InterruptPlayer();
          await StartPlayer(targetChannnel);
          return true;
     }

     public async Task StartPlayer(IVoiceChannel targetChannnel) {
          var Log = async (string str) => await Logger.LogAsync("[Debug/StartPlayer] " + str);
          IAudioClient? AudioClient = await _VoiceStateManager.ConnectAsync(targetChannnel);
          if (AudioClient == null) return;

          _PlayerStateData.StateLock.EnterReadLock();
          do {
               Process? FFMPEGSource = _PlayerStateData.CurrentFFMPEGSource;
               _PlayerStateData.StateLock.ExitReadLock();

               if (FFMPEGSource == null) {
                    await Log("_PlayerStateData.CurrentFFMPEGSource is null, stoppping");
                    return;
               }
               ChangeState(PlayerState.Playing);
               Stream input = FFMPEGSource.StandardOutput.BaseStream;
               using (Stream output = AudioClient.CreatePCMStream(AudioApplication.Mixed)) {
                    try {
                         await input.CopyToAsync(output, _PlayerStateData.InterruptSource.Token);
                    } catch (OperationCanceledException) {
                         return;
                    } catch (Exception e) {
                         await Log("generic exception: " + e.Message);
                    }
                    FFMPEGSource.Kill();
               }; // point of potential Error
               _PlayerStateData.StateLock.EnterReadLock();
          } while (await TryPopQueue());
          _PlayerStateData.StateLock.ExitReadLock();
     }

     private void ChangeState(PlayerState state) {
          _PlayerStateData.StateLock.EnterWriteLock();
          _PlayerStateData.CurrentState = state;
          _PlayerStateData.StateLock.ExitWriteLock();
     }

     private PlayerState ReadState() {
          _PlayerStateData.StateLock.EnterWriteLock();
          PlayerState state = _PlayerStateData.CurrentState;
          _PlayerStateData.StateLock.ExitWriteLock();
          return state;
     }
}