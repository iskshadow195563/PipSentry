import numpy as np, wave, math, random

SR = 48000
DUR = 46.6
N = int(SR * DUR)
rng = np.random.default_rng(7)

def hz(m): return 440.0 * 2 ** ((m - 69) / 12.0)

def env(n, a, d, s_level, r, sr=SR):
    a_n, d_n, r_n = int(a*sr), int(d*sr), int(r*sr)
    s_n = max(0, n - a_n - d_n - r_n)
    parts = []
    if a_n: parts.append(np.linspace(0, 1, a_n, endpoint=False))
    if d_n: parts.append(np.linspace(1, s_level, d_n, endpoint=False))
    if s_n: parts.append(np.full(s_n, s_level))
    if r_n: parts.append(np.linspace(s_level, 0, r_n, endpoint=False))
    e = np.concatenate(parts) if parts else np.zeros(0)
    if len(e) < n: e = np.pad(e, (0, n - len(e)))
    return e[:n]

def lp(x, k):
    if k <= 1: return x
    return np.convolve(x, np.ones(k)/k, mode='same')

def hp(x, k):
    return x - lp(x, k)

def add(buf, start, sig, gain=1.0, pan=0.0):
    i = int(start * SR)
    if i >= len(buf): return
    sig = sig[:len(buf) - i]
    l = math.sqrt((1 - pan) / 2); r = math.sqrt((1 + pan) / 2)
    buf[i:i+len(sig), 0] += sig * gain * l
    buf[i:i+len(sig), 1] += sig * gain * r

def saw(f, n, detune=0.0):
    t = np.arange(n) / SR
    f = f * (1 + detune)
    x = 2 * ((t * f) % 1.0) - 1
    return x

def tri(f, n):
    t = np.arange(n) / SR
    return 2 * np.abs(2 * ((t * f) % 1.0) - 1) - 1

def sine(f, n): return np.sin(2 * np.pi * f * np.arange(n) / SR)

def noise(n): return rng.standard_normal(n) * 0.6

def pad_chord(start, dur, notes, gain=0.16):
    n = int(dur * SR)
    e = env(n, 0.9, 0.6, 0.75, 1.2)
    for m in notes:
        f = hz(m)
        for det, pan in ((-0.004, -0.4), (0.0, 0.0), (0.004, 0.4)):
            s = saw(f, n, det)
            s = lp(s, 60)
            add(buf, start, s * e, gain * 0.5, pan)

def pluck(start, dur, midi, gain=0.2, bright=40):
    n = int(dur * SR)
    e = env(n, 0.005, dur * 0.5, 0.25, dur * 0.4)
    s = tri(hz(midi), n) * 0.7 + saw(hz(midi), n) * 0.3
    s = lp(s, bright)
    add(buf, start, s * e, gain, 0.0)

def bass_note(start, dur, midi, gain=0.3):
    n = int(dur * SR)
    e = env(n, 0.01, 0.08, 0.7, 0.1)
    s = sine(hz(midi), n) * 0.8 + lp(saw(hz(midi), n), 40) * 0.2
    add(buf, start, s * e, gain)

def kick(start, gain=0.55):
    dur = 0.35; n = int(dur * SR); t = np.arange(n) / SR
    f = 110 * np.exp(-t * 18) + 45
    s = np.sin(2 * np.pi * np.cumsum(f) / SR)
    e = env(n, 0.002, 0.05, 0.6, 0.28)
    add(buf, start, s * e, gain)

def hat(start, gain=0.08):
    dur = 0.06; n = int(dur * SR)
    s = hp(noise(n), 8)
    e = env(n, 0.001, 0.01, 0.2, 0.045)
    add(buf, start, s * e, gain, 0.25)

def clap(start, gain=0.13):
    dur = 0.12; n = int(dur * SR)
    s = hp(noise(n), 30)
    e = env(n, 0.001, 0.02, 0.15, 0.09)
    add(buf, start, s * e, gain, -0.2)

def swish(start, gain=0.10, dur=0.55, up=True):
    n = int(dur * SR)
    x = noise(n)
    # crude sweep: crossfade between low and high passed copies
    lo = lp(x, 220); hi = hp(x, 20)
    mix = np.linspace(0, 1, n) if up else np.linspace(1, 0, n)
    s = lo * (1 - mix) + hi * mix
    s = lp(s, 14)
    e = env(n, 0.22, 0.05, 0.5, 0.28)
    add(buf, start, s * e, gain)

def impact(start, gain=0.5):
    dur = 0.9; n = int(dur * SR); t = np.arange(n) / SR
    f = 70 * np.exp(-t * 6) + 38
    s = np.sin(2 * np.pi * np.cumsum(f) / SR)
    e = env(n, 0.004, 0.15, 0.35, 0.7)
    add(buf, start, s * e, gain)
    add(buf, start, lp(noise(n), 120) * env(n, 0.005, 0.1, 0.2, 0.6), gain * 0.25)

