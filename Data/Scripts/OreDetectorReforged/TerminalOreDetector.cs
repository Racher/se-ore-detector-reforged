using ProtoBuf;
using System;
using System.Collections;
using System.Collections.Generic;
using OreDetectorReforged.Detector;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace OreDetectorReforged
{
    static class TerminalOreDetector
    {
        [ProtoContract]
        public class OreDetectorStorage
        {
            [ProtoMember(1)]
            public float range = 30000;

            [ProtoMember(2)]
            int period;

            [ProtoMember(3)]
            public Color color = new Color(255, 220, 140);

            [ProtoMember(4)]
            public int count = 1;

            [ProtoMember(5)]
            int whitelist0;

            [ProtoMember(6)]
            int whitelist1;

            [ProtoMember(7)]
            int whitelist2;

            [ProtoMember(8)]
            int whitelist3;

            readonly int[] buf = new int[4];

            public OreDetectorStorage()
            {
                Whitelist = new BitArray(128, true);
            }

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
                }
            }

            public OreDetectorStorage(OreDetectorStorage other)
            {
                range = other.range;
                color = other.color;
                count = other.count;
                whitelist0 = other.whitelist0;
                whitelist1 = other.whitelist1;
                whitelist2 = other.whitelist2;
                whitelist3 = other.whitelist3;
            }
        }

        static bool inited;
        static float GetMaxRange(IMyTerminalBlock e) => e.CubeGrid.GridSizeEnum == MyCubeSize.Small ? SyncSessionComponent.detRangeSmall : SyncSessionComponent.detRangeLarge;

        public static OreDetectorStorage GetStorage(IMyTerminalBlock entity)
        {
            var r = SyncSessionComponent.GetLocal<OreDetectorStorage>(entity);
            if (r != null)
            {
                r.range = Math.Min(r.range, GetMaxRange(entity));
                r.count = Math.Min(r.count, SyncSessionComponent.detMaxCount);
            }
            return r;
        }

        public static void TryInit(IMyEntity entity)
        {
            if (entity as IMyOreDetector == null)
                return;
            SyncSessionComponent.DownloadStorage<OreDetectorStorage>(entity);
            if (inited)
                return;
            inited = true;
            {
                const string id = "Reforged: Separator";
                var p = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyOreDetector>(id);
                MyAPIGateway.TerminalControls.AddControl<IMyOreDetector>(p);
            }
            {
                const string id = "Reforged: GPSColor";
                var p = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlColor, IMyOreDetector>(id);
                p.Title = MyStringId.GetOrCompute(id);
                p.Visible = (e) => GetStorage(e) != null;
                p.Getter = (e) => GetStorage(e).color;
                p.Setter = (e, v) => SyncSessionComponent.SetAndSendMessageToOthers(e, new OreDetectorStorage(GetStorage(e)) { color = v });
                p.Tooltip = MyStringId.GetOrCompute("The same color for all mod generated ore gps");
                MyAPIGateway.TerminalControls.AddControl<IMyOreDetector>(p);
            }
            {
                const string id = "Reforged: Range";
                var p = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyOreDetector>(id);
                p.Title = MyStringId.GetOrCompute(id);
                p.Visible = (e) => GetStorage(e) != null;
                p.Getter = (e) => GetStorage(e).range;
                p.Setter = (e, v) => SyncSessionComponent.SetAndSendMessageToOthers(e, new OreDetectorStorage(GetStorage(e)) { range = v });
                p.SetLogLimits((e) => 1f, GetMaxRange);
                p.Writer = (e, sb) =>
                {
                    sb.Append(GetStorage(e).range);
                    sb.Append(" m");
                };
                MyAPIGateway.TerminalControls.AddControl<IMyOreDetector>(p);
            }
            {
                const string id = "Reforged: Count";
                var p = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyOreDetector>(id);
                p.Title = MyStringId.GetOrCompute(id);
                p.Visible = (e) => GetStorage(e) != null;
                p.Getter = (e) => GetStorage(e).count;
                p.Setter = (e, v) => SyncSessionComponent.SetAndSendMessageToOthers(e, new OreDetectorStorage(GetStorage(e)) { count = (int)v });
                p.SetLogLimits(1, SyncSessionComponent.detMaxCount);
                p.Writer = (e, sb) =>
                {
                    sb.Append(GetStorage(e).count);
                    sb.Append(" each");
                };
                p.Tooltip = MyStringId.GetOrCompute("Show nearest N of each mined ore");
                MyAPIGateway.TerminalControls.AddControl<IMyOreDetector>(p);
            }
            {
                const string id = "Reforged: Whitelist";
                var p = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyOreDetector>(id);
                p.Title = MyStringId.GetOrCompute(id);
                p.Visible = (e) => GetStorage(e) != null;
                p.VisibleRowsCount = 10;
                p.Multiselect = true;
                p.ListContent = (e, ls, ss) =>
                {
                    var whitelist = GetStorage(e).Whitelist;
                    for (var o = 0; o < MaterialMappingHelper.Static.naturalOres.Length; ++o)
                    {
                        var l = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(MaterialMappingHelper.Static.naturalOres[o]), new MyStringId(), o);
                        ls.Add(l);
                        if (whitelist[o])
                            ss.Add(l);
                    }
                };
                p.ItemSelected = (e, ss) =>
                {
                    var v = GetStorage(e);
                    var whitelist = new BitArray(128);
                    foreach (var l in ss)
                        whitelist[(int)l.UserData] = true;
                    v.Whitelist = whitelist;
                    SyncSessionComponent.SetAndSendMessageToOthers(e, v);
                };
                MyAPIGateway.TerminalControls.AddControl<IMyOreDetector>(p);
            }
            {
                const string id = "Ores";
                var p = MyAPIGateway.TerminalControls.CreateProperty<Dictionary<string, Vector3D>, IMyOreDetector>(id);
                p.Getter = (e) =>
                {
                    var comp = e.Components.Get<LegacyOresComponent>();
                    if (comp == null)
                        e.Components.Add(comp = new LegacyOresComponent());
                    var ores = comp.ores;
                    foreach (var ore in MaterialMappingHelper.Static.naturalOres)
                        DetectorServer.Add(new SearchTask(new BoundingSphereD(e.GetPosition(), 3e4), ore, 1, (vs) =>
                        {
                            if (vs.Count > 0)
                                ores[ore] = vs[0];
                        }));
                    return new Dictionary<string, Vector3D>(comp.ores);
                };
                MyAPIGateway.TerminalControls.AddControl<IMyOreDetector>(p);
            }
        }
        class LegacyOresComponent : MyComponentBase
        {
            public readonly Dictionary<string, Vector3D> ores = new Dictionary<string, Vector3D>();
        }
    }
}
