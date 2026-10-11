# 0003. SkiaSharp replaces ImageSharp

- **Status:** Accepted (Phase 1, PR #2)
- **Date:** 2026-10-10

## Context
Image search resizes uploads (HTTP predictor) and converts images to a 224×224 tensor for the embedded ONNX model. The app used `SixLabors.ImageSharp 1.0.0-beta0006`, which has several high-severity advisories.
- ImageSharp **3.1.x** (latest 3.1.12) still reports open high/moderate advisories.
- ImageSharp **4.x** fails `dotnet publish -c Release` with *"No Six Labors license found"*. It requires a commercial license key under the Six Labors Split License. This surfaced during the container build.

## Decision
Replace ImageSharp with **SkiaSharp 3.119** (MIT) plus `SkiaSharp.NativeAssets.Linux.NoDependencies` for both the ONNX tensor conversion and the upload resize/JPEG encode.

## Consequences
- No license key, no known vulnerabilities: `dotnet list package --vulnerable` reports 0 for the web project.
- The native Skia library ships with the app (bigger image). The `NoDependencies` asset avoids needing fontconfig in the container.
- Resampling differs slightly from ImageSharp, so ONNX predictions could vary at the margins. A smoke test checks that the model runs (200/404, never 500).
- Lesson for the demo: dependency upgrades are also a **licensing** review.
