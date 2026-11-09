using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Small procedural mesh toolkit for the remake's art kit (editor-time generation only).
public static class MeshKit
{
    // ---------------------------------------------------------------- primitives
    public static Mesh Box(Vector3 size)
    {
        var h = size * 0.5f;
        var c = new[]
        {
            new Vector3(-h.x,-h.y,-h.z), new Vector3(h.x,-h.y,-h.z), new Vector3(h.x,h.y,-h.z), new Vector3(-h.x,h.y,-h.z),
            new Vector3(-h.x,-h.y,h.z), new Vector3(h.x,-h.y,h.z), new Vector3(h.x,h.y,h.z), new Vector3(-h.x,h.y,h.z)
        };
        var tris = new List<Vector3[]>
        {
            Q(c[0],c[3],c[2],c[1]), Q(c[5],c[6],c[7],c[4]), Q(c[4],c[7],c[3],c[0]),
            Q(c[1],c[2],c[6],c[5]), Q(c[3],c[7],c[6],c[2]), Q(c[4],c[0],c[1],c[5])
        };
        return FromQuads(tris);
    }

    // Frustum / cylinder / cone along +Y from y=0..h. seg=4 + rot45 => square.
    public static Mesh Frustum(float rBottom, float rTop, float h, int seg, float rotDeg = 0, bool caps = true)
    {
        var quads = new List<Vector3[]>();
        var tris = new List<Vector3[]>();
        for (int i = 0; i < seg; i++)
        {
            float a0 = Mathf.Deg2Rad * (rotDeg + 360f * i / seg), a1 = Mathf.Deg2Rad * (rotDeg + 360f * (i + 1) / seg);
            Vector3 b0 = new(Mathf.Cos(a0) * rBottom, 0, Mathf.Sin(a0) * rBottom), b1 = new(Mathf.Cos(a1) * rBottom, 0, Mathf.Sin(a1) * rBottom);
            Vector3 t0 = new(Mathf.Cos(a0) * rTop, h, Mathf.Sin(a0) * rTop), t1 = new(Mathf.Cos(a1) * rTop, h, Mathf.Sin(a1) * rTop);
            if (rTop > 0.0001f) quads.Add(new[] { b0, t0, t1, b1 }); else tris.Add(new[] { b0, t0, b1 });
            if (caps)
            {
                if (rTop > 0.0001f) tris.Add(new[] { new Vector3(0, h, 0), t1, t0 });
                if (rBottom > 0.0001f) tris.Add(new[] { Vector3.zero, b0, b1 });
            }
        }
        return Build(quads, tris);
    }

    public static Mesh Gear(int teeth, float rIn, float rOut, float thick, float hole = 0f)
    {
        var quads = new List<Vector3[]>(); var tris = new List<Vector3[]>();
        int n = teeth * 4;
        var outer = new List<float>();
        for (int i = 0; i < n; i++) outer.Add((i % 4 == 1 || i % 4 == 2) ? rOut : rIn);
        for (int i = 0; i < n; i++)
        {
            float a0 = 2 * Mathf.PI * i / n, a1 = 2 * Mathf.PI * (i + 1) / n;
            float r0 = outer[i], r1 = outer[(i + 1) % n];
            if (i % 4 == 1) r1 = rOut; // flat tooth top
            Vector3 p0 = new(Mathf.Cos(a0) * r0, 0, Mathf.Sin(a0) * r0), p1 = new(Mathf.Cos(a1) * r1, 0, Mathf.Sin(a1) * r1);
            Vector3 up = Vector3.up * thick;
            quads.Add(new[] { p0, p0 + up, p1 + up, p1 });
            Vector3 h0 = new(Mathf.Cos(a0) * hole, 0, Mathf.Sin(a0) * hole), h1 = new(Mathf.Cos(a1) * hole, 0, Mathf.Sin(a1) * hole);
            if (hole > 0)
            {
                quads.Add(new[] { h0 + up, h1 + up, p1 + up, p0 + up });
                quads.Add(new[] { h1, h0, p0, p1 });
                quads.Add(new[] { h1, h1 + up, h0 + up, h0 });
            }
            else
            {
                tris.Add(new[] { up, p1 + up, p0 + up });
                tris.Add(new[] { Vector3.zero, p0, p1 });
            }
        }
        return Build(quads, tris);
    }

