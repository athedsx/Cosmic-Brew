"""
Gerador de áudio do Cosmic Brew (tudo sintetizado, sem samples externos).

  python Tools/audio_gen/generate_audio.py

Gera em Assets/Audio/:
  Music/  3 faixas lo-fi em loop perfeito (Rhodes FM, baixo, bateria boom-bap com swing,
          chiado de vinil, oscilação de fita, filtro passa-baixa)
  SFX/    efeitos "ASMR" do jogo (máquina de café, moedor, xícara, moedas, "Ahhh…", etc.)
Só depende de numpy. Os resultados são determinísticos (sementes fixas).
"""
import os
import wave
import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_MUSIC = os.path.join(ROOT, "Assets", "Audio", "Music")
OUT_SFX = os.path.join(ROOT, "Assets", "Audio", "SFX")
SR_MUSIC = 24000   # lo-fi de propósito (e arquivos menores)
SR_SFX = 32000


# ------------------------------------------------------------------ utilidades
def midi(n):
    return 440.0 * 2.0 ** ((n - 69) / 12.0)


def write_wav(path, data, sr):
    """data: (n,) mono ou (n, 2) estéreo, float -1..1"""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = np.clip(data, -1.0, 1.0)
    ch = 1 if data.ndim == 1 else data.shape[1]
    pcm = (data * 32767.0).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(ch)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(pcm.tobytes())
    print(f"  {os.path.relpath(path, ROOT)}  ({len(data) / sr:.1f}s)")


def fft_filter(x, sr, gain_fn):
    """Filtro por FFT (circular: perfeito para loops). gain_fn(freqs) -> ganho."""
    spec = np.fft.rfft(x, axis=0)
    f = np.fft.rfftfreq(x.shape[0], 1.0 / sr)
    g = gain_fn(f)
    if x.ndim == 2:
        g = g[:, None]
    return np.fft.irfft(spec * g, n=x.shape[0], axis=0)


def lowpass(fc, order=4):
    return lambda f: 1.0 / np.sqrt(1.0 + (f / fc) ** (2 * order))


def highpass(fc, order=2):
    return lambda f: 1.0 / np.sqrt(1.0 + (np.maximum(f, 1e-3) / fc) ** (-2 * order))


def bandpass(lo, hi):
    return lambda f: highpass(lo)(f) * lowpass(hi)(f)


def resonances(peaks):
    """peaks: [(freq, largura, ganho)] -> soma de gaussianas no espectro (formantes)"""
    def g(f):
        out = np.zeros_like(f)
        for fc, bw, gain in peaks:
            out += gain * np.exp(-0.5 * ((f - fc) / bw) ** 2)
        return out
    return g


def env_exp(n, sr, attack, decay):
    t = np.arange(n) / sr
    return (1.0 - np.exp(-t / max(attack, 1e-4))) * np.exp(-t * decay)


def fade(x, sr, fin=0.005, fout=0.02):
    n = len(x)
    a = min(n, int(fin * sr))
    b = min(n, int(fout * sr))
    x = x.copy()
    if a > 0:
        x[:a] *= np.linspace(0, 1, a)[:, None] if x.ndim == 2 else np.linspace(0, 1, a)
    if b > 0:
        x[-b:] *= np.linspace(1, 0, b)[:, None] if x.ndim == 2 else np.linspace(1, 0, b)
    return x


def normalize(x, peak):
    m = np.max(np.abs(x))
    return x * (peak / m) if m > 0 else x


def pan_stereo(mono, pan):
    """pan -1..1 (lei de potência constante)"""
    a = (pan + 1) * np.pi / 4
    return np.stack([mono * np.cos(a), mono * np.sin(a)], axis=1)


# ------------------------------------------------------------------ instrumentos
def rhodes(freq, dur, vel, sr, rng):
    n = int(dur * sr)
    t = np.arange(n) / sr
    index = 1.6 * np.exp(-t * 3.5) + 0.25          # brilho do ataque que some
    mod = np.sin(2 * np.pi * freq * t) * index
    car = np.sin(2 * np.pi * freq * t + mod)
    car += 0.18 * np.sin(2 * np.pi * freq * 2.0 * t) * np.exp(-t * 6)   # "tine"
    trem = 1.0 + 0.07 * np.sin(2 * np.pi * 4.3 * t + rng.uniform(0, 6.28))
    amp = (1 - np.exp(-t * 400)) * np.exp(-t * 0.9) * trem
    rel = min(n, int(0.08 * sr))
    amp[-rel:] *= np.linspace(1, 0, rel)
    return car * amp * vel


