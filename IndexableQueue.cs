using Serilog;

namespace MP3Logic {
     public class IndexableQueue;
}

public class IndexableQueueNode<T> {
     static int FreeID = 0;
     public IndexableQueueNode<T>? next;
     public IndexableQueueNode<T>? prev;
     public T data;
     public readonly int DebugId;

     public IndexableQueueNode(T data = default!, IndexableQueueNode<T>? prev = null, IndexableQueueNode<T>? next = null) {
          this.data = data;
          this.next = next;
          this.prev = prev;
          this.DebugId = FreeID++;
     }
}

public class IndexableQueue<T> {
     public int Count { get; private set; }
     private IndexableQueueNode<T> sentinel; // next and prev references are always assumed to be non-null

     public IndexableQueue() {
          sentinel = new();
          sentinel.prev = sentinel;
          sentinel.next = sentinel;
     }

     public void Enqueue(T item) {
          IndexableQueueNode<T> newNode = new(item);
          AddAfter(newNode, sentinel);
          Count++;
     }

     // inserts "node" after ReferenceNode
     // assumes referenceNode is a part of a valid IndexableQueue
     private void AddAfter(IndexableQueueNode<T> node, IndexableQueueNode<T> referenceNode) {

          // connect node to reference node
          node.prev = referenceNode;
          referenceNode.next = node;

          // connect to after reference node
          IndexableQueueNode<T> afterReferenceNode = referenceNode.next!;
          node.next = afterReferenceNode;
          afterReferenceNode.prev = node;

     }

     private static void Remove(IndexableQueueNode<T> node) {
          IndexableQueueNode<T> afterNode = node.next!;
          IndexableQueueNode<T> beforeNode = node.next!;

          beforeNode.next = afterNode;
          afterNode.prev = beforeNode;
     }

     public bool TryDequeue(out T item) {
          if (!TryPeek(out item)) return false;
          Remove(sentinel.prev!);

          return true;
     }

     public bool TryPeek(out T item) {
          item = default!;

          IndexableQueueNode<T> node = sentinel.prev!;
          if (node == sentinel) return false;

          item = node.data;
          return true;
     }

     public void Clear() {
          sentinel.next = sentinel;
          sentinel.prev = sentinel;
          Count = 0;
     }

     public List<T> ToList() {
          List<T> list = new(Count);

          IndexableQueueNode<T>? node = sentinel.next!;

          while (node != sentinel) {
               list.Add(node.data);
               node = node.next!;
          }

          return list;
     }

     public void TestTraversal() {
          IndexableQueueNode<T>? node = sentinel.next!;

          while (node != sentinel) {
               Log.Debug("iterating, at id: " + node.DebugId);
               node = node.next!;
          }
     }
}