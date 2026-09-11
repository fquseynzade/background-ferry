# Validation scope — 0.2.1

Validated locally on Windows x64 with .NET SDK 10.0.401. This is a first release, not a claim of universal compatibility with every audio driver or streaming service.

## Automated

`./scripts/test.ps1` builds the complete solution with compiler warnings treated as errors and runs 16 deterministic assertions covering quiet input, attack, hold, gradual return, adaptive bounds, invalid numeric inputs, baseline multiplication, sticky manual override, source conflicts, and settings validation.

`./scripts/test.ps1 -Integration` additionally exercises real Windows Core Audio sessions belonging exclusively to two synthetic waveOut fixtures. It checks:

- Peak measurement and ducking to the expected session volume.
- Original-volume restoration on Stop and on priority-process exit.
- Manual volume override detection and preservation.
- Priority-session creation while the engine is already running.
- Forced termination of a separate mixing owner and recovery by the actual app watchdog.
- The dedicated MTA service used by the UI: immutable snapshots, late-session discovery, start/stop, adaptive bounds and disposal restoration.

The integration harness produces very quiet tones on the default output. It does not change user application volumes. Tests require a live Windows audio endpoint and are intentionally separate from CI's deterministic suite.

## UI inspection

The v0.2 desktop window was opened with a separate test settings folder. Its mixer and settings pages, Russian/English switching, and information tooltip were visually inspected using Computer Use. The main controls and localized descriptions appear in the accessibility tree. High-DPI resizing, tray interactions, and startup preference changes still need a manual pass.

## Chrome companion

Node.js tests cover steady ducking, held silence, return, adaptive bounds, threshold hysteresis and invalid settings. A simulated Web Audio harness additionally checks graph connections, unity priority gain, music attenuation, replacement of music sources, source closure, failed capture, and track/node cleanup. These tests do not establish actual browser capture permissions or audible routing.

A real Playwright/Chromium harness is provided in `tests/chrome/browser.cjs`. It creates only local synthetic tone pages and an isolated profile, loads the extension, invokes its action via CDP, and checks duck/restore/source closure. Run with Playwright plus Chromium installed: `node tests/chrome/browser.cjs`. It was not successfully executed here: process launch failed and a subsequent debug-browser launch was blocked by the local approval reviewer (no detailed reason). The extension remains experimental pending the manual listening workflow in [chrome-tabs.md](chrome-tabs.md).

## Not claimed

No physical headphone-unplug test, ARM64 device test, Windows 10 device test, exclusive-mode test, speech recognition, or Spotify/Apple Music/YouTube service certification. Browser tab capture, latency, protected players, and recovery after browser/audio-device failures need manual validation.

## 0.2.1 application identity

Six additional app tests verify real EXE icon extraction, executable descriptions, frozen cross-thread image data, missing-file fallback, PID identity validation and exited processes. Desktop UI inspection showed native Chrome, AyuGram and WebView2 icons with readable names. Chrome favicons use the documented favicon API but remain pending a real-extension browser check.
