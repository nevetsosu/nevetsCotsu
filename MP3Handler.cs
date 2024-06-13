using Discord;
using Discord.Audio;
using System.Diagnostics;
using System.Collections.Concurrent;

public class MP3Handler {

     public struct SongData {
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
          public Task CurrentPlayerTask; // per mp3 instance
          public MP3Entry? CurrentEntry; // per song
          public PlayerState CurrentState; // global to the mp3 handler instance
          public SemaphoreSlim StateLock; // global to the mp3 handler instance
          public CancellationTokenSource InterruptSource; // per mp3 instance
          public long totalBytesWritten; // per song
          public PlayerStateData() {
               CurrentState = PlayerState.Idle;
               StateLock = new(1, 1);
               InterruptSource = new();
               CurrentPlayerTask = Task.CompletedTask;
               totalBytesWritten = 0;
               CurrentEntry = null;
          }
     }

     public class MP3Entry {
          public string URL;
          public Process? FFMPEG;

          public MP3Entry(string url, Process? ffmpeg = null) {
               URL = url;
               FFMPEG = ffmpeg;
          }
     }

     private VoiceStateManager _VoiceStateManager;
     private MP3Queue SongQueue;
     private ILogger Logger;
     private PlayerStateData _PlayerStateData;

     public int QueueCount { get => SongQueue.Count; }
     public MP3Handler(VoiceStateManager voiceStateManager, ILogger logger) {
          _VoiceStateManager = voiceStateManager;
          _PlayerStateData = new();
          SongQueue = new(_PlayerStateData);
          Logger = logger;
     }

     public void ClearQueue() {
          SongQueue.Clear();
     }

     public async Task Enqueue(MP3Entry entry) {
          await SongQueue.Enqueue(entry);
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
               await Enqueue(new MP3Entry(song));
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

          if (_PlayerStateData.CurrentEntry?.FFMPEG != null) _PlayerStateData.CurrentEntry.FFMPEG.Kill();

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
          var Log = async (string str) => await Logger.LogAsync("[Debug/TryPopQueue] " + str);
          MP3Entry? entry;
          if ((entry = await SongQueue.TryDequeue()) == null) return false;

          if (entry.FFMPEG == null) {
               await Log("Current Entry wasn't preloaded??? Attempting another load");
               entry.FFMPEG = await new FFMPEGHandler().TrySpawnYoutubeFFMPEG(entry.URL, null, 1.0f);
               if (entry.FFMPEG == null) return false;
          }

          _PlayerStateData.CurrentEntry = entry;
          _PlayerStateData.totalBytesWritten = 0;
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
               Process? FFMPEG = _PlayerStateData.CurrentEntry?.FFMPEG;
               if (FFMPEG == null) {
                    await Log("_PlayerStateData.CurrentFFMPEGSource is null, stoppping");
                    return;
               }
               _PlayerStateData.CurrentState = PlayerState.Playing;
               _PlayerStateData.StateLock.Release();

               Stream input = FFMPEG.StandardOutput.BaseStream;
               using (Stream output = AudioClient.CreatePCMStream(AudioApplication.Mixed)) {
                    try {
                         await CopyToAsync(input, output, _PlayerStateData.InterruptSource.Token);
                         // await input.CopyToAsync(output, _PlayerStateData.InterruptSource.Token);
                         await output.FlushAsync();
                         FFMPEG.Kill();
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
          return SongQueue.EntryList();
     }

     private async Task OnDisconnectAsync(ulong id) {
          // if i want all disconnects to make state idle, i can use the state lock and await the current player task and set state to idle
          // awaiting the current player should make this ensure current state
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
          if (_PlayerStateData.CurrentState != PlayerState.Paused && _PlayerStateData.CurrentState != PlayerState.Playing) {
               _PlayerStateData.StateLock.Release();
               return null;
          }
          MP3Entry? entry = _PlayerStateData.CurrentEntry;
          _PlayerStateData.StateLock.Release();

          return entry != null ? new SongData(entry.URL) : null;
     }

     public async Task<long> NowPlayingProgress() {
          var Log = async (string str) => await Logger.LogAsync("[Debug/NowPlayingProgress] " + str);
          long BufferIndex = -1;
          await _PlayerStateData.StateLock.WaitAsync();

          if (_PlayerStateData.CurrentEntry?.FFMPEG == null) {
               await Log("_PlayerStateData.CurrentFFMPEGSource is null");
               return BufferIndex;
          }
          BufferIndex = Interlocked.Read(ref _PlayerStateData.totalBytesWritten);
          _PlayerStateData.StateLock.Release();

          await Log("Index: " + BufferIndex);

          return BufferIndex / (48000 * 2 * 2); // bit rate * # channels * bit depth in bytes
     }

     public async Task CopyToAsync(Stream inputStream, Stream outputStream, CancellationToken token = default) {
          byte[] buffer = new byte[16];
          try {
               while (true) {
                    await inputStream.ReadExactlyAsync(buffer, 0, 16, token);
                    Interlocked.Add(ref _PlayerStateData.totalBytesWritten, 16);
                    await outputStream.WriteAsync(buffer, 0, 16, token).ConfigureAwait(false);
               }
          } catch (EndOfStreamException) {
               await Logger.LogAsync("end of read stream");
               throw new OperationCanceledException();
          } catch {
               throw new OperationCanceledException(token);
          }
     }

     private class MP3Queue {
          public int Count { get => SongQueue.Count; }
          private PlayerStateData _PlayerStateData;
          private ConcurrentQueue<MP3Entry> SongQueue;
          private FFMPEGHandler _FFMPEGHandler;
          private SemaphoreSlim sem;
          bool Preloaded;
          ILogger Logger;

          public MP3Queue(PlayerStateData playerStateData, ILogger? logger = null) {
               _PlayerStateData = playerStateData;
               SongQueue = new();
               sem = new(1, 1);
               Logger = logger ?? new DefaultLogger();
               _FFMPEGHandler = new();
               Preloaded = false;
          }

          public List<MP3Entry> EntryList() {
               return SongQueue.ToList();
          }

          public void Clear() {
               sem.Wait();

               MP3Entry? entry;
               if  (SongQueue.TryPeek(out entry) && entry?.FFMPEG != null) {
                    try {
                         entry.FFMPEG.Kill();
                    } catch {}
               }
               SongQueue.Clear();

               sem.Release();
          }

          public async Task Enqueue(MP3Entry entry) {
               sem.Wait();
               SongQueue.Enqueue(entry);
               await TryPreloadNext();
               sem.Release();
          }

          public async Task<MP3Entry?> TryDequeue() {
               sem.Wait();
               MP3Entry? e;
               if (SongQueue.TryDequeue(out e)) {
                    Preloaded = false;
                    await TryPreloadNext();
                    sem.Release();
                    return e;
               }
               sem.Release();
               return null;

          }

          // returns whether there the top of the queue is preloaded (it may already be preloaded)
          private async Task<bool> TryPreloadNext() {
               if (Preloaded) return true;
               MP3Entry? e;
               if (SongQueue.TryPeek(out e)) {
                    e.FFMPEG = await _FFMPEGHandler.TrySpawnYoutubeFFMPEG(e.URL, null, 1.0f);
                    Preloaded = true;
                    return true;
               }
               Preloaded = false;
               return false;
          }
     }
}