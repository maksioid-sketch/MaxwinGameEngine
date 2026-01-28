using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Engine.Runtime.MonoGame.Assets;

public sealed class TextureStore
{
    private readonly ContentManager _content;
    private readonly Dictionary<string, Texture2D> _cache = new();

    public TextureStore(ContentManager content)
    {
        _content = content;
    }

    public Texture2D Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Texture key is empty.");

        if (_cache.TryGetValue(key, out var tex))
            return tex;

        tex = _content.Load<Texture2D>(key);
        _cache[key] = tex;
        return tex;
    }
}

