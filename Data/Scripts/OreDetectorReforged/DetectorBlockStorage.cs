using ProtoBuf;
using System.Collections;
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
		int whitelist0;

		[ProtoMember(6)]
		int whitelist1;

		[ProtoMember(7)]
		int whitelist2;

		[ProtoMember(8)]
		int whitelist3;

		readonly int[] buf = new int[4];

		public DetectorBlockStorage()
		{
			Whitelist = PlanetMatHelper.GetGeneratedOres().Or(DetectorPageNotPlanet.generatedOres.Get());
		}

		public BitArray Whitelist
		{
			get
			{
				buf[0] = whitelist0;
				buf[1] = whitelist1;
				buf[2] = whitelist2;
				buf[3] = whitelist3;
				return new BitArray(buf);
			}
			set
			{
				value.CopyTo(buf, 0);
				whitelist0 = buf[0];
				whitelist1 = buf[1];
				whitelist2 = buf[2];
				whitelist3 = buf[3];
			}
		}

		public DetectorBlockStorage(DetectorBlockStorage other)
		{
			range = other.range;
			period = other.period;
			color = other.color;
			count = other.count;
			whitelist0 = other.whitelist0;
			whitelist1 = other.whitelist1;
			whitelist2 = other.whitelist2;
			whitelist3 = other.whitelist3;
		}
	}
}