def bell(freq, dur, vel, sr):
    n = int(dur * sr)
    t = np.arange(n) / sr
    s = np.sin(2 * np.pi * freq * t) + 0.35 * np.sin(2 * np.pi * freq * 3.01 * t) * np.exp(-t * 4) \
        + 0.12 * np.sin(2 * np.pi * freq * 4.2 * t) * np.exp(-t * 7)
    return s * (1 - np.exp(-t * 300)) * np.exp(-t * 2.6) * vel


def bass(freq, dur, vel, sr):
    n = int(dur * sr)
    t = np.arange(n) / sr
    s = np.sin(2 * np.pi * freq * t) + 0.25 * np.sin(2 * np.pi * 2 * freq * t) + 0.08 * np.sin(2 * np.pi * 3 * freq * t)
    s = np.tanh(1.4 * s)
    amp = (1 - np.exp(-t * 90)) * np.exp(-t * 1.6)
    rel = min(n, int(0.05 * sr))
    amp[-rel:] *= np.linspace(1, 0, rel)
    return s * amp * vel


def kick(sr, vel=1.0):
    n = int(0.45 * sr)
    t = np.arange(n) / sr
    f = 46 + 95 * np.exp(-t * 32)
    ph = 2 * np.pi * np.cumsum(f) / sr
    s = np.sin(ph) * np.exp(-t * 7.5) + 0.25 * np.exp(-t * 400) * np.sin(2 * np.pi * 1200 * t)
    return np.tanh(1.8 * s) * vel


def snare(sr, rng, vel=1.0):
    n = int(0.3 * sr)
    t = np.arange(n) / sr
    noise = fft_filter(rng.standard_normal(n), sr, bandpass(1200, 5200)) * np.exp(-t * 17)
    tone = np.sin(2 * np.pi * 185 * t) * np.exp(-t * 28)
    return (0.8 * noise / (np.max(np.abs(noise)) + 1e-9) + 0.55 * tone) * vel


def hat(sr, rng, vel=1.0, open_=False):
    n = int((0.22 if open_ else 0.06) * sr)
    t = np.arange(n) / sr
    s = fft_filter(rng.standard_normal(n), sr, highpass(6500, 3))
    s /= (np.max(np.abs(s)) + 1e-9)
    return s * np.exp(-t * (14 if open_ else 70)) * vel


def add_circular(buf, sig, start):
    """soma sig em buf começando em start, dando a volta no fim (loop perfeito)"""
    n = len(buf)
    start %= n
    end = start + len(sig)
    if end <= n:
        buf[start:end] += sig
    else:
        k = n - start
        buf[start:] += sig[:k]
        rest = sig[k:]
        while len(rest) > 0:
            m = min(n, len(rest))
            buf[:m] += rest[:m]
            rest = rest[m:]


# ------------------------------------------------------------------ música
TRACKS = [
    {
        "file": "lofi_01_cafe_na_nebulosa.wav", "title": "Café na Nebulosa", "bpm": 74, "seed": 11,
        # (baixo, [notas do acorde]) — Gmaj9  F#m7(9)  Em9  A13
        "chords": [(43, [59, 62, 66, 69]), (42, [57, 61, 64, 68]), (40, [55, 59, 62, 66]), (45, [55, 61, 66, 71])],
        "scale": [62, 64, 66, 69, 71, 74, 76, 78],
    },
    {
        "file": "lofi_02_orbita_preguicosa.wav", "title": "Órbita Preguiçosa", "bpm": 70, "seed": 23,
        # Cmaj7(9)  Am9  Dm9  G13
        "chords": [(36, [52, 55, 59, 62]), (33, [48, 52, 55, 59]), (38, [53, 57, 60, 64]), (31, [53, 59, 64, 69])],
        "scale": [60, 62, 64, 67, 69, 72, 74, 76],
    },
    {
        "file": "lofi_03_poeira_de_estrelas.wav", "title": "Poeira de Estrelas", "bpm": 80, "seed": 37,
        # Fmaj9  Em7  Dm9  Cmaj9  (+ Bbmaj7 de vez em quando)
        "chords": [(41, [57, 60, 64, 67]), (40, [55, 59, 62, 67]), (38, [53, 57, 60, 64]), (36, [52, 55, 59, 62])],
        "alt": (34, [53, 57, 62, 65]),
        "scale": [60, 62, 65, 67, 69, 72, 74, 77],
    },
]
BARS = 32


