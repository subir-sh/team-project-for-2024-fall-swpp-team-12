using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Remake HUD. Built at runtime from serialized art (font + sprites) so the layout lives in
// one readable place. It only *reads* game state (PlayerController / PhantomController /
// TurnManager); input still flows through the original InputManager.
//
//  ┌────────────── TIMELINE: recorded turns (arrows match the camera view) ─────────────┐
//  │  [▲][▲][▲][▶]|[▶][▲][▲][▲][●][●]   cursor = "now" (amber) · replay range (cyan)   │
//  │  PHANTOM  [▶][▶][▲][▲][▲][●]  · 3 turns left                                        │
//  └─────────────────────────────────────────────────────────────────────────────────────┘
//  TURN 12  ● RECORDING                                       key legend (bottom right)
public class RemakeHUD : MonoBehaviour
{
    [Header("Art")]
    public TMP_FontAsset font;
    public Sprite chip, chipOutline, arrow, waitDot, rewindIcon, keycap, vignette, pill, softDot, marker;

    [Header("Palette")]
    public Color ink = new(0.07f, 0.08f, 0.13f, 0.86f);
    public Color paper = new(0.95f, 0.93f, 0.88f, 1f);
    public Color amber = new(1.00f, 0.66f, 0.26f, 1f);   // the player (present)
    public Color cyan = new(0.36f, 0.90f, 1.00f, 1f);    // the phantom (past)
    public Color muted = new(0.60f, 0.62f, 0.70f, 1f);

    const int MaxChips = 16;
    const float ChipSize = 54f, ChipGap = 8f;

    Canvas canvas;
    RectTransform timeline, chipRow, phantomRow, cursor, replayBracket;
    readonly List<Image> chipBg = new(), chipIcon = new(), phChipBg = new(), phChipIcon = new();
    readonly List<TextMeshProUGUI> chipNum = new();
    TextMeshProUGUI turnBig, modeText, replayText, phantomLabel, levelLabel, bannerText, clearStats, phantomTag;
    Image modeDot, vignetteImg, bannerBg, playerMarker;
    GameObject banner, clearPanel, pausePanel, phantomRowGo, phantomTagGo;
    RectTransform phantomTagRt, playerMarkerRt;

    List<string> pendingReplay = new();
    List<string> phantomQueue = new();
    bool wasRewinding, wasClock;
    int actionsTaken, rewindsUsed;
    bool cleared;

    // ------------------------------------------------------------------ lifecycle
    void Awake()
    {
        Build();
        var ui = FindAnyObjectByType<UIManager>();
        if (ui != null) ui.pauseUI = pausePanel;  // reuse the original pause toggle, new visuals
        LevelManager.OnLevelCleared += OnCleared;
    }

    void OnDestroy() { LevelManager.OnLevelCleared -= OnCleared; }

    void OnCleared()
    {
        cleared = true;
        clearPanel.SetActive(true);
        clearStats.text = $"행동 {actionsTaken}턴  ·  되감기 {rewindsUsed}회";
    }

