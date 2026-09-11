# Status Light companion

## Product
Windows-first personal desktop tray utility for an RP2040 Teams status light.
New portable UI, not a restyle of the old WPF service dashboard. User wants a
compact utility that mostly stays out of the way. Keep Windows Teams detection
isolated; manual device controls can work on other desktop platforms.

## One window, three tabs
- Light: detected status, actual output preview, device connection, automatic vs
  manual control, manual status selection, brightness, lights on/off.
- Appearance: five tidy rows for Available, Busy, Away, Do not disturb, Offline;
  color picker + hex value, steady/blink/pulse selection, period, 3-second Preview.
  Explicit Save to light, clear unsaved indicator, restore defaults.
- Settings: device port + refresh/connect, launch at sign-in, minimize to tray,
  stale timeout. Explain unavailable automatic detection on non-Windows simply.
The UI is a design draft using clearly simulated state, not a real USB connection.
Do not show code, APIs, baud rates, services, admin controls, logs, or dashboards.
Do not claim all-platform automatic Teams detection or that IT policies are bypassed.

## Visual direction
Quiet desktop utility, refined and practical. Window content around 540px wide,
680px tall, with scrollable tab content at smaller heights. One restrained title
bar, three compact tabs, no sidebar or hamburger. App name is text, no invented logo.
Font: Segoe UI, then system-ui/sans-serif. Body 14px, labels 12px, section 16px,
current status 26px semibold. Use sentence case. Avoid all-caps navigation.
Palette: background #111519, surface #191F24, raised #222A30, border #303A42,
primary text #EDF2F5, secondary #A5B2BC, accent #78D6AD, accent text #10251B.
Status swatches: green #00CC6A, red #F06969, amber #E5B84D, purple #B997EA,
offline #72808B. Light preview may glow subtly in the selected status color.
Do not tint the entire window based on status. No gradients beyond a subtle
physical LED glow. No decorative imagery or external assets.
Spacing: 4/8/12/16/24/32px. Inputs/buttons 36px high, 8px radius. Panels 12px
radius, light 1px borders. Use dividers and whitespace rather than nesting cards.
Keep labels and values aligned; fit the five preset rows without huge cards.
Focus ring: 2px #78D6AD. Disabled controls retain readable labels.

## Interaction and states
Default example: Teams Available, light connected, Automatic selected, 40%
brightness. Small unobtrusive Draft preview label makes simulation clear.
Working prototype tabs, manual selector, brightness, lights toggle, and preview
button. Reduced motion respected. Status has text as well as color. Icon-only
controls have accessible labels. All controls keyboard reachable.
Distinguish detected Teams status from manual light output. Missing Teams should
say Status unavailable, never falsely Available. Disconnected light should have
one clear connect action. Editing appearance marks unsaved changes; Save is
explicit. A 3-second preview restores output and never overwrites base status.
Closing/minimizing leaves the intended tray utility running; tray menu concept:
Open, Automatic, Lights off, Quit. Do not fake native OS integration in the draft.
