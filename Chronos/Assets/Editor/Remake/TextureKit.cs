using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Procedural textures/sprites for the remake (UI icons, rings, stone, ghost outline, dial).
// Everything is drawn from signed-distance functions so edges stay crisp at any UI scale.
public static class TextureKit
{
    public delegate float Sdf(float x, float y); // x,y in pixels; returns signed distance (px)

    public static string Save(string path, int w, int h, Func<int, int, Color> px)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var c = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) c[y * w + x] = px(x, y);
        tex.SetPixels(c); tex.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        return path;
    }

    public static string Shape(string path, int size, Sdf sdf, float soft = 1f)
        => Save(path, size, size, (x, y) => new Color(1, 1, 1, Mathf.Clamp01(0.5f - sdf(x + 0.5f, y + 0.5f) / soft)));

    // ---------------------------------------------------------------- sdf helpers
    public static float Circle(float x, float y, float cx, float cy, float r) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
    public static float RoundRect(float x, float y, float cx, float cy, float hw, float hh, float r)
    {
        float qx = Mathf.Abs(x - cx) - hw + r, qy = Mathf.Abs(y - cy) - hh + r;
        return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
    }
    public static float Tri(float x, float y, Vector2 a, Vector2 b, Vector2 c)
    {
        // signed distance to triangle (negative inside)
        Vector2 p = new(x, y);
        float d = Mathf.Min(Seg(p, a, b), Mathf.Min(Seg(p, b, c), Seg(p, c, a)));
        float s1 = Cross(b - a, p - a), s2 = Cross(c - b, p - b), s3 = Cross(a - c, p - c);
        bool inside = (s1 >= 0 && s2 >= 0 && s3 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0);
        return inside ? -d : d;
    }
    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    static float Seg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / ba.sqrMagnitude);
        return (pa - ba * h).magnitude;
    }
    public static float Union(float a, float b) => Mathf.Min(a, b);

    // ---------------------------------------------------------------- noise
    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 2147483647;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }
    }
    public static float ValueNoise(float x, float y, int period, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        int W(int v) => ((v % period) + period) % period;
        float a = Hash(W(x0), W(y0), seed), b = Hash(W(x0 + 1), W(y0), seed), c = Hash(W(x0), W(y0 + 1), seed), d = Hash(W(x0 + 1), W(y0 + 1), seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }
    public static float Fbm(float u, float v, int seed, int oct = 5)
    {
        float s = 0, amp = 0.5f; int per = 4;
        for (int i = 0; i < oct; i++) { s += amp * ValueNoise(u * per, v * per, per, seed + i * 17); amp *= 0.5f; per *= 2; }
        return s;
    }

    // ---------------------------------------------------------------- import settings
    public static void ImportSprite(string path, Vector4 border, bool mip = false)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spriteBorder = border;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = mip;
        ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.SaveAndReimport();
    }

    public static Texture2D ImportTexture(string path, bool alpha, TextureWrapMode wrap = TextureWrapMode.Repeat, bool srgb = true)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Default;
        ti.alphaIsTransparency = alpha;
        ti.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        ti.wrapMode = wrap;
        ti.sRGBTexture = srgb;
        ti.mipmapEnabled = true;
        ti.anisoLevel = 4;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
