# TermRTS - Terminal Real Time Simulation

A simulation engine combining event-driven realtime processing with basic ECS-like logic and content organisation.

## Features for users

- real-time execution
- basic ECS API for (sort of) efficient and ergonomic handling of simulation logic and emergent behaviour

## Features for developers

- comprehensible codebase

## How to run the examples

```sh
dotnet run --project .\TermRTS.Examples\ <example number>
```

Available example numbers:
 - [1] Minimal App
 - [2] Bouncy Ball
 - [3] Circuitry

## Development Plan

 - [ ] serialisation of simulation state to and from save file
 - [ ] concept for integrating menu-based navigation
