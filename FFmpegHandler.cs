using System.Diagnostics;
namespace FFMPEG {
     public class FFMPEGHandler {
          ILogger Logger;
          private static readonly string StandardInIndicator = "pipe:0";
          private static readonly string StandardOutIndicator = "pipe:1";

          public FFMPEGHandler(ILogger logger) {
               Logger = logger;
          }

          private async Task<Process?> TrySpawnFFMPEG(string? inFilePath, string? outFilePath) {
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

               startInfo.Arguments = $"-hide_banner -loglevel panic -i {inSource} -ac 2 -f s16le -ar 48000 {outSource}";

               return Process.Start(startInfo);
          }

          public async Task ReadFileToStream(string filepath, Stream outStream) {
               var Log = async (string str) => await Logger.LogAsync("[FileToOutputStream] " + str);
               Process? process = await TrySpawnFFMPEG(filepath, null);
               if (process == null) {
                    await Log("process has returned null");
                    return;
               }

               using (Stream output = process.StandardOutput.BaseStream) {
                    try {
                         await output.CopyToAsync(outStream);
                         await Log("Stream transfer finished");
                    } catch (OperationCanceledException) {
                         await Log("Stream transfer Canceled (Likely due to output stream disconnection). This is fine if handled correctly.");
                    } catch (Exception e) {
                         await Log($"Generic Stream Exception: {e}");
                    }
                    await outStream.FlushAsync();
               }
               process.Dispose();
          }
     }
}
