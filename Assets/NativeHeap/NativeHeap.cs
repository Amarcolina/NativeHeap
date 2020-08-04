#if ENABLE_UNITY_COLLECTIONS_CHECKS
#define NHEAP_SAFE
#endif
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Unity.Collections {
    using LowLevel.Unsafe;

    using static Unity.Collections.LowLevel.Unsafe.UnsafeUtility;

    public struct NativeHeapIndex {
        internal int TableIndex;
#if NHEAP_SAFE
        internal int Version;
        internal int StructureId;
#endif
    }

    /// <summary>
    /// This is a basic implementation of the MinHeap/MaxHeap data structure.  It allows you
    /// to insert objects into the container with a O(log(n)) cost per item, and it allows you
    /// to extract the min/max from the container with a O(log(n)) cost per item.
    /// 
    /// This implementation provides the ability to remove items from the middle of the container
    /// as well.  This is a critical operation when implementing algorithms like astar.  When an
    /// item is added to the container, an index is returned which can be used to later remove
    /// the item no matter where it is in the heap, for the same cost of removing it if it was
    /// popped normally.
    /// 
    /// This container is parameterized with a comparator type that defines the ordering of the
    /// container.  The default form of the comparator can be used, or you can specify your own.
    /// The item that comes first in the ordering is the one that will be returned by the Pop
    /// operation.  This allows you to use the comparator to parameterize this collection into a 
    /// MinHeap, MaxHeap, or other type of ordered heap using your own custom type.
    /// 
    /// For convinience, this library contains the Min and Max comparator, which provide
    /// comparisons for all built in primitives.
    /// </summary>
    [NativeContainer]
    [DebuggerDisplay("Count = {Count}")]
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeHeap<T, U> : IDisposable
        where T : unmanaged
        where U : unmanaged, IComparer<T> {

        #region API

        public const int DEFAULT_CAPACITY = 128;

        /// <summary>
        /// Returns whether or not the NativeHeap structure is currently using safety checks.
        /// </summary>
        public static bool IsUsingSafetyChecks {
            get {
#if NHEAP_SAFE
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Returns the number of elements that this collection can hold before the internal structures
        /// need to be reallocated.
        /// </summary>
        public int Capacity {
            get {
                unsafe {
#if NHEAP_SAFE
                    AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                    return _data->Capacity;
                }
            }
            set {
                unsafe {
#if NHEAP_SAFE
                    AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
                    if (value < _data->Count) {
                        throw new ArgumentException($"Capacity of {value} cannot be smaller than count of {_data->Count}.");
                    }
#endif
                    TableValue* newTable = (TableValue*)Malloc(SizeOf<TableValue>() * value, AlignOf<TableValue>(), _allocator);
                    void* newHeap = Malloc(SizeOf<HeapNode>() * value, AlignOf<HeapNode>(), _allocator);

                    int toCopy = _data->Capacity < value ? _data->Capacity : value;
                    MemCpy(newTable, _data->Table, toCopy * SizeOf<TableValue>());
                    MemCpy(newHeap, _data->Heap, toCopy * SizeOf<HeapNode>());

                    for (int i = 0; i < value - _data->Capacity; i++) {
                        //For each new heap node, make sure that it has a new unique index
                        WriteArrayElement(newHeap, i + _data->Capacity, new HeapNode() {
                            TableIndex = i + _data->Capacity
                        });

#if NHEAP_SAFE
                        //For each new table value, make sure it has a specific version
                        WriteArrayElement(newTable, i + _data->Capacity, new TableValue() {
                            Version = 1
                        });
#endif
                    }

                    Free(_data->Table, _allocator);
                    Free(_data->Heap, _allocator);

                    _data->Table = newTable;
                    _data->Heap = newHeap;

                    _data->Capacity = value;
                }
            }
        }

        /// <summary>
        /// Returns the number of elements currently contained inside this collection.
        /// </summary>
        public int Count {
            get {
                unsafe {
#if NHEAP_SAFE
                    AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                    return _data->Count;
                }
            }
        }

        /// <summary>
        /// Constructs a new NativeHeap using the given Allocator.  You must call Dispose on this collection
        /// when you are finished with it.
        /// </summary>
        /// <param name="allocator">
        /// You must specify an allocator to use for the creation of the internal data structures.
        /// </param>
        /// <param name="initialCapacity">
        /// You can optionally specify the default number of elements this collection can contain before the internal
        /// data structures need to be realoocated.
        /// </param>
        /// <param name="comparator">
        /// You can optionally specify the comparator used to order the elements in this collection.  The Pop operation will
        /// always return the smallest element according to the ordering specified by this comparator.
        /// </param>
        public NativeHeap(Allocator allocator, int initialCapacity = DEFAULT_CAPACITY, U comparator = default) :
            this(initialCapacity, comparator, allocator, 1) { }

        /// <summary>
        /// Disposes of this container and deallocates its memory immediately.
        /// 
        /// Any NativeHeapIndex structures obtained will be invalidated and cannot be used again.
        /// </summary>
        public void Dispose() {
            unsafe {
#if NHEAP_SAFE
                DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif

                _data->Count = 0;
                _data->Capacity = 0;

                UnsafeUtility.Free(_data->Heap, _allocator);
                UnsafeUtility.Free(_data->Table, _allocator);
                UnsafeUtility.Free(_data, _allocator);
            }
        }

        /// <summary>
        /// Removes all elements from this container.  Any NativeHeapIndex structures obtained will be
        /// invalidated and cannot be used again.
        /// </summary>
        public void Clear() {
            unsafe {
#if NHEAP_SAFE
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);

                for (int i = 0; i < _data->Count; i++) {
                    var node = ReadArrayElement<HeapNode>(_data->Heap, i);
                    _data->Table[node.TableIndex].Version++;
                }
#endif

                _data->Count = 0;
            }
        }

        /// <summary>
        /// Returns whether or not the given NativeHeapIndex is a valid index for this container.  If true,
        /// that index can be used to Remove the element tied to that index from the container.
        /// 
        /// This method will always return true if IsUsingSafetyChecks returns false.
        /// </summary>
        public bool IsValidIndex(NativeHeapIndex index) {
            return IsValidIndex(index, out _);
        }

        /// <summary>
        /// Returns whether or not the given NativeHeapIndex is a valid index for this container.  If true,
        /// that index can be used to Remove the element tied to that index from the container.
        /// 
        /// This method will always return true if IsUsingSafetyChecks returns false.
        /// </summary>
        public bool IsValidIndex(NativeHeapIndex index, out string error) {
            error = null;

#if NHEAP_SAFE
            unsafe {
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);

                if (index.StructureId != _id) {
                    error = "The provided ItemHandle was not valid for this NativeHeap.  It was taken from a different instance.";
                    return false;
                }

                if (index.TableIndex >= _data->Capacity) {
                    error = "The provided ItemHandle was not valid for this NativeHeap.";
                    return false;
                }

                TableValue tableValue = _data->Table[index.TableIndex];
                if (tableValue.Version != index.Version) {
                    error = "The provided ItemHandle was not valid for this NativeHeap.  The item it pointed to might have already been removed.";
                    return false;
                }
            }
#endif

            return true;
        }

        /// <summary>
        /// Returns the next element that would be obtained if Pop was called.  This is the first/smallest
        /// item according to the ordering specified by the comparator.
        /// 
        /// This method is an O(1) operation.
        /// 
        /// This method will throw an InvalidOperationException if the collection is empty.
        /// </summary>
        public T Peek() {
#if NHEAP_SAFE
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif

            if (!TryPeek(out T t)) {
                throw new InvalidOperationException("Cannot Peek NativeHeap when the count is zero.");
            }

            return t;
        }

        /// <summary>
        /// Returns the next element that would be obtained if Pop was called.  This is the first/smallest
        /// item according to the ordering specified by the comparator.
        /// 
        /// This method is an O(1) operation.
        /// 
        /// This method will return true if an element could be obtained, or false if the container is empty.
        /// </summary>
        public bool TryPeek(out T t) {
            unsafe {
#if NHEAP_SAFE
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif

                if (_data->Count == 0) {
                    t = default;
                    return false;
                } else {
                    unsafe {
                        CopyPtrToStructure(_data->Heap, out t);
                        return true;
                    }
                }
            }
        }

        /// <summary>
        /// Removes the first/smallest element from the container and returns it.
        /// 
        /// This method is an O(log(n)) operation.
        /// 
        /// This method will throw an InvalidOperationException if the collection is empty.
        /// </summary>
        public T Pop() {
#if NHEAP_SAFE
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif

            if (!TryPop(out T t)) {
                throw new InvalidOperationException("Cannot Pop NativeHeap when the count is zero.");
            }

            return t;
        }

        /// <summary>
        /// Removes the first/smallest element from the container and returns it.
        /// 
        /// This method is an O(log(n)) operation.
        /// 
        /// This method will return true if an element could be obtained, or false if the container is empty.
        /// </summary>
        public bool TryPop(out T t) {
            unsafe {
#if NHEAP_SAFE
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif

                if (_data->Count == 0) {
                    t = default;
                    return false;
                }

                var rootNode = ReadArrayElement<HeapNode>(_data->Heap, 0);

#if NHEAP_SAFE
                //Update version to invalidate all existing handles
                _data->Table[rootNode.TableIndex].Version++;
#endif

                //Grab the last node off the end and remove it
                int lastNodeIndex = --_data->Count;
                var lastNode = ReadArrayElement<HeapNode>(_data->Heap, lastNodeIndex);

                //Move the previous root to the end of the array to fill the space we just made
                WriteArrayElement(_data->Heap, lastNodeIndex, rootNode);

                //Finally insert the previously last node at the root and bubble it down
                InsertAndBubbleDown(lastNode, 0);

                t = rootNode.Item;
                return true;
            }
        }

        /// <summary>
        /// Inserts the provided element into the container.  It may later be removed by a call to Pop,
        /// TryPop, or Remove.
        /// 
        /// This method returns a NativeHeapIndex.  This index can later be used to Remove the item from
        /// the collection.  Once the item is removed by any means, this NativeHeapIndex will become invalid.
        /// If an item is re-added to the collection after it has been removed, Insert will return a NEW
        /// index that is distinct from the previous index.  Each index can only be used exactly once to
        /// remove a single item.
        /// 
        /// This method is an O(log(n)) operation.
        /// </summary>
        public NativeHeapIndex Insert(in T t) {
            unsafe {
#if NHEAP_SAFE
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif

                if (_data->Count == _data->Capacity) {
                    Capacity *= 2;
                }

                var node = ReadArrayElement<HeapNode>(_data->Heap, _data->Count);
                node.Item = t;

                var insertIndex = _data->Count++;

                InsertAndBubbleUp(node, insertIndex);

                return new NativeHeapIndex() {
                    TableIndex = node.TableIndex,
#if NHEAP_SAFE
                    Version = _data->Table[node.TableIndex].Version,
                    StructureId = _id
#endif
                };
            }
        }

        /// <summary>
        /// Removes the element tied to this NativeHeapIndex from the container.  The NativeHeapIndex must be
        /// the result of a previous call to Insert on this container.  If the item has already been removed by
        /// any means, this method will throw an ArgumentException.
        /// 
        /// This method will invalidate the provided index.  If you re-insert the removed object, you must use
        /// the NEW index to remove it again.
        /// 
        /// This method is an O(log(n)) operation.
        /// </summary>
        public T Remove(NativeHeapIndex index) {
            unsafe {
#if NHEAP_SAFE
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);

                if (!IsValidIndex(index, out string error)) {
                    throw new ArgumentException(error);
                }
#endif
                int indexToRemove = _data->Table[index.TableIndex].HeapIndex;

                HeapNode toRemove = ReadArrayElement<HeapNode>(_data->Heap, indexToRemove);

#if NHEAP_SAFE
                _data->Table[toRemove.TableIndex].Version++;
#endif

                HeapNode lastNode = ReadArrayElement<HeapNode>(_data->Heap, --_data->Count);

                //First we move the node to remove to the end of the heap
                WriteArrayElement(_data->Heap, _data->Count, toRemove);

                if (indexToRemove != 0) {
                    int parentIndex = (indexToRemove - 1) / 2;
                    var parentNode = ReadArrayElement<HeapNode>(_data->Heap, parentIndex);
                    if (_comparator.Compare(lastNode.Item, parentNode.Item) < 0) {
                        InsertAndBubbleUp(lastNode, indexToRemove);
                        return toRemove.Item;
                    }
                }

                //If we couldn't bubble up, bubbling down instead
                InsertAndBubbleDown(lastNode, indexToRemove);

                return toRemove.Item;
            }
        }

        #endregion

        #region IMPLEMENTATION

#if NHEAP_SAFE
        private static int _nextId = 1;
        private int _id;

        internal AtomicSafetyHandle m_Safety;

        [NativeSetClassTypeToNullOnSchedule]
        DisposeSentinel m_DisposeSentinel;
#endif

        [NativeDisableUnsafePtrRestriction]
        private unsafe HeapData* _data;
        private Allocator _allocator;
        private U _comparator;

        internal NativeHeap(int initialCapacity, U comparator, Allocator allocator, int disposeSentinelStackDepth) {
#if NHEAP_SAFE
            if (initialCapacity <= 0) {
                throw new ArgumentException(nameof(initialCapacity), "Must provide an initial capacity that is greater than zero.");
            }

            if (allocator == Allocator.None ||
                allocator == Allocator.Invalid ||
                allocator == Allocator.AudioKernel) {
                throw new ArgumentException(nameof(allocator), "Must provide an Allocator type of Temp, TempJob, or Persistent.");
            }

            DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, disposeSentinelStackDepth, allocator);
            _id = Interlocked.Increment(ref _nextId);
#endif

            unsafe {
                _data = (HeapData*)Malloc(SizeOf<HeapData>(), AlignOf<HeapData>(), allocator);
                _data->Heap = (void*)Malloc(SizeOf<HeapNode>() * initialCapacity, AlignOf<HeapNode>(), allocator);
                _data->Table = (TableValue*)Malloc(SizeOf<TableValue>() * initialCapacity, AlignOf<TableValue>(), allocator);

                _allocator = allocator;

                for (int i = 0; i < initialCapacity; i++) {
                    WriteArrayElement(_data->Heap, i, new HeapNode() {
                        TableIndex = i
                    });
#if NHEAP_SAFE
                    WriteArrayElement(_data->Table, i, new TableValue() {
                        Version = 1
                    });
#endif
                }

                _data->Count = 0;
                _data->Capacity = initialCapacity;
                _comparator = comparator;
            }
        }

        private void InsertAndBubbleDown(HeapNode node, int insertIndex) {
            unsafe {
                while (true) {
                    int indexL = insertIndex * 2 + 1;
                    int indexR = insertIndex * 2 + 2;

                    //If the left index is off the end, we are finished
                    if (indexL >= _data->Count) {
                        break;
                    }

                    if (indexR >= _data->Count || _comparator.Compare(ReadArrayElement<HeapNode>(_data->Heap, indexL).Item,
                                                                      ReadArrayElement<HeapNode>(_data->Heap, indexR).Item) <= 0) {
                        //left is smaller (or the only child)
                        var leftNode = ReadArrayElement<HeapNode>(_data->Heap, indexL);

                        if (_comparator.Compare(node.Item, leftNode.Item) <= 0) {
                            //Last is smaller or equal to left, we are done
                            break;
                        }

                        WriteArrayElement(_data->Heap, insertIndex, leftNode);
                        _data->Table[leftNode.TableIndex].HeapIndex = insertIndex;
                        insertIndex = indexL;
                    } else {
                        //right is smaller
                        var rightNode = ReadArrayElement<HeapNode>(_data->Heap, indexR);

                        if (_comparator.Compare(node.Item, rightNode.Item) <= 0) {
                            //Last is smaller than or equal to right, we are done
                            break;
                        }

                        WriteArrayElement(_data->Heap, insertIndex, rightNode);
                        _data->Table[rightNode.TableIndex].HeapIndex = insertIndex;
                        insertIndex = indexR;
                    }
                }

                WriteArrayElement(_data->Heap, insertIndex, node);
                _data->Table[node.TableIndex].HeapIndex = insertIndex;
            }
        }

        private void InsertAndBubbleUp(HeapNode node, int insertIndex) {
            unsafe {
                while (insertIndex != 0) {
                    int parentIndex = (insertIndex - 1) / 2;
                    var parentNode = ReadArrayElement<HeapNode>(_data->Heap, parentIndex);

                    //If parent is actually less or equal to us, we are ok and can break out
                    if (_comparator.Compare(parentNode.Item, node.Item) <= 0) {
                        break;
                    }

                    //We need to swap parent down
                    WriteArrayElement<HeapNode>(_data->Heap, insertIndex, parentNode);
                    //Update table to point to new heap index
                    _data->Table[parentNode.TableIndex].HeapIndex = insertIndex;

                    //Restart loop trying to insert at parent index
                    insertIndex = parentIndex;
                }

                WriteArrayElement(_data->Heap, insertIndex, node);
                _data->Table[node.TableIndex].HeapIndex = insertIndex;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HeapNode {
            public T Item;
            public int TableIndex;
        }
        #endregion
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TableValue {
        public int HeapIndex;
#if NHEAP_SAFE
        public int Version;
#endif
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HeapData {
        public int Count;
        public int Capacity;
        public unsafe void* Heap;
        public unsafe TableValue* Table;
    }
}
