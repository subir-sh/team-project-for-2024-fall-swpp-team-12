"""Compose the rabbit design review from harness captures (lineup scenario).

usage: python tools/compose_review.py <lineup_capture_dir> <out_dir>
Produces:
  rabbit_options.png      one row per design: close-up | real game camera frame | game-camera crops (2x)
  hop_A.gif / hop_B.gif / hop_C.gif  the hop, cropped from real game-camera frames (2x)
  hop_compare.gif         the three hops side by side
"""
import re
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
FONT = ROOT / "Chronos/Assets/TextMesh Pro/Fonts/NotoSansKR-Medium.ttf"
NAMES = {
    "A": ("A · 황동 태엽 토끼", "원작 설정(기계 토끼 '모델 크로노스')에 가장 가까움. 등의 태엽 키가 이동할 때 돈다."),
    "B": ("B · 도자기 시계 토끼", "광택 도자기 + 금 이음새, 가슴의 시계 바늘이 돈다. 가장 귀엽고 고급스러움."),
    "C": ("C · 등불 정찰 토끼", "어두운 몸체에 주황 등불·바이저. 실루엣과 대비가 가장 강함, 등의 모래시계."),
}
BG = (22, 20, 34)
INK = (240, 236, 226)
MUTED = (160, 160, 180)


def font(size):
    return ImageFont.truetype(str(FONT), size)


def read_vp(cap: Path):
    vp = {}
    for line in (cap / "report_lineup.txt").read_text(encoding="utf-8").splitlines():
        m = re.match(r"VP (\S+) ([\d.]+) ([\d.]+)", line)
        if m:
            vp[m.group(1)] = (float(m.group(2)), float(m.group(3)))
    return vp


def crop_at(img, vx, vy, w, h, dy=0):
    W, H = img.size
    cx, cy = vx * W, (1 - vy) * H + dy
    box = (int(cx - w / 2), int(cy - h / 2), int(cx + w / 2), int(cy + h / 2))
    return img.crop(box)


def board(cap: Path, out: Path, vp):
    row_h, pad = 500, 28
    W = 2560
    sheet = Image.new("RGB", (W, 150 + 3 * (row_h + pad)), BG)
    d = ImageDraw.Draw(sheet)
    d.text((pad, 30), "Chronos 리메이크 — 주인공 토끼 시안 (실제 게임 카메라 비교)", font=font(44), fill=INK)
    d.text((pad, 92), "가운데: 실제 퍼즐 장면 (본체=다리 위, 분신=발판 위, 같은 카메라/조명/HUD)   오른쪽: 같은 프레임의 게임 카메라 픽셀을 2배 확대",
           font=font(22), fill=MUTED)
    for i, v in enumerate("ABC"):
        y = 150 + i * (row_h + pad)
        studio = Image.open(cap / f"studio_{v}.png").convert("RGB").resize((800, 450), Image.LANCZOS)
        game_full = Image.open(cap / f"lineup_{v}.png").convert("RGB")
        game = game_full.resize((800, 450), Image.LANCZOS)
        p = crop_at(game_full, *vp[f"lineup_{v}_player"], 260, 260, dy=-40).resize((420, 420), Image.LANCZOS)
        q = crop_at(game_full, *vp[f"lineup_{v}_phantom"], 260, 260, dy=-40).resize((420, 420), Image.LANCZOS)
        sheet.paste(studio, (pad, y + 44))
        sheet.paste(game, (pad + 820, y + 44))
        sheet.paste(p, (pad + 1640, y + 44))
        sheet.paste(q, (pad + 2080, y + 44))
        title, desc = NAMES[v]
        d.text((pad, y), title, font=font(30), fill=INK)
        d.text((pad + 420, y + 6), desc, font=font(22), fill=MUTED)
        d.text((pad + 1650, y + 470), "본체 (게임 카메라 2x)", font=font(20), fill=(255, 170, 70))
        d.text((pad + 2090, y + 470), "분신 (게임 카메라 2x)", font=font(20), fill=(90, 230, 255))
    sheet.save(out / "rabbit_options.png", optimize=True)


def hop_gifs(cap: Path, out: Path, vp):
    seqs = {}
    for v in "ABC":
        frames = sorted(cap.glob(f"hop_{v}_*.png"))
        sx, sy = vp[f"hop_{v}_start"]
        ex, ey = vp[f"hop_{v}_end"]
        cx, cy = (sx + ex) / 2, (sy + ey) / 2
        seq = []
        for f in frames:
            img = Image.open(f).convert("RGB")
            c = crop_at(img, cx, cy, 380, 260, dy=-30).resize((760, 520), Image.LANCZOS)
            seq.append(c)
        # hold the first/last pose so the loop reads
        seq = [seq[0]] * 8 + seq + [seq[-1]] * 10
        seqs[v] = seq
        seq[0].save(out / f"hop_{v}.gif", save_all=True, append_images=seq[1:], duration=33, loop=0, optimize=True)
    n = min(len(s) for s in seqs.values())
    combo = []
    for k in range(n):
        frame = Image.new("RGB", (3 * 520 + 4 * 16, 400), BG)
        d = ImageDraw.Draw(frame)
        for i, v in enumerate("ABC"):
            tile = seqs[v][k].resize((520, 356), Image.LANCZOS)
            frame.paste(tile, (16 + i * 536, 36))
            d.text((16 + i * 536, 4), NAMES[v][0], font=font(22), fill=INK)
        combo.append(frame)
    combo[0].save(out / "hop_compare.gif", save_all=True, append_images=combo[1:], duration=33, loop=0, optimize=True)


def final_hop(cap: Path, out: Path):
    """Hop of the chosen rabbit during the real recording phase (game camera): full frame + 2x crop."""
    vp = {}
    for line in (cap / "report_final.txt").read_text(encoding="utf-8").splitlines():
        m = re.match(r"VP (\S+) ([\d.]+) ([\d.]+)", line)
        if m:
            vp[m.group(1)] = (float(m.group(2)), float(m.group(3)))
    frames = [Image.open(f).convert("RGB") for f in sorted(cap.glob("hop_[0-9]*.png"))]
    (sx, sy), (ex, ey) = vp["hop_start"], vp["hop_end"]
    cx, cy = (sx + ex) / 2, (sy + ey) / 2
    full, crop = [], []
    for img in frames:
        full.append(img.resize((960, 540), Image.LANCZOS))
        crop.append(crop_at(img, cx, cy, 360, 250, dy=-30).resize((720, 500), Image.LANCZOS))
    hold = lambda s: [s[0]] * 8 + s + [s[-1]] * 10
    full, crop = hold(full), hold(crop)
    full[0].save(out / "hop_final_gamecam.gif", save_all=True, append_images=full[1:], duration=33, loop=0, optimize=True)
    crop[0].save(out / "hop_final_gamecam_2x.gif", save_all=True, append_images=crop[1:], duration=33, loop=0, optimize=True)


if __name__ == "__main__":
    cap, out = Path(sys.argv[1]), Path(sys.argv[2])
    out.mkdir(parents=True, exist_ok=True)
    if (cap / "report_final.txt").exists():
        final_hop(cap, out)
    else:
        vp = read_vp(cap)
        board(cap, out, vp)
        hop_gifs(cap, out, vp)
    print("wrote", *[p.name for p in sorted(out.iterdir())])
