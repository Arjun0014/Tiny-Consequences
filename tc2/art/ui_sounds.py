#!/usr/bin/env python3
"""TC2's interface and stillness sounds (session 31) and (session 32) the powers' and the ground's sounds the library lacked - a quake's lasting rumble,
a storm's gusts, the rain coming on, heat, frost, a withering - synthesised here: nothing recorded, nothing licensed. Writes Ogg Vorbis into
Game/Resources/Audio (ffmpeg), each levelled to a -6 dBFS peak.

    python tc2/art/ui_sounds.py [--only NAME]

Levels are measured against the library's own ui_click so a new sound sits with the rest: it prints each sound's peak and RMS.
"""
import argparse
import os
import subprocess
import tempfile
import wave

import numpy as np
from scipy.signal import butter, sosfilt

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "unity", "TinyConsequences2", "Assets", "TC2", "Game", "Resources", "Audio"))
SR = 44100


def env(n, a=0.002, d=0.1):
    t = np.arange(n) / SR
    return np.minimum(t / max(a, 1e-4), 1.0) * np.exp(-t / d)


def noise(n, seed):
    return np.random.default_rng(seed).normal(0, 1, n)


def band(x, lo, hi, order=2):
    sos = butter(order, [lo / (SR / 2), hi / (SR / 2)], btype="band", output="sos")
    return sosfilt(sos, x)


def low(x, f, order=2):
    return sosfilt(butter(order, f / (SR / 2), btype="low", output="sos"), x)


def high(x, f, order=2):
    return sosfilt(butter(order, f / (SR / 2), btype="high", output="sos"), x)


def tone(n, f, phase=0.0):
    return np.sin(2 * np.pi * f * np.arange(n) / SR + phase)


def norm(x, peak):
    return x / max(1e-9, np.max(np.abs(x))) * peak


def soft(x, k=2.5):
    """Peaks rounded off (tanh) so a few sharp transients do not set a sound's whole level."""
    x = x / max(1e-9, np.max(np.abs(x)))
    return np.tanh(k * x) / np.tanh(k)


def tick():
    """A pointer over a word: a dry little tap of wood."""
    n = int(0.05 * SR)
    x = band(noise(n, 1), 900, 3800) * env(n, 0.0006, 0.006) + 0.35 * tone(n, 1900) * env(n, 0.0005, 0.005)
    return norm(x, 0.5)


def select():
    """Someone chosen: a soft knock with a low wooden body and a faint bell's overtone."""
    n = int(0.42 * SR)
    knock = low(noise(n, 2), 900) * env(n, 0.001, 0.02)
    body = 0.9 * tone(n, 196) * env(n, 0.002, 0.06) + 0.45 * tone(n, 392) * env(n, 0.002, 0.05)
    bell = 0.16 * tone(n, 1174.7) * env(n, 0.004, 0.17) + 0.09 * tone(n, 1760) * env(n, 0.004, 0.12)
    return norm(knock + body + bell, 0.62)


def page():
    """A leaf of the book turned: a soft swish of paper rising and falling, a little crackle at its fold."""
    n = int(0.58 * SR)
    t = np.arange(n) / n
    shape = np.sin(np.pi * np.clip(t * 1.25, 0, 1)) ** 1.6
    swish = band(noise(n, 3), 700, 6000) * shape
    # the paper's flutter
    flutter = 1 + 0.5 * np.sin(2 * np.pi * (7 + 9 * t) * t)
    swish *= flutter
    crack = high(noise(n, 4), 3500) * np.exp(-((t - 0.72) / 0.03) ** 2) * 0.8
    thud = low(noise(n, 5), 260) * np.exp(-((t - 0.82) / 0.05) ** 2) * 2.2
    return norm(swish + crack + thud, 0.55)


