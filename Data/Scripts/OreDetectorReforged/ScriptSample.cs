using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace IngameScript
{
    public partial class Program : MyGridProgram
    {
        void ReforgedDetectN(BoundingSphereD area, string minedOre, int count, Action<IList<Vector3D>> callBack)
        {
            Me.SetValue("ReforgedDetectN", new ValueTuple<BoundingSphereD, string, int, Action<IList<Vector3D>>>(area, minedOre, count, callBack));
        }
        public void Main(string argument, UpdateType updateSource)
        {
            ReforgedDetectN(new BoundingSphereD(Me.GetPosition(), 3e4), "Nickel", 1, (vs) => Me.CustomData += "\n" + vs.FirstOrDefault());
        }
    }
}
