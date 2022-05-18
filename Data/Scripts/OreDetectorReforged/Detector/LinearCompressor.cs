using System;

namespace OreDetectorReforged.Detector
{
    struct LinearCompressor
    {
        readonly float min;
        readonly float step;

        public static LinearCompressor FromMinMax(float min, float max) => new LinearCompressor(min, (max - min) / 255);

        LinearCompressor(float min, float step)
        {
            this.min = min;
            this.step = step;
        }

        public byte CompressLower(float v) => (byte)Math.Min(255, Math.Max(0, (v - min) / step));
        public byte CompressUpper(float v) => (byte)Math.Min(255, Math.Max(0, (v - min) / step + 1));
        public float Decompress(byte v) => min + (v * step);
    }
}
