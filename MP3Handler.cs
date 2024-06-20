using Discord;
using Discord.Audio;
using System.Diagnostics;
using System.Collections.Concurrent;
using YoutubeExplode.Videos;
using Serilog;

public class MP3Handler {
     public enum PlayerCommandStatus {
          EmptyQueue, Already, Ok, Ok2, Disconnected, InvalidArgument, NotCurrentlyPlaying
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

     public class MP3Entry{
          public string VideoID;
          public Process? FFMPEG;
          public Video? VideoData;
          public IGuildUser? RequestUser;
          public MP3Entry(string videoID, IGuildUser? requestUser = null, Process? ffmpeg = null, Video? videoData = null) {
               VideoID = videoID;
               FFMPEG = ffmpeg;
               VideoData = videoData;
               RequestUser = requestUser;
          }
    }

     private VoiceStateManager _VoiceStateManager;
     private MP3Queue SongQueue;
     private PlayerStateData _PlayerStateData;

     public int QueueCount { get => SongQueue.Count; }
     public bool Looping { get => SongQueue.Looping; }

     public MP3Handler(VoiceStateManager voiceStateManager) {
          _VoiceStateManager = voiceStateManager;
          _PlayerStateData = new();
          SongQueue = new();
     }

     public void ClearQueue() {
          SongQueue.Clear();
     }

     public void Enqueue(MP3Entry entry) {
          SongQueue.Enqueue(entry);
     }

     // Pause will usually always succeed, but will return false if the player wasnt already playing something. other wise returns true
     public async Task<PlayerCommandStatus> Pause() {
          await _PlayerStateData.StateLock.WaitAsync();

          // check if its already paused
          if (_PlayerStateData.CurrentState == PlayerState.Paused) {
               _PlayerStateData.StateLock.Release();
               return PlayerCommandStatus.Already;
          }

          // check other non-playing conditions
          if (_PlayerStateData.CurrentState != PlayerState.Playing) {
               _PlayerStateData.StateLock.Release();
               return PlayerCommandStatus.EmptyQueue; // this actually serves as a "not currently playing" error
          }

          // Pause
          InterruptPlayer();
          await _PlayerStateData.CurrentPlayerTask;
          _PlayerStateData.CurrentState = PlayerState.Paused;

          _PlayerStateData.StateLock.Release();
          return PlayerCommandStatus.Ok;
     }

     public async Task<PlayerCommandStatus> TryPlay(IVoiceChannel targetChannel, MP3Entry? entry = null) {
          await _PlayerStateData.StateLock.WaitAsync();
          // queue as long as the VideoID is not null
          if (!string.IsNullOrEmpty(entry?.VideoID)) {
               Enqueue(entry);
          }

          // check if its playing already
          if (_PlayerStateData.CurrentState == PlayerState.Playing) {
               _PlayerStateData.StateLock.Release();
               return PlayerCommandStatus.Already;
          }

          // check if theres anything to play
          if (_PlayerStateData.CurrentState != PlayerState.Paused && !TryPopQueue()) {
               _PlayerStateData.StateLock.Release();
               return PlayerCommandStatus.EmptyQueue;
          }

          // gaurentee that no other player is going to be running at the same time
          InterruptPlayer();
          await _PlayerStateData.CurrentPlayerTask;

          // play
          _PlayerStateData.CurrentPlayerTask = Task.Run(() => StartPlayer(targetChannel, _PlayerStateData.InterruptSource.Token));

          return PlayerCommandStatus.Ok; // this return is not given back fast until the player stops
     }

     public async Task<PlayerCommandStatus> SkipSong() {
          await _PlayerStateData.StateLock.WaitAsync();

          // stop the current player and kill the audio process
          InterruptPlayer();
          await _PlayerStateData.CurrentPlayerTask;
          if (_PlayerStateData.CurrentEntry?.FFMPEG != null) _PlayerStateData.CurrentEntry.FFMPEG.Kill();

          // try to load another song
          if (!TryPopQueue()) {
               _PlayerStateData.CurrentState = PlayerState.Idle;
               _PlayerStateData.StateLock.Release();
               return PlayerCommandStatus.EmptyQueue; // Empty Queue
          }

          // check if the bot is still connected
          IVoiceChannel? targetChannel = _VoiceStateManager.ConnectedVoiceChannel;
          if (targetChannel == null) {
               _PlayerStateData.CurrentState = PlayerState.Idle;
               _PlayerStateData.StateLock.Release();
               return PlayerCommandStatus.Disconnected; // this is more likely to actually indicate the the bot hasnt connected for the first time yet
          }

          _PlayerStateData.CurrentPlayerTask = Task.Run(() => StartPlayer(targetChannel, _PlayerStateData.InterruptSource.Token));

          return PlayerCommandStatus.Ok; // OK
     }

