using Discord;
using Discord.Audio;

public class VoiceStateManager {
     public IAudioClient? AudioClient;
     public IVoiceChannel? ConnectedVoiceChannel;
     private ReaderWriterLockSlim Lock;
     private ILogger Logger;

     public VoiceStateManager(ILogger logger) {
          AudioClient = null;
          ConnectedVoiceChannel = null;
          Lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
          Logger = logger;
     }

     // returns current
     public async Task<IAudioClient?> ConnectAsync(IVoiceChannel targetVoiceChannel) {
          var Log = async (string str) => await Logger.LogAsync("[Debug/ConnectAsync] " + str);
          await Log("Starting ConnectAsync");
          Lock.EnterReadLock();
          if (AudioClient != null && AudioClient.ConnectionState == ConnectionState.Connected && ConnectedVoiceChannel == targetVoiceChannel) {
               Lock.ExitReadLock();
               return AudioClient;
          }
          Lock.ExitReadLock();

          // Try Discord.Net IVoiceChannel.ConnectAsync
          IAudioClient? newAudioClient;
          try {
               newAudioClient = await targetVoiceChannel.ConnectAsync();
          } catch (Exception e) {
               ResetState();
               await Log("Failed to connect to voice Channel: " + e.Message);
               return null;
          }

          if (newAudioClient != null) {
               Lock.EnterWriteLock();

               AudioClient = newAudioClient;
               ConnectedVoiceChannel = targetVoiceChannel;
               AudioClient.ClientDisconnected += OnDisconnectedAsync;

               Lock.ExitWriteLock();
          } else ResetState();

          await Log("Starting ConnectAsync");
          return AudioClient;
     }

     public void ResetState() {
          Lock.EnterWriteLock();
          AudioClient = null;
          ConnectedVoiceChannel = null;
          Lock.ExitWriteLock();
     }

     public async Task OnDisconnectedAsync(ulong id) {
          var Log = async (string str) => await Logger.LogAsync("[Debug/OnDisconnectedAsync] " + str);
          await Log("reset voice state");

          ResetState();
     }
}