    void Update()
    {
        var player = PlayerController.playerController;
        var ph = PhantomController.phantomController;
        var tm = TurnManager.turnManager;
        if (player == null || tm == null || player.listCommandLog == null) return;

        if (Input.GetKeyDown(KeyCode.Return) && cleared) { cleared = false; clearPanel.SetActive(false); actionsTaken = 0; rewindsUsed = 0; }
        if (tm.CLOCK && !wasClock && !player.isTimeRewinding) actionsTaken++;
        wasClock = tm.CLOCK;

        var log = player.listCommandLog;
        int total = log.Count - 1;
        int cursorIdx = Mathf.Clamp(player.commandIterator.GetCurrentIndex(), 0, total);
        bool rewinding = player.isTimeRewinding;

        if (rewinding && !wasRewinding) rewindsUsed++;
        if (rewinding)
        {
            pendingReplay.Clear();
            for (int t = cursorIdx + 1; t <= total; t++) pendingReplay.Add(log[t]);
        }
        if (!rewinding && wasRewinding) phantomQueue = ph != null && ph.isPhantomExisting ? new List<string>(pendingReplay) : new List<string>();
        if (ph == null || !ph.isPhantomExisting) { if (!rewinding && phantomQueue.Count > 0 && ph != null && !ph.gameObject.activeSelf) phantomQueue.Clear(); }
        wasRewinding = rewinding;

        // ---- main timeline window
        int start = Mathf.Max(1, total - MaxChips + 1);
        if (cursorIdx < start + 1) start = Mathf.Max(1, cursorIdx - 2);
        for (int k = 0; k < MaxChips; k++)
        {
            int t = start + k;
            bool exists = t <= total;
            chipBg[k].gameObject.SetActive(exists);
            if (!exists) continue;
            string cmd = log[t];
            SetIcon(chipIcon[k], cmd);
            bool future = t > cursorIdx;
            Color c = future ? (rewinding ? cyan : muted) : amber;
            chipBg[k].color = future ? new Color(c.r * 0.22f, c.g * 0.22f, c.b * 0.22f, 0.92f) : new Color(0.16f, 0.13f, 0.1f, 0.92f);
            chipIcon[k].color = future ? c : paper;
            chipNum[k].text = t.ToString();
            chipNum[k].color = t == cursorIdx ? amber : new Color(muted.r, muted.g, muted.b, 0.8f);
        }
        // cursor sits *after* the chip of the current turn ("now" is between turns)
        float x = ChipX(Mathf.Clamp(cursorIdx - start, -1, MaxChips - 1)) + ChipSize * 0.5f + ChipGap * 0.5f;
        cursor.anchoredPosition = new Vector2(x, cursor.anchoredPosition.y);

        int futureCount = total - cursorIdx;
        replayBracket.gameObject.SetActive(rewinding && futureCount > 0);
        if (rewinding && futureCount > 0)
        {
            int a = Mathf.Max(cursorIdx + 1, start) - start, b = Mathf.Min(total, start + MaxChips - 1) - start;
            float x0 = ChipX(a) - ChipSize * 0.5f, x1 = ChipX(b) + ChipSize * 0.5f;
            replayBracket.anchoredPosition = new Vector2((x0 + x1) * 0.5f, replayBracket.anchoredPosition.y);
            replayBracket.sizeDelta = new Vector2(x1 - x0, replayBracket.sizeDelta.y);
            replayText.text = $"분신이 재생할 행동  {futureCount}턴";
        }

        // ---- phantom queue row
        bool phAlive = ph != null && ph.isPhantomExisting && phantomQueue.Count > 0;
        phantomRowGo.SetActive(phAlive);
        int remaining = 0;
        if (phAlive)
        {
            int done = Mathf.Clamp(ph.commandIterator.GetCurrentIndex(), 0, phantomQueue.Count);
            remaining = phantomQueue.Count - done;
            for (int k = 0; k < MaxChips; k++)
            {
                bool exists = k < phantomQueue.Count;
                phChipBg[k].gameObject.SetActive(exists);
                if (!exists) continue;
                SetIcon(phChipIcon[k], phantomQueue[k]);
                bool consumed = k < done, next = k == done;
                phChipBg[k].color = consumed ? new Color(0.1f, 0.14f, 0.18f, 0.55f) : new Color(0.06f, 0.2f, 0.26f, next ? 1f : 0.85f);
                phChipIcon[k].color = consumed ? new Color(cyan.r, cyan.g, cyan.b, 0.3f) : cyan;
                phChipBg[k].transform.localScale = Vector3.one * (next ? 1.08f : 1f);
            }
            phantomLabel.text = remaining > 0
                ? $"분신  <color=#{ColorUtility.ToHtmlStringRGB(cyan)}>{remaining}</color>턴 남음"
                : "분신  <color=#9AA0B4>다음 턴 소멸</color>";
        }

        // ---- bottom-left status
        turnBig.text = cursorIdx.ToString("00");
        if (rewinding) { modeText.text = $"되감는 중  ·  {tm.rewindTurnCount}턴 전"; modeDot.color = cyan; }
        else if (phAlive) { modeText.text = "기록 중  ·  분신 재생 중"; modeDot.color = Color.Lerp(amber, cyan, 0.5f + 0.5f * Mathf.Sin(Time.time * 4f)); }
        else { modeText.text = "기록 중"; modeDot.color = new Color(1f, 0.35f, 0.25f, 0.6f + 0.4f * Mathf.Sin(Time.time * 4f)); }

        banner.SetActive(rewinding);
        vignetteImg.color = new Color(cyan.r, cyan.g, cyan.b, Mathf.MoveTowards(vignetteImg.color.a, rewinding ? 0.55f : 0f, Time.unscaledDeltaTime * 2.5f));

        // ---- world-anchored tags
        showPhantomTag = phAlive;
        if (phAlive) { phantomTagWorld = ph.transform.position + Vector3.up * 1.9f; phantomTag.text = remaining.ToString(); }
        playerMarkerWorld = player.transform.position + Vector3.up * 1.75f + Vector3.up * 0.08f * Mathf.Sin(Time.time * 3f);
        PlaceWorldTags();
    }