def render_track(spec):
    sr = SR_MUSIC
    rng = np.random.default_rng(spec["seed"])
    spb = 60.0 / spec["bpm"]                  # segundos por batida
    bar = 4 * spb
    n = int(round(BARS * bar * sr))
    keys = np.zeros((n, 2))
    low = np.zeros(n)
    drums = np.zeros((n, 2))
    lead = np.zeros((n, 2))
    duck = np.ones(n)

    swing = 0.33 * (spb / 2)                  # atraso das colcheias "fracas"

    def pos(bar_i, beat):
        """posição em amostras de (compasso, batida em fração) com swing nas colcheias"""
        eighth = beat * 2
        sw = swing if (abs(eighth - round(eighth)) < 1e-6 and int(round(eighth)) % 2 == 1) else 0.0
        return int(round((bar_i * bar + beat * spb + sw) * sr))

    for b in range(BARS):
        chord_i = b % 4
        bass_note, notes = spec["chords"][chord_i]
        if "alt" in spec and b % 8 == 7:
            bass_note, notes = spec["alt"]
        intro = b < 4
        breakdown = 16 <= b < 20

        # ---- Rhodes: acorde no 1 (dedilhado) e às vezes repique no "e" do 2
        for k, note in enumerate(notes):
            v = 0.2 * rng.uniform(0.85, 1.0)
            s = rhodes(midi(note), bar * 1.02, v, sr, rng)
            add_circular(keys[:, 0], s * (0.9 - 0.12 * k), pos(b, 0) + int(k * 0.018 * sr))
            add_circular(keys[:, 1], s * (0.78 + 0.12 * k), pos(b, 0) + int(k * 0.018 * sr))
        if rng.random() < 0.55 and not intro:
            for k, note in enumerate(notes[1:]):
                s = rhodes(midi(note), spb * 1.4, 0.09, sr, rng)
                add_circular(keys[:, 0], s, pos(b, 1.5) + int(k * 0.012 * sr))
                add_circular(keys[:, 1], s, pos(b, 1.5) + int(k * 0.012 * sr))

        # ---- baixo
        if not intro:
            add_circular(low, bass(midi(bass_note), spb * 1.6, 0.42, sr), pos(b, 0))
            second = bass_note + (7 if rng.random() < 0.5 else 12)
            add_circular(low, bass(midi(second), spb * 0.9, 0.3, sr), pos(b, 2.5))
            if rng.random() < 0.4:
                add_circular(low, bass(midi(bass_note), spb * 0.8, 0.28, sr), pos(b, 3.5))

        # ---- bateria boom-bap
        if not intro:
            kicks = [0.0, 1.75, 2.5] if not breakdown else []
            if b % 4 == 3 and not breakdown:
                kicks.append(3.5)
            for kb in kicks:
                p = pos(b, kb)
                kk = kick(sr, 0.85 * rng.uniform(0.9, 1.0))
                add_circular(drums[:, 0], kk, p)
                add_circular(drums[:, 1], kk, p)
                d = 1.0 - 0.35 * np.exp(-np.arange(int(0.3 * sr)) / sr * 9)   # sidechain suave
                seg = np.arange(len(d)) + p
                duck[seg % n] = np.minimum(duck[seg % n], d)
            for sb in (1.0, 3.0):
                sn = snare(sr, rng, 0.5 * rng.uniform(0.9, 1.0))
                add_circular(drums[:, 0], sn * 0.9, pos(b, sb))
                add_circular(drums[:, 1], sn, pos(b, sb))
            if rng.random() < 0.5:   # ghost note
                g = snare(sr, rng, 0.12)
                add_circular(drums[:, 0], g, pos(b, 3.75))
                add_circular(drums[:, 1], g, pos(b, 3.75))
            for e in range(8):
                hv = (0.22 if e % 2 == 0 else 0.13) * rng.uniform(0.7, 1.0)
                h = hat(sr, rng, hv, open_=(e == 7 and b % 2 == 1))
                add_circular(drums[:, 0], h * 0.7, pos(b, e / 2))
                add_circular(drums[:, 1], h, pos(b, e / 2))

        # ---- melodia esparsa de sininhos
        if b >= 8 and not breakdown:
            for e in range(8):
                if rng.random() < 0.16:
                    note = int(rng.choice(spec["scale"])) + 12
                    bl = bell(midi(note), spb * 2.5, 0.1 * rng.uniform(0.7, 1.0), sr)
                    add_circular(lead, pan_stereo(bl, rng.uniform(-0.5, 0.5)), pos(b, e / 2))

    t = np.arange(n) / sr
    mix = keys * duck[:, None] * 0.9 + pan_stereo(low * duck, 0.0) * 0.8 + drums * 0.75 + lead

    # ---- vinil: estalinhos + chiado
    crackle = np.zeros(n)
    count = int(n / sr * 7)
    idx = rng.integers(0, n, count)
    crackle[idx] = rng.uniform(-1, 1, count) * rng.uniform(0.2, 1.0, count) ** 3
    crackle = fft_filter(crackle, sr, bandpass(900, 7000)) * 0.6
    hiss = fft_filter(rng.standard_normal(n), sr, bandpass(2000, 8000)) * 0.004
    mix += pan_stereo(crackle + hiss, 0.0) * 0.9

    # ---- oscilação de fita (wow): LFO com número inteiro de ciclos no loop
    cycles = max(1, round(n / sr * 0.45))
    warp = np.arange(n) + np.sin(2 * np.pi * cycles * np.arange(n) / n) * 0.0022 * sr
    i0 = np.floor(warp).astype(int)
    frac = (warp - i0)[:, None]
    mix = mix[i0 % n] * (1 - frac) + mix[(i0 + 1) % n] * frac

    # ---- cor lo-fi: passa-baixa, sem sub-grave, saturação leve
    mix = fft_filter(mix, sr, lambda f: lowpass(4200, 2)(f) * highpass(38, 2)(f))
    mix = np.tanh(mix * 1.6) / np.tanh(1.6)
    return normalize(mix, 0.62)


