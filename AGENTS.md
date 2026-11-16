# AGENTS.md

Rules for AI coding agents working in this repository. Read before touching code.

## Stack

WinUI 3, Windows App SDK 1.5, .NET 9, x64 only. Do not add dependencies without approval.

## Hard rules (MUST NEVER)

- NEVER reference a resource key that is not defined in ALL five `Themes/*.xaml` files; a missing key crashes at runtime when the dialog opens.
- NEVER use `{StaticResource}` for custom palette brushes — use `{ThemeResource}`.
- NEVER hardcode hex colors in XAML or code-behind; use theme brushes and tint brushes (`*TintBrush`).
- Every ContentDialog MUST set both `XamlRoot` and `RequestedTheme = ThemeService.Instance.FluentTheme` — popups do not inherit the window's theme.
- NEVER remove `DefaultItemExcludes` for `tests\**` in RubyDevice.csproj; the test bin output poisons the XAML compiler and breaks every incremental build.
- NEVER marshal the full RAWINPUT struct — decode the RAWINPUTHEADER only; keep the hook delegate rooted for the process lifetime.
- x:Bind properties that change MUST use `Mode=OneWay`/`TwoWay`; the default is OneTime and never updates.
- ToggleSwitch handlers MUST revert the switch first, let the confirmation dialog drive the change, then re-sync from the view model.
- NEVER leave swallowed exceptions (`catch { }`) around new UI logic; log to the crash log or handle visibly.
- NEVER push to `main` with failing CI; keep single PR diffs under ~400 lines where possible.

## Acceptance checks (must all pass after every change)

```bash
dotnet build RubyDevice.sln -c Debug -p:Platform=x64        # 0 warnings (CI enforces)
dotnet test RubyDevice.sln -c Debug -p:Platform=x64 --no-build
dotnet format RubyDevice.sln --verify-no-changes
```

Commit messages in English.
