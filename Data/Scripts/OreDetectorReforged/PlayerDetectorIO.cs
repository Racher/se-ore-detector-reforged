using System;
using System.Linq;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game.ModAPI;
using VRageMath;

namespace OreDetectorReforged
{
    interface IDetectorIO
    {
        Vector3D GetPosition();

        void UpdateResult(Vector3D[] positions);

        int TaskPerCollect { get; }
    }
    class PlayerDetectorIO : IDetectorIO
    {
        readonly IMyGps[] gpss = new IMyGps[SessionComponent.materialOreMapping.oreNames.Length];

        public int TaskPerCollect => (SessionComponent.config.searchFrequencyPlayer + 5) / 6;

        static bool IsHandDrilling() => MyAPIGateway.Session.Player.Character == MyAPIGateway.Session.ControlledObject
            && (MyAPIGateway.Session.Player.Character.EquippedTool as IMyHandDrill) != null;

        static IMyOreDetector GetControlledVesselDetector() => (MyAPIGateway.Session.ControlledObject as IMyShipController)
            ?.CubeGrid.GetFatBlocks<IMyOreDetector>().FirstOrDefault(det => det.IsWorking);

        public Vector3D GetPosition() => IsHandDrilling() ? MyAPIGateway.Session.Player.GetPosition()
            : GetControlledVesselDetector()?.GetPosition() ?? default(Vector3D);

        public void UpdateResult(Vector3D[] positions)
        {
            if (MyAPIGateway.Gui.GetCurrentScreen != MyTerminalPageEnum.None)
                return;
            var oreNames = SessionComponent.materialOreMapping.oreNames;
            Array.Clear(gpss, 0, gpss.Length);
            foreach (var gps in MyAPIGateway.Session.GPS.GetGpsList(MyAPIGateway.Session.Player.IdentityId))
            {
                var i = Array.IndexOf(oreNames, gps.Name);
                if (i != -1)
                    gpss[i] = gps;
            }
            for (var i = 0; i < positions.Length; ++i)
            {
                var valid = !positions[i].Equals(default(Vector3D));
                var gps = gpss[i];
                if (gps == null)
                {
                    gps = MyAPIGateway.Session.GPS.Create(oreNames[i], "", default(Vector3D), valid);
                    gps.GPSColor = new Color(255, 220, 140);
                    MyAPIGateway.Session.GPS.AddGps(MyAPIGateway.Session.Player.IdentityId, gps);
                }
                else if ((valid && gps.GPSColor != Color.Black) != gps.ShowOnHud)
                    MyAPIGateway.Session.GPS.SetShowOnHud(MyAPIGateway.Session.Player.IdentityId, gps, !gps.ShowOnHud);
                if (valid)
                    gps.Coords = positions[i];
            }
        }
    }
}
