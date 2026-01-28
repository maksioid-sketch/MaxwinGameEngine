using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using Microsoft.Xna.Framework;

namespace Engine.Runtime.MonoGame.Rendering;

internal static class NumericsConversions
{
    public static Matrix ToXna(this Matrix4x4 m) =>
        new(
            m.M11, m.M12, m.M13, m.M14,
            m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34,
            m.M41, m.M42, m.M43, m.M44);

    public static Microsoft.Xna.Framework.Color ToXna(this Engine.Core.Math.Color4 c)
    {
        // MonoGame Color expects bytes; clamp safely.
        byte b(float x) => (byte)System.Math.Clamp((int)(x * 255f), 0, 255);
        return new Microsoft.Xna.Framework.Color(b(c.R), b(c.G), b(c.B), b(c.A));
    }
}
