using System.Collections.Generic;

namespace OreDetectorReforged
{
    struct Node
    {
        public float d;
        public ushort p;
        public ushort x;
        public ushort y;
        public ushort z;

        public Node(float d, ushort p, ushort x, ushort y, ushort z)
        {
            this.d = d;
            this.p = p;
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public struct Comparer : IComparer<Node>
        {
            public int Compare(Node x, Node y) => x.d.CompareTo(y.d);
        }
    }
}