def leaf():
    """A tab of a page: half a page's sound."""
    n = int(0.2 * SR)
    t = np.arange(n) / n
    x = band(noise(n, 6), 900, 5500) * np.sin(np.pi * t) ** 1.2
    return norm(x, 0.42)


def cast():
    """A power let go: air drawn in and a deep pulse under it - the world's own sound of what it does comes after."""
    n = int(0.95 * SR)
    t = np.arange(n) / SR
    air = band(noise(n, 7), 300, 2600) * np.minimum(t / 0.22, 1.0) ** 2 * np.exp(-np.maximum(t - 0.22, 0) / 0.11)
    f = 62 + 40 * np.exp(-t / 0.2)
    pulse = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-np.maximum(t - 0.2, 0) / 0.22) * np.minimum(t / 0.2, 1.0)
    shim = 0.12 * tone(n, 880) * np.exp(-np.maximum(t - 0.2, 0) / 0.18) * np.minimum(t / 0.2, 1.0)
    return norm(air * 0.8 + pulse + shim, 0.7)


def enter():
    """Into the world: a low swell and two soft plucked notes rising, like a breath taken."""
    n = int(2.4 * SR)
    t = np.arange(n) / SR
    swell = low(noise(n, 8), 500) * np.sin(np.pi * np.clip(t / 2.2, 0, 1)) ** 2 * 0.5
    def pluck(f, at, seed):
        tt = np.maximum(t - at, 0)
        a = (t >= at) * np.exp(-tt / 0.7)
        return a * (np.sin(2 * np.pi * f * tt) + 0.4 * np.sin(2 * np.pi * 2 * f * tt) * np.exp(-tt / 0.3) + 0.2 * np.sin(2 * np.pi * 3 * f * tt) * np.exp(-tt / 0.18))
    notes = 0.5 * pluck(196.0, 0.15, 1) + 0.46 * pluck(293.66, 0.55, 2) + 0.38 * pluck(392.0, 0.95, 3)
    return norm(swell + notes, 0.62)


def chime():
    """A great thing come to pass (the years' headlines): one low bell, far off."""
    n = int(2.2 * SR)
    t = np.arange(n) / SR
    x = 0
    for r, a, d in ((1.0, 1.0, 1.1), (2.0, 0.5, 0.9), (2.98, 0.32, 0.7), (4.16, 0.18, 0.5), (5.43, 0.08, 0.35)):
        x = x + a * np.sin(2 * np.pi * 174.6 * r * t) * np.exp(-t / d)
    return norm(x * np.minimum(t / 0.004, 1.0), 0.5)


def whoosh(seconds, rising, seed):
    """The camera rising to orbit or coming down: wind opening and closing through a sweeping band, the ground falling away under it."""
    n = int(seconds * SR)
    t = np.linspace(0, 1, n)
    base = noise(n, seed)
    u = t if rising else 1 - t
    # a sweep made of a few fixed bands crossfaded (kept simple: no time-varying filter needed)
    bands = [(120, 400), (300, 900), (700, 2000), (1500, 4200), (3000, 8000)]
    out = np.zeros(n)
    for i, (lo, hi) in enumerate(bands):
        c = (i + 0.5) / len(bands)
        w = np.exp(-((u - c) / 0.26) ** 2)
        out += band(base, lo, hi) * w
    shape = np.sin(np.pi * np.clip(t, 0, 1)) ** (1.2 if rising else 1.5)
    return norm(out * shape, 0.55)


