using VRage.Game.Components;
using System;
using Sandbox.ModAPI;
using VRageMath;
using System.Collections.Generic;

namespace OreDetectorReforged
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    class PlayerOreDetectorSession : MySessionComponentBase
    {
        const string gpsDesc = "Reforged";
        uint counter;

        class CombinedSetting
        {
            public int period = 1000;
            public readonly int[] count = new int[128];
            public readonly float[] range = new float[128];
            public Color color;

            public static CombinedSetting operator |(CombinedSetting l, DetectorBlockStorage r)
            {
                var whitelist = r.Whitelist;
                for (var i = 0; i < l.count.Length; ++i)
                {
                    if (!whitelist[i])
                        continue;
                    l.period = Math.Min(l.period, Math.Max(1, r.period));
                    l.count[i] = Math.Max(l.count[i], Math.Min(1000, r.count));
                    l.range[i] = Math.Max(l.range[i], r.range);
                    l.color = r.color;
                }
                return l;
            }
        }

        bool GetDetector(out CombinedSetting settings, out Vector3D position)
        {
            settings = null;
            var shipController = Session.ControlledObject as IMyShipController;
            if (shipController != null)
                foreach (var detector in shipController.CubeGrid.GetFatBlocks<IMyOreDetector>())
                    if (detector.IsWorking)
                        settings = (settings ?? new CombinedSetting()) | TerminalSession.GetLocalOrNewDet(detector);
            position = shipController?.GetPosition() ?? new Vector3D();
            return settings != null;
        }

        void RemoveMarkers()
        {
            if (Session.Player != null)
                foreach (var gps in Session.GPS.GetGpsList(Session.Player.IdentityId))
                    if (gps.Description == gpsDesc)
                        Session.GPS.RemoveLocalGps(gps);
        }

        public override void UpdateAfterSimulation()
        {
            const uint loadingTime = 60;
            ++counter;
            CombinedSetting settings;
            Vector3D position;
            if (counter < loadingTime || !GetDetector(out settings, out position))
            {
                RemoveMarkers();
                return;
            }
            var searchOres = new List<int>(MaterialMappingHelper.Static.naturalOres.Length);
            for (var i = 0; i < MaterialMappingHelper.Static.naturalOres.Length; ++i)
                if (settings.count[i] > 0)
                    searchOres.Add(i);
            var bucket = (int)(counter % settings.period);
            var start = bucket * searchOres.Count / settings.period;
            var end = (bucket + 1) * searchOres.Count / settings.period;
            for (var j = start; j < end; ++j)
            {
                var i = searchOres[j];
                var ore = MaterialMappingHelper.Static.naturalOres[i];
                var area = new BoundingSphereD(position, settings.range[i]);
                if (area.Radius < 2)
                    continue;
                DetectorServer.Add(new SearchTask(area, ore, settings.count[i], (results) =>
                {
                    if (MyAPIGateway.Gui.GetCurrentScreen != VRage.Game.ModAPI.MyTerminalPageEnum.None)
                    {
                        RemoveMarkers();
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
