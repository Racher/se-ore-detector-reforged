using VRageMath;

namespace OreDetectorReforged.Detector
{
    interface IDetectorPage
    {
        void PushRoot(float radius, PriorityQueue<Node, Node.Comparer> pq, int currOre, int page, Vector3 centerLocal);
        void Process(ref Node node, SearchTask task, PriorityQueue<Node, Node.Comparer> pq, Vector3 centerLocal);
        Vector3 WorldToLocal(Vector3D v);
    }
}