def stillness(seconds=48):
    """The title's and orbit's air: a very low open fifth breathing slowly, a far shimmer and the sound of wind a long way off - it loops exactly (every part periodic in its length, the air crossfaded)."""
    n = int(seconds * SR)
    t = np.arange(n) / SR
    T = seconds
    def per(f):
        return round(f * T) / T   # a frequency that returns to itself at the loop's end
    def slow(cycles, phase=0.0, lo=0.0, hi=1.0):
        return lo + (hi - lo) * (0.5 + 0.5 * np.sin(2 * np.pi * cycles * t / T + phase))
    x = np.zeros(n)
    # the drone: D2, A2, D3, A3, each with a slow beat of a twin a hair apart
    for f, amp, c, ph in ((73.42, 0.34, 2, 0.0), (110.0, 0.26, 3, 1.1), (146.83, 0.17, 2, 2.3), (220.0, 0.08, 5, 0.7), (293.66, 0.05, 3, 3.9), (440.0, 0.018, 7, 2.0)):
        f1, f2 = per(f), per(f) + 2.0 / T
        x += amp * (np.sin(2 * np.pi * f1 * t) + np.sin(2 * np.pi * f2 * t + ph)) * 0.5 * slow(c, ph, 0.45, 1.0)
    # the shimmer: high soft partials coming and going
    for f, c, ph in ((880.0, 4, 0.4), (1108.73, 3, 1.9), (1318.5, 5, 3.0), (1760.0, 2, 4.4)):
        x += 0.008 * np.sin(2 * np.pi * per(f) * t) * slow(c, ph, 0.0, 1.0) ** 3
    # the air: noise, low and soft, crossfaded over itself so it loops
    cross = int(3 * SR)
    raw = low(noise(n + cross, 21), 420, 2) + 0.3 * band(noise(n + cross, 22), 500, 1800)
    air = raw[:n].copy()
    fade = np.linspace(0, 1, cross)
    air[:cross] = raw[n:n + cross] * (1 - fade) + raw[:cross] * fade
    air = air / np.std(air) * 0.035 * slow(3, 0.2, 0.55, 1.0)
    return norm(x + air, 0.5)


# ---- (session 32) the powers' and the ground's own sounds the library lacked: a quake that goes on rumbling, a storm's gusts, the rain coming on,
# heat, frost and a withering - each made of filtered noise and a few partials, levelled to a -6 dBFS peak like the rest

def droplets(n, rate, seed, lo=2000, hi=7000, decay=0.004):
    """Sparse little impacts at a rate (per second, an array over time): rain on leaves, ice, dry grass."""
    rng = np.random.default_rng(seed)
    hits = (rng.random(n) < np.asarray(rate) / SR) * rng.uniform(0.3, 1.0, n)
    k = int(0.03 * SR)
    kern = noise(k, seed + 1) * np.exp(-np.arange(k) / (decay * SR))
    from scipy.signal import fftconvolve
    return band(fftconvolve(hits, kern)[:n], lo, hi)


def quake_rumble(seed, seconds=12.0):
    """The ground shaking and going on shaking: a thump as it breaks, a deep rumble strong for two seconds and dying slowly, two aftershocks
    swelling under it, things rattling and falling in its first seconds. (The library's quakes build for two seconds and are gone in six.)"""
    n = int(seconds * SR)
    t = np.arange(n) / SR
    deep = low(noise(n, seed), 75, 4)
    deep = deep / np.std(deep)
    mid = band(noise(n, seed + 1), 70, 260, 2)
    mid = mid / np.std(mid)
    am = 0.65 + 0.35 * low(noise(n, seed + 2), 6, 2) / np.std(low(noise(n, seed + 2), 6, 2))   # (the shaking comes in surges)
    shape = np.minimum(t / 0.08, 1.0) * np.where(t < 1.8, 1.0, np.exp(-(t - 1.8) / 2.6))
    after = 0.42 * np.exp(-((t - 5.6) / 0.7) ** 2) + 0.26 * np.exp(-((t - 8.7) / 0.8) ** 2)
    body = (deep + 0.55 * mid) * am * (shape + after)
    f = 70 - 36 * np.minimum(t / 0.45, 1.0)
    thump = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.4) * 2.6
    rattle = low(droplets(n, 150 * np.exp(-t / 1.4) + 35 * np.exp(-((t - 5.6) / 0.5) ** 2), seed + 3, 500, 2600, 0.015), 2800, 4)
    rattle = np.tanh(rattle / max(1e-9, np.std(rattle)) * 0.5) * 0.35
    x = body + thump + rattle
    x *= np.minimum(1.0, (seconds - t) / 1.0)   # (a clean end)
    return norm(soft(x, 2.0), 0.5)


