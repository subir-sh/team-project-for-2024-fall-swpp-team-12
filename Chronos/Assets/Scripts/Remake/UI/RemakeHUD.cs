using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Compact, icon-first HUD. No labels, no metadata text: only what changes a decision.
//  - Timeline strip (top centre): every recorded turn as an arrow (rotated to match the screen) or a
//    wait dot; a caret marks "now". While rewinding, the turns your phantom will replay turn cyan and
//    the Q / E / SPACE keycaps appear.
//  - In the world: a small marker over you, and the phantom's remaining turns over the phantom.
//  - Clear / pause: one icon and one keycap.
// Reads game state only; input still flows through the original InputManager.
public class RemakeHUD : MonoBehaviour
{
    public TMP_FontAsset font;
    public Sprite pill, arrow, waitDot, caret, keycap, check, pause, restart, vignette, ring, softDot, marker;
    public Color ink = new(0.06f, 0.07f, 0.11f, 0.72f);
    public Color paper = new(0.96f, 0.95f, 0.92f, 1f);
    public Color you = new(1.0f, 0.62f, 0.38f, 1f);
    public Color past = new(0.62f, 0.86f, 1.0f, 1f);

    const int Max = 22;
    const float Icon = 26f, Gap = 7f;

    Canvas canvas;
    RectTransform strip, caretRt, phTagRt, youRt;
    Image stripBg, stripEdge, vignetteImg, youImg;
    readonly List<Image> icons = new();
    GameObject keysQ, keysE, keySpace, phTag, clearPanel, pausePanel;
    TextMeshProUGUI phTagText;
    List<string> pendingReplay = new(), phantomQueue = new();
    bool wasRewinding, cleared;

    void Awake()
    {
        Build();
        var ui = FindAnyObjectByType<UIManager>();
        if (ui != null) ui.pauseUI = pausePanel;
        LevelManager.OnLevelCleared += OnCleared;
    }

    void OnDestroy() { LevelManager.OnLevelCleared -= OnCleared; }
    void OnCleared() { cleared = true; clearPanel.SetActive(true); }

    void Update()
    {
        var pl = PlayerController.playerController; var ph = PhantomController.phantomController;
        if (pl == null || pl.listCommandLog == null) return;
        if (cleared && Input.GetKeyDown(KeyCode.Return)) { cleared = false; clearPanel.SetActive(false); }

        var log = pl.listCommandLog;
        int total = log.Count - 1;
        int now = Mathf.Clamp(pl.commandIterator.GetCurrentIndex(), 0, total);
        bool rw = pl.isTimeRewinding;
        if (rw) { pendingReplay.Clear(); for (int t = now + 1; t <= total; t++) pendingReplay.Add(log[t]); }
        if (!rw && wasRewinding) phantomQueue = ph != null && ph.isPhantomExisting ? new List<string>(pendingReplay) : new List<string>();
        wasRewinding = rw;

        // window: keep "now" visible, prefer showing the end of the log
        int start = Mathf.Max(1, total - Max + 1);
        if (now < start + 2) start = Mathf.Max(1, now - 2);
        int shown = Mathf.Clamp(total - start + 1, 0, Max);
        float width = Mathf.Max(1, shown) * (Icon + Gap) + 30f + (rw ? 96f : 0f);
        strip.sizeDelta = new Vector2(Mathf.Lerp(strip.sizeDelta.x, width, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f)), strip.sizeDelta.y);
        float x0 = -(shown - 1) * (Icon + Gap) * 0.5f;
        for (int k = 0; k < Max; k++)
        {
            int t = start + k;
            bool on = k < shown && t <= total;
            icons[k].gameObject.SetActive(on);
            if (!on) continue;
            SetIcon(icons[k], log[t]);
            var rt = icons[k].rectTransform;
            rt.anchoredPosition = new Vector2(x0 + k * (Icon + Gap), 0);
            bool future = t > now;
            float fade = (k == 0 && start > 1) ? 0.35f : (k == 1 && start > 1 ? 0.7f : 1f);
            Color c = future ? (rw ? past : new Color(1, 1, 1, 0.25f)) : paper;
            c.a *= fade;
            icons[k].color = c;
        }
        // caret sits under the last executed turn ("now")
        float cx = x0 + (now - start) * (Icon + Gap);
        caretRt.anchoredPosition = new Vector2(now >= start ? cx : x0 - (Icon + Gap) * 0.5f, -26f);
        caretRt.gameObject.SetActive(total > 0);

        keysQ.SetActive(rw); keysE.SetActive(rw); keySpace.SetActive(rw);
        stripEdge.color = new Color(past.r, past.g, past.b, Mathf.MoveTowards(stripEdge.color.a, rw ? 0.9f : 0f, Time.unscaledDeltaTime * 5f));
        vignetteImg.color = new Color(past.r * 0.5f, past.g * 0.7f, past.b, Mathf.MoveTowards(vignetteImg.color.a, rw ? 0.5f : 0f, Time.unscaledDeltaTime * 3f));

