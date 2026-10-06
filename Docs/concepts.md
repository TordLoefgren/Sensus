# Concepts

This document describes the project's concepts, precise definitions, and naming
conventions. It provides a shared vocabulary for the application and its code.
Open questions, proposed changes, and decision discussions are tracked in
GitHub issues.

The terminology below describes the current Mk. 1-A software and its intended
hardware model. Future concepts are identified separately. Sample structures,
field names, and conventions may still change as the experiments establish what
is needed.

## Sensus terminology

### Hardware

The Mk. 1-A scanner uses:

* [HC-SR04 ultrasonic ranging sensor](<Datasheets/HC-SR04 Ultrasonic Sensor Module.pdf>)
* [SG90 servo motor](<Datasheets/SG90 Servo Motor.pdf>)
* [ELEGOO UNO R3](<Datasheets/ELEGOO UNO R3 Board.pdf>)

The firmware tells the servo where to point, then measures how long the sensor
echo takes to return.

### Scanner

A scanner is the complete unit responsible for orienting a sensor, initiating
measurements, and producing samples.

For the initial prototype, the scanner consists of the ELEGOO UNO R3, a servo,
and an ultrasonic sensor.

### Scanner simulation

A scanner simulation represents the behaviour of the complete scanner rather
than only the range sensor or microcontroller. It may therefore model the
combined behaviour of the controller, servo, and ranging sensor while producing
the same samples that a physical scanner would expose to Sensus.

The simulation generator produces structured domain data such as `RangeSample`
values. It should remain independent of how those samples are serialized or
transported. Serialization and stream behaviour belong to separate layers.

```text
Scanner simulation
        ↓
RangeSample
        ↓
Range-sample serialization
        ↓
Transport / stream
        ↓
Sensus
```

A `RangeSample` describes one range acquisition and the metadata required to
interpret it. Its bearing may come from the scanner's servo while its
round-trip duration comes from the ranging sensor, so the sample already
represents information aggregated from multiple scanner components.

Future rover motion, localization, camera data, battery telemetry, and similar
information should not automatically be added to `RangeSample`. These describe
other parts of the larger device and may instead be represented as separate,
time-correlated data sources. A future rover simulation can compose those
sources while keeping the scanner as a distinct subsystem.

```text
Rover
├── Scanner
│   ├── Servo
│   └── Range sensor
├── Motion / localization
└── Camera
```

### Bearing

A bearing is the direction in which the sensor is facing, expressed as an angle
relative to the scanner's forward direction.

* `-90°` = left
* `0°` = forward
* `+90°` = right

The reported bearing is the angle the firmware asked the servo to move to.
The SG90 does not provide position feedback to the board, so the firmware
cannot read its actual angle.

Samples and the serial protocol store the bearing in degrees as `BearingDegrees`.
`RangeObservation` derives `BearingRadians` for trigonometric calculations.

### Sweep

A sweep is one complete angular traversal from one configured boundary to
another.

```text
Sweep 1: -90° → +90°
Sweep 2: +90° → -90°
Sweep 3: -90° → +90°
```

A sweep may run in either direction. Acquiring samples in both directions is
called bidirectional or ping-pong scanning.

### Scan cycle

A scan cycle is a complete outward-and-return movement:

```text
-90° → +90° → -90°
```

One scan cycle therefore consists of two sweeps. Sensus primarily uses the
sweep rather than the scan cycle as its unit of grouping.

### Acquisition

An acquisition is one attempt to obtain sensor data at a particular elapsed
time and bearing. An acquisition occurs even when no valid echo is detected.

For each trigger, the HC-SR04 automatically emits an eight-cycle, 40 kHz
ultrasonic burst. The complete burst-and-echo operation constitutes one
acquisition attempt.

### Session

A session represents one acquisition run and owns the observations produced
during that run. It is independent of the lifetime of the source that produced
the data.

For Mk. 1-A, a new session is created automatically after the handshake succeeds,
just before the application sends `START`.
Stopping or disconnecting the source ends acquisition but does not destroy the
session. Its observations remain available in memory for inspection and
visualization. Starting a new acquisition creates a new session, replacing the
previous one, while clearing the current session is an explicit operation.

