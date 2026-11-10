using System.Collections.Generic;
using UnityEngine;

// The phantom leaves fading echoes of itself while it moves: an afterimage of the past self.
// Snapshots every mesh under this transform and redraws them with decreasing alpha.
public class AfterimageTrail : MonoBehaviour
{
    public Material echoMaterial;           // Remake/Afterimage (instancing enabled)
    public float interval = 0.05f;
    public float life = 0.32f;
    public float minMove = 0.04f;

    struct Echo { public Mesh mesh; public int sub; public Matrix4x4 m; public float t; }
    readonly List<Echo> echoes = new();
    MeshFilter[] filters;
    Vector3 last;
    float timer;
    MaterialPropertyBlock mpb;
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    void OnEnable() { filters = GetComponentsInChildren<MeshFilter>(); last = transform.position; echoes.Clear(); }

    void LateUpdate()
    {
        if (!echoMaterial) return;
        mpb ??= new MaterialPropertyBlock();
        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer = 0f;
            if ((transform.position - last).sqrMagnitude > minMove * minMove)
                foreach (var f in filters)
                    if (f && f.sharedMesh && f.gameObject.activeInHierarchy)
                        for (int s = 0; s < f.sharedMesh.subMeshCount; s++)
                            echoes.Add(new Echo { mesh = f.sharedMesh, sub = s, m = f.transform.localToWorldMatrix, t = Time.time });
            last = transform.position;
        }
        for (int i = echoes.Count - 1; i >= 0; i--)
        {
            float age = (Time.time - echoes[i].t) / life;
            if (age >= 1f) { echoes.RemoveAt(i); continue; }
            mpb.SetFloat(AlphaId, (1f - age) * 0.45f);
            Graphics.DrawMesh(echoes[i].mesh, echoes[i].m, echoMaterial, gameObject.layer, null, echoes[i].sub, mpb, false, false);
        }
    }
}
