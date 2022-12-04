using System.Collections;
using System.Collections.Generic;
using OreDetectorReforged.Detector;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace OreDetectorReforged
{
    static class TerminalOreDetector
    {
        static bool inited;

        public static void InitTerminalControls(IMyEntity entity)
        {
            if (inited || !(entity is IMyOreDetector))
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
                p.Visible = (e) => OreDetectorData.Parse(e) != null;
                p.Getter = (e) => OreDetectorData.Parse(e).Color;
                p.Setter = (e, v) => OreDetectorData.Parse(e).Color = v;
                p.Tooltip = MyStringId.GetOrCompute("The same color for all mod generated ore gps");
                MyAPIGateway.TerminalControls.AddControl<IMyOreDetector>(p);
            }
            {
                const string id = "Reforged: Range";
                var p = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyOreDetector>(id);
                p.Title = MyStringId.GetOrCompute(id);
                p.Visible = (e) => OreDetectorData.Parse(e) != null;
                p.Getter = (e) => OreDetectorData.Parse(e).Range;
                p.Setter = (e, v) => OreDetectorData.Parse(e).Range = v;
                p.SetLogLimits((e) => 1f, (e) => OreDetectorData.Parse(e).rangeLimit);
                p.Writer = (e, sb) => sb.AppendFormat("{0} m", OreDetectorData.Parse(e).Range);
                MyAPIGateway.TerminalControls.AddControl<IMyOreDetector>(p);
            }
            {
                const string id = "Reforged: Count";
                var p = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyOreDetector>(id);
                p.Title = MyStringId.GetOrCompute(id);
                p.Visible = (e) => OreDetectorData.Parse(e) != null;
                p.Getter = (e) => OreDetectorData.Parse(e).Count;
                p.Setter = (e, v) => OreDetectorData.Parse(e).Count = (int)v;
                p.SetLogLimits((e) => 1f, (e) => Config.Static.gpsCountPerOreLimit);
                p.Writer = (e, sb) => sb.AppendFormat("{0} each", OreDetectorData.Parse(e).Count);
                p.Tooltip = MyStringId.GetOrCompute("Show nearest N of each mined ore");
                MyAPIGateway.TerminalControls.AddControl<IMyOreDetector>(p);
            }
            {
                const string id = "Reforged: Whitelist";
                var p = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyOreDetector>(id);
                p.Title = MyStringId.GetOrCompute(id);
                p.Visible = (e) => OreDetectorData.Parse(e) != null;
                p.VisibleRowsCount = 10;
                p.Multiselect = true;
                p.ListContent = (e, ls, ss) =>
                {
                    var whitelist = OreDetectorData.Parse(e).Whitelist;
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
                    var whitelist = new BitArray(128);
                    foreach (var l in ss)
                        whitelist[(int)l.UserData] = true;
                    OreDetectorData.Parse(e).Whitelist = whitelist;
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
