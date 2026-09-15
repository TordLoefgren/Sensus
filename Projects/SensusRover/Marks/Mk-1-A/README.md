# Mk. 1-A — Wired scanner

**Status:** In progress

Mk. 1-A establishes the first complete path from a physical measurement to
Sensus.

The first experiment is intentionally small: read a distance from an HC-SR04
with an Arduino Uno, send it over USB serial, and visualize it as a single line
in Sensus.

This gives me a concrete system through which to learn how the **hardware**,
**Arduino firmware**, **serial connection**, and **desktop application** fit
together before adding servo movement or mapping.

## First slice

**HC-SR04 → Arduino Uno → USB serial → Sensus → line**

Once this works, I plan to separate the acquisition and presentation logic into
testable services and viewmodels before adding the SG90 and angular sweeps.

## Scope

Mk. 1-A will explore:

- GPIO and pulse timing
- HC-SR04 measurements and invalid echoes
- Arduino firmware and flashing
- UART and USB serial
- Windows COM ports
- message framing
- timestamps and sequence numbers
- servo bearing and sweeps
- recording and replay

Wireless communication, rover motion, localization, and mapping are deliberately
left for later stages.
