using Serilog;
using Discord;
using Discord.Audio;
using Discord.WebSocket;

public class VoiceStateManager {
     public IAudioClient? AudioClient;
     public SocketVoiceChannel? ConnectedVoiceChannel;
     private readonly SemaphoreSlim Lock;

     public VoiceStateManager() {
          AudioClient = null;
          ConnectedVoiceChannel = null;
          Lock = new(1, 1);
     }

     public async Task<IAudioClient?> ConnectAsync(SocketVoiceChannel targetVoiceChannel, Func <Exception, Task>? OnDisconnectAsync = null) {
          Log.Debug("Starting ConnectAsync");

          await Lock.WaitAsync();

          // return the current AudioClient if already connected on the channel
          if (AudioClient != null && AudioClient.ConnectionState == ConnectionState.Connected && ConnectedVoiceChannel == targetVoiceChannel) {
               Lock.Release();
               return AudioClient;
          }

          // try to open a new voice connection
          IAudioClient? newAudioClient;
          try {
               newAudioClient = await targetVoiceChannel.ConnectAsync(selfDeaf: true, selfMute: false);
          } catch (Exception e) {
               ResetState();
               Lock.Release();
               Log.Debug("Failed to connect to voice Channel: " + e.Message);
               return null;
          }

          // update state
          if (newAudioClient != null) {
               Log.Debug("Registering a new Audio Client");
               AudioClient = newAudioClient;
               ConnectedVoiceChannel = targetVoiceChannel;
               // AudioClient.Disconnected += OnDisconnectedAsync;
               AudioClient.ClientDisconnected += OnClientDisconnectAsync;
               if (OnDisconnectAsync != null) AudioClient.Disconnected += OnDisconnectAsync;
          } else {
               ResetState();
          }

          Lock.Release();

          return newAudioClient;
     }

     public async Task DisconnectAsync() {
          await Lock.WaitAsync();
          if (AudioClient != null) {
               try {
                    await AudioClient.StopAsync();
               } catch {}
          }
          ResetState();

          Lock.Release();
     }

     // this should only be called when the StateLock is already acquired
     private void ResetState() {
          AudioClient = null;
          ConnectedVoiceChannel = null;
     }

     // call back for when the bot disconnects
     // public async Task OnDisconnectedAsync(Exception e) {
     //      Log.Debug("reset voice state: " + e.Message);

     //      // await Lock.WaitAsync();
     //      // ResetState();
     //      // Lock.Release();
     // }

     // call back for when memebers of the same voice channel disconnect
     public async Task OnClientDisconnectAsync(ulong id) {
          Log.Debug("ClientDisconnected: id " + id);
          await Lock.WaitAsync();

          // leave when the bot is the only one in the channnel
          if (ConnectedVoiceChannel != null) {
               int count = ConnectedVoiceChannel.ConnectedUsers.Count();
               Log.Debug($"number of people remaining in channel {ConnectedVoiceChannel.Id}: {count}");

               if (count <= 1) {
                    // disconnect
                    if (AudioClient != null) {
                         try {
                              await AudioClient.StopAsync();
                         } catch {}
                    }
                    ResetState();
               }
          }

          Lock.Release();
     }
}