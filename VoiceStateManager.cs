using Serilog;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System.Diagnostics;

public class VoiceStateManager {
     public class VoiceState {
          public bool Connected { get; private set; }
          public IAudioClient? _AudioClient;
          public SocketVoiceChannel? _VoiceChannel;
          public Task ConnectionTask;
          public CancellationTokenSource InterruptSource;

          public VoiceState() {
               ConnectionTask = Task.CompletedTask;
               ResetState();
               InterruptSource = new CancellationTokenSource();
          }

          public void ResetState() {
               Connected = false;
               _AudioClient = null;
               _VoiceChannel = null;
          }

          public void SetConnected(IAudioClient audioClient, SocketVoiceChannel voiceChannel) {
               Connected = true;
               _AudioClient = audioClient;
               _VoiceChannel = voiceChannel;
          }

          public async Task Interrupt() {
               InterruptSource.Cancel();
               InterruptSource.Dispose();
               InterruptSource = new CancellationTokenSource();
               await ConnectionTask;
          }

          public SocketVoiceChannel? GetVoiceChannel() {
               return _VoiceChannel;
          }

          public IAudioClient GetAudioClient() {
               if (_AudioClient == null) throw new Exception("AudioClient cannot exist before connection is made");
               return _AudioClient;
          }

     }

     private VoiceState State;
     private readonly FFMPEGHandler _FFMPEGHandler;
     private Process FFMPEG;
     private readonly SemaphoreSlim Lock;

     public VoiceStateManager(FFMPEGHandler? ffmpegHandler = default) {
          State = new();
          Lock = new(1, 1);
          _FFMPEGHandler = ffmpegHandler ?? new();

          FFMPEG = _FFMPEGHandler.TrySpawnFFMPEG(null, null, 1.0f);
     }

     public SocketVoiceChannel? GetVoiceChannel() {
          Lock.Wait();
          SocketVoiceChannel? VoiceChannel = State.GetVoiceChannel();
          Lock.Release();
          return VoiceChannel;

     }
     public Stream GetInputStream() {
          return FFMPEG.StandardOutput.BaseStream;
     }

     public async Task ConnectAsync(SocketVoiceChannel targetVoiceChannel, Func <Exception, Task>? OnDisconnectAsync = null) {
          await Lock.WaitAsync();

          // return the current AudioClient if already connected on the channel
          if (State.Connected && State._VoiceChannel == targetVoiceChannel) {
               Lock.Release();
               return;
          }

          State.ResetState();

          // try to open a new voice connection
          IAudioClient? AudioClient;
          try {
               AudioClient = await targetVoiceChannel.ConnectAsync();
          } catch (Exception e) {
               Lock.Release();
               Log.Error("SocketVoiceChannel.ConnectAsync failed during the VoiceStateManager.ConnectAsync() process: " + e.Message);
               return;
          }

          // update state
          if (AudioClient == null) {
               Lock.Release();
               Log.Error("SocketVoiceChannel.ConnectAsync came back as null");
               return;
          }

          // Set Event Handlers
          AudioClient.ClientDisconnected += OnClientDisconnectAsync;

          // Set State
          State.SetConnected(AudioClient, targetVoiceChannel);

          // Start FFMPEG to Discord Connnection Handler
          State.ConnectionTask = Task.Run(() => HandleConnection(AudioClient, FFMPEG.StandardOutput.BaseStream));

          Lock.Release();
     }

     // copies from ffpmeg to discord
     // if interrupted using Interrupt(), should be awaited to make sure changes to state don't conflict
     private async Task HandleConnection(IAudioClient AudioClient, Stream FFMPEGStream) {
          using (Stream DiscordStream = AudioClient.CreateDirectPCMStream(AudioApplication.Mixed)) {
               try {
                    await FFMPEGStream.CopyToAsync(DiscordStream, State.InterruptSource.Token); // replace this with my custom copy to know when a read or write fails
               } catch (OperationCanceledException e) {
                    Log.Debug("FFMPEG to Discord copy canceled: " + e.Message);
               } catch (Exception e) {
                    Log.Warning("FFMPEG to Discord unexpectedly interrupted: " + e.ToString());
               }
          }

          State.ResetState();
     }

     // acquires sem and disconnects
     public async Task DisconnectAsync() {
          await Lock.WaitAsync();

          await NoSemDisconnectAsync(State.GetAudioClient()); 

          Lock.Release();
     }

     // assumes sem is already acquired
     private async Task NoSemDisconnectAsync(IAudioClient audioClient) {
          await State.Interrupt();
          await TryStopAsync(audioClient);
     }

     // assume sem is already acquired
     private async Task TryStopAsync(IAudioClient audioClient) {
          try {
               await audioClient.StopAsync();
          } catch (Exception e) {
               Log.Warning("tried to call IAudioClient.StopAsync but failed. perhaps its already disconnected?" + e.ToString());
          }
     }

     // call back for when memebers of the same voice channel disconnect
     private async Task OnClientDisconnectAsync(ulong id) {
          Log.Debug("ClientDisconnected: id " + id);
          await Lock.WaitAsync();

          IAudioClient AudioClient = State.GetAudioClient();
          SocketVoiceChannel? VoiceChannel = State.GetVoiceChannel();
          if (VoiceChannel == null) {
               Lock.Release();
               return;
          }

          int UserCount = VoiceChannel.ConnectedUsers.Count();

          Log.Debug($"# Remaining in Channel {VoiceChannel.Id}: {UserCount}");

          if (UserCount <= 1) await NoSemDisconnectAsync(AudioClient);

          Lock.Release();
     }
}