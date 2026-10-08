<h1 align="center">
  <br>
  <img src="Docs/Assets/sensus-logo.png" alt="Sensus" width="320">
</h1>

<p align="center">
  <img src="https://img.shields.io/badge/language-C%23-512BD4?style=flat&logo=dotnet&logoColor=white">
  <img src="https://img.shields.io/badge/UI-WPF-4F6BED?style=flat">
  <img src="https://img.shields.io/badge/platform-Windows-0078D6?style=flat&logo=windows11&logoColor=white">
  <img src="https://img.shields.io/badge/status-Prototype-B8A600?style=flat">
</p>

<p align="center"><i>
A personal experiment in embedded systems, electronics, and integrating hardware with software.
</i></p>

## Overview

**Sensus** is a **C# WPF** workbench for visualizing and experimenting with
physical sensor data. Recording and replay are planned.

The project develops the **hardware, embedded firmware, and desktop software**
together through small, progressively more capable experiments.

The current project is [**Sensus Rover**](Projects/SensusRover/README.md),
beginning with an ultrasonic scanner built around an ELEGOO UNO R3. Later stages
introduce wireless communication, motion, networking, and spatial mapping.

## Current Snapshot

Sensus is in active development, with work focused on
[**Mark 1-A — Wired scanner**](Projects/SensusRover/Marks/Mark-1-A/README.md).
Sensus displays range measurements from the wired scanner over USB or from a
desktop simulation. The workspace shows the scan, a timeline, and details of
the latest observation.

When Sensus connects to a scanner, it completes a handshake and starts
acquisition automatically.
The status bar shows whether Sensus is idle, connecting, or active. Connection
errors appear in the scanner panel. Stopping leaves the session available for
inspection. Starting a new run replaces it.

The ELEGOO UNO R3 firmware commands the servo through a sweep and sends HC-SR04
measurements, including missing echoes, over USB.

The full progression from scanner to rover is described in the
[**Sensus Rover roadmap**](Projects/SensusRover/README.md#progression).

<p align="center">
  <img src="snapshot.png" alt="Sensus workspace showing a simulated scan, measurement timeline, scanner controls, and inspector." width="900">
</p>

<p align="center"><i>
The Sensus workspace showing scan coverage, collected observations, and a timeline of distance, bearing, and sample status.
</i></p>

## Quick Start

Requires Windows and the .NET 10 SDK. From the repository root:

```powershell
dotnet run --project Sensus/Sensus.csproj
```

To try the application without hardware, choose a simulation scenario in the
scanner panel and press **Start**. With the current firmware uploaded, select
the board's COM port and press **Connect** to use USB serial at 9600 baud.

## Documentation

Project terminology is defined in [**Concepts**](Docs/concepts.md).

Hardware references and datasheets are collected in
[**Datasheets**](Docs/Datasheets/).

Each completed mark or stage will include supporting material, results, and
reflections in its README.

## Layout

- `Sensus/` — C# WPF application
- `Sensus.Tests/` — automated tests for the Sensus application
- `Projects/` — hardware, firmware, and experiments built around Sensus
- `Docs/` — shared concepts, hardware references, and README assets
