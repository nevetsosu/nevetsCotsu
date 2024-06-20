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

     public async Task<IAudioClient?> ConnectAsync(IVoiceChannel targetVoiceChannel, Func <Exception, Task>? OnDisconnectAsync = null) {
          var Log = async (string str) => await Logger.LogAsync("[Debug/ConnectAsync] " + str);
          await Log("Starting ConnectAsync");

          await Lock.WaitAsync();

          // return the current AudioClient if already connected on the channel
          if (AudioClient != null && AudioClient.ConnectionState == ConnectionState.Connected && ConnectedVoiceChannel == targetVoiceChannel) {
               Lock.Release();
               return AudioClient;
          }

          // try to open a new voice connection
          IAudioClient? newAudioClient;
          try {
               newAudioClient = await targetVoiceChannel.ConnectAsync();
          } catch (Exception e) {
               ResetState();
               Lock.Release();
               await Log("Failed to connect to voice Channel: " + e.Message);
               return null;
          }

          // update state
          if (newAudioClient != null) {
               AudioClient = newAudioClient;
               ConnectedVoiceChannel = targetVoiceChannel;
               AudioClient.Disconnected += OnDisconnectedAsync;
               AudioClient.ClientDisconnected += OnClientDisconnectAsync;
               if (OnDisconnectAsync != null) AudioClient.Disconnected += OnDisconnectAsync;
          } else ResetState();

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
          ResetState();

          Lock.Release();
     }

     // this should only be called when the StateLock is already acquired
     private void ResetState() {
          AudioClient = null;
          ConnectedVoiceChannel = null;
     }

     // call back for when the bot disconnects
     public async Task OnDisconnectedAsync(Exception e) {
          var Log = async (string str) => await Logger.LogAsync("[Debug/OnDisconnectedAsync] " + str);
          await Log("reset voice state: " + e.Message);

          await Lock.WaitAsync();
          ResetState();
          Lock.Release();
     }

     // call back for when memebers of the same voice channel disconnect
     public async Task OnClientDisconnectAsync(ulong id) {
          var Log = async (string str) => await Logger.LogAsync("[Debug/OnClientDisconnectAsync] " + str);
          await Log("ClientDisconnected: id " + id);
          await Lock.WaitAsync();

          // leave when the bot is the only one in the channnel
          if (AudioClient != null && ConnectedVoiceChannel != null && await ConnectedVoiceChannel.GetUsersAsync().CountAsync() == 1) {
               try {
                    await AudioClient.StopAsync();
               } catch {}

               ResetState();
          }

          Lock.Release();
     }
}