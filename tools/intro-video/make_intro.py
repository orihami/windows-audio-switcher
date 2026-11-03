#!/usr/bin/env python3
"""
Windows Audio Switcher の紹介動画(1920x1080・30fps・約40秒・音なし)を作る。
選択UIと設定画面は、アプリ本物の描画(GitHub Actions の --render-preview で撮った PNG)を使う。

  python3 make_intro.py <preview の PNG があるフォルダ> <アイコン app.ico> <出力.mp4>

必要: Pillow, ffmpeg, Noto Sans CJK
"""
import math
import subprocess
import sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

PV, ICON, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
W, H, FPS = 1920, 1080, 30
BOLD = "/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc"
REG = "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc"
CYAN = (80, 220, 230)
PURPLE = (185, 120, 255)
WHITE = (255, 255, 255)
SUB = (175, 180, 200)

_fonts = {}


def font(size, bold=True):
    key = (size, bold)
    if key not in _fonts:
        _fonts[key] = ImageFont.truetype(BOLD if bold else REG, size, index=0)
    return _fonts[key]


def ease(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def fade(t, start, dur=0.4):
    return ease((t - start) / dur)


def load(name):
    return Image.open(f"{PV}/{name}").convert("RGBA")


# ---- 素材 ----
icon = Image.open(ICON)
icon.size = (256, 256)
icon = icon.convert("RGBA")
overlay = [load(f"video-{i}-dark.png") for i in range(7)]
settings = {k: load(f"settings-{k}-dark.png") for k in ("devices", "keys", "display")}


def gradient(c1, c2):
    g = Image.new("RGB", (1, 256))
    for y in range(256):
        k = y / 255
        g.putpixel((0, y), tuple(int(c1[i] + (c2[i] - c1[i]) * k) for i in range(3)))
    return g.resize((W, H))


BG_DARK = gradient((16, 17, 28), (28, 26, 46))


def wallpaper():
    """Windows 11 の壁紙のような、やわらかい光の形。"""
    im = gradient((14, 26, 48), (30, 22, 58)).convert("RGBA")
    glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(glow)
    d.ellipse([W * 0.30, H * 0.10, W * 0.78, H * 0.95], fill=(70, 120, 255, 120))
    d.ellipse([W * 0.45, H * 0.25, W * 0.95, H * 1.05], fill=(150, 90, 255, 110))
    d.ellipse([W * 0.20, H * 0.35, W * 0.55, H * 0.90], fill=(60, 200, 230, 70))
    glow = glow.filter(ImageFilter.GaussianBlur(140))
    im.alpha_composite(glow)
    return im


WALL = wallpaper()
TASKBAR_H = 72


def taskbar(im):
    bar = Image.new("RGBA", (W, TASKBAR_H), (28, 28, 34, 235))
    d = ImageDraw.Draw(bar)
    d.line([(0, 0), (W, 0)], fill=(60, 60, 70, 255))
    # 中央のアプリのアイコン(四角)
    cx = W // 2
    for i, col in enumerate([(0, 120, 212), (60, 60, 70), (230, 160, 30), (40, 170, 90), (200, 60, 80), (90, 90, 110)]):
        x = cx - 3 * 56 + i * 56
        d.rounded_rectangle([x + 8, 16, x + 48, 56], radius=8, fill=col + (255,))
    # 通知領域と時計
    f = font(18, False)
    d.text((W - 150, 12), "22:05", font=f, fill=WHITE)
    d.text((W - 172, 38), "2026/11/03", font=f, fill=WHITE)
    for i in range(3):
        x = W - 300 + i * 34
        d.rounded_rectangle([x, 26, x + 20, 46], radius=4, outline=(200, 200, 210, 255), width=2)
    im.alpha_composite(bar, (0, H - TASKBAR_H))


def text_at(im, x, y, s, size, color, a=1.0, bold=True, center=False):
    """文字を透明度つきで描く(色を暗くするのではなく、背景に溶かす)。"""
    if a <= 0:
        return
    f = font(size, bold)
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    if center:
        x = (W - d.textlength(s, font=f)) / 2
    d.text((x, y), s, font=f, fill=tuple(color) + (int(255 * min(1.0, a)),))
    im.alpha_composite(layer)


def text_center(im, y, s, size, color, a=1.0, bold=True):
    text_at(im, 0, y, s, size, color, a, bold, center=True)


def with_alpha(img, a):
    if a >= 1:
        return img
    r = img.copy()
    r.putalpha(r.getchannel("A").point(lambda v: int(v * a)))
    return r


def keycap(im, x, y, label, pressed, a=1.0):
    """キーの絵。押しているときは沈んで色が付く。"""
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    f = font(44)
    w = int(d.textlength(label, font=f)) + 70
    off = 8 if pressed else 0
    if not pressed:
        d.rounded_rectangle([x, y + 10, x + w, y + 100], radius=16, fill=(10, 10, 16, 255))
    face = (50, 120, 200) if pressed else (58, 60, 78)
    d.rounded_rectangle([x, y + off, x + w, y + 90 + off], radius=16, fill=face + (255,), outline=(CYAN if pressed else (90, 92, 110)) + (255,), width=3)
    d.text((x + 35, y + 14 + off), label, font=f, fill=WHITE)
    im.alpha_composite(with_alpha(layer, a))
    return w


def mouse(im, x, y, wheel_dir, t, a=1.0):
    """マウスとホイールの矢印。wheel_dir: +1 下へ / -1 上へ / 0 止まっている。"""
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.rounded_rectangle([x, y, x + 120, y + 190], radius=60, fill=(58, 60, 78, 255), outline=(110, 112, 130, 255), width=3)
    d.line([(x + 60, y), (x + 60, y + 80)], fill=(110, 112, 130, 255), width=3)
    glow = PURPLE if wheel_dir else (140, 140, 160)
    d.rounded_rectangle([x + 50, y + 26, x + 70, y + 66], radius=10, fill=glow + (255,))
    if wheel_dir:
        # 矢印が流れる
        ph = (t * 2.2) % 1.0
        for k in range(2):
            p = (ph + k * 0.5) % 1.0
            yy = y + 46 + wheel_dir * (20 + 60 * p)
            aa = int(255 * (1 - p))
            tip = yy + wheel_dir * 16
            d.polygon([(x + 150, yy), (x + 182, yy), (x + 166, tip)], fill=PURPLE + (aa,))
    im.alpha_composite(with_alpha(layer, a))


def callout(im, x, y, s, a):
    if a <= 0:
        return
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    f = font(30)
    w = d.textlength(s, font=f)
    d.rounded_rectangle([x, y, x + w + 40, y + 58], radius=29, fill=(185, 120, 255, 235))
    d.text((x + 20, y + 8), s, font=f, fill=WHITE)
    im.alpha_composite(with_alpha(layer, a))


def caption(im, step, title, sub, a):
    """左上の説明。"""
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.rounded_rectangle([90, 110, 160, 180], radius=35, fill=CYAN + (255,))
    d.text((112, 113), step, font=font(46), fill=(16, 17, 28))
    d.text((190, 108), title, font=font(60), fill=WHITE)
    if sub:
        d.text((192, 196), sub, font=font(32, False), fill=SUB)
    im.alpha_composite(with_alpha(layer, a))


# ---- 場面 ----

def scene_title(t):
    im = BG_DARK.convert("RGBA")
    a = fade(t, 0.2, 0.8)
    ic = icon.resize((300, 300))
    y = int(200 + 30 * (1 - a))
    im.alpha_composite(with_alpha(ic, a), ((W - 300) // 2, y))
    text_center(im, 560, "Windows Audio Switcher", 96, WHITE, fade(t, 0.9, 0.7))
    text_center(im, 710, "キーとホイールで、音の出力先をすばやく切り替え", 48, CYAN, fade(t, 1.6, 0.7), bold=False)
    return im


def scene_problem(t):
    im = BG_DARK.convert("RGBA")
    text_center(im, 250, "モニター・イヤホン・スピーカー…", 64, WHITE, fade(t, 0.1))
    path = "設定  →  システム  →  サウンド  →  出力  →  選ぶ"
    text_center(im, 420, path, 50, SUB, fade(t, 0.9), bold=False)
    d = ImageDraw.Draw(im)
    if t > 1.8:
        # 打ち消し線
        k = ease((t - 1.8) / 0.5)
        f = font(50, False)
        w = d.textlength(path, font=f)
        x0 = (W - w) / 2
        d.line([(x0 - 10, 455), (x0 - 10 + (w + 20) * k, 455)], fill=(255, 90, 110), width=8)
    text_center(im, 620, "切り替えのたびに、これはめんどう。", 64, PURPLE, fade(t, 2.5))
    return im


# 実演の時間割(秒、場面の中の時間)
DEMO = [
    (0.0, None),  # 何も出ていない
    (1.2, 0),  # Ctrl+Alt を押すと出る
    (3.0, 1),  # ホイールで A
    (4.2, 2),  # C(B は未接続なので飛ばす)
    (5.8, 3),  # 有線
    (7.0, 4),  # 循環してモニター
    (8.6, 5),  # 上へもどして C
    (10.2, 6),  # 離すと切り替え
]
DEMO_LEN = 14.0


def demo_state(t):
    cur = None
    for at, idx in DEMO:
        if t >= at:
            cur = (at, idx)
    return cur


def scene_demo(t):
    im = WALL.copy()
    taskbar(im)
    at, idx = demo_state(t)
    # 選択UI(右下、時計の少し上)
    if idx is not None:
        ov = overlay[idx]
        a = 1.0
        if idx == 0:
            a = fade(t, at, 0.15)
        if idx == 6:
            a = 1 - fade(t, at + 2.6, 0.4)
        x = W - ov.width - 6
        y = H - TASKBAR_H - ov.height - 2
        if idx != 6 and t - at < 0.12:
            # 選び直したとき、少しだけ浮く
            y += int(4 * (1 - (t - at) / 0.12))
        im.alpha_composite(with_alpha(ov, a), (x, y))
        # 吹き出し
        if idx == 2:
            callout(im, W - ov.width - 460, y + 220, "未接続のBは自動でスキップ", fade(t, at, 0.2) * (1 - fade(t, at + 1.4, 0.2)))
        if idx == 4:
            callout(im, W - ov.width - 420, y + 60, "最後の次は最初へ(循環)", fade(t, at, 0.2) * (1 - fade(t, at + 1.4, 0.2)))
    # 左の説明・キー・マウス
    pressed = 1.0 <= t < 10.2
    if t < 2.9:
        caption(im, "1", "Ctrl + Alt を押したまま", "画面の右下に、出力先の一覧が出る", fade(t, 0.2))
    elif t < 10.0:
        caption(im, "2", "ホイールを回して選ぶ", "選んでいる間は、まだ音は切り替わらない", fade(t, 2.9))
    else:
        caption(im, "3", "キーを離すと、切り替え", "選んだ出力先から音が出る", fade(t, 10.0))
    ka = fade(t, 0.3)
    w1 = keycap(im, 200, 470, "Ctrl", pressed, ka)
    text_at(im, 200 + w1 + 22, 480, "+", 60, SUB, ka)
    keycap(im, 200 + w1 + 90, 470, "Alt", pressed, ka)
    wheel = 0
    if 2.9 <= t < 8.2:
        wheel = 1
    elif 8.2 <= t < 9.6:
        wheel = -1
    mouse(im, 230, 640, wheel, t, fade(t, 2.7))
    if t >= 10.4:
        text_at(im, 200, 880, "→ ワイヤレスイヤホンC から音が出る", 40, CYAN, fade(t, 10.4), bold=False)
    if wheel:
        text_at(im, 440, 690, "下へ回すと次へ" if wheel > 0 else "上へ回すと前へ", 36, SUB, 1.0, bold=False)
    return im


def scene_settings(t, which, step, title, lines):
    im = BG_DARK.convert("RGBA")
    shot = settings[which]
    # 右側に画面、ゆっくり動かす(長い画面は下へ流す)
    target_w = 1040
    k = target_w / shot.width
    sh = shot.resize((target_w, int(shot.height * k)))
    view_h = 900
    pan = 0  # 上から見せる(長い画面は下を切る)
    crop = sh.crop((0, int(pan), target_w, int(pan) + min(view_h, sh.height)))
    a = fade(t, 0.0, 0.35)
    frame = Image.new("RGBA", (target_w + 16, crop.height + 16), (0, 0, 0, 0))
    ImageDraw.Draw(frame).rounded_rectangle([0, 0, frame.width - 1, frame.height - 1], radius=14, fill=(70, 72, 92, 255))
    frame.alpha_composite(crop, (8, 8))
    im.alpha_composite(with_alpha(frame, a), (W - target_w - 90, (H - frame.height) // 2))
    text_at(im, 90, 150, "設定", 36, CYAN, a, bold=False)
    text_at(im, 90, 200, title, 58, WHITE, a)
    for i, line in enumerate(lines):
        text_at(im, 96, 330 + i * 78, "・" + line, 36, SUB, fade(t, 0.4 + i * 0.35), bold=False)
    return im


def scene_end(t):
    im = BG_DARK.convert("RGBA")
    a = fade(t, 0.1, 0.6)
    ic = icon.resize((200, 200))
    im.alpha_composite(with_alpha(ic, a), ((W - 200) // 2, 150))
    text_center(im, 390, "Windows Audio Switcher", 80, WHITE, a)
    text_center(im, 530, "押して ・ 回して ・ 離すだけ", 60, CYAN, fade(t, 0.6))
    a3 = fade(t, 1.2)
    text_center(im, 680, "Windows 10 / 11 ・ インストール不要 ・ タスクトレイに常駐", 38, SUB, a3, bold=False)
    text_center(im, 760, "ダウンロード: GitHub の Releases(AudioSwitcher.exe)", 38, SUB, a3, bold=False)
    return im


SCENES = [
    (4.0, scene_title),
    (4.0, scene_problem),
    (DEMO_LEN, scene_demo),
    (4.0, lambda t: scene_settings(t, "devices", 1, "デバイスと順番", ["Windows の出力デバイスから登録", "表示名・使う/使わない", "上下で並べ替え(ホイールの順)", "接続中・未接続がひと目でわかる"])),
    (4.0, lambda t: scene_settings(t, "keys", 2, "操作", ["ショートカットはキーを押して決める", "ホイールの向き", "循環のオン/オフ", "一時停止"])),
    (4.0, lambda t: scene_settings(t, "display", 3, "表示", ["大きさ・位置", "ダーク/ライト", "切り替えた結果の表示", "Windows の通知"])),
    (5.0, scene_end),
]


def main():
    total = sum(d for d, _ in SCENES)
    n = int(total * FPS)
    ff = subprocess.Popen(
        ["ffmpeg", "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-",
         "-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p", "-movflags", "+faststart", OUT],
        stdin=subprocess.PIPE,
    )
    prev = None
    for i in range(n):
        t = i / FPS
        acc = 0.0
        for k, (dur, fn) in enumerate(SCENES):
            if t < acc + dur or k == len(SCENES) - 1:
                frame = fn(t - acc).convert("RGB")
                # 場面のつなぎ目は少し重ねて切り替える
                lt = t - acc
                if k > 0 and lt < 0.3 and prev is not None:
                    pf = SCENES[k - 1][1](SCENES[k - 1][0] - 0.001).convert("RGB")
                    frame = Image.blend(pf, frame, ease(lt / 0.3))
                break
            acc += dur
        prev = True
        ff.stdin.write(frame.tobytes())
        if i % 150 == 0:
            print(f"{i}/{n}", flush=True)
    ff.stdin.close()
    ff.wait()
    print("done", OUT, f"{total:.1f}s")


if __name__ == "__main__":
    if len(sys.argv) > 4 and sys.argv[4] == "--stills":
        # 確認用: 場面ごとの静止画
        for at in [1.5, 3.5, 6.5, 10.5, 12.0, 14.5, 16.0, 20.5, 24.0, 28.0, 32.0, 36.5]:
            acc = 0.0
            for dur, fn in SCENES:
                if at < acc + dur:
                    fn(at - acc).convert("RGB").save(f"{OUT}-{at:05.1f}.png")
                    break
                acc += dur
    else:
        main()
