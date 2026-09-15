# Concepts

This document is used to describe the project's concepts, precise definitions,
and naming conventions. It provides a shared vocabulary for the application
and its code. Open questions, proposed changes, and decision discussions are
tracked in GitHub issues.

The terminology below describes the proposed Mk. 1-A design. Sample structures,
field names, and conventions are not yet implemented and may change as the
first experiments establish what is needed.

## Sensus terminology

### Hardware

The planned Mk. 1-A scanner uses:

- [HC-SR04 ultrasonic ranging sensor](<Datasheets/HC-SR04 Ultrasonic Sensor Module.pdf>)
- [SG90 servo motor](<Datasheets/SG90 Servo Motor.pdf>)
- [Arduino Uno R3](<Datasheets/ELEGOO UNO R3 Board.pdf>)

The first experiment uses a fixed sensor. A later step will mount the sensor
on the servo, allowing it to take measurements at different bearings.

### Scanner

A scanner is the complete unit responsible for orienting a sensor, initiating
measurements, and producing samples.

For the initial prototype, the scanner consists of the Arduino, servo, and
ultrasonic sensor.

### Bearing

A bearing is the direction in which the sensor is facing, expressed as an angle
relative to the scanner's forward direction.

- `-90°` = left
- `0°` = forward
- `+90°` = right

Initially, the bearing represents the commanded servo angle rather than a
directly measured physical angle.

### Sweep

A sweep is one complete angular traversal from one configured boundary to
another.

```text
Sweep 0: -90° → +90°
Sweep 1: +90° → -90°
Sweep 2: -90° → +90°
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

An acquisition is one attempt to obtain sensor data at a particular time and
bearing. An acquisition occurs even when no valid echo is detected.

For each trigger, the HC-SR04 automatically emits an eight-cycle, 40 kHz
ultrasonic burst. The complete burst-and-echo operation constitutes one
acquisition attempt.

### Sample

A sample is the lowest-level structured representation of a sensor result used
by Sensus. It contains the measurement result, or the absence of one,
together with the metadata required to interpret it.

```text
Sample
├── Sequence
├── SweepId
├── Timestamp
├── Bearing
├── Status
└── Measurement
```

### Range sample

A range sample is a sample produced by a sensor that measures distance or the
information required to derive distance.

For the HC-SR04, the sensor-specific measurement is the complete round-trip
duration.

```text
RangeSample
├── Sequence
├── SweepId
├── TimestampUs
├── BearingRadians
├── RoundTripDurationUs
└── Status
```

The term range is not specific to ultrasonic sensors. It may also apply to
LiDAR, radar, and Time-of-Flight sensors.

### Sequence

A sequence number is a monotonically increasing number assigned to every
acquisition attempt. It increments even when no valid measurement is produced.

```text
Sequence 100: Valid
Sequence 101: NoEcho
Sequence 102: Valid
```

The sequence resets when the acquisition device restarts.

### Sweep ID

A sweep ID identifies the sweep during which a sample was acquired. All samples
produced during the same angular traversal share the same sweep ID.

### Timestamp

A timestamp records when the acquisition was initiated according to the
acquisition device's monotonic clock. For the ultrasonic prototype, it
approximately represents when the trigger pulse was emitted.

### Status

The status describes the outcome of an acquisition and determines whether its
measurement is valid.

Initial statuses are:

- `Valid`
- `NoEcho`

Additional statuses may be introduced later if needed.

### Round-trip duration

The round-trip duration is the measured time between emitting the ultrasonic
pulse and detecting its return.

The complete round-trip duration is stored in the sample. Distance is derived
later by dividing the travelled distance by two.

HC-SR04 material commonly calls this value the echo duration or ECHO pulse
width because it is measured from the high time of the module's ECHO pin.
Sensus uses `RoundTripDurationUs` because it describes the meaning of the
value independently of that hardware interface.

### Measurement

A measurement is the sensor-specific payload contained in a sample. Examples
include:

- Ultrasonic round-trip duration
- LiDAR distance and intensity
- Temperature
- Acceleration

### Range

A range is the estimated distance between the sensor and a detected target. For
the HC-SR04, range is derived from the round-trip duration.

### Hit

A hit is a derived detection at a particular bearing and range. Hits may later
be converted into positions and occupancy-grid updates.

### Frame

A frame is a collection of measurements produced by a single acquisition.

The HC-SR04 produces one measurement per acquisition. A multi-zone sensor may
instead produce a frame containing many range measurements.

## Future terminology — occupancy grids

Occupancy grids are planned for a later stage, after Mk. 1-A. These definitions
describe the intended conventions for that work; no grid model is implemented
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
on the viewport, zoom, and available screen area.