```text
Start acquisition
        ↓
New session
        ↓
RangeObservation
RangeObservation
RangeObservation
        ↓
Stop acquisition
        ↓
Session remains available
```

Persistence, replay, and session editing are outside the current session model.
The session boundary is intended to allow those capabilities to be added later
without coupling session lifetime to a serial connection, simulation, or other
input source.

### Handshake and acquisition

Serial and simulation use the same CRLF-terminated message format. This example
uses the Mk. 1-A scanner's values:

```text
Sensus  -> scanner: SENSUS,1,PREPARE
Scanner -> Sensus:  SENSUS,1,DESCRIPTION
Scanner -> Sensus:  SCANNER,Sensus Rover,Mk. 1-A
Scanner -> Sensus:  BOARD,ELEGOO UNO R3,ATmega328
Scanner -> Sensus:  RANGE_SENSOR,HC-SR04,2,400,15
Scanner -> Sensus:  SERVO,SG90,180
Scanner -> Sensus:  CONFIGURATION,-90,90,1,100,30000
Scanner -> Sensus:  SENSUS,1,READY
Sensus  -> scanner: SENSUS,1,START
Scanner -> Sensus:  1,1,1250000,-90,5800,0
Sensus  -> scanner: SENSUS,1,STOP
Scanner -> Sensus:  SENSUS,1,STOPPED
```

Opening a connection gives Sensus access to the transport. The scanner receives
`SENSUS,1,PREPARE` as a request to reset and return to the starting bearing.
`SENSUS,1,DESCRIPTION` starts the handshake response. The following rows
describe the scanner and its active configuration.
Range values are in centimetres, angles in degrees, acquisition delay in
milliseconds, and echo timeout in microseconds. `SENSUS,1,READY` ends the
response after the scanner commands the starting bearing and allows time for
the servo to settle. Sensus validates the complete response, then automatically
sends `SENSUS,1,START`. Missing,
malformed, or unsupported responses prevent acquisition. Manual priming and
starting are not implemented yet. The three-second handshake timeout covers the
entire response.

In the firmware, `PREPARE` resets scanner fields and disables sample output until
`START` is received. This is a protocol reset, not a microcontroller reboot.
Revision 1 identifies the wire format independently of the scanner's hardware
mark. The simulation reports its selected configuration through the same format.
A simulation stream is created anew for each run. Connection, device, and session
lifetimes are distinct. On a normal stop or disconnect, Sensus sends `STOP` and
waits up to two seconds for `STOPPED` before closing the connection. `STOP` halts
sample output and resets run state. Another run requires `PREPARE`. A lost
connection cannot deliver `STOP`. Stopping acquisition does not discard the
session's observations.

### Range sample

A `RangeSample` is the scanner's reported acquisition result, including its
status and the metadata needed to interpret it. It provides the information
required to derive distance, or indicates that no valid echo was received.

For the HC-SR04, the sensor-specific measurement is the complete round-trip
duration.

```text
RangeSample
├── Sequence
├── SweepId
├── ElapsedUs
├── BearingDegrees
├── RoundTripDurationUs
└── Status
```

The term range is not specific to ultrasonic sensors. It may also apply to
LiDAR, radar, and Time-of-Flight sensors.

### Range observation

A range observation is Sensus' interpreted representation of a `RangeSample`.

It keeps the original sample and adds values that can be derived from it,
such as range, bearing in radians, Cartesian position, and the interpreted
range state.

```text
RangeObservation
├── Sample
│   └── RangeSample
├── ElapsedSeconds
├── BearingRadians
├── DistanceCm
├── PositionXCm
├── PositionYCm
└── RangeStatus
```

The distinction is intentional:

```text
Scanner                         Sensus
   │                               │
   └── RangeSample ───────────────→│
                                   ↓
                            RangeObservation
```

A `RangeSample` represents what the scanner reported. A `RangeObservation`
represents what Sensus can derive and interpret from that sample.

Derived values should generally not be serialized back into the scanner
protocol when they can be reproduced from the original sample.

### Sequence

A sequence number is a monotonically increasing number assigned to every
acquisition attempt. It increments even when no valid measurement is produced.

```text
Sequence 100: Valid
Sequence 101: NoEcho
Sequence 102: Valid
```

The firmware sequence starts at one on reboot or receipt of `PREPARE`. A fresh
simulation enumeration also starts at one.