        // world tags
        var cam = Camera.main;
        bool phAlive = ph != null && ph.isPhantomExisting && phantomQueue.Count > 0;
        int remaining = phAlive ? Mathf.Max(0, phantomQueue.Count - Mathf.Clamp(ph.commandIterator.GetCurrentIndex(), 0, phantomQueue.Count)) : 0;
        phTag.SetActive(phAlive && cam);
        if (phAlive && cam) { Anchor(phTagRt, cam, ph.transform.position + Vector3.up * 0.95f); phTagText.text = remaining.ToString(); }
        youRt.gameObject.SetActive(cam);
        if (cam) Anchor(youRt, cam, pl.transform.position + Vector3.up * (0.78f + 0.05f * Mathf.Sin(Time.time * 3f)));
        youImg.color = rw ? new Color(you.r, you.g, you.b, 0.5f) : you;
    }

    public void PlaceWorldTags() { Update(); }

    void Anchor(RectTransform rt, Camera cam, Vector3 world)
    {
        Vector3 vp = cam.WorldToViewportPoint(world);
        Vector2 size = ((RectTransform)canvas.transform).rect.size;
        rt.anchoredPosition = new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y);
        if (vp.z <= 0) rt.gameObject.SetActive(false);
    }

    void SetIcon(Image img, string cmd)
    {
        Vector3 d = cmd switch { "w" => Vector3.forward, "s" => Vector3.back, "a" => Vector3.left, "d" => Vector3.right, _ => Vector3.zero };
        if (d == Vector3.zero) { img.sprite = waitDot; img.rectTransform.localRotation = Quaternion.identity; return; }
        img.sprite = arrow;
        var cam = Camera.main; float ang = 0;
        if (cam)
        {
            Vector3 c = PlayerController.playerController.transform.position;
            Vector3 a = cam.WorldToScreenPoint(c), b = cam.WorldToScreenPoint(c + d);
            ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
        }
        img.rectTransform.localRotation = Quaternion.Euler(0, 0, ang);
    }

    // ------------------------------------------------------------------ construction
    void Build()
    {
        var go = new GameObject("HUD", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
        var sc = go.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)go.transform;

        vignetteImg = Img(root, "Vignette", vignette, new Color(0, 0, 0, 0)); Stretch(vignetteImg.rectTransform);

        stripBg = Img(root, "Strip", pill, ink); stripBg.type = Image.Type.Sliced;
        strip = stripBg.rectTransform; Place(strip, new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(120, 46));
        stripEdge = Img(strip, "Edge", pill, new Color(0, 0, 0, 0)); stripEdge.type = Image.Type.Sliced; stripEdge.fillCenter = false;
        Stretch(stripEdge.rectTransform, -2);
        for (int k = 0; k < Max; k++)
        {
            var ic = Img(strip, "I" + k, arrow, paper);
            Place(ic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Icon, Icon));
            icons.Add(ic);
        }
        var car = Img(strip, "Now", caret, you); caretRt = car.rectTransform;
        Place(caretRt, new Vector2(0.5f, 0.5f), new Vector2(0, -26), new Vector2(14, 9));
        keysQ = Key(strip, "Q", new Vector2(0, 0.5f), new Vector2(22, 0));
        keysE = Key(strip, "E", new Vector2(1, 0.5f), new Vector2(-22, 0));
        keySpace = Key(strip, "SPACE", new Vector2(0.5f, 0), new Vector2(0, -34), 92);

        phTag = Img(root, "PhantomTag", softDot, new Color(0.08f, 0.14f, 0.2f, 0.88f)).gameObject;
        phTagRt = (RectTransform)phTag.transform; Place(phTagRt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34));
        var rg = Img(phTagRt, "Ring", ring, past); Stretch(rg.rectTransform);
        phTagText = Txt(phTagRt, "", 19, past); Stretch(phTagText.rectTransform);

        youImg = Img(root, "You", marker, you); youRt = youImg.rectTransform;
        Place(youRt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 13));

        clearPanel = Panel(root, "Clear", check, "Enter", restart); clearPanel.SetActive(false);
        pausePanel = Panel(root, "Pause", pause, "Esc", null); pausePanel.SetActive(false);
    }

    GameObject Panel(RectTransform root, string name, Sprite icon, string key, Sprite keyIcon)
    {
        var dim = Img(root, name, null, new Color(0.02f, 0.03f, 0.06f, 0.5f)); Stretch(dim.rectTransform);
        var ic = Img(dim.rectTransform, "Icon", icon, paper);
        Place(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(120, 120));
        var k = Key(dim.rectTransform, key, new Vector2(0.5f, 0.5f), new Vector2(keyIcon ? -22 : 0, -78), 84);
        if (keyIcon)
        {
            var ki = Img(dim.rectTransform, "KeyIcon", keyIcon, paper);
            Place(ki.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(46, -78), new Vector2(26, 26));
        }
        return dim.gameObject;
    }

    GameObject Key(RectTransform parent, string label, Vector2 anchor, Vector2 pos, float w = 30)
    {
        var k = Img(parent, "Key" + label, keycap, new Color(0.95f, 0.94f, 0.9f, 0.95f)); k.type = Image.Type.Sliced;
        Place(k.rectTransform, anchor, pos, new Vector2(w, 28));
        var t = Txt(k.rectTransform, label, 15, new Color(0.08f, 0.09f, 0.12f)); t.fontStyle = FontStyles.Bold; Stretch(t.rectTransform);
        return k.gameObject;
    }

    static Image Img(Transform parent, string name, Sprite s, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var i = go.AddComponent<Image>(); i.sprite = s; i.color = c; i.raycastTarget = false; return i;
    }

    TextMeshProUGUI Txt(Transform parent, string s, float size, Color c)
    {
        var go = new GameObject("T", typeof(RectTransform)); go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>(); if (font) t.font = font;
        t.text = s; t.fontSize = size; t.color = c; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap; return t;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    { rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = size; }

    static void Stretch(RectTransform rt, float inset = 0)
    { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset); }
}
