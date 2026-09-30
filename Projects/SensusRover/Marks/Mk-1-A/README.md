# Mk. 1-A — Wired scanner

**Status:** In progress

Mk. 1-A establishes the first complete path from a physical measurement to
Sensus.

The first experiment read a distance from an HC-SR04 with an Arduino Uno, sent
it over USB serial, and visualized it as a single line in Sensus. The desktop
application now supports structured scan samples and keeps observations in
an in-memory session. A simulation stream provides alternative input without
hardware.

This gives me a concrete system through which to learn how the **hardware**,
**Arduino firmware**, **serial connection**, and **desktop application** fit
together, now including servo movement. Mapping remains a later step.

## First slice

**HC-SR04 → Arduino Uno → USB serial → Sensus → line**

The first working slice is complete. The application now separates acquisition
services from presentation and supports scanner handshakes, sessions, angular
scan visualization, and a timeline of measurement metrics.

## Current behavior

- USB serial and simulation use the same handshake and sample-processing path.
- A successful handshake creates a session and automatically starts acquisition.
- The scan view fits the configured coverage, shows collected scan points, and draws a
  line from the scanner to the latest observation. The inspector shows that
  observation's details. Point color shows whether a measurement is valid and
  within the sensor's range.
- The timeline shows distance, bearing, and sample status over a rolling
  15-second window, with a shared cursor for inspecting time.
- Stopping or disconnecting preserves the session. Starting again replaces it.
  **Clear Session** removes it while idle.
- The status bar shows `Idle`, `Connecting`, or `Active`. Connection errors,
  including handshake timeouts, appear in the scanner panel.

The [firmware](Mk-1-A.ino) measures HC-SR04 echo pulses with a 30 ms timeout and
reports `NoEcho` when no complete pulse is received. It adds a 100 ms delay after
each sample. Measurement and serial transmission take additional time.

The firmware moves the servo from -90 to +90 degrees in 1-degree steps.
After requesting each position, it waits 20 ms before taking a measurement.
This pause gives the servo time to move. Each sample reports the requested
angle.

The scanner settings shown in the desktop application control the simulation
only. They are not sent to the physical scanner.

## Hardware and connections

The component references are the checked-in datasheets:

- [HC-SR04](<../../../../Docs/Datasheets/HC-SR04 Ultrasonic Sensor Module.pdf>):
  5 V ultrasonic ranging module, specified range 2 cm to 4 m and measuring
  angle 15 degrees. A pulse of at least 10 microseconds triggers a measurement.
- [SG90](<../../../../Docs/Datasheets/SG90 Servo Motor.pdf>): approximately
  180 degrees of rotation for orienting the sensor.
- [ELEGOO UNO R3](<../../../../Docs/Datasheets/ELEGOO UNO R3 Board.pdf>):
  5 V controller with a 16 MHz clock and USB serial connectivity.

The firmware assigns the HC-SR04 trigger to digital pin 9, echo to digital
pin 10, and servo control to digital pin 11.

Sensus calls straight ahead 0 degrees, with left at -90 and right at +90.
The servo library uses 0 to 180 instead, so the firmware adds 90 when sending
an angle: left becomes 0, straight ahead 90, and right 180.

The sensor measures within a 15-degree cone at each position. The servo turns
that cone across the scanner's 180-degree sweep.

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
Disconnecting closes the PC connection. It does not send a command to
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

Recording and replay, wireless communication, rover motion, localization, and
mapping are deliberately left for later stages.
