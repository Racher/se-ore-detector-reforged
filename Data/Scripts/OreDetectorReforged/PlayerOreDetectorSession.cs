using System.Collections.Generic;
using Sandbox.ModAPI;
using VRageMath;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using OreDetectorReforged.Detector;

namespace OreDetectorReforged
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    class PlayerOreDetectorSession : MySessionComponentBase
    {
        const string gpsDesc = "Reforged";
        int counter;
        List<int>[] gpss;

        HashSet<IMyCubeGrid> grids;

        public override void BeforeStart()
        {
            grids = new HashSet<IMyCubeGrid>();
            HashSet<IMyEntity> allEnts = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(allEnts, (a) => {
                Entities_OnEntityAdd(a);
                return false;
            });

            MyAPIGateway.Entities.OnEntityAdd += Entities_OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove += Entities_OnEntityRemove;
        }

        private void Entities_OnEntityAdd(IMyEntity obj)
        {
            if(obj is IMyCubeGrid)
            {
                grids.Add(obj as IMyCubeGrid);
            }
        }

        private void Entities_OnEntityRemove(IMyEntity obj)
        {
            if(grids.Contains(obj as IMyCubeGrid))
            {
                grids.Remove(obj as IMyCubeGrid);
            }
        }


        protected override void UnloadData()
        {
            MyAPIGateway.Entities.OnEntityAdd -= Entities_OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove -= Entities_OnEntityRemove;
        }

        public override void UpdateAfterSimulation()
        {
            var period = MaterialMappingHelper.Static.naturalOres.Length * SyncSessionComponent.detFrequencyDivider;
            counter = (counter + 1) % period;
            if (counter % SyncSessionComponent.detFrequencyDivider != 0)
                return;
            if (gpss == null)
            {
                gpss = new List<int>[MaterialMappingHelper.Static.naturalOres.Length];
                for (var i = 0; i < gpss.Length; ++i)
                    gpss[i] = new List<int>();
            }
            if (MyAPIGateway.Gui.GetCurrentScreen != MyTerminalPageEnum.None)
                for (var i = 0; i < gpss.Length; ++i)
                    RemoveMarkers(i);
            var o = counter / SyncSessionComponent.detFrequencyDivider % MaterialMappingHelper.Static.naturalOres.Length;
            var detector = GetDetector(o);
            if (detector == null)
            {
                RemoveMarkers(o);
                return;
            }
            var settings = TerminalOreDetector.GetStorage(detector);
            var ore = MaterialMappingHelper.Static.naturalOres[o];
            var area = new BoundingSphereD(detector.GetPosition(), settings.range);
            DetectorServer.Add(new SearchTask(area, ore, settings.count, (results) =>
            {
                RemoveMarkers(o);
                if (MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.None)
                    foreach (var result in results)
                    {
                        var gps = Session.GPS.Create(ore, gpsDesc, result, true);
                        gps.GPSColor = settings.color;
                        Session.GPS.AddLocalGps(gps);
                        gpss[o].Add(gps.Hash);
                    }
            }));
        }

        IMyOreDetector GetDetector(int o)
        {
            var shipController = Session.ControlledObject as IMyShipController;
            var maxrange = 1.5f;
            var minDistance = -1d;
            IMyOreDetector det = null;
            if (shipController != null)
            {
                foreach (var detector in shipController.CubeGrid.GetFatBlocks<IMyOreDetector>())
                {
                    var settings = TerminalOreDetector.GetStorage(detector);
                    if (!detector.IsWorking || settings == null || !settings.Whitelist[o] || settings.range < maxrange)
                        continue;
                    maxrange = settings.range;
                    det = detector;
                }
            }

            if (det == null)
            {
                Vector3D playerPos = Session.ControlledObject.Entity.GetPosition();
                foreach(var grid in grids)
                {
                    foreach (var detector in grid.GetFatBlocks<IMyOreDetector>())
                    {
                        var settings = TerminalOreDetector.GetStorage(detector);
                        var distance = Vector3D.DistanceSquared(detector.GetPosition(), playerPos);
                        if (!detector.IsWorking || !detector.BroadcastUsingAntennas || !detector.HasPlayerAccess(MyAPIGateway.Session.Player.IdentityId) || settings == null || !settings.Whitelist[o] || (minDistance > 0 && (settings.range - distance) < (maxrange - minDistance)))
                            continue;

                        foreach (var ant in grid.GetFatBlocks<IMyRadioAntenna>())
                        {
                            if (ant.IsWorking && ant.IsBroadcasting && ant.HasPlayerAccess(MyAPIGateway.Session.Player.IdentityId) && distance < ant.Radius*ant.Radius)
                            {
                                minDistance = distance;
                                maxrange = settings.range;
                                det = detector;
                                break;
                            }
                        }
                    }
                }
            }
            return det;
        }

        void RemoveMarkers(int ore)
        {
            foreach(var gps in gpss[ore])
                Session.GPS?.RemoveLocalGps(gps);
            gpss[ore].Clear();
        }
    }
}
