# Changelog

## 1.2.0 — 2026-10-10

### Changed
- Settings can be resized and maximized. They open maximized when their scaled size exceeds the display.
- Settings headings scroll with the options, leaving more room on smaller screens.
- Soft piano is the default sound at 15% volume. Previously saved sound choices remain unchanged.

### Fixed
- Prevented GDI+ errors from sub-pixel shortcut indicator arcs at the start of a hold.
- Deferred initial settings DPI scaling until all controls are assembled.
- Adjusted dropdown rows and tab headers to enlarged fonts; wrapped long checkbox labels at narrow widths.
- Alt+Tab uses held Alt state even when an injected event omits its Alt flag.
- Restored keyboard capture when keys reach settings after the hook is removed.
- Consumed window-switching system commands and application commands on the active canvas. Touchpad gestures that bypass application input still require configuration in Windows or the driver.

## 1.1.0 — 2026-10-04

### Added
- Piano, bells, xylophone and synthesizer through Windows MIDI, alongside the three original soft sounds.
- Alphabet and key names based on the current keyboard layout, including Shift and Caps Lock. Alt+Shift switches installed layouts.
- Transparent canvas with adjustable opacity from 10% to 100% and background color for dimming or tinting.
- A separate Shortcuts tab for quit/unlock, clear and settings, with Ctrl/Alt/Shift modifiers and hold times from 0.5 to 10 seconds.

### Changed
- Redesigned settings with rounded cards, purple accents, synchronized sliders and numeric fields, and a responsive layout with fixed action buttons.
- Piano is the default sound, at 15% volume.
- Startup hints display the configured shortcuts and hold times directly on the canvas.

### Fixed
- Settings checkboxes and rounded buttons clear their drawing buffers, preventing overlapping text and dark fragments during live repainting.
- Sound previews share the canvas MIDI output instead of attempting to open a second device.
- Cancelling settings restores the canvas instrument after previewing another sound.
- The original soft piano, bells and xylophone remain available without MIDI.

Windows x64 portable build. Extract the ZIP and run KeyCanvas.exe; no separate .NET installation is required. Existing saved preferences are preserved. Ctrl+Alt+Delete remains available.
