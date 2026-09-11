# Contributing

Use Windows and the .NET 10 SDK. Open `BackgroundFerry.slnx` or build with the CLI. Run `./scripts/test.ps1` before submitting a change. For session/volume/recovery changes, also run `./scripts/test.ps1 -Integration` on a real audio output; this plays quiet test tones.

Keep the listening experience predictable. Prefer explicit source selection, preserve manual volume, avoid new runtime dependencies, and document limitations honestly. Include a reproducible scenario and evidence of testing in pull requests.

Potential next steps, not existing features: independent browser-tab support, optional speech detection, localization, and opt-in resume of a saved mix on startup. Discuss scope before introducing account integrations or a virtual driver.

Do not include personal settings, process dumps, access tokens, or recorded audio in bug reports. List Windows version, audio device type, app versions, mode and timing settings instead.