# ------------------------------------------------------------------ efeitos
def sfx_all():
    sr = SR_SFX
    rng = np.random.default_rng(7)
    out = {}

    def T(d):
        return np.arange(int(d * sr)) / sr

    # máquina de café: bomba vibrando + vapor + gotas no final (≈1.4 s)
    t = T(1.5)
    pump = np.sign(np.sin(2 * np.pi * 50 * t)) * 0.25 + np.sin(2 * np.pi * 100 * t) * 0.2
    pump = fft_filter(pump, sr, lowpass(900)) * np.clip(t / 0.08, 0, 1) * np.clip((1.25 - t) / 0.1, 0, 1)
    steam = fft_filter(rng.standard_normal(len(t)), sr, bandpass(2500, 9000))
    steam = steam / np.max(np.abs(steam)) * 0.35 * np.clip((t - 0.35) / 0.3, 0, 1) * np.clip((1.3 - t) / 0.15, 0, 1)
    drips = np.zeros(len(t))
    for d0 in (1.18, 1.32, 1.43):
        s = int(d0 * sr)
        tt = np.arange(int(0.05 * sr)) / sr
        drips[s:s + len(tt)] += np.sin(2 * np.pi * (900 + 1400 * np.exp(-tt * 60)) * tt) * np.exp(-tt * 70) * 0.35
    out["coffee_brew"] = fade(pump + steam + drips, sr)

    # moedor: raspado granulado com pulsos da manivela (≈1.1 s)
    t = T(1.15)
    grain = fft_filter(rng.standard_normal(len(t)), sr, bandpass(700, 5000))
    grain /= np.max(np.abs(grain))
    crank = 0.55 + 0.45 * np.abs(np.sin(2 * np.pi * 3.2 * t))
    clicks = (rng.random(len(t)) < 0.004) * rng.uniform(0.3, 1.0, len(t))
    clicks = fft_filter(clicks, sr, bandpass(1500, 6000)) * 3
    sparkle = sum(np.sin(2 * np.pi * f * t) * np.exp(-((t - t0) ** 2) / 0.001) * 0.06
                  for f, t0 in ((2637, 0.3), (3136, 0.55), (3520, 0.8)))
    out["grinder"] = fade((grain * crank * 0.45 + clicks) * np.clip(t / 0.05, 0, 1) + sparkle, sr)

    # xícara tilintando
    t = T(0.7)
    clink = sum(a * np.sin(2 * np.pi * f * t) * np.exp(-t * dec) for f, a, dec in
                ((2350, 0.5, 9), (3170, 0.35, 12), (5120, 0.2, 18), (1180, 0.15, 7)))
    out["cup_clink"] = fade(clink * 0.8, sr, 0.001)

    # moedas: arpejo de sininhos
    t = T(1.0)
    coin = np.zeros(len(t))
    for i, note in enumerate((88, 92, 95, 100)):
        s = int(i * 0.07 * sr)
        b = bell(midi(note), 1.0 - i * 0.07, 0.4, sr)
        coin[s:s + len(b)] += b[:len(coin) - s]
    out["coin"] = fade(coin, sr, 0.001)

    # "Ahhh…" do cliente: voz sintetizada (pulso glotal + formantes da vogal "a")
    t = T(1.1)
    f0 = 300 * np.exp(-t * 0.35) + 10 * np.sin(2 * np.pi * 5.5 * t)
    ph = np.cumsum(f0) / sr
    glottal = (ph % 1.0) * 2 - 1
    glottal += 0.25 * rng.standard_normal(len(t))
    voice = fft_filter(glottal, sr, resonances([(820, 90, 1.0), (1250, 110, 0.6), (2900, 200, 0.25), (3600, 250, 0.1)]))
    voice = voice / np.max(np.abs(voice)) * np.clip(t / 0.07, 0, 1) * np.clip((1.1 - t) / 0.45, 0, 1)
    out["customer_ahh"] = fade(voice * 0.5, sr)

    # pedido errado: "hm-hmm" fofinho descendo
    t = T(0.55)
    wrong = np.zeros(len(t))
    for s0, f, d in ((0.0, 520, 0.2), (0.24, 390, 0.28)):
        s = int(s0 * sr)
        tt = np.arange(int(d * sr)) / sr
        tone = np.sin(2 * np.pi * f * tt) + 0.3 * np.sin(2 * np.pi * f * 2 * tt)
        wrong[s:s + len(tt)] += tone * np.sin(np.pi * tt / d) * 0.35
    out["wrong"] = fade(wrong, sr)

    # lixeira: sopro + batidinha
    t = T(0.6)
    whoosh = fft_filter(rng.standard_normal(len(t)), sr, bandpass(400, 3000))
    whoosh = whoosh / np.max(np.abs(whoosh)) * np.sin(np.pi * np.clip(t / 0.35, 0, 1)) * 0.35
    thud = np.zeros(len(t)); s = int(0.34 * sr); tt = np.arange(len(t) - s) / sr
    thud[s:] = np.sin(2 * np.pi * (140 * np.exp(-tt * 10) + 60) * tt) * np.exp(-tt * 18) * 0.6
    out["trash"] = fade(whoosh + thud, sr)

    # colocar decoração: "pop" + batida de madeira
    t = T(0.35)
    pop = np.sin(2 * np.pi * (300 + 900 * (1 - np.exp(-t * 40))) * t) * np.exp(-t * 22) * 0.45
    knock = fft_filter(rng.standard_normal(len(t)), sr, bandpass(600, 2500)) * np.exp(-t * 60)
    out["place"] = fade(pop + knock / np.max(np.abs(knock)) * 0.25, sr, 0.001)

    # guardar decoração: pop invertido (sobe e some)
    out["store"] = fade(out["place"][::-1] * np.linspace(0.3, 1, len(out["place"])), sr)

    # clique de UI
    t = T(0.08)
    out["ui_click"] = fade(np.sin(2 * np.pi * 1800 * t) * np.exp(-t * 80) * 0.35, sr, 0.001)

    # abrir/fechar construção: sopro que sobe / desce
    t = T(0.55)
    sweep = fft_filter(rng.standard_normal(len(t)), sr, bandpass(300, 4000))
    sweep /= np.max(np.abs(sweep))
    chime_up = sum(bell(midi(n), 0.4, 0.12, sr)[:len(t)] * 1 for n in (79,))
    open_ = sweep * np.sin(np.pi * t / 0.55) * 0.25
    open_[int(0.2 * sr):] += bell(midi(84), 0.35, 0.18, sr)[:len(t) - int(0.2 * sr)]
    out["build_open"] = fade(open_, sr)
    close = sweep[::-1] * np.sin(np.pi * t / 0.55) * 0.25
    close[int(0.2 * sr):] += bell(midi(79), 0.35, 0.18, sr)[:len(t) - int(0.2 * sr)]
    out["build_close"] = fade(close, sr)

    # fita: clique do cassete + rebobinada curtinha
    t = T(0.9)
    tape = np.zeros(len(t))
    for c0 in (0.0, 0.07):
        s = int(c0 * sr); tt = np.arange(int(0.04 * sr)) / sr
        tape[s:s + len(tt)] += fft_filter(rng.standard_normal(len(tt)), sr, bandpass(800, 6000)) * np.exp(-tt * 120) * 0.8
    s = int(0.25 * sr); tt = np.arange(len(t) - s) / sr
    whirr = np.sin(2 * np.pi * np.cumsum(180 + 900 * tt) / sr) * 0.12 + fft_filter(rng.standard_normal(len(tt)), sr, bandpass(1500, 5000)) * 0.08
    tape[s:] += whirr * np.sin(np.pi * tt / tt[-1])
    out["tape_switch"] = fade(tape, sr)

    # passos do Ro (3 variações): baque macio + tique metálico
    for i in range(3):
        t = T(0.18)
        f = 90 + 15 * i
        thump = np.sin(2 * np.pi * (f + 60 * np.exp(-t * 50)) * t) * np.exp(-t * 32) * 0.55
        tick = np.sin(2 * np.pi * (2400 + 300 * i) * t) * np.exp(-t * 90) * 0.08
        out[f"step_{i + 1}"] = fade(thump + tick, sr, 0.001)

    # cliente chegando: brilhinho "pop"
    t = T(0.6)
    arrive = np.zeros(len(t))
    for i, note in enumerate((84, 91)):
        s = int(i * 0.06 * sr)
        b = bell(midi(note), 0.5, 0.22, sr)
        arrive[s:s + len(b)] += b[:len(arrive) - s]
    out["customer_arrive"] = fade(arrive, sr, 0.001)

    # ingrediente adicionado: "blup"
    t = T(0.25)
    out["ingredient"] = fade(np.sin(2 * np.pi * (500 + 700 * t / 0.25) * t) * np.sin(np.pi * t / 0.25) * 0.35, sr)

    # nave passando ao fundo (6 s): ronco grave com efeito doppler
    t = T(6.0)
    center = 3.0
    doppler = 1 + 0.08 * np.tanh((center - t) * 1.2)
    ph = np.cumsum(55 * doppler) / sr
    hum = np.sin(2 * np.pi * ph) + 0.5 * np.sin(2 * np.pi * 2 * ph) + 0.25 * np.sin(2 * np.pi * 3.5 * ph)
    air = fft_filter(rng.standard_normal(len(t)), sr, bandpass(150, 1200))
    air /= np.max(np.abs(air))
    loud = np.exp(-((t - center) ** 2) / 2.2)
    ship = (hum * 0.35 + air * 0.25) * loud
    stereo = np.stack([ship * np.clip(1.2 - t / 4, 0.2, 1), ship * np.clip(t / 4, 0.2, 1)], axis=1)
    out["ship_pass"] = fade(stereo, sr, 0.2, 0.5)

    # ambiente do espaço (loop de 20 s): zumbido suave e ar
    t = T(20.0)
    n = len(t)
    pad = sum(np.sin(2 * np.pi * f * t + p) * a for f, a, p in ((55, 0.25, 0), (82.5, 0.15, 1), (110.2, 0.08, 2)))
    pad *= 0.8 + 0.2 * np.sin(2 * np.pi * 0.1 * t)
    air = fft_filter(rng.standard_normal(n), sr, bandpass(200, 1500)) * 0.35
    out["space_ambience"] = normalize(pad + air, 0.35)

    for name, data in out.items():
        peak = 0.9 if name not in ("space_ambience", "ship_pass") else np.max(np.abs(data))
        data = normalize(data, peak) if name not in ("space_ambience",) else data
        write_wav(os.path.join(OUT_SFX, name + ".wav"), data.astype(np.float64), sr)


if __name__ == "__main__":
    print("Músicas:")
    for spec in TRACKS:
        write_wav(os.path.join(OUT_MUSIC, spec["file"]), render_track(spec), SR_MUSIC)
    print("Efeitos:")
    sfx_all()
