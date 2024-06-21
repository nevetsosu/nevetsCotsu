using System.Diagnostics;
using Serilog;

public class FFMPEGHandler {
     public static float DefaultVolume = 0.5f;
     public float Volume {
          get => _Volume;
          set => SetVolume(value);
     }

     private float _Volume;
     private static readonly string StandardInIndicator = "pipe:0";
     private static readonly string StandardOutIndicator = "pipe:1";

     public FFMPEGHandler() {
          Volume = DefaultVolume;
     }

     public void SetVolume(float volume) {
          _Volume = float.Clamp(volume, 0.0f, 1.0f);
     }

     public Process? TrySpawnFFMPEG(string? inFilePath, string? outFilePath, float baseVolume = 1.0f) {
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

          startInfo.Arguments = $"-hide_banner -loglevel level+panic -progress output.log -i {inSource} -filter:a \"loudnorm, volume={Volume * baseVolume:0.00}\" -ac 2 -f s16le -ar 48000 {outSource}";
          Log.Debug("Spawning ffmpeg with Arguments: " + startInfo.Arguments);
          return Process.Start(startInfo);
     }

     public Process? TrySpawnYoutubeFFMPEG(string VideoID, string? outFilePath, float baseVolume = 1.0f) {
          ProcessStartInfo startInfo = new ProcessStartInfo() {
               FileName = "/bin/bash",
               UseShellExecute = false,
               CreateNoWindow = true,
          };
          string URL = @"https://www.youtube.com/v/" + VideoID;
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
          Log.Debug("spawn youtube: using total volume: " + (Volume * baseVolume));
          startInfo.Arguments = $"-c \"yt-dlp --progress -o - -f bestaudio \'{URL}\' 2>ytdlp.err.log | ffmpeg -hide_banner -loglevel level+panic -progress output.log -i pipe:0 -filter:a \'loudnorm, volume={Volume * baseVolume:0.00}\' -ac 2 -f s16le -ar 48000 {outSource}\"";
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
          process.Dispose();
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
          process.Dispose();
     }
}
