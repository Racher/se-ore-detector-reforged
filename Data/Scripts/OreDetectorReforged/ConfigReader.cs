using Sandbox.ModAPI;
using System;
using VRageMath;

namespace OreDetectorReforged
{
    class ConfigReader
    {
        bool sleep;
        int hashCode;
        readonly string name = "OreDetectorReforgedConfig";

        public void Update()
        {
            if (MyAPIGateway.Session.Player == null || sleep && MyAPIGateway.Gui.GetCurrentScreen != VRage.Game.ModAPI.MyTerminalPageEnum.Gps)
                return;
            sleep = MyAPIGateway.Gui.GetCurrentScreen != VRage.Game.ModAPI.MyTerminalPageEnum.Gps;
            foreach (var gps in MyAPIGateway.Session.GPS.GetGpsList(MyAPIGateway.Session.Player.IdentityId))
                if (gps.Name == name)
                    try
                    {
                        var hashCode = gps.Description.GetHashCode();
                        if (hashCode != this.hashCode)
                        {
                            SessionComponent.config = MyAPIGateway.Utilities.SerializeFromXML<Config>(gps.Description);
                            ++SessionComponent.configVersion;
                            this.hashCode = hashCode;
                        }
                        return;
                    }
                    catch
                    {
                        MyAPIGateway.Session.GPS.RemoveGps(MyAPIGateway.Session.Player.IdentityId, gps);
                        break;
                    }
            SessionComponent.config = new Config();
            ++SessionComponent.configVersion;
            var desc = MyAPIGateway.Utilities.SerializeToXML(SessionComponent.config);
            var configGps = MyAPIGateway.Session.GPS.Create(name, desc, default(Vector3D), false);
            this.hashCode = desc.GetHashCode();
            MyAPIGateway.Session.GPS.AddGps(MyAPIGateway.Session.Player.IdentityId, configGps);
        }
    }
}
