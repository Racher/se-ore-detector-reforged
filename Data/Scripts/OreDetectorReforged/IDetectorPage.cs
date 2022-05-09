using System;
using VRageMath;

namespace OreDetectorReforged
{
    interface IDetectorPage
    {
        void Setup(PriorityQueue<Node> pq, Vector3D center, int page, int ore);
        Vector3D Pop();
    }
}
