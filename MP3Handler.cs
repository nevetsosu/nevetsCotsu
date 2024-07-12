using Discord;
using Discord.WebSocket;
using Discord.Audio;
using System.Diagnostics;
using YoutubeExplode.Videos;
using Serilog;

using MP3Logic;

namespace MP3Logic {
     public class MP3Handler;

     public class MP3Entry : ICloneable {
          public Process? FFMPEG;
          public Video VideoData;
          public SocketGuildUser? RequestUser;
          public MP3Entry(Video videoData, SocketGuildUser? requestUser = null, Process? ffmpeg = null) {
               FFMPEG = ffmpeg;
               VideoData = videoData;
               RequestUser = requestUser;
          }

          public object Clone() {
               return new MP3Entry(VideoData, RequestUser, FFMPEG);
          }
    }
    public enum PlayerCommandStatus {
          EmptyQueue, Already, Ok, Ok2, Disconnected, InvalidArgument, OutOfRange, NotCurrentlyPlaying
     }
}

public class MP3Handler {
     private enum PlayerState {
          Paused, Playing, Idle
     }

     private struct PlayerStateData {
          public Task CurrentPlayerTask; // per mp3 instance
          public MP3Entry? CurrentEntry; // per song
          public PlayerState CurrentState; // global to the mp3 handler instance
          public SemaphoreSlim StateLock; // global to the mp3 handler instance
          public CancellationTokenSource InterruptSource; // per mp3 instance
          public TimeSpan StartTime;
          public long BytesWritten; // per song
          public PlayerStateData() {
               CurrentState = PlayerState.Idle;
               StateLock = new(1, 1);
               InterruptSource = new();
               CurrentPlayerTask = Task.CompletedTask;
               BytesWritten = 0;
               CurrentEntry = null;
          }
     }

     private readonly VoiceStateManager _VoiceStateManager;
     private readonly MP3Queue SongQueue;
     private readonly FFMPEGHandler _FFMPEGHandler;
     private PlayerStateData _PlayerStateData;

     public float Volume {
          get => _FFMPEGHandler.Volume;
          set => _FFMPEGHandler.Volume = value;
     }

     public MP3Handler(VoiceStateManager voiceStateManager, FFMPEGHandler? ffmpegHandler = null) {
          _VoiceStateManager = voiceStateManager;
          _PlayerStateData = new();
          _FFMPEGHandler = ffmpegHandler ?? new FFMPEGHandler();
          SongQueue = new(_FFMPEGHandler);
     }
     public int QueueCount => SongQueue.Count;
     public bool Looping => SongQueue.Looping;
     public void ClearQueue() => SongQueue.Clear();
     public void Enqueue(MP3Entry entry) => SongQueue.Enqueue(entry);
     public void Remove(int index) => SongQueue.Remove(index);
     public void Swap(int IndexA, int IndexB) => SongQueue.Swap(IndexA, IndexB);
     public void SkipTo(int index) => SongQueue.SkipTo(index);
     public MP3Entry? GetEntry(int index) => SongQueue.GetEntry(index);

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
          await InterruptPlayer();
          _PlayerStateData.CurrentState = PlayerState.Paused;

