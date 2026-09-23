# Mk. 1-A — Wired scanner

**Status:** In progress

Mk. 1-A establishes the first complete path from a physical measurement to
Sensus.

The first experiment read a distance from an HC-SR04 with an Arduino Uno, sent
it over USB serial, and visualized it as a single line in Sensus. The desktop
application now supports structured scan samples and retains observations in
an in-memory session. A simulation stream provides alternative input without
hardware.

This gives me a concrete system through which to learn how the **hardware**,
**Arduino firmware**, **serial connection**, and **desktop application** fit
together before adding servo movement or mapping.

## First slice

**HC-SR04 → Arduino Uno → USB serial → Sensus → line**

The first working slice is complete. The current work adds scanner handshakes,
sessions, and angular scan visualization. Acquisition and presentation logic
will be separated into services and viewmodels before the milestone is finished.

## Current behavior

- USB serial and simulation use the same handshake and sample-processing path.
- A successful handshake creates a session and automatically starts acquisition.
- The viewport retains scan points and highlights the latest observation.
- Stopping or disconnecting retains the session. Starting again replaces it;
  **Clear Session** removes it while idle.
- The status bar shows `Idle`, `Connecting`, or `Active`. Connection errors,
  including handshake timeouts, appear in the scanner panel.

The [firmware](Mk-1-A.ino) measures HC-SR04 echo pulses with a 30 ms timeout and
reports `NoEcho` when no complete pulse is received. It adds a 100 ms delay after
each sample; measurement and serial transmission take additional time.

The reported bearing still advances in software without driving the SG90, so the
serial input does not yet represent a physical angular sweep. The desktop
simulation uses the displayed scanner configuration; these settings are not
sent to the firmware.

## Serial protocol

The current link uses 9600 baud and CRLF-terminated text lines:

```text
Sensus  -> scanner: HELLO
Scanner -> Sensus:  HELLO BACK
Sensus  -> scanner: START
Scanner -> Sensus:  sequence,sweepId,elapsedUs,bearingDegrees,roundTripDurationUs,status
```

Sensus waits up to three seconds for the handshake reply, then sends `START`
automatically. The firmware accepts `HELLO` while running: it resets scanner
fields and pauses output until another `START`. There is no stop command yet.
Disconnecting closes the PC connection; it does not itself send a command to
stop the firmware.

See [Concepts](../../../../Docs/concepts.md) for sample fields, units, and session
lifetime. Manual priming, calibration, and separate start controls are future work.

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
