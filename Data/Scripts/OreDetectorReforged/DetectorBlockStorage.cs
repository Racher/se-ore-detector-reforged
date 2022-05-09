using ProtoBuf;
using Sandbox.Definitions;
using System;
using VRageMath;

namespace OreDetectorReforged
{
	[ProtoContract]
	class DetectorBlockStorage
	{
		[ProtoMember(1)]
		public float range = 30000;

		[ProtoMember(2)]
		public int period = 30;

		[ProtoMember(3)]
		public Color color = new Color(255, 220, 140);

		[ProtoMember(4)]
		public int count = 1;

		[ProtoMember(5)]
		public ulong whitelist;

		public DetectorBlockStorage()
		{
			var valid = PlanetMatHelper.GetGeneratedOres() | DetectorPageNotPlanet.generatedOres.Get();
			for (var o = 0; o < 64; ++o)
				if ((valid & 1ul << o) != 0)
					whitelist |= 1ul << o;
		}

		public DetectorBlockStorage(DetectorBlockStorage other)
		{
			range = other.range;
			period = other.period;
			color = other.color;
			count = other.count;
			whitelist = other.whitelist;
		}
	}
}
