# TermRTS.Shared

Building blocks for console-based games and simulations on top of **TermRTS** and **ConsoleRenderer**.

- **World** – `WorldComponent`, `IWorldGen` / `VoronoiWorld`, `DefaultWorldDimensions`
- **Harness** – `ConsoleTitleHelper`, `GracefulShutdown` (CTRL+C → `Shutdown` event)
- **Ui** – `ConsoleCanvasSetup`, `StatusLineText`, `ViewportMapViewBase` (viewport, panning, coordinate scales)

Reference this project from new apps that should not depend on `TermRTS.Examples`.
