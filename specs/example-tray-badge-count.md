# Spec: Tray icon shows the disabled-device count

Status: proposed (not yet implemented). Written as the worked example for
`specs/TEMPLATE.md`.

## Goal

The tray icon gives at-a-glance awareness of how many input devices are
currently disabled, so the user can tell from the notification area whether any
device is blocked without opening the window.

## Boundary (explicitly not doing)

- The tray context menu is unchanged (still Open / Exit only).
- No per-device list, tooltip changes, or balloon notifications for count changes.
- The main window UI is unchanged; this is tray-icon-only.

## Acceptance criteria

- Given 0 devices are disabled, When the tray icon is rendered or updated,
  Then its accessible name is exactly "RubyDevice" (no count suffix).
- Given 1 or more devices are disabled, When any enable/disable completes,
  Then the tray entry's accessible name contains the new count within 2 seconds
  (e.g. "RubyDevice — 1 disabled").
- Given the interface language is switched, Then the tray accessible name
  behavior is unchanged (the count is locale-neutral).
- Given a device toggles while the window is hidden to the tray, Then the count
  still updates (no dependency on window visibility).

## Verification commands

```powershell
# Open the tray overflow flyout, then check the tray button's name via UI Automation:
#   FindFirst(NameContains: "RubyDevice") -> Current.Name reflects the rule above.
# Automated check to be added alongside the implementation.
```

## Out of scope

- Overlay/badge icon compositing styles (any visible rendering that satisfies the
  accessible-name contract is acceptable).
- Persisting the count or showing history.
