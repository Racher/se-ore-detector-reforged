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

        public static void InitTerminalControls(IMyEntity entity)
        {
            if (inited || !(entity is IMyProgrammableBlock))
                return;
            inited = true;
            {
                const string id = "ReforgedDetectN";
                var p = MyAPIGateway.TerminalControls.CreateProperty<ValueTuple<BoundingSphereD, string, int, Action<List<Vector3D>>>, IMyProgrammableBlock>(id);
                p.Setter = (e, v) => DetectorServer.Add(new SearchTask(v));
                MyAPIGateway.TerminalControls.AddControl<IMyProgrammableBlock>(p);
            }
        }
    }
}