     // should be called with the state lock acquired
     private void InterruptPlayer() {
          _PlayerStateData.InterruptSource.Cancel();
          _PlayerStateData.InterruptSource = new();
     }

     // should be called with the state lock acquired
     private bool TryPopQueue() {
          MP3Entry? entry;
          if ((entry = SongQueue.TryDequeue()) == null) return false;

          // preloaded again if the entry wasnt already preloaded
          if (entry.FFMPEG == null) {
               Log.Debug("Current Entry wasn't preloaded??? Attempting another load");
               entry.FFMPEG = new FFMPEGHandler().TrySpawnYoutubeFFMPEG(entry.VideoID, null, 1.0f);
               if (entry.FFMPEG == null) return false; // if the preload doesnt work again
          }

          _PlayerStateData.CurrentEntry = entry;
          _PlayerStateData.totalBytesWritten = 0;
          return true;
     }

     // state lock should already be acquired on call
     private async Task StartPlayer(IVoiceChannel targetChannnel, CancellationToken token) {
          // return if failed to get AudioClient
          IAudioClient? AudioClient = await _VoiceStateManager.ConnectAsync(targetChannnel, OnDisconnectAsync);
          if (AudioClient == null) {
               Log.Debug("failed to acquire AudioClient, exiting Player");
               return;
          }

          do {
               Process? FFMPEG = _PlayerStateData.CurrentEntry?.FFMPEG;
               if (FFMPEG == null) {
                    Log.Debug("_PlayerStateData.CurrentEntry?.FFMPEG is null, exiting Player");
                    return;
               }
               _PlayerStateData.CurrentState = PlayerState.Playing;
               _PlayerStateData.StateLock.Release();

               // main player time spent
               Stream input = FFMPEG.StandardOutput.BaseStream;
               using (Stream output = AudioClient.CreatePCMStream(AudioApplication.Mixed)) {
                    try {
                         await CopyToAsync(input, output, token);
                         await output.FlushAsync();
                         FFMPEG.Kill();
                    } catch (OperationCanceledException) { // Happens on Interrupt or when bot is disconnected (writing fails)
                         _PlayerStateData.CurrentState = PlayerState.Paused;
                         return;
                    } catch (Exception e) {
                         Log.Debug("generic exception: " + e.Message);
                    } finally {
                         Log.Debug("canceled from playing song: " + _PlayerStateData.CurrentEntry?.VideoID);
                    }
               };
               // reaches here when song is finished or there is a read failure (CopyToAsync returns immediately on ReadFailure)

               await _PlayerStateData.StateLock.WaitAsync();
          } while (TryPopQueue());

          // natural player exit (the queue has become empty)
          _PlayerStateData.CurrentState = PlayerState.Idle;
          _PlayerStateData.StateLock.Release();
     }

     public List<MP3Entry> GetQueueAsList() {
          return SongQueue.EntryList();
     }

     private async Task OnDisconnectAsync(Exception e) {
          Log.Debug("[Debug/MP3Handler/OnDisconnectAsync] Triggered");
          await Task.CompletedTask;
     }

     public async Task<MP3Entry?> NowPlaying() {
          await _PlayerStateData.StateLock.WaitAsync();

          // check if the player is paused or playing
          if (_PlayerStateData.CurrentState != PlayerState.Paused && _PlayerStateData.CurrentState != PlayerState.Playing) {
               _PlayerStateData.StateLock.Release();
               return null;
          }
          MP3Entry? entry = _PlayerStateData.CurrentEntry;
          if (entry == null) {
               _PlayerStateData.StateLock.Release();
               return null;
          }

          // make a copy of the entry to avoid reflecting changes during other state changes
          MP3Entry copy = new MP3Entry(entry.VideoID, entry.RequestUser, entry.FFMPEG, entry.VideoData);

          _PlayerStateData.StateLock.Release();

          return copy;
     }

     // returns the number of seconds (based on calculation from the byte stream) of data that have been copied out to discord from the source
     // returns -1 on error
     public async Task<long> NowPlayingProgress() {
          long BufferIndex = -1;
          await _PlayerStateData.StateLock.WaitAsync();

          if (_PlayerStateData.CurrentEntry?.FFMPEG == null) {
               Log.Debug("_PlayerStateData.CurrentFFMPEGSource is null");
               _PlayerStateData.StateLock.Release();
               return BufferIndex;
          }
          BufferIndex = Interlocked.Read(ref _PlayerStateData.totalBytesWritten);

          _PlayerStateData.StateLock.Release();
          return BufferIndex / (48000 * 2 * 2); // bit rate * # channels * bit depth in bytes
     }

