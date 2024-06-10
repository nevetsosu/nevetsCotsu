using AudioPipeline;
using Discord;
using Discord.Audio;
using System.Diagnostics;
using System.Collections.Concurrent;

public class MP3Handler {
     private enum PlayerState {
          Play, Pause, Paused, Exit, Skip, None, Resume
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
     private CancellationTokenSource InteruptSource;
     private ILogger Logger;
     private PlayerStateData _PlayerStateData;

     public int QueueCount { get => SongQueue.Count; }
     public MP3Handler(VoiceStateManager voiceStateManager, ILogger logger) {
          _VoiceStateManager = voiceStateManager;
          SongQueue = new();
          InteruptSource = new();
          _PlayerStateData = new();

          Logger = logger;
     }

     public void ClearQueue() {
          SongQueue.Clear();
     }

     public void AddQueue(MP3Entry entry) {
          SongQueue.Enqueue(entry);
     }

     public async Task<bool> SkipSong() {
          _PlayerStateData.CurrentState = PlayerState.Skip;

          InterruptPlayer();
          if (!await TryPopQueue()) return false;

          if (_VoiceStateManager.ConnectedVoiceChannel == null) return false;
          else await StartPlayer(_VoiceStateManager.ConnectedVoiceChannel);

          return true;
     }

     private void InterruptPlayer() {
          InteruptSource.Cancel();
          InteruptSource = new CancellationTokenSource();
     }

     public void TestInterrupt() { // a semaphore should be used for anything that causes an interrupt (interrupt causer will acquire and the player will release)
          _PlayerStateData.CurrentState = PlayerState.Play;
          InterruptPlayer();
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
          await StartPlayer(targetChannnel).ConfigureAwait(true);
          return true;
     }

     public async Task StartPlayer(IVoiceChannel targetChannnel) {
          var Log = async (string str) => await Logger.LogAsync("[Debug/StartPlayer] " + str);
          IAudioClient? AudioClient = await _VoiceStateManager.ConnectAsync(targetChannnel);
          if (AudioClient == null) return;

          while (SongQueue.Count > 0) {
               if (_PlayerStateData.CurrentFFMPEGSource == null) {
                    await Log("_PlayerStateData.CurrentFFMPEGSource is null, going to the next song");
                    continue;
               }
               Stream input = _PlayerStateData.CurrentFFMPEGSource.StandardOutput.BaseStream;
               using (Stream output = AudioClient.CreatePCMStream(AudioApplication.Mixed)) {
                    try {
                         await input.CopyToAsync(output, InteruptSource.Token);
                    } catch (OperationCanceledException) {
                         break;
                    } catch (Exception e) {
                         await Log("generic exception: " + e.Message);
                    }
               }; // point of potential Error
          }
     }
}