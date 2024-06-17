using Discord;
using Discord.Audio;

public class VoiceStateManager {
     public IAudioClient? AudioClient;
     public IVoiceChannel? ConnectedVoiceChannel;
     private SemaphoreSlim Lock;
     private ILogger Logger;

     public VoiceStateManager(ILogger logger) {
          AudioClient = null;
          ConnectedVoiceChannel = null;
          Lock = new(1, 1);
          Logger = logger;
     }

     // returns current
     public async Task<IAudioClient?> ConnectAsync(IVoiceChannel targetVoiceChannel, Func <Exception, Task>? OnDisconnectAsync = null) {
          var Log = async (string str) => await Logger.LogAsync("[Debug/ConnectAsync] " + str);
          await Log("Starting ConnectAsync");

          await Lock.WaitAsync();
          if (AudioClient != null && AudioClient.ConnectionState == ConnectionState.Connected && ConnectedVoiceChannel == targetVoiceChannel) {
               Lock.Release();
               return AudioClient;
          }

          // Try Discord.Net IVoiceChannel.ConnectAsync
          IAudioClient? newAudioClient;
          try {
               await Log("here 2");
               newAudioClient = await targetVoiceChannel.ConnectAsync();
          } catch (Exception e) {
               ResetState();
               Lock.Release();
               await Log("Failed to connect to voice Channel: " + e.Message);
               return null;
          }

          if (newAudioClient != null) {
               AudioClient = newAudioClient;
               ConnectedVoiceChannel = targetVoiceChannel;
               AudioClient.Disconnected += OnDisconnectedAsync;
               AudioClient.ClientDisconnected += OnClientDisconnectAsync;
               if (OnDisconnectAsync != null) AudioClient.Disconnected += OnDisconnectAsync;
          } else {
               AudioClient = null;
               ConnectedVoiceChannel = null;
          }
          Lock.Release();

          return AudioClient;
     }

     public async Task DisconnectAsync(IVoiceChannel voiceChannel) {
          await Lock.WaitAsync();
          if (ConnectedVoiceChannel != null) {
               try {
                    await voiceChannel.DisconnectAsync();
               } catch {}
          }

          AudioClient = null;
          ConnectedVoiceChannel = null;

          Lock.Release();
     }

     public void ResetState() {
          AudioClient = null;
          ConnectedVoiceChannel = null;
     }

     public async Task OnDisconnectedAsync(Exception e) {
          var Log = async (string str) => await Logger.LogAsync("[Debug/OnDisconnectedAsync] " + str);
          await Log("reset voice state: " + e.Message);
          await Lock.WaitAsync();
          ResetState();
          Lock.Release();
     }

     public async Task OnClientDisconnectAsync(ulong id) {
          var Log = async (string str) => await Logger.LogAsync("[Debug/OnClientDisconnectAsync] " + str);
          await Log("ClientDisconnected: id " + id);
          await Lock.WaitAsync();
          if (AudioClient != null) {
               try {
                    await AudioClient.StopAsync();
               } catch {}
          }

          AudioClient = null;
          ConnectedVoiceChannel = null;

          Lock.Release();
     }
}