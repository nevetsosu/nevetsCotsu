using AudioPipeline;
using Discord;
using Discord.Audio;
using System.Diagnostics;

public class MP3Handler {
     public struct MP3Entry {
          public string URL;

          public MP3Entry(string url) {
               URL = url;
          }
     }
     private enum PlayerState {
          Play, Pause, Resume, Exit, Skip, None
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

     private VoiceStateManager _VoiceStateManager;
     private Queue<MP3Entry> Queue;
     private ReaderWriterLockSlim QueueLock;
     private CancellationTokenSource InteruptSource;
     private ILogger Logger;
     private PlayerStateData _PlayerStateData;

     public int QueueCount { get => Queue.Count; }
     public MP3Handler(VoiceStateManager voiceStateManager, ILogger logger) {
          _VoiceStateManager = voiceStateManager;
          Queue = new();
          QueueLock = new();
          InteruptSource = new();
          _PlayerStateData = new();

          Logger = logger;
     }

     public void ClearQueue() {
          QueueLock.EnterWriteLock();
          Queue.Clear();
          QueueLock.ExitWriteLock();
     }

     public void AddQueue(MP3Entry entry) {
          QueueLock.EnterWriteLock();
          Queue.Enqueue(entry);
          QueueLock.ExitWriteLock();
     }

     public void SkipSong() {
          _PlayerStateData.CurrentState = PlayerState.Play;
          InterruptPlayer();
     }

     private void InterruptPlayer() {
          InteruptSource.Cancel();
          InteruptSource = new CancellationTokenSource();
     }

     public void TestInterrupt() { // a semaphore should be used for anything that causes an interrupt (interrupt causer will acquire and the player will release)
          _PlayerStateData.CurrentState = PlayerState.Resume;
          InterruptPlayer();
     }

     public async Task StartPlayer(IVoiceChannel targetChannnel) {
          var Log = async (string str) => await Logger.LogAsync("[Debug/StartPlayer] " + str);
          IAudioClient? AudioClient = await _VoiceStateManager.ConnectAsync(targetChannnel);
          if (AudioClient == null) return;

          QueueLock.EnterWriteLock();
          while (Queue.Count > 0) {
               MP3Entry entry = Queue.Dequeue();
               QueueLock.ExitWriteLock();

               _PlayerStateData.CurrentFFMPEGSource = await new FFMPEGHandler(Logger).TrySpawnYoutubeFFMPEG(entry.URL, null, 1.0f);
               if (_PlayerStateData.CurrentFFMPEGSource == null) { QueueLock.EnterWriteLock(); continue; }
               Stream input = _PlayerStateData.CurrentFFMPEGSource.StandardOutput.BaseStream;
               using (Stream output = AudioClient.CreatePCMStream(AudioApplication.Mixed)) {
                    while (true) {
                         try {
                              await input.CopyToAsync(output, InteruptSource.Token);
                         } catch (OperationCanceledException) {
                              if (_PlayerStateData.CurrentState == PlayerState.Resume) {
                                   continue;
                              } else if (_PlayerStateData.CurrentState == PlayerState.Skip) {
                                   await Log("skipping to next song");
                                   break;
                              } else break;
                         } catch (Exception e) {
                              await Log("generic exception: " + e.Message);
                         }
                    }
               }; // point of potential Error
               QueueLock.EnterWriteLock();
          }
          QueueLock.ExitWriteLock();
     }
}