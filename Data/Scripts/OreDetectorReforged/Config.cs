using Sandbox.Definitions;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using VRage.Serialization;
using VRage.Utils;
using VRageMath;

namespace OreDetectorReforged
{
    public class Config
    {
        public bool writeConfigInWorldStorage = false;
        public bool gpsAngleInfo = false;
        public int detectEveryNthUpdate = 2;
        public int gpsCountPerOreLimit = 100;
        public int gpsColorR = 255;
        public int gpsColorG = 220;
        public int gpsColorB = 140;
        public float detectorRangeDefault = 30000f;
        public float detectorRangeLimitDefault = 1e9f;
        public SerializableDictionary<string, float> detectorRangeLimits = new SerializableDictionary<string, float>(new Dictionary<string, float>
        {
            { "LargeOreDetector", 1e9f },
            { "SmallBlockOreDetector", 1e9f },
        });
        [XmlIgnore] public Color GpsColorDefault => new Color(gpsColorR, gpsColorG, gpsColorB);

        public static Config Static;

        public static void HandleMsg(ushort msgId, byte[] data, ulong sender, bool reliable)
        {
            if (MyAPIGateway.Multiplayer.IsServer)
                MyAPIGateway.Parallel.Start(() => MyAPIGateway.Multiplayer.SendMessageTo(Main.configMsgId, Encoding.UTF8.GetBytes(MyAPIGateway.Utilities.SerializeToXML(Static)), sender));
            else
                MyAPIGateway.Parallel.Start(() =>
                {
                    Static = MyAPIGateway.Utilities.SerializeFromXML<Config>(Encoding.UTF8.GetString(data)) ?? new Config();
                });
        }

        static void LoadConfigFile()
        {
            const string configFileName = "OreDetectorReforgedConfig.xml";
            try
            {
                using (var tr = MyAPIGateway.Utilities.ReadFileInWorldStorage(configFileName, typeof(Config)))
                    Static = MyAPIGateway.Utilities.SerializeFromXML<Config>(tr.ReadToEnd());
            }
            catch
            {
                try
                {
                    using (var tr = MyAPIGateway.Utilities.ReadFileInGlobalStorage(configFileName))
                        Static = MyAPIGateway.Utilities.SerializeFromXML<Config>(tr.ReadToEnd());
                }
                catch { }
            }
            Static = Static ?? new Config();
            Static.detectorRangeLimits = Static.detectorRangeLimits ?? new SerializableDictionary<string, float>();
            try
            {
                using (var tw = Static.writeConfigInWorldStorage ? MyAPIGateway.Utilities.WriteFileInWorldStorage(configFileName, typeof(Config)) : MyAPIGateway.Utilities.WriteFileInGlobalStorage(configFileName))
                    tw.Write(MyAPIGateway.Utilities.SerializeToXML(Static));
            }
            catch { }
        }

        public static void LoadConfig()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
                LoadConfigFile();
            else
                MyAPIGateway.Multiplayer.SendMessageToServer(Main.configMsgId, new byte[0]);
        }
    }
}