          _PlayerStateData.StateLock.Release();
          return PlayerCommandStatus.Ok;
     }

     public async Task<PlayerCommandStatus> Seek(TimeSpan start) {
          await _PlayerStateData.StateLock.WaitAsync();

          // check if theres anything to play
          if (_PlayerStateData.CurrentState != PlayerState.Playing) {
               _PlayerStateData.StateLock.Release();
               return PlayerCommandStatus.NotCurrentlyPlaying;
          }

          TimeSpan? Duration = _PlayerStateData.CurrentEntry?.VideoData?.Duration;
          Log.Debug("Duration: " + Duration);
          if (Duration == null) {
               _PlayerStateData.StateLock.Release();
               return PlayerCommandStatus.InvalidArgument;
          } else if (start > (Duration - new TimeSpan(0, 0, 30))) { // cannot seek within 30 seconds of the end (seeking too close to the end causes weird behavior with ffmpeg)
               _PlayerStateData.StateLock.Release();
               return PlayerCommandStatus.OutOfRange;
          }

          // gaurentee that no other player is going to be running at the same time
          await InterruptPlayer();

          // check if the bot is still connected
          SocketVoiceChannel? targetChannel = _VoiceStateManager.ConnectedVoiceChannel;
          if (targetChannel == null || _PlayerStateData.CurrentEntry?.FFMPEG == null) {
               _PlayerStateData.CurrentState = PlayerState.Paused;
               _PlayerStateData.StateLock.Release();
               return PlayerCommandStatus.Disconnected; // this is more likely to actually indicate the the bot hasnt connected for the first time yet
          }

          // kill the previous FFMPEG
          Process FFMPEG = _PlayerStateData.CurrentEntry.FFMPEG;
          _ = Task.Run(() => FFMPEGHandler.CleanProcess(FFMPEG));
          _PlayerStateData.CurrentEntry.FFMPEG = null;

          // Spawn new FFMPEG at seek location
          _PlayerStateData.StartTime = start;
          Interlocked.And(ref _PlayerStateData.BytesWritten, 0);
          string mediaURL = await YTAPIManager.GetMediaURL(_PlayerStateData.CurrentEntry.VideoData);
          _PlayerStateData.CurrentEntry.FFMPEG = _FFMPEGHandler.TrySpawnYoutubeFFMPEG(mediaURL, null, 1.0f, start);

          // play
          _PlayerStateData.CurrentPlayerTask = Task.Run(() => StartPlayer(targetChannel, _PlayerStateData.InterruptSource.Token));

          return PlayerCommandStatus.Ok;
     }

     public async Task<PlayerCommandStatus> TryPlay(SocketVoiceChannel targetChannel, MP3Entry? entry = null) {
          await _PlayerStateData.StateLock.WaitAsync();
          // queue as long as the VideoID is not null
          if (!string.IsNullOrEmpty(entry?.VideoData.Id)) {
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
          await InterruptPlayer();

          // play
          _PlayerStateData.CurrentPlayerTask = Task.Run(() => StartPlayer(targetChannel, _PlayerStateData.InterruptSource.Token));

          return PlayerCommandStatus.Ok; // this return is not given back fast until the player stops
     }

     public async Task<PlayerCommandStatus> SkipSong() {
          await _PlayerStateData.StateLock.WaitAsync();

          // stop the current player and kill the audio process
          await InterruptPlayer();
          if (_PlayerStateData.CurrentEntry?.FFMPEG != null) {
               Process FFMPEG = _PlayerStateData.CurrentEntry.FFMPEG;
               _ = Task.Run(() => FFMPEGHandler.CleanProcess(FFMPEG));
               _PlayerStateData.CurrentEntry.FFMPEG = null;
               Log.Debug("clean previous entry process");
          }

          // try to load another song
          if (!TryPopQueue()) {
               _PlayerStateData.CurrentState = PlayerState.Idle;
               _PlayerStateData.StateLock.Release();
               return PlayerCommandStatus.EmptyQueue; // Empty Queue
          }

          // check if the bot is still connected
          SocketVoiceChannel? targetChannel = _VoiceStateManager.ConnectedVoiceChannel;
          if (targetChannel == null) {
               _PlayerStateData.CurrentState = PlayerState.Idle;
               _PlayerStateData.StateLock.Release();
               return PlayerCommandStatus.Disconnected; // this is more likely to actually indicate the the bot hasnt connected for the first time yet
          }

          _PlayerStateData.CurrentPlayerTask = Task.Run(() => StartPlayer(targetChannel, _PlayerStateData.InterruptSource.Token));

          return PlayerCommandStatus.Ok; // OK
     }

     // should be called with the state lock acquired
     private async Task InterruptPlayer() {
          _PlayerStateData.InterruptSource.Cancel();
          _PlayerStateData.InterruptSource = new();
          await _PlayerStateData.CurrentPlayerTask;
     }

     // should be called with the state lock acquired
     private bool TryPopQueue() {
          MP3Entry? entry;
          if ((entry = SongQueue.TryDequeue()) == null) return false;

          // preloaded again if the entry wasnt already preloaded
          if (entry.FFMPEG == null) {
               Log.Debug("Current Entry wasn't preloaded??? Attempting another load");
               entry.FFMPEG = _FFMPEGHandler.TrySpawnYoutubeFFMPEG(YTAPIManager.GetMediaURL(entry.VideoData).Result, null, 1.0f);
               if (entry.FFMPEG == null) return false; // if the load doesnt work again
          }

          _PlayerStateData.CurrentEntry = entry;
          _PlayerStateData.BytesWritten = 0;
          _PlayerStateData.StartTime = TimeSpan.Zero;
          return true;
     }

     // state lock should already be acquired on call
     private async Task StartPlayer(SocketVoiceChannel targetChannnel, CancellationToken token) {
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
                         _ = Task.Run(() => FFMPEGHandler.CleanProcess(FFMPEG));
                    } catch (OperationCanceledException) { // Happens on Interrupt or when bot is disconnected (writing fails)
                         _PlayerStateData.CurrentState = PlayerState.Paused;
                         return;
                    } catch (Exception e) {
                         Log.Debug("generic exception: " + e.Message);
                    } finally {
                         Log.Debug("canceled from playing song: " + _PlayerStateData.CurrentEntry?.VideoData.Id);
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

          _PlayerStateData.StateLock.Release();

          return entry.Clone() as MP3Entry;
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
          BufferIndex = Interlocked.Read(ref _PlayerStateData.BytesWritten);
          _PlayerStateData.StateLock.Release();
          return BufferIndex / (48000 * 2 * 2) + (long)_PlayerStateData.StartTime.TotalSeconds; // bit rate * # channels * bit depth in bytes
     }

     // public void SetVolume(float volume) => _FFMPEGHandler.SetVolume(volume);

     public async Task CopyToAsync(Stream inputStream, Stream outputStream, CancellationToken token = default) {
          const int BUFFERSIZE = 16;
          byte[] buffer = new byte[BUFFERSIZE];
          while (true) {
               // read failures mean immediate exit
               try {
                    await inputStream.ReadExactlyAsync(buffer, 0, BUFFERSIZE); // no cancellation token here since using one could desync the totalBytesWritten count
               } catch (Exception e) {
                    Log.Debug("UNEXPECTED READ FAIL: " + e.Message);
                    return;
               }

               Interlocked.Add(ref _PlayerStateData.BytesWritten, BUFFERSIZE);
               // write errors mean OperationCanceledException
               try {
                    await outputStream.WriteAsync(buffer, 0, BUFFERSIZE, token);
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
}