using ProtoBuf;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using System;
using System.Collections;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace OreDetectorReforged
{
    [ProtoContract]
    public class OreDetectorData
    {
        [ProtoMember(1)]
        float range = Config.Static.detectorRangeDefault;

        [ProtoMember(2)]
        int period;

        [ProtoMember(3)]
        Color color = Config.Static.GpsColorDefault;

        [ProtoMember(4)]
        int count = 1;

        [ProtoMember(5)]
        int whitelist0 = -1;

        [ProtoMember(6)]
        int whitelist1 = -1;

        [ProtoMember(7)]
        int whitelist2 = -1;

        [ProtoMember(8)]
        int whitelist3 = -1;

        IMyEntity entity;

        readonly int[] buf = new int[4];

        public float rangeLimit = Config.Static.detectorRangeLimitDefault;

        void Save() => SyncModStorage.SetBytes(entity, MyAPIGateway.Utilities.SerializeToBinary(this));

        public float Range { get { return Math.Max(1, Math.Min(rangeLimit, range)); } set { range = value; Save(); } }

        public Color Color { get { return color; } set { color = value; Save(); } }

        public int Count { get { return Math.Max(1, Math.Min(Config.Static.gpsCountPerOreLimit, count)); } set { count = value; Save(); } }

        public BitArray Whitelist
        {
            get
            {
                buf[0] = whitelist0;
                buf[1] = whitelist1;
                buf[2] = whitelist2;
                buf[3] = whitelist3;
                return new BitArray(buf);
            }
            set
            {
                value.CopyTo(buf, 0);
                whitelist0 = buf[0];
                whitelist1 = buf[1];
                whitelist2 = buf[2];
                whitelist3 = buf[3];
                Save();
            }
        }

        public static OreDetectorData Parse(IMyEntity e)
        {
            if (!(e is IMyOreDetector) || Config.Static == null)
                return null;
            var bytes = SyncModStorage.GetBytes(e);
            if (bytes == null)
                return null;
            OreDetectorData o;
            try { o = MyAPIGateway.Utilities.SerializeFromBinary<OreDetectorData>(bytes); }
            catch { o = new OreDetectorData(); }
            o.entity = e;
            if (!Config.Static.detectorRangeLimits.Dictionary.TryGetValue((e as IMyTerminalBlock).BlockDefinition.SubtypeId, out o.rangeLimit))
                o.rangeLimit = Config.Static.detectorRangeLimitDefault;
            return o;
        }
    }
}
