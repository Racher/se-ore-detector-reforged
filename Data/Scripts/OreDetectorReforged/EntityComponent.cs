using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using VRageMath;

namespace OreDetectorReforged
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), true)]
    class EntityComponent : MyGameLogicComponent, IDetectorIO
    {
        Detector detector;
        readonly Dictionary<string, Vector3D> result = new Dictionary<string, Vector3D>();

        public int TaskPerCollect => SessionComponent.config.searchFrequencyScript * 100 / 60;

        public Vector3D GetPosition() => (Entity as IMyOreDetector).IsFunctional ? Entity.GetPosition() : default(Vector3D);

        public void UpdateResult(Vector3D[] positions)
        {
            var oreNames = SessionComponent.materialOreMapping.oreNames;
            result.Clear();
            for (var m = 0; m < oreNames.Length; ++m)
                if (positions[m] != default(Vector3D))
                    result.Add(oreNames[m], positions[m]);
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Container.Add(this);
        }

        public override void UpdateAfterSimulation100()
        {
            try
            {
                detector?.Update();
            }
            catch (Exception e)
            {
                SessionComponent.lastException = e;
            }
        }

        public Dictionary<string, Vector3D> GetResult()
        {
            if (detector == null)
                detector = new Detector(this);
            return result;
        }

        public void Reset()
        {
            detector = null;
        }
    }
}
