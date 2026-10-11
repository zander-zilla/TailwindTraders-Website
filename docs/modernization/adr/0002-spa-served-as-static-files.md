# 0002. Serve the React SPA as static files

- **Status:** Accepted (Phase 1, PR #2)
- **Date:** 2026-10-10

## Context
The app used `Microsoft.AspNetCore.SpaServices.Extensions` (`UseSpa`, `UseReactDevelopmentServer`), so `dotnet run` would launch `npm start`. That package model is deprecated in modern templates. It couples the backend build to Node, and in Development it made the test host try to spawn the React dev server.

## Decision
- Remove SpaServices. Serve `ClientApp/build` with `UseStaticFiles` (own `PhysicalFileProvider`) and `MapFallbackToFile("index.html")` for client-side routes.
- In development, run the CRA dev server separately (`npm start`). The Docker multi-stage build produces `ClientApp/build`.
- Minimal frontend changes so it builds on Node 22: `node-sass` → `sass`, `react-scripts` 5, drop browser `dotenv`, pin the 2021 ACS/Fluent/Cosmos package versions.

## Consequences
- The backend has no runtime dependency on Node, and the container serves the SPA directly.
- API routes take precedence. Unknown paths fall back to `index.html`, including unknown `/api/...` GETs, which is acceptable for this app.
- A full frontend modernization (Vite, React 18/19, router 6) is deferred to Phase 7. `npm ci` currently needs `--legacy-peer-deps`.
