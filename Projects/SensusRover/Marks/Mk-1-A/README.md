# Mk. 1-A — Wired scanner

**Status:** In progress

Mk. 1-A establishes the first complete path from a physical measurement to
Sensus.

The first experiment is intentionally small: read a distance from an HC-SR04
with an Arduino Uno, send it over USB serial, and visualize it as a single line
in Sensus. A simulation stream can provide alternative input by generating
synthetic samples.

This gives me a concrete system through which to learn how the **hardware**,
**Arduino firmware**, **serial connection**, and **desktop application** fit
together before adding servo movement or mapping.

## First slice

**HC-SR04 → Arduino Uno → USB serial → Sensus → line**

The first working slice is complete. The next step is to add the SG90 servo motor and
angular sweeps. After that, the acquisition and presentation logic will be
separated into services and viewmodels.

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
