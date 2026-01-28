using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Engine.Core.Math;

public readonly struct IntRect
{
    public readonly int X, Y, W, H;

    public IntRect(int x, int y, int w, int h)
    {
        X = x; Y = y; W = w; H = h;
    }

    public bool IsEmpty => W <= 0 || H <= 0;
}

