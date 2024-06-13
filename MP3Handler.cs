using AudioPipeline;
using Discord;
using Discord.Audio;
using System.Diagnostics;
using System.Collections.Concurrent;

public class MP3Handler {

     public class SongData {
          public string URL;
          public SongData(string url) {
               URL = url;
          }
     }

     public enum PlayerCommandStatus {
          EmptyQueue, Already, Ok, Disconnected
     }
     private enum PlayerState {
          Paused, Playing, Idle
     }

     private struct PlayerStateData {
          public Process? CurrentFFMPEGSource;
          public Task CurrentPlayerTask;
          public MP3Entry CurrentEntry;
          public PlayerState CurrentState;
          public SemaphoreSlim StateLock;
          public CancellationTokenSource InterruptSource;
          public long totalBytesWritten;
          public PlayerStateData() {
               CurrentState = PlayerState.Idle;
               CurrentFFMPEGSource = null;
               StateLock = new(1, 1);
               InterruptSource = new();
               CurrentPlayerTask = Task.CompletedTask;
               totalBytesWritten = 0;
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
     public async Task<PlayerCommandStatus> Pause() {
          await _PlayerStateData.StateLock.WaitAsync();

          if (_PlayerStateData.CurrentState == PlayerState.Paused) {
               _PlayerStateData.StateLock.Release();
               return PlayerCommandStatus.Already; // return ALREADY
          }

          if (_PlayerStateData.CurrentState != PlayerState.Playing) {
               _PlayerStateData.StateLock.Release();
               return PlayerCommandStatus.EmptyQueue;
          }
          InterruptPlayer();
          await _PlayerStateData.CurrentPlayerTask;

          _PlayerStateData.CurrentState = PlayerState.Paused;
          await Logger.LogAsync("state after pause: " + _PlayerStateData.CurrentState);

          _PlayerStateData.StateLock.Release();
          return PlayerCommandStatus.Ok; // return OK
     }

     public async Task<PlayerCommandStatus> TryPlay(IVoiceChannel targetChannnel, string? song = null) {
          await _PlayerStateData.StateLock.WaitAsync();
          if (!string.IsNullOrEmpty(song)) {
               AddQueue(new MP3Entry(song));
          }

          if (_PlayerStateData.CurrentState == PlayerState.Playing) {
               _PlayerStateData.StateLock.Release();
               return PlayerCommandStatus.Already; // return Already
          }

          if (_PlayerStateData.CurrentState != PlayerState.Paused && !await TryPopQueue()) {
               _PlayerStateData.StateLock.Release();
               return PlayerCommandStatus.EmptyQueue; // return EMPTYQUEUE
          }

          InterruptPlayer();
          await _PlayerStateData.CurrentPlayerTask;

          _PlayerStateData.CurrentPlayerTask = StartPlayer(targetChannnel);
          await _PlayerStateData.CurrentPlayerTask;

          return PlayerCommandStatus.Ok; // return GOOD
     }

     public async Task<PlayerCommandStatus> SkipSong() {
          await _PlayerStateData.StateLock.WaitAsync();

          InterruptPlayer();
          await _PlayerStateData.CurrentPlayerTask;

          if (_PlayerStateData.CurrentFFMPEGSource != null) _PlayerStateData.CurrentFFMPEGSource.Kill();

          if (!await TryPopQueue()) {
               _PlayerStateData.CurrentState = PlayerState.Idle;
               _PlayerStateData.StateLock.Release();
               return PlayerCommandStatus.EmptyQueue; // Empty Queue
          }

          if (_VoiceStateManager.ConnectedVoiceChannel == null) {
               _PlayerStateData.CurrentState = PlayerState.Idle;
               _PlayerStateData.StateLock.Release();
               return PlayerCommandStatus.Disconnected; // Disconnected 
          }

          _PlayerStateData.CurrentPlayerTask = StartPlayer(_VoiceStateManager.ConnectedVoiceChannel);
          await _PlayerStateData.CurrentPlayerTask;

          return PlayerCommandStatus.Ok; // OK
     }

     private void InterruptPlayer() {
          _PlayerStateData.InterruptSource.Cancel();
          _PlayerStateData.InterruptSource = new();
     }

     private async Task<bool> TryPopQueue() {
          MP3Entry entry;
          if (!SongQueue.TryDequeue(out entry)) return false;

          _PlayerStateData.CurrentFFMPEGSource = await new FFMPEGHandler(Logger).TrySpawnYoutubeFFMPEG(entry.URL, null, 1.0f);
          if (_PlayerStateData.CurrentFFMPEGSource == null) return false;
          _PlayerStateData.CurrentEntry = entry;
          return true;
     }

     // public async Task<PlayerCommandStatus> TryPlay(IVoiceChannel targetChannnel) {
     //      await _PlayerStateData.StateLock.WaitAsync();
     //      if (!await TryPopQueue()) return PlayerCommandStatus.EmptyQueue; // Empty Queue

     //      InterruptPlayer();
     //      await StartPlayer(targetChannnel);
     //      return PlayerCommandStatus.Ok;
     // }

     // It is ASSUMED that the StateLock is Already acquired BEFORE a call to the StartPlayer function
     private async Task StartPlayer(IVoiceChannel targetChannnel) {
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
                         await CopyToAsync(input, output, _PlayerStateData.InterruptSource.Token);
                         // await input.CopyToAsync(output, _PlayerStateData.InterruptSource.Token);
                         await output.FlushAsync();
                         FFMPEGSource.Kill();
                    } catch (OperationCanceledException) {
                         _PlayerStateData.CurrentState = PlayerState.Idle;
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

     public List<MP3Entry> GetQueueAsList() {
          return SongQueue.ToList();
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

     public async Task<SongData?> NowPlaying() {
          await _PlayerStateData.StateLock.WaitAsync();
          if (_PlayerStateData.CurrentState == PlayerState.Paused && _PlayerStateData.CurrentState == PlayerState.Playing) {
               _PlayerStateData.StateLock.Release();
               return null;
          }
          MP3Entry entry = _PlayerStateData.CurrentEntry;
          _PlayerStateData.StateLock.Release();

          return new SongData(entry.URL);
     }

     public async Task<long> NowPlayingProgress() {
          var Log = async (string str) => await Logger.LogAsync("[Debug/NowPlayingProgress] " + str);
          long BufferIndex = -1;
          await _PlayerStateData.StateLock.WaitAsync();

          if (_PlayerStateData.CurrentFFMPEGSource == null) {
               await Log("_PlayerStateData.CurrentFFMPEGSource is null");
               return BufferIndex;
          }
          BufferIndex = Interlocked.Read(ref _PlayerStateData.totalBytesWritten);
          _PlayerStateData.StateLock.Release();

          await Log("Index: " + BufferIndex);

          return BufferIndex / (48000 * 2 * 2);
     }

     public async Task CopyToAsync(Stream inputStream, Stream outputStream, CancellationToken token = default) {
          byte[] buffer = new byte[16];
          try {
               while (true) {
                    await inputStream.ReadExactlyAsync(buffer, 0, 16, token);
                    Interlocked.Add(ref _PlayerStateData.totalBytesWritten, 16);
                    await outputStream.WriteAsync(buffer, 0, 16, token).ConfigureAwait(false);
               }
          } catch {
               throw new OperationCanceledException(token);
          }
     }
}