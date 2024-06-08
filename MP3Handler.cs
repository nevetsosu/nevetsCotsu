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

     private enum State {
          Play, Pause, Resume, Exit, None
     }

     private VoiceStateManager _VoiceStateManager;
     private Process? CurrentFFMPEGProcess;
     private Queue<MP3Entry> Queue;
     private ReaderWriterLockSlim QueueLock;
     private CancellationTokenSource InteruptSource;
     private ILogger Logger;
     private Process? CurrentFFMPEGSource;
     private State CurrentState;

     public int QueueCount { get => Queue.Count; }
     public MP3Handler(VoiceStateManager voiceStateManager, ILogger logger) {
          _VoiceStateManager = voiceStateManager;
          CurrentFFMPEGProcess = null;
          Queue = new();
          QueueLock = new();
          Logger = logger;
          CurrentState = State.None;
          InteruptSource = new();
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
          CurrentState = State.Play;
          InterruptPlayer();
     }

     private void InterruptPlayer() {
          InteruptSource.Cancel();
          InteruptSource = new CancellationTokenSource();
     }

     public void TestInterrupt() { // a semaphore should be used for anything that causes an interrupt (interrupt causer will acquire and the player will release)
          CurrentState = State.Resume;
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

               CurrentFFMPEGSource = await new FFMPEGHandler(Logger).TrySpawnYoutubeFFMPEG(entry.URL, null, 1.0f);
               if (CurrentFFMPEGSource == null) { QueueLock.EnterWriteLock(); continue; }
               Stream input = CurrentFFMPEGSource.StandardOutput.BaseStream;
               using (Stream output = AudioClient.CreatePCMStream(AudioApplication.Mixed)) {
                    while (true) {
                         try {
                              await input.CopyToAsync(output, InteruptSource.Token);
                         } catch (OperationCanceledException) {
                              if (CurrentState == State.Resume) {
                                   continue;
                              } else if (CurrentState == State.Play) {
                                   await Log("going to next song");
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