def wind_gust(seed, seconds=4.2):
    """A storm's gust: wind rising, tearing past with a whistle in it, and falling away; its surges uneven."""
    n = int(seconds * SR)
    t = np.arange(n) / SR
    u = np.clip(t / seconds, 0, 1)
    g = np.sin(np.pi * np.clip(u / 0.42, 0, 1) * 0.5) ** 2 * np.where(u < 0.42, 1.0, np.exp(-(u - 0.42) / 0.22))
    turb = low(noise(n, seed + 1), 4, 2)
    g = g * (0.75 + 0.25 * turb / np.std(turb))
    base = noise(n, seed)
    out = np.zeros(n)
    for i, (lo, hi) in enumerate([(90, 300), (250, 800), (600, 1800), (1400, 4000)]):
        c = (i + 0.5) / 4
        out += band(base, lo, hi) / (1 + i * 0.4) * (0.35 + 0.65 * np.exp(-((g - c) / 0.35) ** 2))
    whistle = band(noise(n, seed + 2), 640, 690, 2) * 6
    return norm((out + whistle * g ** 2) * g, 0.5)


def rain_onset(seconds=4.0):
    """The rain brought: first drops, then more, then a steady fall - the rain's own bed takes it up from there."""
    n = int(seconds * SR)
    t = np.arange(n) / SR
    rate = 25 + 1100 * np.clip(t / 2.6, 0, 1) ** 2
    drops = droplets(n, rate, 41, 1800, 7500, 0.003)
    wash = band(noise(n, 42), 700, 6500) * np.clip((t - 0.6) / 2.4, 0, 1) ** 1.5 * 0.5
    drops = np.tanh(drops / max(1e-9, np.std(drops[-n // 4:])) * 0.7)
    x = drops * 0.6 + wash / max(1e-9, np.std(wash[-n // 4:])) * 0.4
    x *= np.minimum(1.0, (seconds - t) / 0.9)
    return norm(soft(x, 1.8), 0.5)


def heat_haze(seconds=2.8):
    """Warm, dry air laid over the land (warmth, drought): a breath of hot wind with a dry rustle in it over a low warm hum, the haze shimmering."""
    n = int(seconds * SR)
    t = np.arange(n) / SR
    swell = np.sin(np.pi * np.clip(t / seconds, 0, 1)) ** 1.4
    air = band(noise(n, 51), 2500, 9000) * 0.6 + band(noise(n, 52), 400, 1500) * 0.5
    ph = 2 * np.pi * np.cumsum(1 + 0.004 * np.sin(2 * np.pi * 5.5 * t)) / SR
    hum = (np.sin(110 * ph) + 0.5 * np.sin(165 * ph) + 0.2 * np.sin(330 * ph)) * 0.018
    crackle = droplets(n, 22 * swell, 53, 3000, 8000, 0.002)
    crackle = np.tanh(crackle / max(1e-9, np.std(crackle)) * 0.4) * 0.05
    shimmer = 1 + 0.25 * np.sin(2 * np.pi * np.cumsum(3 + 4 * t) / SR)
    return norm(soft((air * shimmer / np.std(air) * 0.1 + hum + crackle) * swell, 2.2), 0.5)


def frost(seconds=2.6):
    """Cold air laid over the land: a breath of icy wind, ice crackling as it forms, a few glassy tinkles."""
    n = int(seconds * SR)
    t = np.arange(n) / SR
    breath = band(noise(n, 61), 900, 4200) * np.sin(np.pi * np.clip(t / seconds, 0, 1)) ** 1.6
    ice = droplets(n, 20 + 260 * np.clip(t / 1.6, 0, 1), 62, 4000, 11000, 0.0015) * np.minimum(1.0, (seconds - t) / 0.6)
    tinkle = np.zeros(n)
    rng = np.random.default_rng(63)
    for at in (0.35, 0.8, 1.15, 1.6, 1.95):
        f0 = rng.choice([2637.0, 3136.0, 3520.0, 4186.0])
        tt = np.maximum(t - at, 0)
        tinkle += (t >= at) * np.exp(-tt / 0.18) * (np.sin(2 * np.pi * f0 * tt) + 0.4 * np.sin(2 * np.pi * f0 * 2.76 * tt))
    x = breath / np.std(breath) * 0.12 + ice / max(1e-9, np.std(ice)) * 0.05 + tinkle * 0.06
    return norm(x, 0.5)


def wither(seconds=2.4):
    """A blight on the fields: a dry rustle running down and away, as of leaves curling, over a low sigh."""
    n = int(seconds * SR)
    t = np.arange(n) / SR
    u = np.clip(t / seconds, 0, 1)
    base = noise(n, 71)
    out = np.zeros(n)
    for i, (lo, hi) in enumerate([(4000, 8000), (2200, 4500), (1100, 2400), (500, 1200)]):
        c = (i + 0.5) / 4
        out += band(base, lo, hi) * np.exp(-((u - c) / 0.28) ** 2)
    flutter = 0.55 + 0.45 * np.abs(low(noise(n, 72), 24, 2)) / np.std(low(noise(n, 72), 24, 2))
    sigh = band(noise(n, 73), 160, 420) * np.sin(np.pi * u) ** 2 * 0.6
    shape = np.minimum(t / 0.15, 1.0) * np.minimum(1.0, (seconds - t) / 0.5)
    return norm((out * flutter + sigh) * shape, 0.5)


SOUNDS = dict(
    ui_tick=tick, ui_select=select, ui_page=page, ui_leaf=leaf, ui_cast=cast, ui_enter=enter, ui_chime=chime,
    lapse_rise=lambda: whoosh(3.2, True, 31), lapse_fall=lambda: whoosh(3.8, False, 32),
    amb_stillness=stillness,
    quake_rumble_1=lambda: quake_rumble(81), quake_rumble_2=lambda: quake_rumble(91, 13.0),
    wind_gust_1=lambda: wind_gust(101), wind_gust_2=lambda: wind_gust(111, 4.8),
    rain_onset=rain_onset, heat_haze=heat_haze, frost=frost, wither=wither,
)

META = """fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: {load}
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.7
    conversionMode: 0
    preloadAudioData: 0
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def write(name, x):
    x = np.clip(x, -1, 1)
    with tempfile.TemporaryDirectory() as d:
        wav_path = os.path.join(d, name + ".wav")
        with wave.open(wav_path, "wb") as f:
            f.setnchannels(1); f.setsampwidth(2); f.setframerate(SR)
            f.writeframes((x * 32767).astype(np.int16).tobytes())
        out = os.path.join(OUT, name + ".ogg")
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav_path, "-c:a", "libvorbis", "-q:a", "4", out], check=True)
    import hashlib
    guid = hashlib.md5(("tc2-snd-" + name).encode()).hexdigest()
    with open(os.path.join(OUT, name + ".ogg.meta"), "w", newline="\n") as f:
        f.write(META.format(guid=guid, load=2 if len(x) > SR * 8 else 0))
    print(f"{name:16} {len(x) / SR:6.2f} s  peak {np.max(np.abs(x)):.3f}  rms {np.sqrt(np.mean(x * x)):.3f}  {os.path.getsize(out) // 1024} KB")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default=None)
    a = ap.parse_args()
    for name, fn in SOUNDS.items():
        if a.only and a.only != name:
            continue
        write(name, fn())


if __name__ == "__main__":
    main()
