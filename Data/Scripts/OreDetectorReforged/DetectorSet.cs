using Sandbox.ModAPI;
using VRage.Game.Components;

namespace OreDetectorReforged
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    class DetectorSet : TerminalBlockSet<IMyOreDetector>
    {
    }
}
