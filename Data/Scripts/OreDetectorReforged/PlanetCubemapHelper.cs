using System;
using VRageMath;

namespace OreDetectorReforged
{

    static class PlanetCubemapHelper
    {
        public static string GetFaceMatInfix(int f)
        {
            switch (f)
            {
                case 0: return "front";
                case 1: return "back";
                case 2: return "right";
                case 3: return "left";
                case 4: return "up";
                case 5: return "down";
                default: return null;
            }
        }

        static Vector2 Project(float x, float y, float z)
        {
            if (z < 1E-20f)
                z = 1E-20f;
            return new Vector2(x / z, y / z);
        }

        public static Vector2 LocalToFace(Vector3 l, int f)
        {
            switch (f)
            {
                case 0: return Project(-l.X, -l.Y, -l.Z);
                case 1: return Project(l.X, -l.Y, l.Z);
                case 2: return Project(l.Z, -l.Y, -l.X);
                case 3: return Project(-l.Z, -l.Y, l.X);
                case 4: return Project(-l.X, -l.Z, l.Y);
                case 5: return Project(l.X, -l.Z, -l.Y);
                default: throw new Exception();
            }
        }

        public static Vector3 FaceToCube(Vector2 t, int f)
        {
            switch (f)
            {
                case 0: return new Vector3(-t.X, -t.Y, -1);
                case 1: return new Vector3(t.X, -t.Y, 1);
                case 2: return new Vector3(-1, -t.Y, t.X);
                case 3: return new Vector3(1, -t.Y, -t.X);
                case 4: return new Vector3(-t.X, 1, -t.Y);
                case 5: return new Vector3(t.X, -1, -t.Y);
                default: throw new Exception();
            }
        }

        public static Vector3 TexToCube(Vector2 tx, int width, int f) => FaceToCube((tx * 2 / width) - 1, f);
    }
}
