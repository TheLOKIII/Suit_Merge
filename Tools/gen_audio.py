# Suit Merge — tạo âm thanh TẠM bằng code (Animation phần 5).
# Chạy: python Tools/gen_audio.py   (cần numpy)
# Ghi: Assets/_Game/Audio/SFX/*.wav (21 hiệu ứng) + Assets/_Game/Audio/Music/music-loop.wav (~29s, lặp liền).
# Đây là âm tổng hợp đơn giản (sóng sine / nhiễu lọc) để game có tiếng — khi có file âm thanh thật, chỉ cần thả vào
# ô tương ứng của Data/Theme/SfxTheme.asset (và FxTheme cho 4 chất), không cần sửa code.
import os
import wave
import numpy as np

SR = 44100
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', '_Game', 'Audio')
SFX = os.path.join(ROOT, 'SFX')
MUSIC = os.path.join(ROOT, 'Music')
rng = np.random.default_rng(7)  # cố định → chạy lại ra đúng file cũ


# ------------------------------------------------------------------ khối dựng

def t_axis(dur):
    return np.arange(int(SR * dur)) / SR


def env(n, attack=0.005, decay=0.1, sustain=0.0, release=0.05, hold=0.0):
    """ADSR (giây) cho n mẫu; sustain là mức giữ giữa decay và release."""
    a, d, r, h = (int(SR * x) for x in (attack, decay, release, hold))
    e = np.zeros(n)
    i = 0
    seg = min(a, n - i); e[i:i + seg] = np.linspace(0, 1, seg, endpoint=False); i += seg
    seg = min(h, n - i); e[i:i + seg] = 1; i += seg
    seg = min(d, n - i); e[i:i + seg] = np.linspace(1, sustain, seg, endpoint=False); i += seg
    rest = n - i
    if rest > 0:
        hold_n = max(0, rest - r)
        e[i:i + hold_n] = sustain; i += hold_n
        e[i:] = np.linspace(sustain, 0, n - i)
    return e


def exp_env(n, tau):
    return np.exp(-np.arange(n) / (SR * tau))


def osc(freq, dur, shape='sine'):
    """freq: số hoặc mảng (quét tần số)."""
    n = int(SR * dur)
    f = np.full(n, freq, dtype=float) if np.isscalar(freq) else np.asarray(freq, dtype=float)[:n]
    phase = 2 * np.pi * np.cumsum(f) / SR
    if shape == 'sine':
        return np.sin(phase)
    if shape == 'tri':
        return 2 / np.pi * np.arcsin(np.sin(phase))
    if shape == 'soft_square':
        return np.tanh(3 * np.sin(phase))
    raise ValueError(shape)


def sweep(f0, f1, dur, curve=1.0):
    k = np.linspace(0, 1, int(SR * dur)) ** curve
    return f0 * (f1 / f0) ** k


def lowpass(x, cutoff):
    """1 cực; cutoff số hoặc mảng (Hz)."""
    c = np.full(len(x), cutoff, dtype=float) if np.isscalar(cutoff) else np.asarray(cutoff, dtype=float)
    alpha = 1 - np.exp(-2 * np.pi * c / SR)
    y = np.zeros_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += alpha[i] * (x[i] - acc)
        y[i] = acc
    return y


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def noise(dur):
    return rng.uniform(-1, 1, int(SR * dur))


def pluck(freq, dur, tau=0.18, bright=0.35):
    n = int(SR * dur)
    return (osc(freq, dur) + bright * osc(freq * 2, dur) + 0.12 * osc(freq * 3, dur)) * exp_env(n, tau) * env(n, 0.003, dur, 1, 0.01)


def bell(freq, dur, tau=0.5):
    n = int(SR * dur)
    x = osc(freq, dur) + 0.5 * osc(freq * 2.76, dur) * exp_env(n, tau * 0.4) + 0.25 * osc(freq * 5.4, dur) * exp_env(n, tau * 0.2)
    return x * exp_env(n, tau) * env(n, 0.002, dur, 1, 0.02)


def mix(length, *parts):
    """parts: (thời điểm bắt đầu giây, tín hiệu, âm lượng)."""
    out = np.zeros(int(SR * length))
    for start, sig, vol in parts:
        i = int(SR * start)
        j = min(len(out), i + len(sig))
        out[i:j] += vol * sig[:j - i]
    return out


def note(name):
    names = {'C': -9, 'C#': -8, 'D': -7, 'D#': -6, 'E': -5, 'F': -4, 'F#': -3, 'G': -2, 'G#': -1, 'A': 0, 'A#': 1, 'B': 2}
    octave = int(name[-1])
    return 440.0 * 2 ** ((names[name[:-1]] + 12 * (octave - 4)) / 12)


