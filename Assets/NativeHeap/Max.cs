using System.Collections.Generic;

namespace Unity.Collections {

    public struct Max : IComparer<byte>,
                        IComparer<ushort>,
                        IComparer<short>,
                        IComparer<uint>,
                        IComparer<int>,
                        IComparer<ulong>,
                        IComparer<long>,
                        IComparer<float>,
                        IComparer<double>,
                        IComparer<decimal> {

        public int Compare(byte x, byte y) {
            return y.CompareTo(x);
        }

        public int Compare(ushort x, ushort y) {
            return y.CompareTo(x);
        }

        public int Compare(short x, short y) {
            return y.CompareTo(x);
        }

        public int Compare(uint x, uint y) {
            return y.CompareTo(x);
        }

        public int Compare(int x, int y) {
            return y.CompareTo(x);
        }

        public int Compare(ulong x, ulong y) {
            return y.CompareTo(x);
        }

        public int Compare(long x, long y) {
            return y.CompareTo(x);
        }

        public int Compare(float x, float y) {
            return y.CompareTo(x);
        }

        public int Compare(double x, double y) {
            return y.CompareTo(x);
        }

        public int Compare(decimal x, decimal y) {
            return y.CompareTo(x);
        }
    }
}
