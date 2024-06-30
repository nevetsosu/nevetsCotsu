#define preload
// #undef preload

using Serilog;

using MP3Logic;

namespace MP3Logic {
     public class MP3Queue;
}

public class MP3Queue {
     public int Count { get => Queue.Count; }
     private LinkedList<MP3Entry> Queue;
     private SemaphoreSlim sem;
#if preload
     private bool SongQueueNextPreloaded;
#endif
     private MP3Entry? LoopingEntry;
     public bool Looping { get; private set; }

     private readonly YTAPIManager _YTAPIManager;
     public MP3Queue(YTAPIManager? ytAPIManager = default) {
          Queue = new();
          sem = new(1, 1);
          _YTAPIManager = ytAPIManager ?? new();
          Looping = false;
          LoopingEntry = null;
#if preload
          SongQueueNextPreloaded = false;
#endif
     }

     public List<MP3Entry> EntryList() {
          sem.Wait();
          List<MP3Entry> list = Queue.ToList();
          sem.Release();
          return list;
     }

     public void Clear() {
          sem.Wait();

#if preload
          // kill preloaded audio if there is any
          MP3Entry? entry;
          if (TryPeek(out entry) && entry.HasStream()) {
               entry.DisposeStream();
          }
#endif
          Queue.Clear();

          sem.Release();
     }

     // assumes the sem is already acquired
     private bool TryPeek(out MP3Entry entry) {
          entry = default!;

          if (Queue.First == null) {
               return false;
          }
          entry = Queue.First.ValueRef;

          return true;
     }

     public void Swap(int IndexA, int IndexB) {
          sem.Wait();
          if (IndexA < 0 || IndexA >= Queue.Count) {
               sem.Release();
               throw new ArgumentOutOfRangeException("IndexA is out of range.");
          }
          if (IndexB < 0 || IndexB >= Queue.Count) {
               sem.Release();
               throw new ArgumentOutOfRangeException("IndexB is out of range.");
          }
          if (IndexA == IndexB) {
               sem.Release();
               return;
          }
          try {
               LinkedListNode<MP3Entry> NodeA = GetLinkedListNodeByIndex(IndexA);
               LinkedListNode<MP3Entry> NodeB = GetLinkedListNodeByIndex(IndexB);

               MP3Entry tmp = NodeA.Value;
               NodeA.Value = NodeB.Value;
               NodeB.Value = tmp;
          } finally {
               sem.Release();
          }
     }

     public void SkipTo(int index) {
          sem.Wait();
          if (index < 0) throw new ArgumentOutOfRangeException("index cannot be less than 0");
          if (index >= Queue.Count) {
               Queue.Clear();
               sem.Release();
               return;
          }

          int i = 0;
          while (i++ < index) {
               Queue.RemoveFirst();
          }

          sem.Release();
     }

     public void Remove(int index) {
          sem.Wait();
          try {
               Queue.Remove(GetLinkedListNodeByIndex(index));
          } finally {
               sem.Release();
          }
     }

     public MP3Entry? GetEntry(int index) {
          if (Queue.Count == 0) return null;
          if (index < 0 || index >= Queue.Count) throw new ArgumentOutOfRangeException("invalid index");

          MP3Entry node = GetLinkedListNodeByIndex(index).Value;
          return node.Clone() as MP3Entry;
     }

     // assumes sem is acquired
     private LinkedListNode<MP3Entry> GetLinkedListNodeByIndex(int index) {
          if (index < 0 || index >= Queue.Count) throw new ArgumentOutOfRangeException("invalid index");
          if (Queue.Count == 0) throw new InvalidOperationException("List is empty");

          int OppositeIndex = Queue.Count - index - 1;
          if (index <= OppositeIndex) {
               return GetLinkedListNodeByIndexFromFirst(index);
          } else return GetLinkedListNodeByIndexFromLast(OppositeIndex);
     }

