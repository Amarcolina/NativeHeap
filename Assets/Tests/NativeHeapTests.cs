using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

namespace Tests {

    public class NativeHeapTests {

        private NativeHeap<int, Min> Heap;

        [SetUp]
        public void SetUp() {
            Heap = new NativeHeap<int, Min>(Allocator.Persistent);
        }

        [TearDown]
        public void TearDown() {
            Heap.Dispose();
        }

        [Test]
        public void TestInsertionAndRemoval() {
            List<int> list = new List<int>();
            for (int i = 0; i < 100; i++) {
                Heap.Insert(i);
                list.Add(i);
            }

            for (int i = 0; i < 1000; i++) {
                var min = Heap.Pop();
                Assert.That(min, Is.EqualTo(list.Min()));

                list.Remove(min);

                int toInsert = Random.Range(0, 100);
                Heap.Insert(toInsert);
                list.Add(toInsert);
            }
        }

        [Test]
        public void TestCanRemoveUsingIndex() {
            List<(int, NativeHeapIndex)> itemRefs = new List<(int, NativeHeapIndex)>();
            for (int i = 0; i < 100; i++) {
                int value = Random.Range(0, 1000);
                var itemRef = Heap.Insert(value);
                itemRefs.Add((value, itemRef));
            }

            foreach ((var value, var itemRef) in itemRefs) {
                var item = Heap.Remove(itemRef);
                Assert.That(item, Is.EqualTo(value));
            }
        }

        [Test]
        public void TestRemovingTwiceThrowsException() {
            InconclusiveIfNoSafety();

            for (int i = 0; i < 10; i++) Heap.Insert(i);

            var itemRef = Heap.Insert(5);

            for (int i = 0; i < 10; i++) Heap.Insert(i);

            Heap.Remove(itemRef);

            Assert.That(() => { Heap.Remove(itemRef); }, Throws.ArgumentException);
        }

        [Test]
        public void TestIndicesBecomeInvalidAfterPopping() {
            InconclusiveIfNoSafety();

            List<NativeHeapIndex> indices = new List<NativeHeapIndex>();
            for (int i = 0; i < 10; i++) {
                indices.Add(Heap.Insert(Random.Range(0, 100)));
            }

            for (int i = 0; i < 10; i++) {
                Heap.Pop();
            }

            foreach (var index in indices) {
                Assert.That(Heap.IsValidIndex(index), Is.False);
            }
        }

        [Test]
        public void TestIndicesBecomeInvalidAfterClearing() {
            InconclusiveIfNoSafety();

            List<NativeHeapIndex> indices = new List<NativeHeapIndex>();
            for (int i = 0; i < 100; i++) {
                indices.Add(Heap.Insert(i));
            }

            Heap.Clear();

            foreach (var index in indices) {
                Assert.That(Heap.IsValidIndex(index), Is.False);
            }
        }

        [Test]
        public void TestIndicesAreStillValidAfterRealloc() {
            InconclusiveIfNoSafety();

            List<NativeHeapIndex> indices = new List<NativeHeapIndex>();
            for (int i = 0; i < 100; i++) {
                indices.Add(Heap.Insert(i));
            }

            Heap.Capacity *= 2;

            for (int i = 0; i < 100; i++) {
                Assert.That(Heap.Peek(), Is.EqualTo(i));
                Heap.Remove(indices[i]);
            }
            Assert.That(Heap.Count, Is.Zero);
        }

        [Test]
        public void TestIndicesFromOneHeapAreInvalidForAnother() {
            InconclusiveIfNoSafety();

            using (var Heap2 = new NativeHeap<int, Min>(Allocator.Temp)) {
                var index = Heap.Insert(0);
                Heap2.Insert(0);

                Assert.That(Heap2.IsValidIndex(index), Is.False);
                Assert.That(() => { Heap2.Remove(index); }, Throws.ArgumentException);
            }
        }

        [Test]
        public void TestPeekIsSameAsPop() {
            for (int i = 0; i < 100; i++) {
                Heap.Insert(Random.Range(0, 1000));
            }

            while (Heap.Count > 0) {
                int value1 = Heap.Peek();
                int value2 = Heap.Pop();

                Assert.That(value1, Is.EqualTo(value2));
            }
        }

        [Test]
        public void TestRemoveFromMiddle() {
            List<int> items = new List<int>();
            int GetValue() => Random.value > 0.5f ? Random.Range(0, 1000) : Random.Range(1001, 2000);

            for (int i = 0; i < 100; i++) {
                var value = GetValue();
                items.Add(value);
                Heap.Insert(value);
            }

            var index = Heap.Insert(1000);

            for (int i = 0; i < 100; i++) {
                var value = GetValue();
                items.Add(value);
                Heap.Insert(value);
            }

            Heap.Remove(index);

            foreach (var item in items.OrderBy(i => i)) {
                Assert.That(Heap.Pop(), Is.EqualTo(item));
            }
        }

        [Test]
        public void TestCopyReflectsChanges() {
            var heapCopy = Heap;
            heapCopy.Insert(5);
            heapCopy.Capacity *= 2;

            Assert.That(Heap.Peek(), Is.EqualTo(5));

            heapCopy.Pop();

            Assert.That(Heap.Count, Is.Zero);
            Assert.That(Heap.Capacity, Is.EqualTo(heapCopy.Capacity));
        }

        private void InconclusiveIfNoSafety() {
            bool isOn = false;
            CheckSafetyChecks(ref isOn);
            if (!isOn) {
                Assert.Inconclusive("This test requires safety checks");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckSafetyChecks(ref bool isOn) {
            isOn = true;
        }
    }
}
