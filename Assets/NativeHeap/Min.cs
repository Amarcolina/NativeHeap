using System.Collections.Generic;

namespace Unity.Collections {

    public struct Min : IComparer<byte>,
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
            return x.CompareTo(y);
        }

        public int Compare(ushort x, ushort y) {
            return x.CompareTo(y);
        }

        public int Compare(short x, short y) {
            return x.CompareTo(y);
        }

        public int Compare(uint x, uint y) {
            return x.CompareTo(y);
        }

        public int Compare(int x, int y) {
            return x.CompareTo(y);
        }

        public int Compare(ulong x, ulong y) {
            return x.CompareTo(y);
        }

        public int Compare(long x, long y) {
            return x.CompareTo(y);
        }

        public int Compare(float x, float y) {
            return x.CompareTo(y);
        }

        public int Compare(double x, double y) {
            return x.CompareTo(y);
        }

        public int Compare(decimal x, decimal y) {
            return x.CompareTo(y);
        }
    }
}
