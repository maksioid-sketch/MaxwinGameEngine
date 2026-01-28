using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Engine.Core.Math;

public readonly struct Color4
{
    public readonly float R, G, B, A;

    public Color4(float r, float g, float b, float a = 1f)
    {
        R = r; G = g; B = b; A = a;
    }

    public static Color4 White => new(1f, 1f, 1f, 1f);
}
