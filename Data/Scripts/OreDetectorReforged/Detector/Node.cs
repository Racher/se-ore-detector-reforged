using System.Collections.Generic;

namespace OreDetectorReforged.Detector
{
    struct Node
    {
        public readonly float D;
        public readonly ushort P;
        public readonly short Face;
        public readonly ulong I;

        public Node(float d, int p, long i, int face)
        {
            D = d;
            P = (ushort)p;
            I = (ulong)i;
            Face = (short)face;
        }

        public struct Comparer : IComparer<Node>
        {
            public int Compare(Node x, Node y)
            {
                return x.D.CompareTo(y.D);
            }
        }
    }
}