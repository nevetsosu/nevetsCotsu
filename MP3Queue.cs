using System.Collections.Concurrent;
using Serilog;

using MP3Logic;

namespace MP3Logic {
     public class MP3Queue;
}

public class MP3Queue {
     public int Count { get => SongQueue.Count; }
     private IndexableQueue<MP3Entry> SongQueue;
     private FFMPEGHandler _FFMPEGHandler;
     private SemaphoreSlim sem;
     private bool SongQueueNextPreloaded;
     private MP3Entry? LoopingEntry;
     public bool Looping { get; private set; }

     public MP3Queue(FFMPEGHandler? ffmpegHandler = null) {
          SongQueue = new();
          sem = new(1, 1);
          _FFMPEGHandler = ffmpegHandler ?? new();
          SongQueueNextPreloaded = false;
          Looping = false;
          LoopingEntry = null;
     }

     public List<MP3Entry> EntryList() {
          return SongQueue.ToList();
     }

     public void Clear() {
          sem.Wait();

          // kill preloaded audio if there is any
          MP3Entry? entry;
          if  (SongQueue.TryPeek(out entry) && entry?.FFMPEG != null) {
               try {
                    entry.FFMPEG.Kill();
               } catch {}
          }
          SongQueue.Clear();

          sem.Release();
     }

     public void Enqueue(MP3Entry entry) {
          Log.Debug("MP3Queue Adding entry with Video Title" + entry.VideoData?.Title);
          sem.Wait();
          SongQueue.Enqueue(entry);
          bool preloaded = TryPreloadNext();
          Log.Debug($"Is the queue currently preloaded?: {preloaded}");
          sem.Release();
     }

     public MP3Entry? TryDequeue() {
          sem.Wait();
          MP3Entry? entry;

          // if looping, return the current looping entry and prepare a new looping entry
          if (Looping && LoopingEntry != null) {
               entry = LoopingEntry;
               LoopingEntry = new MP3Entry(entry.VideoID, entry.RequestUser, _FFMPEGHandler.TrySpawnYoutubeFFMPEG(entry.VideoID, null, 1.0f));
               sem.Release();
               return entry;
          }

          // return the top entry and preload the next one
          if (SongQueue.TryDequeue(out entry)) {
               SongQueueNextPreloaded = false;
               TryPreloadNext();
               sem.Release();
               return entry;
          }
          sem.Release();

          // null when queue is empty
          return null;
     }

     // returns whether there the top of the queue is preloaded (it may already be preloaded)
     // assumes that the sem is already acquired
     private bool TryPreloadNext() {
          if (SongQueueNextPreloaded) return true;
          MP3Entry? entry;
          if (SongQueue.TryPeek(out entry) && entry != null) {
               entry.FFMPEG = _FFMPEGHandler.TrySpawnYoutubeFFMPEG(entry.VideoID, null, 1.0f);
               SongQueueNextPreloaded = true;
               return true;
          }
          SongQueueNextPreloaded = false;
          return false;
     }

     public async Task EnableLooping(MP3Entry entry) {
          await sem.WaitAsync();

          // return if already looping
          if (Looping) {
               sem.Release();
               return;
          }
          LoopingEntry = LoopingEntry ?? new MP3Entry(entry.VideoID, entry.RequestUser, _FFMPEGHandler.TrySpawnYoutubeFFMPEG(entry.VideoID, null, 1.0f), entry.VideoData);

          Looping = true;
          sem.Release();
     }

     public async Task DisableLooping() {
          await sem.WaitAsync();

          // return if already not looping
          if (!Looping) {
               sem.Release();
               return;
          }

          Looping = false;
          sem.Release();
     }
}