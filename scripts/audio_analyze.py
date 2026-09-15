#!/usr/bin/env python3
"""Analyze WAV loudness: duration, peak dBFS, overall RMS dBFS, active RMS dBFS."""

import glob
import math
import os
import sys

import numpy as np
import wave

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")


def analyze_wav(path):
    with wave.open(path, "rb") as w:
        channels = w.getnchannels()
        sample_width = w.getsampwidth()
        rate = w.getframerate()
        frames = w.getnframes()
        data = w.readframes(frames)

    dtype = {1: np.int8, 2: np.int16, 4: np.int32}.get(sample_width)
    if dtype is None:
        return None
    samples = np.frombuffer(data, dtype=dtype).astype(np.float64)
    if channels > 1:
        samples = samples.reshape(-1, channels).mean(axis=1)
    max_val = 2.0 ** (8 * sample_width - 1)
    samples /= max_val

    duration = frames / rate
    peak = float(np.max(np.abs(samples))) if samples.size else 0.0
    peak_db = 20.0 * math.log10(peak + 1e-12) if peak > 1e-6 else -120.0

    def rms_db(x):
        rms = float(np.sqrt(np.mean(x * x))) if x.size else 0.0
        return 20.0 * math.log10(rms + 1e-12) if rms > 1e-6 else -120.0

    overall_db = rms_db(samples)

    # Active RMS: frames above a floor, weighted by frame length.
    frame_len = int(rate * 0.05)
    frame_len = max(1, frame_len)
    if samples.size < frame_len:
        frame_rms = np.array([np.sqrt(np.mean(samples * samples))])
    else:
        n_frames = samples.size // frame_len
        trimmed = samples[: n_frames * frame_len].reshape(n_frames, frame_len)
        frame_rms = np.sqrt(np.mean(trimmed * trimmed, axis=1))
    active = frame_rms[frame_rms > 1e-4]  # > -80 dBFS
    active_db = rms_db(active) if active.size else -120.0

    return {
        "duration": duration,
        "peak_db": peak_db,
        "rms_db": overall_db,
        "active_db": active_db,
    }


def main():
    roots = [
        r"D:\UnityDemo\LeihuoDemo\Assets\Resources\UI\sound",
        r"D:\unity资料\雷火图片资源\Music\处理好的music",
    ]
    files = []
    for root in roots:
        files.extend(glob.glob(os.path.join(root, "*.wav")))
    files = sorted(set(files))

    print(f"{'file':<50} {'dur':>7} {'peak':>7} {'rms':>7} {'act':>7}")
    for path in files:
        name = os.path.basename(path)
        if name in ("主世界BGM.wav", "SpecialWorldBGM.wav", "恐怖气氛（SpecialWorld的背景音）.wav"):
            kind = "(bgm)"
        else:
            kind = ""
        res = analyze_wav(path)
        if res is None:
            print(f"{name:<50} unsupported")
            continue
        print(
            f"{name:<50} {res['duration']:>7.2f} {res['peak_db']:>7.1f} "
            f"{res['rms_db']:>7.1f} {res['active_db']:>7.1f} {kind}"
        )


if __name__ == "__main__":
    main()
