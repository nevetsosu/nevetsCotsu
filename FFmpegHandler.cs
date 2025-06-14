using System.Diagnostics;
using Serilog;
using YoutubeExplode.Videos;

public class FFMPEGHandler {
     public static float DefaultVolume = 0.2f;
     public float Volume {
          get => _Volume;
          set => SetVolume(value);
     }

     private float _Volume;
     private static readonly string StandardInIndicator = "pipe:0";
     private static readonly string StandardOutIndicator = "pipe:1";
     private readonly YTAPIManager _YTAPIManager;

     public FFMPEGHandler(YTAPIManager? ytAPIManager = null) {
          Volume = DefaultVolume;
          _YTAPIManager = ytAPIManager ?? new();
     }

     public void SetVolume(float volume) {
          _Volume = float.Clamp(volume, 0.0f, 1.0f);
     }

     public Process? TrySpawnFFMPEG(string? inFilePath, string? outFilePath, float baseVolume = 1.0f, TimeSpan start = default) {
          ProcessStartInfo startInfo = new ProcessStartInfo() {
               FileName = "ffmpeg",
               UseShellExecute = false,
               CreateNoWindow = true
          };

          string inSource, outSource;

          // use standard in if inFilePath is null
          if (inFilePath == null) {
               inSource = StandardInIndicator;
               startInfo.RedirectStandardInput = true;
          } else if (!File.Exists(inFilePath)) {
               Log.Debug($"inFilePath: \"{inFilePath}\" does not exist");
               return null;
          } else {
               inSource = inFilePath;
          }

          // use standard out if inFilePath is null
          if (outFilePath == null) {
               outSource = StandardOutIndicator;
               startInfo.RedirectStandardOutput = true;
          } else if (!File.Exists(outFilePath)) {
               Log.Debug($"outFilePath: \"{outFilePath}\" does not exist");
               return null;
          } else {
               outSource = outFilePath;
          }

          startInfo.Arguments = $"-hide_banner -loglevel level+panic -progress output.log -i {inSource} -filter:a \"loudnorm, volume={Volume * baseVolume:0.00}\" -ss {start} -ac 2 -f s16le -ar 48000 {outSource}";
          Log.Debug("Spawning ffmpeg with Arguments: " + startInfo.Arguments);
          return Process.Start(startInfo);
     }

     public Process? TrySpawnYoutubeFFMPEG(string URL, string? outFilePath, float baseVolume = 1.0f, TimeSpan start = default) {
          ProcessStartInfo startInfo = new ProcessStartInfo() {
               FileName = "ffmpeg",
               UseShellExecute = false,
               CreateNoWindow = true,
          };
          string outSource;
          // use standard out if inFilePath is null
          if (outFilePath == null) {
               startInfo.RedirectStandardOutput = true;
               outSource = StandardOutIndicator;
          } else if (!File.Exists(outFilePath)) {
               Log.Debug($"outFilePath: \"{outFilePath}\" does not exist");
               return null;
          } else {
               outSource = outFilePath;
          }
          Log.Debug($"spawn youtube: using total volume: {Volume * baseVolume}\nMedia URL: {URL}");
          // startInfo.Arguments = $"-loglevel verbose -ss {start} -i \"{URL}\" -vn -sn -dn -f s16le -ac 2 -ar 48000 -af loudnorm,volume={Volume * baseVolume:0.00} {outSource}";
          startInfo.Arguments = $"-loglevel verbose -ss {start} -i \"{URL}\" -reconnect -reconnect_max_retries=10 -vn -sn -dn -f s16le -ac 2 -ar 48000 -af volume={Volume * baseVolume:0.00} {outSource}";
          return Process.Start(startInfo);
     }

     public async Task YoutubeToStream(string URL, Stream outStream, CancellationToken token = default, float baseVolume = 1.0f) {
          Process? process = TrySpawnYoutubeFFMPEG(URL, null, float.MaxNumber(baseVolume, 0.0f));
          if (process == null) {
               Log.Warning("process has returned null");
               return;
          }

          using (Stream output = process.StandardOutput.BaseStream) {
               try {
                    await output.CopyToAsync(outStream, token);
                    await outStream.FlushAsync();
                    Log.Debug("Stream transfer finished");
               } catch (OperationCanceledException) {
                    Log.Debug("Stream transfer Canceled (Likely due to output stream disconnection). This is fine if handled correctly.");
               } catch (Exception e) {
                    Log.Debug($"Generic Stream Exception: {e}");
               }
          }
          _ = Task.Run(() => CleanProcess(process));
     }

     public async Task ReadFileToStream(string filepath, Stream outStream, CancellationToken token = default, float baseVolume = 1.0f) {
          Process? process = TrySpawnFFMPEG(filepath, null, float.MaxNumber(baseVolume, 0.0f));
          if (process == null) {
               Log.Debug("process has returned null");
               return;
          }

          using (Stream output = process.StandardOutput.BaseStream) {
               try {
                    await output.CopyToAsync(outStream, token);
                    await outStream.FlushAsync();
                    Log.Debug("Stream transfer finished");
               } catch (OperationCanceledException) {
                    Log.Debug("Stream transfer Canceled (Likely due to output stream disconnection). This is fine if handled correctly.");
               } catch (Exception e) {
                    Log.Debug($"Generic Stream Exception: {e}");
               }
          }

          _ = Task.Run(() => CleanProcess(process));
     }
     public static async Task CleanProcess(Process process) {
          try {
               // process.Kill(entireProcessTree: true);
               process.StandardOutput.Close();
               await process.WaitForExitAsync();
               process.Dispose();
          } catch (Exception e) {
               Log.Warning("process was already disposed??" + e.Message);
               return;
          }

     }
}