def finish(x, peak=0.7):
    fade = int(SR * 0.004)
    x = x.copy()
    x[:fade] *= np.linspace(0, 1, fade)
    x[-fade:] *= np.linspace(1, 0, fade)
    m = np.max(np.abs(x))
    return x * (peak / m) if m > 0 else x


def write(path, x):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


# ------------------------------------------------------------------ hiệu ứng

def sfx():
    s = {}
    # Thao tác
    n = int(SR * 0.09)
    s['pick'] = osc(sweep(520, 900, 0.09), 0.09) * env(n, 0.004, 0.08) * 0.8 + highpass(noise(0.09), 2500) * exp_env(n, 0.02) * 0.25
    n = int(SR * 0.16)
    thump = osc(sweep(200, 90, 0.16, 0.6), 0.16) * exp_env(n, 0.045)
    s['place'] = thump + lowpass(noise(0.16), 1800) * exp_env(n, 0.015) * 0.6
    a = osc(233, 0.09, 'soft_square') * env(int(SR * 0.09), 0.004, 0.08, 0.6, 0.02)
    b = osc(196, 0.13, 'soft_square') * env(int(SR * 0.13), 0.004, 0.12, 0.5, 0.04)
    s['invalid'] = lowpass(mix(0.26, (0, a, 0.6), (0.11, b, 0.6)), 1600)
    n = int(SR * 0.12)
    s['rotate'] = lowpass(noise(0.12), sweep(600, 4000, 0.12)) * env(n, 0.01, 0.1) * 1.2 + osc(sweep(700, 1100, 0.12), 0.12) * env(n, 0.005, 0.1) * 0.25
    n = int(SR * 0.05)
    s['button'] = osc(sweep(1100, 700, 0.05), 0.05) * env(n, 0.002, 0.045)
    # Lượt chơi
    n = int(SR * 0.22)
    whoosh = lowpass(noise(0.22), sweep(400, 5000, 0.22, 1.5)) * env(n, 0.12, 0.1)
    s['merge'] = mix(0.4, (0, whoosh, 0.8), (0.17, bell(1318.5, 0.23, 0.12), 0.5))
    s['create'] = mix(0.25, (0, osc(sweep(660, 990, 0.05), 0.05) * env(int(SR * 0.05), 0.002, 0.05, 0.3, 0.0), 0.5),
                      (0.04, pluck(note('A5'), 0.2, 0.08), 0.7))
    s['combo'] = mix(0.3, (0, pluck(note('C6'), 0.12, 0.05), 0.6), (0.06, pluck(note('E6'), 0.12, 0.05), 0.6),
                     (0.12, pluck(note('G6'), 0.18, 0.08), 0.7))
    n = int(SR * 0.14)
    s['draw'] = lowpass(noise(0.14), sweep(3500, 900, 0.14)) * env(n, 0.02, 0.12) * 0.9
    # Khoảnh khắc lớn
    chord = [note(x) for x in ('C5', 'E5', 'G5', 'C6', 'E6')]
    parts = [(0.0, lowpass(noise(0.5), sweep(6000, 300, 0.5)) * exp_env(int(SR * 0.5), 0.12), 0.6),
             (0.0, osc(sweep(120, 45, 0.5), 0.5) * exp_env(int(SR * 0.5), 0.15), 0.7)]
    for i, f in enumerate(chord):
        parts.append((0.06 + 0.05 * i, bell(f, 1.0, 0.45), 0.35))
    for i in range(10):
        parts.append((0.15 + rng.uniform(0, 0.6), bell(rng.uniform(2500, 4500), 0.25, 0.06), 0.12))
    s['ace'] = mix(1.3, *parts)
    s['goal-step'] = bell(note('C6'), 0.35, 0.15)
    s['goal-done'] = mix(0.6, (0, bell(note('G5'), 0.3, 0.12), 0.6), (0.12, bell(note('D6'), 0.48, 0.2), 0.7))
    # Popup / kết quả
    n = int(SR * 0.16)
    s['popup'] = lowpass(noise(0.16), sweep(800, 3000, 0.16)) * env(n, 0.03, 0.12) * 0.5 + osc(sweep(400, 650, 0.16), 0.16) * env(n, 0.01, 0.14) * 0.4
    win = [(0.0, 'C5'), (0.12, 'E5'), (0.24, 'G5'), (0.36, 'C6')]
    parts = [(t, pluck(note(x), 0.25, 0.1), 0.6) for t, x in win]
    for f in ('C5', 'E5', 'G5', 'C6'):
        parts.append((0.5, pluck(note(f), 1.0, 0.4, 0.2), 0.35))
    s['win'] = mix(1.6, *parts)
    lose = [(0.0, 'G4'), (0.22, 'E4'), (0.44, 'C4')]
    s['lose'] = lowpass(mix(1.2, *[(t, osc(note(x), 0.5, 'tri') * env(int(SR * 0.5), 0.01, 0.4, 0.2, 0.08), 0.6) for t, x in lose]), 1800)
    parts = [(0.06 * i, bell(note(x), 0.6, 0.25), 0.4) for i, x in enumerate(('C6', 'E6', 'G6', 'B6', 'D7'))]
    parts.append((0, lowpass(noise(0.6), sweep(1000, 8000, 0.6)) * env(int(SR * 0.6), 0.2, 0.4) * 0.15, 1.0))
    s['unlock'] = mix(0.95, *parts)
    # 4 chất (FxTheme)
    n = int(SR * 0.3)
    s['fx-stamp'] = osc(sweep(140, 55, 0.3, 0.5), 0.3) * exp_env(n, 0.07) + lowpass(noise(0.3), 2500) * exp_env(n, 0.012) * 0.8
    parts = [(rng.uniform(0, 0.3), bell(rng.uniform(2200, 4200), 0.25, 0.05), 0.35) for _ in range(9)]
    s['fx-diamond'] = mix(0.6, *parts)
    s['fx-heart-step'] = pluck(note('E5'), 0.22, 0.09, 0.5)
    s['fx-club'] = mix(0.3, (0, osc(sweep(380, 720, 0.07), 0.07) * env(int(SR * 0.07), 0.003, 0.06), 0.7),
                       (0.1, osc(sweep(460, 860, 0.08), 0.08) * env(int(SR * 0.08), 0.003, 0.07), 0.7))
    n = int(SR * 0.7)
    s['fx-spade'] = lowpass(noise(0.7), sweep(5000, 200, 0.7, 0.5)) * exp_env(n, 0.16) + osc(sweep(90, 35, 0.7), 0.7) * exp_env(n, 0.2) * 0.9
    return s


