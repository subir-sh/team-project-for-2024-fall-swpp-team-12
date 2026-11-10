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
    "A": ("A  모카", "큰 둥근 박스 머리에 바이저 얼굴, 빛나는 알약 눈. 게임 거리에서 가장 또렷함. 등에 작은 태엽 키."),
    "B": ("B  톡", "가늘고 꼿꼿한 시계토끼. 외눈 안경과 가슴의 회중시계. 가장 우아하지만 작은 실루엣."),
    "C": ("C  모찌", "둥글고 납작한 떡 토끼, 등에 작은 모래시계. 가장 귀엽고 부드러움."),
    "D": ("D  세이블", "흑연빛 안드로이드 산토끼. 옆으로 난 호박색 눈, 황동 목깃과 등뼈 장식. 밝은 타일 위에서 대비가 가장 강함."),
    "E": ("E  아이보리", "웅크린 자연스러운 토끼 자세의 도자기 오토마톤. 금빛 관절 링과 등의 태엽 키. 가장 단정하고 차분함."),
    "G": ("G  그래파이트", "흑연빛 몸, 호박색 측면 눈, 등 위로 선 큰 황동 태엽 키. 밝은 바닥에서 대비가 가장 강함."),
    "H": ("H  아이보리", "상아빛 몸, 짙은 눈, 같은 황동 태엽 키. 가장 차분하고 단정함. 어두운 바닥과 잘 어울림."),
    "I": ("I  녹청", "산화된 구리 같은 녹청빛 몸, 금빛 눈과 태엽. 시계 장치 느낌이 가장 강함."),
    "M": ("M  빈티지 태엽 장난감", "큰 둥근 양철 머리, 큰 렌즈 눈과 볼 칠, 짧은 다리, 리벳과 이음 띠, 등의 태엽 키."),
    "N": ("N  봉제 + 기계", "부드러운 펠트 몸에 황동 관절 캡, 귀 경첩과 귀 끝 금속, 단추 눈, 등의 태엽 키."),
    "O": ("O  둥근 토끼 + 부품", "매끈한 토끼 실루엣에 이음 띠, 리벳, 옆면 렌즈 눈, 큰 태엽 키."),
    "J": ("J  양철 적색", "칠이 바랜 붉은 양철 몸, 크림색 판, 황동 장식. 고전 태엽 장난감 그대로의 기계 토끼."),
    "K": ("K  건메탈", "회청색 금속 몸, 검은 판, 황동 장식. 가장 기계적이고 무게감 있음."),
    "L": ("L  구리와 녹청", "구리 몸에 녹청 패널. 오래된 시설에서 녹슬며 버텨 온 느낌."),
    "F": ("F  드리프트", "흑연빛 몸에 길게 늘어진 산호색 스카프의 시간 여행자. 게임 거리에서도 실루엣과 이야기가 읽힘."),
}
LETTERS = "ABC"
BG = (22, 20, 34)
INK = (240, 236, 226)
MUTED = (160, 160, 180)


def font(size):
    return ImageFont.truetype(str(FONT), size)


def read_vp(cap: Path):
    vp = {}
    rep = next(cap.glob("report_*.txt"))
    for line in rep.read_text(encoding="utf-8").splitlines():
        m = re.match(r"VP(16)? (\S+) ([\d.]+) ([\d.]+)", line)
        if m:
            x, y = float(m.group(3)), float(m.group(4))
            if not m.group(1):  # legacy: logged with the batch-mode 4:3 screen aspect, captures are 16:9
                x = 0.5 + (x - 0.5) * 0.75
            vp[m.group(2)] = (x, y)
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
    d.text((pad, 30), "Chronos 리메이크 주인공 토끼 시안", font=font(44), fill=INK)
    d.text((pad, 92), "모델 크로노스: 불완전한 태엽 기계 토끼 인형. 왼쪽 근접, 가운데 실제 게임 화면, 오른쪽 같은 프레임 2배. 분신은 과거에서 온 아지랑이",
           font=font(22), fill=MUTED)
    for i, v in enumerate(LETTERS):
        y = 150 + i * (row_h + pad)
        studio = Image.open(cap / f"studio_{v}.png").convert("RGB").resize((800, 450), Image.LANCZOS)
        game_full = Image.open(cap / f"lineup_{v}.png").convert("RGB")
        game = game_full.resize((800, 450), Image.LANCZOS)
        p = crop_at(game_full, *vp[f"lineup_{v}_player"], 230, 230, dy=-30).resize((420, 420), Image.LANCZOS)
        q = crop_at(game_full, *vp[f"lineup_{v}_phantom"], 230, 230, dy=-30).resize((420, 420), Image.LANCZOS)
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
    for v in LETTERS:
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
        for i, v in enumerate(LETTERS):
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
    if (cap / "report_final.txt").exists() and not (cap / "lineup_A.png").exists():
        final_hop(cap, out)
    else:
        LETTERS = "".join(sorted(p.stem[-1] for p in cap.glob("lineup_?.png")))
        vp = read_vp(cap)
        board(cap, out, vp)
        hop_gifs(cap, out, vp)
    print("wrote", *[p.name for p in sorted(out.iterdir())])
