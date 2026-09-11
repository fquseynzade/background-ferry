# Background Ferry 0.2.1 — Beta

Music makes room for your video or conversation, then returns in the pauses.

- Automatic application icons and readable names in both desktop source selectors. Icons come from locally installed executables; no fixed list of supported brands or icon downloads is needed.
- Metadata loads on background tasks and is cached separately from the audio engine. Inaccessible, missing or iconless applications get a universal audio icon and their process name. Hover over a source to see its executable name.
- Selected Chrome tabs display site favicons via Chrome's favicon API, with a local fallback. This adds the favicon permission; page URLs are held only with live captures, not saved to settings or sent to an icon service.
- Dark graphite/mint interface, Russian and English, compact information tooltips.

## Download

On this release, download **BackgroundFerry-win-x64.zip**, extract the entire archive, and run **BackgroundFerry.exe**. Exit the previous version from the tray first. .NET is included.

For Chrome tabs, load the included **chrome-extension** folder on chrome://extensions using Developer mode → Load unpacked. A separate **BackgroundFerry-Chrome.zip** is also attached. Assign Music and Priority tabs through the extension icon, then start mixing there.

## Beta scope

Windows audio duck/restore and identity extraction have automated tests. Chrome envelope and routing logic have simulated tests; real tab capture is still experimental pending a manual check with your browser and sites. The desktop and extension mixers are independent: do not apply both to Chrome simultaneously.

Some packaged, protected, or helper processes can expose a generic executable icon/name. Streaming-service compatibility, DRM capture, ARM64 and Windows 10 devices have not been individually certified. See the repository's docs/validation.md and docs/chrome-tabs.md.