# ------------------------------------------------------------------ nhạc nền

def music():
    bpm = 100
    beat = 60 / bpm
    bar = 4 * beat
    prog = [('C3', ['C4', 'E4', 'G4']), ('A2', ['A3', 'C4', 'E4']), ('F2', ['F3', 'A3', 'C4']), ('G2', ['G3', 'B3', 'D4'])]
    bars = 12
    length = bars * bar
    tail = 2.0
    out = np.zeros(int(SR * (length + tail)))

    def add(start, sig, vol):
        i = int(SR * start)
        j = min(len(out), i + len(sig))
        out[i:j] += vol * sig[:j - i]

    for b in range(bars):
        bass, chord = prog[b % 4]
        t0 = b * bar
        # pad: hợp âm sine hơi lệch, vào chậm
        for f in chord:
            fr = note(f)
            d = bar + 0.6
            n = int(SR * d)
            pad = (osc(fr, d) + osc(fr * 1.003, d) + 0.3 * osc(fr * 2, d)) * env(n, 0.5, 0.3, 0.7, 0.6)
            add(t0, pad, 0.05)
        # arpeggio nốt móc đơn
        tones = [note(chord[0]) * 2, note(chord[1]) * 2, note(chord[2]) * 2, note(chord[0]) * 4]
        pattern = [0, 1, 2, 3, 2, 1, 2, 1]
        for k, idx in enumerate(pattern):
            add(t0 + k * beat / 2, pluck(tones[idx], 0.45, 0.16, 0.25), 0.07 if k % 2 == 0 else 0.05)
        # bass phách 1 và 3
        for k in (0, 2):
            n = int(SR * 0.6)
            add(t0 + k * beat, osc(note(bass), 0.6) * exp_env(n, 0.25) * env(n, 0.01, 0.6, 1, 0.05), 0.18)
        # shaker phách lẻ rất nhẹ
        for k in range(4):
            n = int(SR * 0.05)
            add(t0 + k * beat + beat / 2, highpass(noise(0.05), 6000) * exp_env(n, 0.012), 0.05)

    # đuôi cuối vòng cộng vào đầu vòng → lặp không có khe
    loop = out[:int(SR * length)].copy()
    loop[:int(SR * tail)] += out[int(SR * length):int(SR * (length + tail))]
    m = np.max(np.abs(loop))
    return loop * (0.5 / m)


# Âm nghe thường xuyên (thao tác) để nhỏ hơn → không mệt tai; âm "khoảnh khắc lớn" giữ to
PEAK = {'button': 0.4, 'invalid': 0.4, 'pick': 0.5, 'popup': 0.45, 'rotate': 0.55, 'draw': 0.5, 'place': 0.6, 'create': 0.6,
        'goal-step': 0.55}


def main():
    for name, x in sfx().items():
        write(os.path.join(SFX, name + '.wav'), finish(x, PEAK.get(name, 0.7)))
        print('SFX', name, f'{len(x) / SR:.2f}s')
    m = music()
    write(os.path.join(MUSIC, 'music-loop.wav'), m)
    print('Music', f'{len(m) / SR:.1f}s')


if __name__ == '__main__':
    main()
