# Sensus Rover

**Sensus Rover** is the first physical project built around Sensus.

It starts with a wired ultrasonic scanner, makes the same scanner wireless, and
eventually puts it on a small LEGO rover. Each stage introduces a new engineering
boundary while preserving the measurement and visualization work from the previous one.

## About

I am building Sensus Rover to develop a practical understanding of **embedded
programming**, **basic electronics**, **hardware**, and how physical devices
integrate with desktop software through one evolving system.

The early stages deliberately use inexpensive and approachable components such
as an ELEGOO UNO R3, HC-SR04 ultrasonic sensor, and SG90 servo.

I expect to outgrow some of this hardware over time. Starting simple lets me
concentrate on one unfamiliar problem at a time — measurement timing, serial
communication, power, wireless communication, motors, networking, and eventually
localization — before introducing the next layer.

The goal is not to reach the final rover as quickly as possible. Each stage
should leave me with a better understanding of the system and something concrete
that the next stage can build on.

## Progression

**Mark 1** is the first generation of the Rover. Its A/B/C stages evolve the same
basic scanner from a tethered experiment into a mobile platform.

Later generations replace larger parts of the system as the earlier experiments
make those changes meaningful.

| Mark | New boundary | Direction |
| --- | --- | --- |
| [**Mark 1-A — Wired scanner**](Marks/Mark-1-A/README.md) | Physical measurement + USB serial | Receive and visualize real range observations. |
| **Mark 1-B — Wireless scanner** | Portable power + wireless link | Remove the USB tether without changing the basic measurement model. |
| **Mark 1-C — Rover** | Motion + drive control | Put the scanner on a small mobile platform, control it from Sensus, and record and replay scan sessions. |
| **Mark 2 — Wi-Fi rover** | IP networking | Replace the serial-like wireless link with a real network transport. |
| **Mark 3 — Integrated controller** | Embedded architecture | Move measurement, control, and networking onto one more capable controller. |

Later directions include encoder-based motion estimates, localization, cameras,
and richer range sensors. Accurate mapping can wait until motion and its errors
are understood.

## Current mark

Work is currently focused on
[**Mark 1-A — Wired scanner**](Marks/Mark-1-A/README.md).

The first vertical slice established the path:

**HC-SR04 → ELEGOO UNO R3 → USB serial → Sensus → visualization**

The desktop application now handles scanner handshakes, structured samples,
in-memory sessions, scan visualization, and a timeline of measurement metrics.
The firmware drives the servo and sends HC-SR04 echo durations over USB serial.
A desktop simulation provides several scan scenarios without hardware.
Mounting the sensor on the servo and documenting the complete working scanner
are the next steps.

## Starting hardware

- [ELEGOO UNO R3 board](<../../Docs/Datasheets/ELEGOO UNO R3 Board.pdf>)
- [HC-SR04 ultrasonic sensor](<../../Docs/Datasheets/HC-SR04 Ultrasonic Sensor Module.pdf>)
- [SG90 servo](<../../Docs/Datasheets/SG90 Servo Motor.pdf>)
- Breadboard and jumper wires
- USB connection to the development PC

The hardware will change when a limitation gives me a reason to change it.
