# Architecture

## Projects

- **Core**: pure settings validation, gate/envelope, reversible volume ownership. No platform dependency.
- **Windows**: COM interop for Core Audio, enumeration and per-session volume control, JSON settings, recovery journal, mixing coordinator.
- **App**: WPF UI, WinForms tray icon, optional HKCU startup entry, watchdog mode in the same executable.
- **Tests**: dependency-free executable assertions; two waveOut fixtures; Windows integration runner.

## Signal and control path

```text
selected priority sessions → peak (max across sources) → gate + hysteresis
                                                       ↓
                                               hold + envelope
                                                       ↓
selected music sessions → original volume × envelope gain → session volume
```

Core Audio's session peak is a signal peak, not a perceptual loudness measurement. Muted/inactive sessions are ignored. Steady mode applies one target gain. Adaptive maps the interval from the threshold to 30 dB above it into a limited attenuation band. The detector never watches the music process itself. The app does not capture PCM, classify speech, change the master volume or install audio routing.

The gain moves exponentially; the configured fade duration reaches about 95% of a step. The four-dB gate hysteresis and silence hold avoid rapid toggling. A maximum 250 ms integration step prevents a stalled UI or resumed system from instantly jumping volume.

## Threading and discovery

COM wrappers are created, used and released on a dedicated MTA worker thread. The WPF UI reads immutable snapshots and submits serialized start/stop commands. The worker ticks about every 50 ms. One-second enumeration discovers active render endpoints; registered `IAudioSessionNotification` callbacks retain newly created sessions and queue them for the worker. Sessions reported only by callbacks are kept until expiration/device removal because the Windows enumerator does not necessarily include them. Handles have explicit ownership; individual disappearing sessions/devices are tolerated. The UI updates meters at 2 Hz. A music process may own several sessions: each gets a separate baseline and recovery record. Matching uses process names, not foreground window focus.

## Manual changes and recovery

Before each meaningful write, the journal atomically replaces a small JSON file with the original, previously written and intended next volume for that session instance. Only then is the COM volume setter called. It skips changes smaller than 0.001. The tolerance for float roundoff/ownership comparisons is 0.002.

If observed volume differs from the last value written by the engine, that session is released for the rest of this run. Stop restores only sessions whose volume still matches the engine's value. Mute is never modified. This respects most manual Windows mixer changes, but any polling controller has a small race between reading and writing a value.

The watchdog validates the owner's process start time to avoid PID reuse, waits for exit, then attempts restoration five times. A per-settings-folder mutex prevents an old watchdog from restoring over a replacement app. Recovery only changes matching session instance IDs whose current volume matches the last/intended write. Disconnected sessions remain in the journal for a later launch; replacement sessions are not guessed. Restoration cannot be guaranteed across device loss, simultaneous player crashes, or power failure. No “always restores” claim is made.

Journal writes happen on the control thread for ordering. This trades occasional filesystem latency for straightforward crash recovery; large-scale audio routing or professional low-latency DSP is outside this project's scope.

## Sources

The interop layout follows Microsoft's [audio sessions](https://learn.microsoft.com/en-us/windows/win32/coreaudio/audio-sessions), [session manager](https://learn.microsoft.com/en-us/windows/win32/api/audiopolicy/nn-audiopolicy-iaudiosessionmanager2), [session control](https://learn.microsoft.com/en-us/windows/win32/api/audiopolicy/nn-audiopolicy-iaudiosessioncontrol2), [volume](https://learn.microsoft.com/en-us/windows/win32/api/audioclient/nn-audioclient-isimpleaudiovolume), and [peak meter](https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nn-endpointvolume-iaudiometerinformation) interfaces. COM IIDs are public Windows interface identifiers.
