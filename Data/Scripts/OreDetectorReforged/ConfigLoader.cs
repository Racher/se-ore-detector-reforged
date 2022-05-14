using Sandbox.ModAPI;
using VRage.Game.Components;

namespace OreDetectorReforged
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    class ConfigLoader : MySessionComponentBase
    {
        public static Config Static;

        public override void LoadData()
        {
            Static = new Config();
        }
    }
}
