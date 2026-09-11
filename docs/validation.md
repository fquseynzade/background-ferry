# Validation scope — 0.1.0

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

The WPF window was opened and visually inspected at its default size. Headings, source controls, sliders, help and start/stop controls were present in the accessibility tree. Starting without sources produced the expected validation message. Later work moved audio execution off the UI thread; that service is covered by the integration test above.

A final interactive pass of the portable build was interrupted by the operator. Do not treat this as end-to-end UI automation coverage. The remaining recommended manual checks are tray menu interaction, startup preference toggling, high-DPI resizing, and listening with the user's actual music/video apps.

## Not claimed

No physical headphone-unplug test, ARM64 device test, Windows 10 device test, exclusive-mode test, browser-tab isolation, speech recognition, or Spotify/Apple Music certification. These limits do not change the normal shared-mode two-application workflow, but should be kept visible in release notes.
