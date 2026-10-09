# Concepts

This document defines current Sensus terminology, units, and data conventions.

It is intended as a concise shared reference for the codebase and development
tools.

Design discussion, experiments, development history, and future ideas belong in
project documentation or issues rather than here.

## Project progression

A **mark** is a numbered Sensus Rover designation such as `Mark 1`. Each mark
represents a **generation**. A **stage** is a lettered step within a mark, such
as `Mark 1-A`. **Revision** is reserved for independently versioned technical
formats and contracts, currently the scanner protocol. Marks and stages identify
project progression. Protocol revisions identify the protocol. They can change
independently.

## Project and rover

**Sensus Rover** is the overall physical project. A **rover** is a mobile
platform. The current physical scanner is a distinct subsystem.

## Scanner

A **scanner** is the complete subsystem responsible for orienting a range sensor,
performing acquisitions, and producing `RangeSample` values.

The current physical scanner is:

```text
Scanner
├── ELEGOO UNO R3
├── SG90 servo
└── HC-SR04 range sensor
```

## Scanner simulation

A **scanner simulation** models scanner behaviour without physical hardware.

The simulation produces the same protocol and sample model expected from a
physical scanner.

Sample generation, serialization, transport, and application processing are
separate responsibilities.

```text
simulation
    ↓
RangeSample
    ↓
serialization
    ↓
stream
    ↓
Sensus
```

## Scanner source

A **scanner source** presents scanner behaviour to Sensus. The current sources
are the physical scanner and the scanner simulation.

## Acquisition pipeline

The **acquisition pipeline** is the shared application flow that handles scanner
communication and incoming samples, turns samples into observations, and stores
them in the current session. The physical scanner and simulation use the same
pipeline. This term describes the flow, not the `AcquisitionService` class alone.

## Transport

A **transport** carries scanner protocol data between a scanner source and
Sensus. The current physical scanner uses USB serial. The scanner protocol is
defined independently of the transport.

## Bearing

Sensus defines **bearing** as a direction relative to the scanner's forward
direction. Zero is forward, negative is left, and positive is right. Viewed
from above, counterclockwise decreases bearing and clockwise increases bearing.

```text
-80° = configured left limit
  0° = forward
+80° = configured right limit
```

Protocol and domain samples store bearing in degrees as `BearingDegrees`.

`RangeObservation` may derive radians for calculations.

`BearingDegrees` is the target scanner bearing used for an acquisition.

The physical scanner does not independently measure its bearing: the SG90
provides no position feedback to the UNO. Firmware maps the target scanner
bearing to the servo control pulse. Sensus and `RangeSample` use bearing, not
pulse width.

## Sweep

A **sweep** is one traversal toward a configured angular boundary. The
turnaround endpoint belongs to the sweep that reached it.

```text
Sweep 1: -80° → +80°
Sweep 2: +79° → -80°
```

`SweepId` increases after sampling a turnaround endpoint. The next sweep starts
one step inside that endpoint, so the endpoint is not sampled twice in a row.

## Acquisition

An **acquisition** is one attempt to obtain a range measurement at a particular
bearing and elapsed time.

An acquisition still exists when no valid echo is received.

## Session

A **session** represents one acquisition run and owns its observations.

Currently:

- a new session is created after a successful `PREPARE` exchange and before
  acquisition begins
- stopping the source does not discard the session
- starting another run replaces the current session
- clearing the current session is explicit
- sessions are stored only in memory

Transport lifetime and session lifetime are separate.

## RangeSample

A `RangeSample` is the scanner's reported result of one acquisition.

```text
RangeSample
├── Sequence
├── SweepId
├── ElapsedUs
├── BearingDegrees
├── RoundTripDurationUs
└── Status
```

A sample contains scanner-reported data.

Derived values should not be serialized when they can be reproduced from the
sample.

## RangeObservation

A `RangeObservation` is Sensus' interpretation of a `RangeSample`.

It keeps the original sample and derives values used by the application.

```text
RangeObservation
├── Sample
├── ElapsedSeconds
├── BearingRadians
├── DistanceCm
├── PositionXCm
├── PositionYCm
└── RangeStatus
```

```text
Scanner                    Sensus
   │                          │
   └── RangeSample ──────────→│
                              ↓
                       RangeObservation
```

## Sequence

`Sequence` is a monotonically increasing acquisition number.

It increments for every acquisition attempt, including `NoEcho`.

Sequence numbering starts at `1` when run state is reset by `PREPARE`.

## SweepId

`SweepId` identifies the sweep during which a sample was acquired.

All samples within one angular traversal use the same ID.

## ElapsedUs

`ElapsedUs` is the source's elapsed time in microseconds.

It is not wall-clock time.

The physical scanner uses the UNO's `micros()` clock. The simulation uses its
own elapsed-time source.

Real-world session date/time, if needed later, is separate metadata.

## Sample status

Current sample statuses are:

```text
0 = Valid
1 = NoEcho
```

`Valid` means an echo duration was reported.

`NoEcho` means no echo completed before the configured timeout and
`RoundTripDurationUs` is zero.

## RoundTripDurationUs

`RoundTripDurationUs` is the measured ultrasonic round-trip time in
microseconds.

Sensus derives range from this value.

The scanner reports the measured duration rather than the derived distance.

## Scanner protocol revision

The current scanner protocol revision is `1`.

The protocol revision identifies the scanner message format. A Sensus Rover
mark and stage identify project progression, while the transport carries the
protocol data. Each can change independently.

Protocol lines use CRLF termination.

A normal revision 1 lifecycle is:

```text
PREPARE
    ↓
DESCRIPTION
scanner description
configuration
READY
    ↓
START
    ↓
samples
    ↓
STOP
    ↓
STOPPED
```

`PREPARE` resets scanner run state and requests its description.

`READY` completes the description.

Sensus validates the response before sending `START`.

`STOP` ends acquisition.

A new run requires another `PREPARE`.

## Units

Unless explicitly stated otherwise:

| Value | Unit |
| --- | --- |
| Bearing | degrees |
| Range | centimetres |
| Round-trip duration | microseconds |
| Elapsed time | microseconds |
| Acquisition delay | milliseconds |
| Echo timeout | microseconds |
