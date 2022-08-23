using System;
using System.Collections.Generic;
using OreDetectorReforged.Detector;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace OreDetectorReforged
{
    static class TerminalProgrammableBlock
    {
        static bool inited;

        public static void TryInit(IMyEntity entity)
        {
            if (entity as IMyProgrammableBlock == null)
                return;
            if (inited)
                return;
            inited = true;
            {
                const string id = "ReforgedDetectN";
                var p = MyAPIGateway.TerminalControls.CreateProperty<ValueTuple<BoundingSphereD, Vector3D, string, int, Action<List<Vector3D>>>, IMyProgrammableBlock>(id);
                p.Setter = (e, v) => DetectorServer.Add(new SearchTask(v));
                MyAPIGateway.TerminalControls.AddControl<IMyProgrammableBlock>(p);
            }
        }
    }
}
