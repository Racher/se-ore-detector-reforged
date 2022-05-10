﻿using VRage.Game;
using VRage.Game.Components;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.ObjectBuilders;
using System;
using System.Linq;

namespace OreDetectorReforged
{
    class DetectorComponent : MyGameLogicComponent
    {
        public readonly IDetectorPage[] data;

        public DetectorComponent(MyVoxelBase vb)
        {
            vb.Components.Add(this);
            var planet = vb as MyPlanet;
            if (planet == null)
                data = new IDetectorPage[] { new DetectorPageNotPlanet(vb) };
            else
                data = Enumerable.Range(0, 6).Select(f => new DetectorPagePlanet(planet, f)).ToArray();
        }
    }
}