    bool showPhantomTag;
    Vector3 phantomTagWorld, playerMarkerWorld;

    // Public so the capture harness can re-place tags after switching canvas render mode.
    public void PlaceWorldTags()
    {
        var cam = Camera.main;
        phantomTagGo.SetActive(showPhantomTag && cam != null);
        playerMarker.gameObject.SetActive(cam != null);
        if (cam == null) return;
        if (showPhantomTag) Anchor(phantomTagRt, cam, phantomTagWorld);
        Anchor(playerMarkerRt, cam, playerMarkerWorld);
    }

    void Anchor(RectTransform rt, Camera cam, Vector3 world)
    {
        // Viewport-based so it is correct for any output resolution / aspect.
        Vector3 vp = cam.WorldToViewportPoint(world);
        Vector2 size = ((RectTransform)canvas.transform).rect.size;
        rt.anchoredPosition = new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y);
        if (vp.z <= 0) rt.gameObject.SetActive(false);
    }

    // Arrows are rotated to match the on-screen direction of each world move.
    void SetIcon(Image img, string cmd)
    {
        Vector3 dir = cmd switch { "w" => Vector3.forward, "s" => Vector3.back, "a" => Vector3.left, "d" => Vector3.right, _ => Vector3.zero };
        if (dir == Vector3.zero) { img.sprite = waitDot; img.rectTransform.localRotation = Quaternion.identity; return; }
        img.sprite = arrow;
        var cam = Camera.main;
        float ang = 0f;
        if (cam != null)
        {
            Vector3 c = PlayerController.playerController.transform.position;
            Vector3 a = cam.WorldToScreenPoint(c), b = cam.WorldToScreenPoint(c + dir);
            ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
        }
        img.rectTransform.localRotation = Quaternion.Euler(0, 0, ang);
    }

    float ChipX(int k) => -((MaxChips - 1) * (ChipSize + ChipGap)) * 0.5f + k * (ChipSize + ChipGap);

    // ------------------------------------------------------------------ construction
    void Build()
    {
        var go = new GameObject("RemakeHUDCanvas", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)go.transform;

        // rewind vignette (under everything)
        vignetteImg = Img(root, "RewindVignette", vignette, new Color(cyan.r, cyan.g, cyan.b, 0f));
        Stretch(vignetteImg.rectTransform);
        vignetteImg.raycastTarget = false;

        // ---- top: level label + timeline card
        levelLabel = Text(root, "LevelLabel", "1-3  <color=#9AA0B4>·</color>  분신에게 발판을 맡겨라", 26, paper, TextAlignmentOptions.TopLeft);
        Place(levelLabel.rectTransform, new Vector2(0, 1), new Vector2(40, -30), new Vector2(900, 40));

        var card = Img(root, "TimelineCard", chip, ink);
        card.type = Image.Type.Sliced;
        timeline = card.rectTransform;
        Place(timeline, new Vector2(0.5f, 1), new Vector2(0, -76), new Vector2((ChipSize + ChipGap) * MaxChips + 150, 132));
        var tlLabel = Text(timeline, "Label", "TIMELINE", 16, muted, TextAlignmentOptions.Left);
        tlLabel.characterSpacing = 8;
        Place(tlLabel.rectTransform, new Vector2(0, 1), new Vector2(22, -12), new Vector2(200, 22), new Vector2(0, 1));

        chipRow = Empty(timeline, "Chips");
        Place(chipRow, new Vector2(0.5f, 1), new Vector2(40, -66), new Vector2(10, ChipSize));
        for (int k = 0; k < MaxChips; k++)
        {
            var bg = Img(chipRow, $"Chip{k}", chip, Color.gray); bg.type = Image.Type.Sliced;
            Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(ChipX(k), 0), new Vector2(ChipSize, ChipSize));
            var ic = Img(bg.rectTransform, "Icon", arrow, paper);
            Place(ic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            var num = Text(bg.rectTransform, "Num", "", 14, muted, TextAlignmentOptions.Center);
            Place(num.rectTransform, new Vector2(0.5f, 0), new Vector2(0, -14), new Vector2(40, 18));
            chipBg.Add(bg); chipIcon.Add(ic); chipNum.Add(num);
        }
        var cur = Img(chipRow, "NowCursor", pill, amber);
        cursor = cur.rectTransform;
        Place(cursor, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(5, ChipSize + 22));
        var nowLbl = Text(cursor, "Now", "NOW", 13, amber, TextAlignmentOptions.Center);
        nowLbl.fontStyle = FontStyles.Bold;
        Place(nowLbl.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 14), new Vector2(60, 18));

        var br = Img(chipRow, "ReplayBracket", pill, new Color(cyan.r, cyan.g, cyan.b, 0.9f)); br.type = Image.Type.Sliced;
        replayBracket = br.rectTransform;
        Place(replayBracket, new Vector2(0.5f, 0.5f), new Vector2(0, -ChipSize * 0.5f - 34), new Vector2(100, 6));
        replayText = Text(replayBracket, "Txt", "", 17, cyan, TextAlignmentOptions.Center);
        Place(replayText.rectTransform, new Vector2(0.5f, 0), new Vector2(0, -14), new Vector2(420, 24));

        // phantom queue row (appears under the timeline card while a phantom lives)
        var phCard = Img(root, "PhantomCard", chip, new Color(0.03f, 0.12f, 0.16f, 0.88f)); phCard.type = Image.Type.Sliced;
        phantomRowGo = phCard.gameObject;
        Place(phCard.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -218), new Vector2((ChipSize + ChipGap) * MaxChips + 150, 64));
        phantomLabel = Text(phCard.rectTransform, "Label", "", 18, paper, TextAlignmentOptions.Left);
        Place(phantomLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(200, 30), new Vector2(0, 0.5f));
        phantomRow = Empty(phCard.rectTransform, "Chips");
        Place(phantomRow, new Vector2(0, 0.5f), new Vector2(200, 0), new Vector2(10, 40), new Vector2(0, 0.5f));
        for (int k = 0; k < MaxChips; k++)
        {
            var bg = Img(phantomRow, $"P{k}", chip, Color.gray); bg.type = Image.Type.Sliced;
            Place(bg.rectTransform, new Vector2(0, 0.5f), new Vector2(k * 46, 0), new Vector2(40, 40), new Vector2(0, 0.5f));
            var ic = Img(bg.rectTransform, "Icon", arrow, cyan);
            Place(ic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22));
            phChipBg.Add(bg); phChipIcon.Add(ic);
        }

        // ---- rewind banner
        bannerBg = Img(root, "RewindBanner", pill, new Color(0.02f, 0.1f, 0.14f, 0.9f)); bannerBg.type = Image.Type.Sliced;
        banner = bannerBg.gameObject;
        // sits where the phantom row would be: entering rewind always removes the phantom
        Place(bannerBg.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -228), new Vector2(760, 64));
        var rwIcon = Img(bannerBg.rectTransform, "Icon", rewindIcon, cyan);
        Place(rwIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(34, 34), new Vector2(0, 0.5f));
        bannerText = Text(bannerBg.rectTransform, "Txt",
            $"<b>되감기</b>   <color=#9AA0B4>Q</color> 과거   <color=#9AA0B4>E</color> 미래   <color=#9AA0B4>SPACE</color> 이 시점에서 분신 소환", 22, paper, TextAlignmentOptions.Left);
        Place(bannerText.rectTransform, new Vector2(0, 0.5f), new Vector2(80, 0), new Vector2(680, 40), new Vector2(0, 0.5f));

        // ---- bottom-left: turn + mode
        var status = Img(root, "StatusCard", chip, ink); status.type = Image.Type.Sliced;
        Place(status.rectTransform, new Vector2(0, 0), new Vector2(40, 40), new Vector2(380, 120), new Vector2(0, 0));
        var tlbl = Text(status.rectTransform, "TurnLbl", "TURN", 16, muted, TextAlignmentOptions.TopLeft); tlbl.characterSpacing = 8;
        Place(tlbl.rectTransform, new Vector2(0, 1), new Vector2(24, -16), new Vector2(120, 22), new Vector2(0, 1));
        turnBig = Text(status.rectTransform, "Turn", "00", 64, paper, TextAlignmentOptions.BottomLeft);
        turnBig.fontStyle = FontStyles.Bold;
        Place(turnBig.rectTransform, new Vector2(0, 0), new Vector2(22, 8), new Vector2(120, 80), new Vector2(0, 0));
        modeDot = Img(status.rectTransform, "Dot", softDot, amber);
        Place(modeDot.rectTransform, new Vector2(0, 0.5f), new Vector2(150, -6), new Vector2(18, 18), new Vector2(0, 0.5f));
        modeText = Text(status.rectTransform, "Mode", "기록 중", 22, paper, TextAlignmentOptions.Left);
        Place(modeText.rectTransform, new Vector2(0, 0.5f), new Vector2(178, -6), new Vector2(200, 30), new Vector2(0, 0.5f));

        // ---- bottom-right: key legend
        var legend = Empty(root, "Legend");
        Place(legend, new Vector2(1, 0), new Vector2(-40, 40), new Vector2(420, 220), new Vector2(1, 0));
        var rows = new (string key, string label)[] { ("WASD", "이동"), ("R", "제자리 대기 (1턴)"), ("SPACE", "되감기 모드"), ("Backspace", "한 턴 무르기"), ("Enter", "처음부터") };
        for (int i = 0; i < rows.Length; i++)
        {
            float y = 8 + (rows.Length - 1 - i) * 42;
            var kc = Img(legend, "Key", keycap, new Color(0.95f, 0.93f, 0.88f, 0.95f)); kc.type = Image.Type.Sliced;
            float kw = 34 + rows[i].key.Length * 11;
            Place(kc.rectTransform, new Vector2(1, 0), new Vector2(-230, y), new Vector2(kw, 34), new Vector2(1, 0));
            var kt = Text(kc.rectTransform, "T", rows[i].key, 16, new Color(0.1f, 0.1f, 0.14f), TextAlignmentOptions.Center);
            kt.fontStyle = FontStyles.Bold; Stretch(kt.rectTransform);
            var lt = Text(legend, "L", rows[i].label, 19, new Color(paper.r, paper.g, paper.b, 0.9f), TextAlignmentOptions.Left);
            Place(lt.rectTransform, new Vector2(1, 0), new Vector2(-10, y), new Vector2(206, 34), new Vector2(1, 0));
        }

        // ---- world tags
        var pt = Img(root, "PhantomTag", softDot, new Color(0.03f, 0.16f, 0.2f, 0.95f));
        phantomTagGo = pt.gameObject; phantomTagRt = pt.rectTransform;
        Place(phantomTagRt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40));
        var ring = Img(phantomTagRt, "Ring", chipOutline, cyan); Stretch(ring.rectTransform);
        phantomTag = Text(phantomTagRt, "N", "", 22, cyan, TextAlignmentOptions.Center); phantomTag.fontStyle = FontStyles.Bold; Stretch(phantomTag.rectTransform);
        playerMarker = Img(root, "PlayerMarker", marker, amber);
        playerMarkerRt = playerMarker.rectTransform;
        Place(playerMarkerRt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 20));
        // tags are anchored to canvas centre space
        phantomTagRt.anchorMin = phantomTagRt.anchorMax = new Vector2(0.5f, 0.5f);
        playerMarkerRt.anchorMin = playerMarkerRt.anchorMax = new Vector2(0.5f, 0.5f);

        // ---- clear + pause panels
        clearPanel = Panel(root, "ClearPanel", "퍼즐 완료", out clearStats, "Enter  다시 하기");
        clearPanel.SetActive(false);
        pausePanel = Panel(root, "PausePanel", "일시정지", out _, "Esc  계속하기");
        pausePanel.SetActive(false);
    }

    GameObject Panel(RectTransform root, string name, string title, out TextMeshProUGUI stats, string hint)
    {
        var dim = Img(root, name, null, new Color(0.02f, 0.03f, 0.07f, 0.55f)); Stretch(dim.rectTransform);
        var card = Img(dim.rectTransform, "Card", chip, new Color(0.95f, 0.93f, 0.88f, 0.98f)); card.type = Image.Type.Sliced;
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 300));
        var t = Text(card.rectTransform, "Title", title, 64, new Color(0.1f, 0.1f, 0.15f), TextAlignmentOptions.Center); t.fontStyle = FontStyles.Bold;
        Place(t.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(560, 90));
        stats = Text(card.rectTransform, "Stats", "", 26, new Color(0.25f, 0.27f, 0.35f), TextAlignmentOptions.Center);
        Place(stats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(560, 40));
        var h = Text(card.rectTransform, "Hint", hint, 22, new Color(0.45f, 0.36f, 0.2f), TextAlignmentOptions.Center);
        Place(h.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(560, 34));
        return dim.gameObject;
    }

    // ------------------------------------------------------------------ ui helpers
    static RectTransform Empty(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image Img(Transform parent, string name, Sprite s, Color c)
    {
        var rt = Empty(parent, name);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = s; img.color = c; img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI Text(Transform parent, string name, string s, float size, Color c, TextAlignmentOptions align)
    {
        var rt = Empty(parent, name);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.text = s; t.fontSize = size; t.color = c; t.alignment = align;
        t.raycastTarget = false; t.richText = true; t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