     // assumes sem is acquired
     private LinkedListNode<MP3Entry> GetLinkedListNodeByIndexFromFirst(int index) {
          LinkedListNode<MP3Entry>? node = Queue.First!;
          int i = 0;
          while (i++ < index) {
               node = node!.Next;
          }
          return node!;
     }

      // assumes sem is acquired
     private LinkedListNode<MP3Entry> GetLinkedListNodeByIndexFromLast(int index) {
          LinkedListNode<MP3Entry>? node = Queue.Last!;
          if (node == null) throw new ();

          int i = 0;
          while (i++ < index) {
               node = node!.Previous;
          }
          return node!;
     }

     public void Enqueue(MP3Entry entry) {
          Log.Debug("MP3Queue Adding entry with Video Title" + entry.VideoData?.Title);
          sem.Wait();

          Queue.AddLast(entry);
#if preload
          if (!Looping) {
               bool preloaded = TryPreloadNext();
               Log.Debug($"Is the queue currently preloaded?: {preloaded}");
          } else Log.Debug("not attempting to preload since its looping right now");
#endif
          sem.Release();
     }

     public MP3Entry? TryDequeue() {
          MP3Entry entry;

          sem.Wait();

          // if looping, return the current looping entry and prepare a new looping entry
          if (Looping && LoopingEntry != null) {
               entry = LoopingEntry;
               LoopingEntry = entry.Clone() as MP3Entry;
#if preload
               if (LoopingEntry != null) {
                    LoopingEntry.SetStream(_YTAPIManager.GetAudioStream(entry.VideoID).Result);
               }
#endif
               sem.Release();
               return entry;
          }
#if preload
          if (LoopingEntry != null && LoopingEntry.HasStream()) {
               Log.Debug("switching the Looping Entry");
               LoopingEntry.DisposeStream();
               LoopingEntry = null;
          }
#endif

          if (Queue.First == null) {
               sem.Release();
               return null;
          }

          entry = Queue.First.Value;
          Queue.RemoveFirst();
#if preload
          if (!Looping) {
               SongQueueNextPreloaded = false;
               TryPreloadNext();
          }
#endif

          sem.Release();

          return entry;
     }

     // returns whether there the top of the queue is preloaded (it may already be preloaded)
     // assumes that the sem is already acquired
#if preload
     private bool TryPreloadNext() {
          if (SongQueueNextPreloaded) return true;
          MP3Entry entry;
          if (TryPeek(out entry) && !entry.HasStream()) {
               entry.SetStream(_YTAPIManager.GetAudioStream(entry.VideoID).Result);
               SongQueueNextPreloaded = true;
               return true;
          }
          SongQueueNextPreloaded = false;
          return false;
     }
#endif

     public async Task EnableLooping(MP3Entry entry) {
          await sem.WaitAsync();

          // return if already looping
          if (Looping) {
               sem.Release();
               return;
          }
          if (LoopingEntry == null) Log.Debug("LoopinEntry is currently null on loop enable");
#if preload
          LoopingEntry ??= new MP3Entry(entry.VideoID, entry.RequestUser, entry.VideoData, _YTAPIManager.GetAudioStream(entry.VideoID).Result);

          // stop preloading next in the queue when looping is on
          if (SongQueueNextPreloaded) {
               MP3Entry? e;
               if (TryPeek(out e) && e.HasStream()) {
                    e.DisposeStream();
                    SongQueueNextPreloaded = false;
               }
          }
#else
          LoopingEntry ??= new MP3Entry(entry.VideoID, entry.RequestUser, null, entry.VideoData);
#endif
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

#if preload
          // remove the looping entry preload
          if (LoopingEntry != null && LoopingEntry.HasStream()) {
              LoopingEntry.DisposeStream();
               LoopingEntry = null;
          }

          // preload next in queue
          if (!SongQueueNextPreloaded) {
               TryPreloadNext();
          } else Log.Warning("next song in the queue is already said to be preloaed after a loop disable??");
#endif
          Looping = false;
          sem.Release();
     }
}