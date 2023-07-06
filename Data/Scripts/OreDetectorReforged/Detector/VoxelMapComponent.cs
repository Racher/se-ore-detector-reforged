using Sandbox.Game.Entities;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.Components;

namespace OreDetectorReforged.Detector
{
    class VoxelMapComponent : MyComponentBase
    {
        public readonly IDetectorPage[] data;

        public VoxelMapComponent(MyVoxelBase vb)
        {
            vb.Components.Add(this);
            var planet = vb as MyPlanet;
            bool fakeBoulder = vb.BoulderInfo.HasValue && (vb.BoulderInfo.Value.SectorId >> 51) > 0;
            if (fakeBoulder)
                data = new IDetectorPage[0];
            else if (planet != null)
                data = Enumerable.Range(0, 6).Select(f => new DetectorPagePlanet(planet, f)).ToArray();
            else
                data = new IDetectorPage[] { new DetectorPageNotPlanet(vb) };
        }
    }
}
