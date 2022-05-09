using VRage.Game.Components;
using System;
using Sandbox.ModAPI;
using VRageMath;
using Sandbox.Definitions;
using System.Linq;
using System.Collections.Generic;

namespace OreDetectorReforged
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    class PlayerOreDetectorSession : MySessionComponentBase
    {
        uint counter;
        public override void UpdateAfterSimulation()
        {
            ++counter;
            if (counter < 60)
                return;
            var detector = (Session.ControlledObject as IMyShipController)?.CubeGrid.GetFatBlocks<IMyOreDetector>().FirstOrDefault(det => det.IsWorking);
            if (detector == null)
                return;
            var settings = TerminalSession.GetLocalOrNew<DetectorBlockStorage>(detector);
            if (settings == null || settings.period < 1 || settings.range <= 1.5f || settings.count < 1 || settings.whitelist == 0)
                return;
            var area = new BoundingSphereD(detector.GetPosition(), settings.range);
            List<string> searchOres;
            {
                string[] oreNames;
                MyDefinitionManager.Static.GetOreTypeNames(out oreNames);
                searchOres = new List<string>(oreNames.Length);
                for (var i = 0; i < oreNames.Length; ++i)
                    if ((settings.whitelist & (1ul << i)) != 0)
                        searchOres.Add(oreNames[i]);
            }
            var bucket = (int)(counter % settings.period);
            var start = bucket * searchOres.Count / settings.period;
            var end = (bucket + 1) * searchOres.Count / settings.period;
            for (var i = start; i < end; ++i)
            {
                var ore = searchOres[i];
                DetectorServer.Add(new SearchTask(area, ore, Math.Min(1000, settings.count), (results) =>
                {
                    const string gpsDesc = "Reforged";
                    if (MyAPIGateway.Gui.GetCurrentScreen != VRage.Game.ModAPI.MyTerminalPageEnum.None)
                    {
                        foreach (var gps in Session.GPS.GetGpsList(Session.Player.IdentityId))
                            if (gps.Description == gpsDesc)
                                Session.GPS.RemoveLocalGps(gps);
                        return;
                    }
                    foreach (var result in results)
                    {
                        var gps = Session.GPS.Create(ore, gpsDesc, result, true);
                        gps.DiscardAt = Session.ElapsedPlayTime + TimeSpan.FromMilliseconds(16 * settings.period);
                        gps.GPSColor = settings.color;
                        Session.GPS.RemoveLocalGps(gps);
                        Session.GPS.AddLocalGps(gps);
                    }
                }));
            }
        }
    }
}
