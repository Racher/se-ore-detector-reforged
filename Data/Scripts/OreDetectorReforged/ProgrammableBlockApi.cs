using System;
using OreDetectorReforged.Detector;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.Components;
using VRage.ModAPI;
using VRageMath;

// ReSharper disable UnusedType.Global

namespace OreDetectorReforged
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    class ProgrammableBlockApi : MySessionComponentBase
    {
        public override void LoadData()
        {
            MyEntities.OnEntityCreate += OnEntityCreate;
        }

        protected override void UnloadData()
        {
            MyEntities.OnEntityCreate -= OnEntityCreate;
        }

        static void OnEntityCreate(IMyEntity entity)
        {
            if (!(entity is IMyProgrammableBlock)) return;
            const string id = "DetectOre";
            var p = MyAPIGateway.TerminalControls.CreateProperty<MyTuple<BoundingSphereD, string, Action<Vector3D, Exception>>, IMyProgrammableBlock>(id);
            p.Setter = (e, v) => DetectorServer.SubmitSearch(v.Item1, v.Item2, v.Item3);
            MyAPIGateway.TerminalControls.AddControl<IMyProgrammableBlock>(p);
            MyEntities.OnEntityCreate -= OnEntityCreate;
        }
    }
}