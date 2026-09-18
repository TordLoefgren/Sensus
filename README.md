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

**Sensus** is a **C# WPF** workbench for visualizing, replaying, and experimenting
with physical sensor data.

The project develops the **hardware, embedded firmware, and desktop software**
together through small, progressively more capable experiments.

The current project is [**Sensus Rover**](Projects/SensusRover/README.md),
beginning with a simple Arduino-based ultrasonic scanner and gradually
introducing wireless communication, motion, networking, and spatial mapping.

## Current Snapshot

Sensus is in active development, with work focused on
[**Mk. 1-A — Wired scanner**](Projects/SensusRover/Marks/Mk-1-A/README.md).
The application currently supports live range samples from a real Arduino over
a serial port and also generated synthetic samples from a simulation stream. Both inputs use
the same async processing path and renders the latest measurement in the
viewport.

The next milestone will expand this from a single range value to more detailed range
samples and their visualization.

The full progression from scanner to rover is described in the
[**Sensus Rover roadmap**](Projects/SensusRover/README.md#progression).

<p align="center">
  <img src="snapshot.png" alt="Current Sensus application workspace." width="900">
</p>

<p align="center"><i>
The Sensus workspace showing live and simulated range visualization.
</i></p>

## Quick Start

Requires Windows and the .NET 10 SDK. From the repository root:

```powershell
dotnet run --project Sensus/Sensus.csproj
```

## Documentation

Project terminology is defined in [**Concepts**](Docs/concepts.md).

Hardware references and datasheets are collected in
[**Datasheets**](Docs/Datasheets/).

Each completed mark or stage will include supporting material, results, and
reflections in its README.

## Layout

- `Sensus/` — C# WPF application
- `Projects/` — hardware, firmware, and experiments built around Sensus
- `Docs/` — shared concepts, hardware references, and README assets
