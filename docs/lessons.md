# Lessons log

Each entry records a real incident and where the rule that prevents it now
lives. When an agent (or you) gets bitten by something here, the fix belongs in
`AGENTS.md` — not in the chat history.

## 2026-11 — revival incidents

- **Missing theme-dictionary key crashed dialogs at runtime.** `DangerTintBrush`
  was referenced in dialog XAML but never added to any `Themes/*.xaml`; the
  XamlParseException was swallowed by the global handler, so the disable flow
  silently did nothing. → `AGENTS.md`: every resource key must exist in all
  five theme dictionaries.
- **Dialogs rendered with the system (dark) theme.** The window forces
  `RequestedTheme` for the hand-rolled palettes, but popup roots do not inherit
  it, so dialog titles were unreadable dark-on-dark. → `AGENTS.md`: every
  ContentDialog sets `ThemeService.Instance.FluentTheme`.
- **Every incremental build failed after tests were wired in.** The test
  project's bin output contains deployed copies of the app's `.xaml` files; the
  app's implicit Page glob picked them up and the XAML compiler saw duplicate
  pages. → `AGENTS.md` + `RubyDevice.csproj` (`DefaultItemExcludes`).
- **OneTime x:Bind never updates.** Status labels bound without `Mode=OneWay`
  stayed blank/stale after state changes; labels were also only set behind an
  `_isLoaded` guard that the constructor call could never pass. → `AGENTS.md`;
  pages call `UpdateTexts()` from `OnNavigatedTo`.
- **Swallowed exceptions hide broken flows.** A dialog constructor threw and
  the global handler ate it, so toggling a device appeared to do nothing.
  Diagnosed only by reading `%AppData%\RubyDevice\crash.log`. → `AGENTS.md`
  (no new silent `catch { }` around UI logic).

## 2026-06 — original revival of the shelved codebase

- **Low-level hooks cannot identify their device.** Blocking by "last raw input
  device" can misattribute input and freeze everything; blocked input never
  generates WM_INPUT so the state goes stale. → `Core/DeviceManager.cs`: per-type
  (keyboard/mouse) tracking + both-Shift emergency unlock.
- **Raw-input handles are not stable identities.** Keying persisted data on the
  handle orphaned notes/usage every reboot. → `Core/DeviceManager.cs`: path-derived
  stable `DeviceId`.
- **Private-copy tests prove nothing.** The test suite re-implemented the
  helpers it claimed to cover and had drifted from the real code. → tests
  reference the app project directly (`tests/RubyDevice.Tests`).
- **Scripted edits write LF into a CRLF repo.** Line-ending drift broke
  `dotnet format --verify-no-changes`. → `.editorconfig` pins the conventions;
  CI enforces them.
- **`GetWindowPlacement` and `AppWindow.Move/Resize` share one DPI-scaled
  coordinate space.** Save/restore is only correct because both sides use the
  same transform. → `Core/NativeMethods.cs` comments; do not mix in
  `GetWindowRect`-style physical pixels with one side and not the other.
