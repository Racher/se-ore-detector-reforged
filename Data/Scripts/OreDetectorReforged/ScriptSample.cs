using System;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage;
using VRageMath;

// ReSharper disable ConvertToConstant.Local
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedParameter.Global
// ReSharper disable UnusedType.Global

namespace OreDetectorReforged
{
    class Program : MyGridProgram
    {
        public void Main(string argument, UpdateType updateSource)
        {
            var area = new BoundingSphereD(Me.GetPosition(), 15000);
            var ore = "Nickel";
            Action<Vector3D, Exception> callBack = (v, e) => Me.CustomData = e?.ToString() ?? v.ToString();
            var tup = new MyTuple<BoundingSphereD, string, Action<Vector3D, Exception>>(area, ore, callBack);
            Me.SetValue("DetectOre", tup);
        }
    }
}