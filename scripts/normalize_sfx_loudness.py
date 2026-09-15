#!/usr/bin/env python3
"""Normalize SFX loudness in Assets/Resources/UI/sound to a unified active-RMS target.

Uses numpy/scipy: reads each WAV, measures active RMS (50ms frames above floor),
applies linear gain, then a soft limiter to keep peaks below -1 dBFS.
Excludes BGM wav files. Originals are untouched (backup lives in _codex_backups).
"""

import glob
import os
import sys

import numpy as np
import scipy.io.wavfile as wavfile


SOUND_DIR = r"D:\UnityDemo\LeihuoDemo\Assets\Resources\UI\sound"
TARGET_ACTIVE_RMS_DB = -18.0
PEAK_CEILING = 0.891  # -1 dBFS
MIN_GAIN = 0.1
MAX_GAIN = 8.0


def to_float(data, sample_width=None):
    if data.dtype == np.int16:
        return data.astype(np.float64) / 32768.0
    if data.dtype == np.int32:
        return data.astype(np.float64) / 2147483648.0
    if data.dtype == np.uint8:
        return (data.astype(np.float64) - 128.0) / 128.0
    if data.dtype == np.float32 or data.dtype == np.float64:
        return data.astype(np.float64)
    raise ValueError(f"unsupported dtype: {data.dtype}")


def to_int16(x):
    x = np.clip(x, -1.0, 1.0)
    return (x * 32767.0).astype(np.int16)


def active_rms_db(x, rate):
    frame_len = max(1, int(rate * 0.05))
    if x.size < frame_len:
        frame_rms = np.array([np.sqrt(np.mean(x * x))])
    else:
        n = x.size // frame_len
        frames = x[: n * frame_len].reshape(n, frame_len)
        frame_rms = np.sqrt(np.mean(frames * frames, axis=1))
    active = frame_rms[frame_rms > 1e-4]
    if active.size == 0:
        return -120.0
    return 20.0 * np.log10(np.sqrt(np.mean(active * active)) + 1e-12)


def soft_limit(x, ceiling=PEAK_CEILING):
    ax = np.abs(x)
    over = ax - ceiling
    limited = ceiling + (1.0 - ceiling) * np.tanh(over / max(ceiling, 1e-6))
    return np.where(ax > ceiling, np.sign(x) * limited, x)


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    files = sorted(glob.glob(os.path.join(SOUND_DIR, "*.wav")))
    targets = []
    for path in files:
        name = os.path.basename(path)
        if name in (
            "主世界BGM.wav",
            "SpecialWorldBGM.wav",
            "恐怖气氛（SpecialWorld的背景音）.wav",
        ):
            continue
        targets.append(path)

    print(f"normalizing {len(targets)} sfx files to active RMS {TARGET_ACTIVE_RMS_DB} dBFS ...")
    for path in targets:
        name = os.path.basename(path)
        try:
            rate, data = wavfile.read(path)
        except Exception as exc:
            print(f"SKIP {name}: {exc}")
            continue

        x_all = to_float(data)
        mono = x_all if x_all.ndim == 1 else x_all.mean(axis=1)
        before = active_rms_db(mono, rate)
        gain = float(np.clip(10.0 ** ((TARGET_ACTIVE_RMS_DB - before) / 20.0), MIN_GAIN, MAX_GAIN))

        scaled = x_all * gain
        limited = soft_limit(scaled)
        out = to_int16(limited)
        wavfile.write(path, rate, out)

        _, check = wavfile.read(path)
        check_float = to_float(check)
        check_mono = check_float if check_float.ndim == 1 else check_float.mean(axis=1)
        after = active_rms_db(check_mono, rate)
        peak = float(np.max(np.abs(check_float)))
        peak_db = 20.0 * np.log10(peak + 1e-12)
        print(f"{name:<32} {before:>6.1f}dB -> {after:>6.1f}dB  gain={gain:.2f} peak={peak_db:>5.1f}dB")

    print("done.")


if __name__ == "__main__":
    main()