    public static Mesh Torus(float R, float r, int seg = 32, int side = 8)
    {
        var quads = new List<Vector3[]>();
        Vector3 P(int i, int j)
        {
            float u = 2 * Mathf.PI * i / seg, v = 2 * Mathf.PI * j / side;
            float rr = R + r * Mathf.Cos(v);
            return new Vector3(Mathf.Cos(u) * rr, r * Mathf.Sin(v), Mathf.Sin(u) * rr);
        }
        for (int i = 0; i < seg; i++) for (int j = 0; j < side; j++)
            quads.Add(new[] { P(i, j), P(i, j + 1), P(i + 1, j + 1), P(i + 1, j) });
        return Build(quads, new List<Vector3[]>(), smooth: true);
    }

    // Faceted (flat-shaded) ellipsoid, the low-poly look of the original rabbit.
    public static Mesh Facet(int lat = 6, int lon = 8, bool smooth = false)
    {
        var quads = new List<Vector3[]>(); var tris = new List<Vector3[]>();
        Vector3 P(int a, int b)
        {
            float th = Mathf.PI * a / lat, ph = 2 * Mathf.PI * b / lon;
            return new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)) * 0.5f;
        }
        for (int a = 0; a < lat; a++) for (int b = 0; b < lon; b++)
        {
            if (a == 0) tris.Add(new[] { P(0, 0), P(1, b + 1), P(1, b) });
            else if (a == lat - 1) tris.Add(new[] { P(a, b), P(a, b + 1), P(lat, 0) });
            else quads.Add(new[] { P(a, b), P(a, b + 1), P(a + 1, b + 1), P(a + 1, b) });
        }
        return Build(quads, tris, smooth);
    }

    // Hanging rock chunk under floating tiles: jagged rings tapering to a point (y<=0).
    public static Mesh Rock(int seed, float topR, float depth, int seg = 7)
    {
        var rnd = new System.Random(seed);
        float R() => (float)rnd.NextDouble();
        int rings = 3;
        var ring = new List<Vector3[]>();
        for (int k = 0; k <= rings; k++)
        {
            float t = (float)k / rings;
            float y = -depth * Mathf.Pow(t, 0.85f);
            float rr = topR * (1 - t * 0.85f);
            var pts = new Vector3[seg];
            for (int i = 0; i < seg; i++)
            {
                float a = 2 * Mathf.PI * (i + R() * 0.35f) / seg;
                float jitter = k == 0 ? 1f : 0.72f + 0.45f * R();
                pts[i] = new Vector3(Mathf.Cos(a) * rr * jitter, y + (k == 0 ? 0 : (R() - 0.5f) * depth * 0.12f), Mathf.Sin(a) * rr * jitter);
            }
            ring.Add(pts);
        }
        var quads = new List<Vector3[]>(); var tris = new List<Vector3[]>();
        Vector3 tip = new((R() - 0.5f) * topR * 0.3f, -depth * (1.05f + R() * 0.25f), (R() - 0.5f) * topR * 0.3f);
        for (int k = 0; k < rings; k++)
            for (int i = 0; i < seg; i++)
                quads.Add(new[] { ring[k][i], ring[k][(i + 1) % seg], ring[k + 1][(i + 1) % seg], ring[k + 1][i] });
        for (int i = 0; i < seg; i++) tris.Add(new[] { ring[rings][i], ring[rings][(i + 1) % seg], tip });
        for (int i = 1; i < seg - 1; i++) tris.Add(new[] { ring[0][0], ring[0][i + 1], ring[0][i] });
        return Build(quads, tris);
    }

    public static Mesh Quad(float w, float h)
    {
        var m = new Mesh
        {
            vertices = new[] { new Vector3(-w / 2, 0, -h / 2), new Vector3(w / 2, 0, -h / 2), new Vector3(w / 2, 0, h / 2), new Vector3(-w / 2, 0, h / 2) },
            uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) },
            triangles = new[] { 0, 2, 1, 0, 3, 2 },
            normals = Enumerable.Repeat(Vector3.up, 4).ToArray()
        };
        m.RecalculateBounds();
        return m;
    }

    // ---------------------------------------------------------------- combining
    public struct Part
    {
        public Mesh mesh; public Matrix4x4 m; public int mat;
        public Part(Mesh mesh, Vector3 p, Vector3 euler, Vector3 s, int mat) { this.mesh = mesh; m = Matrix4x4.TRS(p, Quaternion.Euler(euler), s); this.mat = mat; }
    }

    public static Mesh Combine(IEnumerable<Part> parts, int materialCount)
    {
        var list = parts.ToList();
        var perMat = new List<CombineInstance>();
        for (int k = 0; k < materialCount; k++)
        {
            var ci = list.Where(p => p.mat == k).Select(p => new CombineInstance { mesh = p.mesh, transform = p.m }).ToArray();
            var sub = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            if (ci.Length > 0) sub.CombineMeshes(ci, true, true);
            else
            {   // unused material slot: keep the submesh index stable with a zero-area triangle
                sub.vertices = new[] { Vector3.zero, Vector3.zero, Vector3.zero };
                sub.normals = new[] { Vector3.up, Vector3.up, Vector3.up };
                sub.uv = new[] { Vector2.zero, Vector2.zero, Vector2.zero };
                sub.triangles = new[] { 0, 1, 2 };
            }
            perMat.Add(new CombineInstance { mesh = sub, transform = Matrix4x4.identity });
        }
        var result = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        result.CombineMeshes(perMat.ToArray(), false, false);
        result.RecalculateBounds();
        return result;
    }

    // ---------------------------------------------------------------- internals
    static Vector3[] Q(Vector3 a, Vector3 b, Vector3 c, Vector3 d) => new[] { a, b, c, d };
    static Mesh FromQuads(List<Vector3[]> q) => Build(q, new List<Vector3[]>());

    static Mesh Build(List<Vector3[]> quads, List<Vector3[]> tris, bool smooth = false)
    {
        var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            int i = v.Count; v.Add(a); v.Add(b); v.Add(c); t.Add(i); t.Add(i + 1); t.Add(i + 2);
            // box projection by dominant face normal (top faces get x/z, sides get the wall plane)
            Vector3 n = Vector3.Cross(b - a, c - a);
            Vector3 an = new(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
            Vector2 P(Vector3 p) => an.y >= an.x && an.y >= an.z ? new Vector2(p.x, p.z) * 0.5f
                                  : an.x >= an.z ? new Vector2(p.z, p.y) * 0.5f : new Vector2(p.x, p.y) * 0.5f;
            uv.Add(P(a)); uv.Add(P(b)); uv.Add(P(c));
        }
        foreach (var q in quads) { Tri(q[0], q[1], q[2]); Tri(q[0], q[2], q[3]); }
        foreach (var tr in tris) Tri(tr[0], tr[1], tr[2]);
        var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(v); m.SetTriangles(t, 0); m.SetUVs(0, uv);
        if (smooth) { m.Optimize(); WeldAndSmooth(m); } else m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    static void WeldAndSmooth(Mesh m)
    {
        var verts = m.vertices; var tris = m.triangles;
        var map = new Dictionary<Vector3Int, int>(); var nv = new List<Vector3>(); var remap = new int[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            var key = Vector3Int.RoundToInt(verts[i] * 10000f);
            if (!map.TryGetValue(key, out int idx)) { idx = nv.Count; nv.Add(verts[i]); map[key] = idx; }
            remap[i] = idx;
        }
        m.Clear();
        m.SetVertices(nv);
        m.SetTriangles(tris.Select(i => remap[i]).ToArray(), 0);
        m.RecalculateNormals();
    }
}
