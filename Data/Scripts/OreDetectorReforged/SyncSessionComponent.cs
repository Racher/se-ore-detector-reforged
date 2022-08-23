using System;
using System.Collections.Generic;
using OreDetectorReforged.Detector;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;
using VRageMath;

namespace OreDetectorReforged
{

    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    class SyncSessionComponent : MySessionComponentBase
    {
        const ushort syncMsgId = 54986;
        static readonly Guid guid = new Guid("88d1eb02574adb88cf18b124f7bbea10");
        public static float detRangeSmall = 1e9f;
        public static float detRangeLarge = 1e9f;
        public static int detMaxCount = 100;
        public static int detFrequencyDivider = 2;
        IDisposable[] listeners;

        public override void LoadData()
        {
            listeners = new IDisposable[]
            {
                new ListenerOnEntityCreate(InitBlock),
                new ListenerMultiplayer(syncMsgId, HandleMsg),
                new ListenerMod(8420680221873732947, (o) => detRangeSmall = Convert.ToSingle(o)),
                new ListenerMod(6990672449530303784, (o) => detRangeLarge = Convert.ToSingle(o)),
                new ListenerMod(5488172606588660921, (o) => detMaxCount = Convert.ToInt32(o)),
                new ListenerMod(0738962904976040424, (o) => detFrequencyDivider = Convert.ToInt32(o)),
                new ListenerMod(3703961318234800629, (o) => DetectorServer.Add(new SearchTask((ValueTuple<BoundingSphereD, Vector3D, string, Func<Vector3D, bool>, Action>)o))),
            };
        }

        protected override void UnloadData()
        {
            foreach (var listener in listeners)
                listener.Dispose();
        }

        public static void DownloadStorage<T>(IMyEntity entity) where T : new()
        {
            if (!MyAPIGateway.Multiplayer.IsServer)
                MyAPIGateway.Multiplayer.SendMessageToServer(syncMsgId, BitConverter.GetBytes(entity.EntityId));
            else if (GetLocal<T>(entity) == null)
                SetLocal(entity, new T());
        }

        static void InitBlock(IMyEntity entity)
        {
            TerminalOreDetector.TryInit(entity);
            TerminalProgrammableBlock.TryInit(entity);
        }

        public static T GetLocal<T>(IMyEntity entity) where T : new()
        {
            var bytes = GetLocalBytes(entity);
            if (bytes == null)
                return default(T);
            try { return MyAPIGateway.Utilities.SerializeFromBinary<T>(bytes); }
            catch { return new T(); }
        }

        static byte[] GetLocalBytes(IMyEntity entity)
        {
            string s = null;
            entity?.Storage?.TryGetValue(guid, out s);
            return s == null ? null : Convert.FromBase64String(s);
        }

        static void SetLocalBytes(IMyEntity entity, byte[] v)
        {
            var s = Convert.ToBase64String(v);
            if (entity.Storage == null)
                entity.Storage = new MyModStorageComponent();
            entity.Storage[guid] = s;
        }

        static byte[] PrependData(long eid, byte[] payload)
        {
            var data = BitConverter.GetBytes(eid);
            Array.Resize(ref data, 8 + payload.Length);
            Array.Copy(payload, 0, data, 8, payload.Length);
            return data;
        }

        static void HandleMsg(ushort msgId, byte[] data, ulong sender, bool reliable)
        {
            var e = MyAPIGateway.Entities.GetEntityById(BitConverter.ToInt64(data, 0));
            if (e == null)
                return;
            else if (data.Length == 8)
                MyAPIGateway.Multiplayer.SendMessageTo(syncMsgId, PrependData(e.EntityId, GetLocalBytes(e) ?? new byte[1]), sender);
            else
            {
                var payload = new byte[data.Length - 8];
                Array.Copy(data, 8, payload, 0, payload.Length);
                SetLocalBytes(e, payload);
            }
        }

        static void SetLocal<T>(IMyEntity e, T v)
        {
            SetLocalBytes(e, MyAPIGateway.Utilities.SerializeToBinary(v));
        }

        public static void SetAndSendMessageToOthers<T>(IMyEntity e, T v)
        {
            SetLocal(e, v);
            MyAPIGateway.Multiplayer.SendMessageToOthers(syncMsgId, PrependData(e.EntityId, MyAPIGateway.Utilities.SerializeToBinary(v)));
        }
    }
}