buf = np.zeros((N, 2))

chords = [
    (0.0,  [57, 60, 64]),   # Am
    (4.8,  [53, 57, 60]),   # F
    (9.6,  [60, 64, 67]),   # C
    (14.4, [55, 59, 62]),   # G
    (19.2, [57, 60, 64]),
    (24.0, [53, 57, 60]),
    (28.8, [60, 64, 67]),
    (33.6, [55, 59, 62]),
    (38.4, [57, 60, 64]),
    (43.2, [53, 57, 60]),
]

# pad throughout, softer in the break
for start, notes in chords:
    section_break = 27.5 <= start < 34.0
    pad_chord(start, 5.2, notes, 0.10 if section_break else 0.15)

# arpeggio
arp_pattern = [0, 2, 1, 2, 0, 2, 1, 2]
step = 0.15
t = 2.0
while t < 42.5:
    section = "intro" if t < 6 else "build" if t < 12 else "groove" if t < 18 else "full" if t < 27.5 else "break" if t < 34.6 else "full2" if t < 40.3 else "outro"
    if section == "intro":
        idx = int(t / (step * 2)) % len(arp_pattern)
        chord = [ns for s, ns in chords if s <= t][-1]
        pluck(t, 0.35, chord[arp_pattern[idx] % 3] + 12, 0.07)
        t += step * 2
    elif section == "outro":
        t += step * 2
    else:
        idx = int(t / step) % len(arp_pattern)
        chord = [ns for s, ns in chords if s <= t][-1]
        gain = 0.09 if section in ("build", "break") else 0.12
        pluck(t, 0.28, chord[arp_pattern[idx] % 3] + 12, gain)
        t += step

# drums
def drums(t0, t1, kick_on=True, hats=True, claps=True):
    b = t0
    beat = 0.6  # 100 bpm
    i = 0
    while b < t1:
        if kick_on and i % 4 == 0: kick(b)
        if claps and i % 4 == 2: clap(b)
        if hats:
            hat(b + beat / 2, 0.05)
            hat(b, 0.03)
        b += beat; i += 1

drums(12.0, 27.5)
drums(34.6, 40.3)

# sub bass pulses
b = 6.0
while b < 40.3:
    root = [45, 41, 48, 43][int((b // 4.8) % 4)]
    if 27.5 <= b < 34.0:
        bass_note(b, 0.35, root, 0.16)
        b += 1.2
    else:
        bass_note(b, 0.28, root, 0.26)
        bass_note(b + 0.3, 0.22, root, 0.18)
        b += 0.6

# sparse lead in the full sections
lead_notes = [76, 74, 72, 74, 79, 76, 72, 74, 76, 74, 72, 69]
b = 20.0
i = 0
while b < 27.0:
    pluck(b, 0.9, lead_notes[i % len(lead_notes)], 0.09, 25)
    b += 1.2; i += 1
b = 35.0
while b < 40.0:
    pluck(b, 0.9, lead_notes[i % len(lead_notes)] + 12, 0.07, 25)
    b += 1.2; i += 1

# riser into 12s and 34s
for start, dur in ((9.6, 2.2), (31.8, 2.4)):
    n = int(dur * SR)
    x = noise(n)
    lo = lp(x, 300); hi = hp(x, 12)
    mix = np.linspace(0, 1, n) ** 1.5
    e = np.linspace(0, 1, n) ** 2
    add(buf, start, (lo * (1 - mix) + hi * mix) * e, 0.12)

# soft impacts at section starts
for t in (5.85, 12.0, 18.15, 21.0, 27.65, 35.3, 40.95):
    impact(t, 0.35)

# transition swishes at cut points (video cuts at 5.65, 11.8, 18.45, 25.1, 32.75, 38.4)
for t in (5.5, 11.65, 17.8, 20.65, 27.3, 34.95, 40.6):
    swish(t, 0.07, 0.55, up=True)

# fade in/out
fade_in = int(0.8 * SR); fade_out = int(1.6 * SR)
buf[:fade_in] *= np.linspace(0, 1, fade_in)[:, None]
buf[-fade_out:] *= np.linspace(1, 0, fade_out)[:, None]

# gentle limiter and normalise
peak = np.max(np.abs(buf))
buf = np.tanh(buf / max(peak, 1e-6) * 1.1) * 0.85
buf = lp(buf[:, 0], 2), lp(buf[:, 1], 2)
out = np.stack(buf, axis=1)

out16 = np.clip(out, -1, 1)
pcm = (out16 * 32767).astype(np.int16)
with wave.open(r"D:/double_window/promo/assets/music.wav", "wb") as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes(pcm.tobytes())
print("wrote", pcm.shape, "peak", float(np.max(np.abs(out16))))