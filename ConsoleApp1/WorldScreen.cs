using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace FreeBhopEz
{
    public static class WorldScreen
    {
        public static Vector2 WorldToScreen(float[] m, Vector3 pos, Vector2 win)
        {
            float w = m[12] * pos.X + m[13] * pos.Y + m[14] * pos.Z + m[15];
            if (w < 0.001f) return new Vector2(-99, -99);

            float x = m[0] * pos.X + m[1] * pos.Y + m[2] * pos.Z + m[3];
            float y = m[4] * pos.X + m[5] * pos.Y + m[6] * pos.Z + m[7];

            return new Vector2(
                (win.X / 2) + (win.X / 2) * x / w,
                (win.Y / 2) - (win.Y / 2) * y / w
            );
        }

    }
} 