# Native Heap

This repo contains a very simple NativeHeap implementation for use with the new Unity Job System, Burst compiler, and DOTS.  The NativeHeap data structure can be very useful when implementing many different types of algorithms, such as A-star pathfinding.  It allows you to insert items into the collection with a cost of O(log(n)) per item, and it also allows you to extract them in sorted order with a cost of O(log(n)) per item.

This implementation contains a specific feature not always present in heap constructions.  It allows you to remove items from the center of the collection with the same cost as removing them in-order.  When an item is added, a special `Index` is returned that can later be used to remove the item, no matter where it is!  This feature in particular is very useful when implementing a-star, since removing items from within the heap is a common operation.

### How To Use It?
NativeHeap is a self-contained data structure with no dependencies, and so you can just copy [NativeHeap.cs](https://github.com/Amarcolina/NativeHeap/blob/master/Assets/NativeHeap/NativeHeap.cs) into your project and start working! Just a few considerations:
 - NativeHeap uses unsafe code, so make sure it is enabled in your project, or that NativeHeap is placed under the care of an Asmdef with unsafe code enabled.
 - You can optionally copy over the provided [Min.cs](https://github.com/Amarcolina/NativeHeap/blob/master/Assets/NativeHeap/Min.cs) and [Max.cs](https://github.com/Amarcolina/NativeHeap/blob/master/Assets/NativeHeap/Max.cs) to get a set of default comparators for built-in scalar types like int and float.
 - NativeHeap has no private members, only internal members, so if you are wary of using an unsafe method by accident just wrap the NativeHeap inside an Asmdef so that the internals are not exposed.

### Parameterization
The NativeHeap is generic like NativeArray<T> or NativeList<T>, allowing you to store structs of type T into the collection.  However unlike NativeArray<T> or NativeList<T>, NativeHeap has _two_ generic parameters.  The second parameter is the `Comparator` and is responsible for determining the ordering of the items inside the heap.
  
Two comparators are included, the Min and Max comparator, which are able to construct a MinHeap or a MaxHeap for the built-in csharp primitives such as `int` and `float`.  Writing your own comparator is as simple as implementing the IComparer<T> interface for the type you would like to insert.  And don't worry about slowdown due to interfaces, thanks to the Burst compiler you can use custom comparators with _zero_ extra overhead compared to integrating the comparator directly into the data structure!
  
### Example Usage
Basic example
```csharp
//Construct a new MinHeap for integers
NativeHeap<int, Min> heap = new NativeHeap(Allocator.Temp);

//Insert some numbers into the heap
heap.Insert(5);
heap.Insert(3);
heap.Insert(10);

print(heap.Pop()); //3
print(heap.Pop()); //5
print(heap.Pop()); //10

//Always remember to dispose when you are finished!
heap.Dispose();
```

Custom comparator example
```csharp
//Define a custom comparator that orders floats by their distance to the 
//constant 100
public struct DistanceTo100 : IComparer<float> {
    public int Compare(float a, float b) {
        float distForA = Mathf.Abs(a - 100.0f);
        float distForB = Mathf.Abs(b - 100.0f);
        return distForA.CompareTo(distForB);
    }
}

NativeHeap<float, DistanceTo100> heap = new NativeHeap(Allocator.Temp);
...
```

Using an `Index` to remove an item
```csharp
NativeHeap<int, Min> heap = new NativeHeap(Allocator.Temp);

heap.Insert(5);
heap.Insert(3);
heap.Insert(10);

NativeHeapIndex indexOf7 = heap.Insert(7);

print(heap.Pop()); //3

//Remove the item 7 from the heap, even though it is
//not next up
heap.Remove(indexOf7);

print(heap.Pop()); //5
print(heap.Pop()); //10

heap.Dispose();
```
