using System;
using System.Collections.Generic;
using OreDetectorReforged.Detector;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace OreDetectorReforged
{
    static class SyncModStorage
    {
        public static void SetBytes(IMyEntity entity, byte[] bytes)
        {
            SetLocalBytes(entity, bytes);
            MyAPIGateway.Multiplayer.SendMessageToOthers(Main.storageMsgId, PrependData(entity.EntityId, bytes));
        }

        public static byte[] GetBytes(IMyEntity entity)
        {
            string s = null;
            entity?.Storage?.TryGetValue(Main.guid, out s);
            return s == null ? null : Convert.FromBase64String(s);
        }

        public static void DownloadStorage(IMyEntity entity)
        {
            if (!(entity is IMyOreDetector))
                return;
            if (!MyAPIGateway.Multiplayer.IsServer)
                MyAPIGateway.Multiplayer.SendMessageToServer(Main.storageMsgId, BitConverter.GetBytes(entity.EntityId));
            else
                MyAPIGateway.Parallel.Start(() => { }, () =>
                {
                    if (GetBytes(entity) == null)
                        SetLocalBytes(entity, new byte[1]);
                });
        }

        static void SetLocalBytes(IMyEntity entity, byte[] v)
        {
            var s = Convert.ToBase64String(v);
            if (entity.Storage == null)
                entity.Storage = new MyModStorageComponent();
            entity.Storage[Main.guid] = s;
        }

        static byte[] PrependData(long eid, byte[] payload)
        {
            var data = BitConverter.GetBytes(eid);
            Array.Resize(ref data, 8 + payload.Length);
            Array.Copy(payload, 0, data, 8, payload.Length);
            return data;
        }

        public static void HandleMsg(ushort msgId, byte[] data, ulong sender, bool reliable)
        {
            var e = MyAPIGateway.Entities.GetEntityById(BitConverter.ToInt64(data, 0));
            if (e == null || e.Closed)
                return;
            if (data.Length == 8)
            {
                MyAPIGateway.Parallel.Start(() => { }, () =>
                {
                    var payload = GetBytes(e);
                    if (payload == null || payload.Length == 0)
                        payload = new byte[1];
                    MyAPIGateway.Multiplayer.SendMessageTo(Main.storageMsgId, PrependData(e.EntityId, payload), sender);
                });
            }
            else
            {
                var payload = new byte[data.Length - 8];
                Array.Copy(data, 8, payload, 0, payload.Length);
                MyAPIGateway.Parallel.Start(() => { }, () => SetLocalBytes(e, payload));
            }
        }
    }
}