     public async Task CopyToAsync(Stream inputStream, Stream outputStream, CancellationToken token = default) {
          byte[] buffer = new byte[16];

          while (true) {
               // read failures mean immediate exit
               try {
                    await inputStream.ReadExactlyAsync(buffer, 0, 16); // no cancellation token here since using one could desync the totalBytesWritten count
               } catch (Exception e) {
                    Log.Debug("UNEXPECTED READ FAIL: " + e.Message);
                    return;
               }

               Interlocked.Add(ref _PlayerStateData.totalBytesWritten, 16);
               // write errors mean OperationCanceledException
               try {
                    await outputStream.WriteAsync(buffer, 0, 16, token).ConfigureAwait(false);
               } catch (OperationCanceledException e) {
                    Log.Debug("write canceled: " + e.Message);
                    throw new OperationCanceledException();
               } catch (Exception e) {
                    Log.Debug("UNEXPECTED WRITE FAIL: " + e.Message);
                    throw new OperationCanceledException(token);
               }
          }
     }

     public async Task<PlayerCommandStatus> ToggleLooping() {
          await _PlayerStateData.StateLock.WaitAsync();

          if (SongQueue.Looping) { // turn off if on
               await SongQueue.DisableLooping();
               _PlayerStateData.StateLock.Release();
               return PlayerCommandStatus.Ok; // not looping
          }
          else if (_PlayerStateData.CurrentEntry != null) { // make sure there is an entry to loop
               await SongQueue.EnableLooping(_PlayerStateData.CurrentEntry);
               _PlayerStateData.StateLock.Release();
               return PlayerCommandStatus.Ok2; // looping
          }

          _PlayerStateData.StateLock.Release();
          return PlayerCommandStatus.Disconnected;
     }

     private class MP3Queue {
          public int Count { get => SongQueue.Count; }
          private ConcurrentQueue<MP3Entry> SongQueue;
          private FFMPEGHandler _FFMPEGHandler;
          private SemaphoreSlim sem;
          private bool SongQueueNextPreloaded;
          private MP3Entry? LoopingEntry;
          public bool Looping { get; private set; }

          public MP3Queue() {
               SongQueue = new();
               sem = new(1, 1);
               _FFMPEGHandler = new();
               SongQueueNextPreloaded = false;
               Looping = false;
               LoopingEntry = null;
          }

          public List<MP3Entry> EntryList() {
               return SongQueue.ToList();
          }

          public void Clear() {
               sem.Wait();

               // kill preloaded audio if there is any
               MP3Entry? entry;
               if  (SongQueue.TryPeek(out entry) && entry?.FFMPEG != null) {
                    try {
                         entry.FFMPEG.Kill();
                    } catch {}
               }
               SongQueue.Clear();

               sem.Release();
          }

          public void Enqueue(MP3Entry entry) {
               sem.Wait();
               SongQueue.Enqueue(entry);
               Log.Debug($"Is the queue currently preloaded?: {TryPreloadNext()}");
               sem.Release();
          }

          public MP3Entry? TryDequeue() {
               sem.Wait();
               MP3Entry? entry;

               // if looping, return the current looping entry and prepare a new looping entry
               if (Looping && LoopingEntry != null) {
                    entry = LoopingEntry;
                    LoopingEntry = new MP3Entry(entry.VideoID, entry.RequestUser, _FFMPEGHandler.TrySpawnYoutubeFFMPEG(entry.VideoID, null, 1.0f));
                    sem.Release();
                    return entry;
               }

               // return the top entry and preload the next one
               if (SongQueue.TryDequeue(out entry)) {
                    SongQueueNextPreloaded = false;
                    TryPreloadNext();
                    sem.Release();
                    return entry;
               }
               sem.Release();

               // null when queue is empty
               return null;
          }

          // returns whether there the top of the queue is preloaded (it may already be preloaded)
          // assumes that the sem is already acquired
          private bool TryPreloadNext() {
               if (SongQueueNextPreloaded) return true;
               MP3Entry? entry;
               if (SongQueue.TryPeek(out entry)) {
                    entry.FFMPEG = _FFMPEGHandler.TrySpawnYoutubeFFMPEG(entry.VideoID, null, 1.0f);
                    SongQueueNextPreloaded = true;
                    return true;
               }
               SongQueueNextPreloaded = false;
               return false;
          }

          public async Task EnableLooping(MP3Entry entry) {
               await sem.WaitAsync();

               // return if already looping
               if (Looping) {
                    sem.Release();
                    return;
               }
               LoopingEntry = LoopingEntry ?? new MP3Entry(entry.VideoID, entry.RequestUser, _FFMPEGHandler.TrySpawnYoutubeFFMPEG(entry.VideoID, null, 1.0f), entry.VideoData);

               Looping = true;
               sem.Release();
          }

          public async Task DisableLooping() {
               await sem.WaitAsync();

               // return if already not looping
               if (!Looping) {
                    sem.Release();
                    return;
               }

               Looping = false;
               sem.Release();
          }
     }
}