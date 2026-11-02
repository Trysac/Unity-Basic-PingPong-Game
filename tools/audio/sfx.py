"""Sound effect recipes for Retro Rally. Each function returns a rendered Track."""

from synth import Track, note_freq, saw, square, triangle


def pulse(duty):
    return lambda p: square(p, duty)


def paddle():
    """The classic paddle 'ping'. The game raises its pitch as the ball speeds up."""
    t = Track(0.16)
    t.add_tone(0, 0.06, note_freq("A5"), pulse(0.5), 0.3, attack=0.001, decay=0.03, sustain=0.6, release=0.06)
    t.add_tone(0, 0.03, note_freq("A6"), pulse(0.25), 0.08, release=0.02)
    return t


def wall():
    """A softer, lower blip off the top and bottom walls."""
    t = Track(0.12)
    t.add_tone(0, 0.04, note_freq("D5"), pulse(0.5), 0.22, attack=0.001, release=0.05)
    return t


def serve():
    """A short rising whoosh as the ball leaves the paddle."""
    t = Track(0.25)
    t.add_tone(0, 0.14, 300, triangle, 0.25, freq_end=900, release=0.05)
    t.add_noise(0, 0.12, 0.12, smoothing=0.5, decay_power=1.2, seed=1)
    return t


def goal():
    """A point is scored: a crunchy burst under a falling buzz."""
    t = Track(0.7)
    t.add_noise(0, 0.35, 0.45, smoothing=0.6, decay_power=1.5, seed=2)
    t.add_tone(0, 0.3, 520, pulse(0.25), 0.22, freq_end=130, release=0.12)
    t.add_tone(0, 0.2, 110, triangle, 0.5, freq_end=45, release=0.1)
    return t


def match_point():
    """A two-note warning when a player is one point from winning."""
    t = Track(0.8)
    for at, name in [(0.0, "E5"), (0.18, "E5"), (0.36, "B5")]:
        t.add_tone(at, 0.12, note_freq(name), pulse(0.125), 0.2, release=0.12)
    return t


def pickup_spawn():
    """A power-up appears: a soft sparkle."""
    t = Track(0.5)
    for i, name in enumerate(["C6", "G6", "E6"]):
        t.add_tone(i * 0.05, 0.06, note_freq(name), triangle, 0.18, release=0.2)
    return t


def grow():
    """Big paddle: a rising arpeggio."""
    t = Track(0.6)
    for i, name in enumerate(["C5", "E5", "G5", "C6", "E6"]):
        t.add_tone(i * 0.05, 0.08 if i < 4 else 0.25, note_freq(name), pulse(0.25), 0.18, release=0.1)
    return t


def shrink():
    """Shrink ray: a falling zap."""
    t = Track(0.5)
    t.add_tone(0, 0.32, 1400, pulse(0.125), 0.2, freq_end=180, release=0.08, vibrato=0.03)
    t.add_tone(0.02, 0.3, 700, saw, 0.08, freq_end=90, release=0.08)
    return t


def multiball():
    """Multi-ball: three quick pings that spread apart in pitch."""
    t = Track(0.5)
    for i, name in enumerate(["A5", "C#6", "E6"]):
        t.add_tone(i * 0.07, 0.07, note_freq(name), pulse(0.5), 0.2, release=0.1)
    t.add_noise(0, 0.08, 0.1, smoothing=0.2, decay_power=2.0, seed=3)
    return t


def win():
    """The match is won: a bright fanfare."""
    t = Track(1.6)
    for name, at, length in [("C5", 0.0, 0.1), ("E5", 0.12, 0.1), ("G5", 0.24, 0.1),
                             ("C6", 0.36, 0.2), ("G5", 0.6, 0.1), ("C6", 0.72, 0.6)]:
        t.add_tone(at, length, note_freq(name), pulse(0.25), 0.22, release=0.15)
    t.add_tone(0.72, 0.6, note_freq("C3"), triangle, 0.45, release=0.3)
    return t


def lose():
    """The CPU wins the match: a slow falling line."""
    t = Track(1.8)
    for name, at, length in [("G4", 0.0, 0.25), ("F#4", 0.3, 0.25), ("F4", 0.6, 0.25), ("E4", 0.9, 0.7)]:
        t.add_tone(at, length, note_freq(name), triangle, 0.35, release=0.2, vibrato=0.006)
    t.add_tone(0.9, 0.7, note_freq("C2"), triangle, 0.4, release=0.3)
    return t


def move():
    """Menu cursor."""
    t = Track(0.06)
    t.add_tone(0, 0.02, 1100, pulse(0.5), 0.14, release=0.02)
    return t


def select():
    """Menu confirm."""
    t = Track(0.16)
    t.add_tone(0, 0.04, note_freq("E6"), pulse(0.25), 0.18, release=0.02)
    t.add_tone(0.05, 0.06, note_freq("B6"), pulse(0.25), 0.18, release=0.05)
    return t


def pause():
    """The game pauses or resumes."""
    t = Track(0.25)
    t.add_tone(0, 0.08, note_freq("E5"), triangle, 0.3, release=0.05)
    t.add_tone(0.09, 0.08, note_freq("B4"), triangle, 0.3, release=0.08)
    return t


EFFECTS = {
    "paddle.ogg": paddle,
    "wall.ogg": wall,
    "serve.ogg": serve,
    "goal.ogg": goal,
    "match_point.ogg": match_point,
    "pickup_spawn.ogg": pickup_spawn,
    "grow.ogg": grow,
    "shrink.ogg": shrink,
    "multiball.ogg": multiball,
    "win.ogg": win,
    "lose.ogg": lose,
    "move.ogg": move,
    "select.ogg": select,
    "pause.ogg": pause,
}