### Sweep ID

A sweep ID identifies the sweep during which a sample was acquired. All samples
produced during the same angular traversal share the same sweep ID.

The sweep ID increases whenever the scanner changes sweep direction.

### Elapsed time

Elapsed time is a timestamp in microseconds from the source's clock. The current
firmware reads `micros()` just before writing each sample, so its origin is the
board's current execution, not the latest handshake. Resetting the scanner fields
does not reset that clock. The simulation measures time from the start of its
generator enumeration.

```text
ElapsedUs = 1,523,418
```

means approximately 1.523418 seconds after that source's time origin.

Elapsed time is stored as an unsigned 32-bit microsecond value (`uint`) in
`RangeSample`, matching the UNO R3's `unsigned long`:

```text
ElapsedUs
```

Sensus may derive other representations for inspection and display:

```text
ElapsedSeconds = ElapsedUs / 1,000,000
```

Elapsed time is not a wall-clock date or time. If Sensus later needs to record
when an acquisition session occurred in real-world time, that should be stored
separately from the sample's monotonic elapsed time.

### Status

The status describes the outcome of an acquisition and determines whether its
measurement is valid.

Initial statuses are:

* `Valid`
* `NoEcho`

Additional statuses may be introduced later if needed.

The scanner sends the status as the final field of each sample line: `0` for
`Valid` and `1` for `NoEcho`. The simulation uses the same format. `Valid`
indicates a returned echo. `NoEcho` indicates that no echo was detected before
the timeout and carries a zero `RoundTripDurationUs`.

Sensus keeps both kinds of samples in the timeline and inspector. It derives
distance, position, and range state when a `Valid` sample has a nonzero
duration. The scan plots observations with derived positions.

### Round-trip duration

The round-trip duration is the measured time between emitting the ultrasonic
pulse and detecting its return.

The complete round-trip duration is stored in the sample. Distance is derived
later by accounting for the outward-and-return path travelled by the sound.

HC-SR04 material commonly calls this value the echo duration or ECHO pulse
width because it is measured from the high time of the module's ECHO pin.
Sensus uses `RoundTripDurationUs` because it describes the meaning of the
value independently of that hardware interface.

### Measurement

A measurement is the sensor-specific payload contained in a sample. Examples
include:

* Ultrasonic round-trip duration
* LiDAR distance and intensity
* Temperature
* Acceleration

A measurement represents information directly obtained from the sensing
process. Values calculated from a measurement, such as distance derived from
an ultrasonic round-trip duration, are derived data rather than additional raw
measurements.

### Range

A range is the estimated distance between the sensor and a detected target.

For the HC-SR04, range is derived from the measured round-trip duration and the
assumed speed of sound.

### Hit

A hit is a derived spatial detection at a particular bearing and range.

A hit represents the location of a detected target relative to the sensor or
scanner and may later be used for visualization, aggregation, or occupancy-grid
updates.

### Frame

A frame is a collection of measurements produced by a single acquisition.

The HC-SR04 produces one measurement per acquisition. A multi-zone sensor may
instead produce a frame containing many range measurements.

## Future terminology — occupancy grids

Occupancy grids are planned for a later stage, after Mk. 1-A. These definitions
describe the intended conventions for that work. No grid model is implemented
in the current application.

### Grid size

The grid size is its complete physical width and height. The planned canonical
physical unit is metres. A grid may therefore cover an area such as eight
metres by four metres.

### Cell size

Cell size is the physical side length of a square grid cell, expressed in
metres per cell. It is also referred to as the grid's spatial resolution.
A cell size of 0.05 metres represents a square measuring 5 centimetres on
each side.

### Grid dimensions

Grid dimensions are the number of columns and rows, expressed in cells. A
grid that is eight metres wide and four metres high, with half a metre per
cell, has sixteen columns and eight rows.

### Total cells

The total cell count is the number of columns multiplied by the number of
rows. A grid with sixteen columns and eight rows therefore contains 128 cells.

### Metres per cell

Metres per cell is the conversion scale between physical map coordinates and
grid coordinates. The future model will need this scale because column and
row counts alone do not say how much physical space the occupancy map represents.

Rendered pixels per cell are separate from metres per cell. Pixel size depends
on the viewing transform, zoom, and available screen area.
