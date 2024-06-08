using System.Diagnostics;
namespace AudioPipeline {
     public class FFMPEGHandler {
          public static float DefaultVolume = 1.0f;
          public float Volume {
               get;
               private set;
          }

          private ILogger Logger;
          private static readonly string StandardInIndicator = "pipe:0";
          private static readonly string StandardOutIndicator = "pipe:1";

          public FFMPEGHandler(ILogger logger) {
               Logger = logger;
               Volume = DefaultVolume;
          }

          public void SetVolume(float volume) {
               Volume = float.Clamp(volume, 0.0f, 1.0f);
          }

          private async Task<Process?> TrySpawnFFMPEG(string? inFilePath, string? outFilePath, float baseVolume = 1.0f) {
               var Log = async (string str) => await Logger.LogAsync("[Debug/TrySpawnFFMPEG] " + str);

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
                    await Log($"inFilePath: \"{inFilePath}\" does not exist");
                    return null;
               } else {
                    inSource = inFilePath;
               }

               // use standard out if inFilePath is null
               if (outFilePath == null) {
                    outSource = StandardOutIndicator;
                    startInfo.RedirectStandardOutput = true;
               } else if (!File.Exists(outFilePath)) {
                    await Log($"outFilePath: \"{outFilePath}\" does not exist");
                    return null;
               } else {
                    outSource = outFilePath;
               }

               startInfo.Arguments = $"-hide_banner -loglevel panic -i {inSource} -filter:a \"loudnorm, volume={Volume * baseVolume:0.00}\" -ac 2 -f s16le -ar 48000 {outSource}";
               await Log("Spawning ffmpeg with Arguments: " + startInfo.Arguments);
               return Process.Start(startInfo);
          }

          public async Task ReadFileToStream(string filepath, Stream outStream, CancellationToken token, float baseVolume = 1.0f) {
               var Log = async (string str) => await Logger.LogAsync("[FileToOutputStream] " + str);
               Process? process = await TrySpawnFFMPEG(filepath, null, float.MaxNumber(baseVolume, 0.0f));
               if (process == null) {
                    await Log("process has returned null");
                    return;
               }

               using (Stream output = process.StandardOutput.BaseStream) {
                    try {
                         await output.CopyToAsync(outStream, token);
                         await outStream.FlushAsync();
                         await Log("Stream transfer finished");
                    } catch (OperationCanceledException) {
                         await Log("Stream transfer Canceled (Likely due to output stream disconnection). This is fine if handled correctly.");
                    } catch (Exception e) {
                         await Log($"Generic Stream Exception: {e}");
                    }
               }
               process.Dispose();
          }
     }
}
