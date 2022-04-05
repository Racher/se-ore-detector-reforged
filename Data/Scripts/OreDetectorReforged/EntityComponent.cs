using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using VRageMath;

namespace OreDetectorReforged
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), true)]
    class EntityComponent : MyGameLogicComponent, IDetectorIO
    {
        static bool terminalInit;
        Detector detector;
        Vector3D[] positions;

        public int TaskPerCollect => SessionComponent.config.searchFrequencyScript * 100 / 60;

        public Vector3D GetPosition()
        {
            if ((Entity as IMyOreDetector).IsWorking)
                return Entity.GetPosition();
            detector = null;
            return default(Vector3D);
        }

        public void UpdateResult(Vector3D[] positions)
        {
            this.positions = positions;
            if (MyAPIGateway.Session.IsServer)
                return;
            var result = new ValueTuple<long, Vector3D[]>(Entity.EntityId, positions);
            MyAPIGateway.Multiplayer.SendMessageToServer(SessionComponent.msgId, MyAPIGateway.Utilities.SerializeToBinary(result));
        }

        public static void HandleDetectorRequest(ushort id, byte[] message, ulong recipient, bool reliable)
        {
            if (!MyAPIGateway.Session.IsServer)
            {
                var data = MyAPIGateway.Utilities.SerializeFromBinary<long>(message);
                VRage.ModAPI.IMyEntity e;
                if (!MyAPIGateway.Entities.TryGetEntityById(data, out e))
                    return;
                var comp = e.Components.Get<EntityComponent>();
                if (comp.detector == null)
                    comp.detector = new Detector(comp);
            }
            else
            {
                var data = MyAPIGateway.Utilities.SerializeFromBinary<ValueTuple<long, Vector3D[]>>(message);
                VRage.ModAPI.IMyEntity e;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.Item1, out e))
                    return;
                e.Components.Get<EntityComponent>().positions = data.Item2;
            }
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Container.Add(this);
            if (terminalInit)
                return;
            terminalInit = true;
            var terminalProp = MyAPIGateway.TerminalControls.CreateProperty<Dictionary<string, Vector3D>, IMyOreDetector>("Ores");
            terminalProp.Getter = (e) => e.Components.Get<EntityComponent>().GetResult();
            MyAPIGateway.TerminalControls.AddControl<IMyOreDetector>(terminalProp);
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
            var owner = MyAPIGateway.Players.TryGetSteamId((Entity as IMyOreDetector).OwnerId);
            if (MyAPIGateway.Multiplayer.MultiplayerActive && owner != MyAPIGateway.Multiplayer.ServerId)
            {
                var message = MyAPIGateway.Utilities.SerializeToBinary(Entity.EntityId);
                if (!MyAPIGateway.Multiplayer.SendMessageTo(SessionComponent.msgId, message, owner))
                    throw new Exception("Detector owner not available");
            }
            else if (detector == null)
                detector = new Detector(this);
            var oreNames = SessionComponent.materialOreMapping.oreNames;
            var dictResult = new Dictionary<string, Vector3D>();
            for (var m = 0; m < oreNames.Length; ++m)
                if (positions != null && positions[m] != default(Vector3D))
                    dictResult.Add(oreNames[m], positions[m]);
            return dictResult;
        }
    }
}
