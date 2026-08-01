using System;
using OreDetectorReforged.Detector;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.Components;
using VRageMath;

// ReSharper disable UnusedType.Global

namespace OreDetectorReforged
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    class ModApi : MySessionComponentBase
    {
        const long DetectOreMsgId = 7330961312834800629L;
        const long ListNaturalOresMsgId = 7330961312834800630L;

        public override void LoadData()
        {
            MyAPIGateway.Utilities.RegisterMessageHandler(DetectOreMsgId, OnDetectMessage);
            MyAPIGateway.Utilities.RegisterMessageHandler(ListNaturalOresMsgId, OnListMessage);
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(DetectOreMsgId, OnDetectMessage);
            MyAPIGateway.Utilities.UnregisterMessageHandler(ListNaturalOresMsgId, OnListMessage);
        }

        static void OnDetectMessage(object o)
        {
            var tup = (MyTuple<BoundingSphereD, string, Action<Vector3D, Exception>>)o;
            DetectorServer.SubmitSearch(tup.Item1, tup.Item2, tup.Item3);
        }

        static void OnListMessage(object o)
        {
            var cb = (Action<string[]>)o;
            cb((string[])MaterialMappingHelper.NaturalOres?.Clone());
        }
    }
}