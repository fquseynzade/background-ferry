# Background Ferry 0.2.0

- Added an experimental Chrome MV3 companion for one music tab and multiple priority tabs. Local tab audio capture, smooth gain, pause/release, saved preferences.
- Rebuilt the desktop UI from an original generated concept: graphite panels, mint controls, sidebar navigation.
- Added persistent Russian and English interfaces to desktop and extension.
- Replaced long inline setting explanations with information tooltips. Keyboard focus also exposes help.
- Language/startup preferences no longer stop the desktop mix.
- Included the extension and installation guide in the portable release, plus a separate extension archive.

Build and deterministic tests pass; real Windows session regression tests pass. Desktop UI and language/tooltips were inspected. Chrome audio graph logic is tested with simulated Web Audio objects; an end-to-end browser test is included but was not successfully run in this environment. The local permission reviewer blocked a debug-browser launch without a detailed reason. Treat tab capture as experimental until the manual check in [chrome-tabs.md](chrome-tabs.md) passes with your chosen sites.

Desktop and Chrome mixers are independent. Do not apply both to the same Chrome audio. Chrome Store publishing, service-specific DRM testing, and automatic extension installation are outside this release.
