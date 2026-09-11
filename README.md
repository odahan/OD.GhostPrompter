# GhostPrompter

GhostPrompter is a Windows presenter overlay for recording technical, educational, and musical demonstrations. The overlay remains topmost while the demonstrated application keeps focus. Its Windows capture-exclusion request was validated with real Camtasia and OBS recordings on the target workstation; exact software versions and capture methods must be recorded in [the validation log](Docs/prototype-capture-validation.md) for each release candidate.

## DonationWare

GhostPrompter is DonationWare: it is free to use for personal and internal professional work. If it helps your work, please consider supporting its development on [Buy Me a Coffee](https://buymeacoffee.com/knhsynths).

Read the [English DonationWare License](LICENSE.en.md) or the [licence DonationWare française](LICENSE.fr.md).

## Run

Run `GhostPrompter.exe`. The application opens in Configuration mode, paused, with a separate overlay window. Load a TXT, Markdown, or DOCX script. Use **Edit text…** for quick corrections; **Save as…** writes a new UTF-8 `.txt` file and immediately loads that copy into the prompter. The imported source file is never overwritten automatically.

Sample scripts are available in [Docs/sample](Docs/sample): one for Blocks mode and one for Scroll mode.
Use [the functional validation checklist](Docs/functional-validation.md) for the final interactive recipe.

## Script syntax

Every format is converted to plain text and then interpreted with the same small syntax:

```text
[A title]
// A presenter-only comment
Spoken text.

---
```

`[A title]` is a non-empty, whole line title. `---` creates a logical block. `//` at the start of a line creates a presenter comment. Leading and trailing spaces do not affect marker recognition. Markers are reserved; there is no escaping syntax.

TXT must be valid UTF-8. Markdown formatting is removed while preserving readable text, links labels, code, and GhostPrompter markers. DOCX imports direct main-body paragraphs; tables, images, headers, footers, comments, and text boxes are ignored.

## Modes and controls

Blocks shows one automatically paginated page at a time. Next and Previous stop at the document boundaries. Scroll moves continuous content at 50 DIP/s by default, adjustable from 2 to 150 in 2 DIP/s increments. Its first line begins at 50% of useful height by default; Starting height is adjustable from 10% to 80%. Restart always returns to the beginning and stays paused.

Default global shortcuts:

| Shortcut | Blocks | Scroll |
| --- | --- | --- |
| Ctrl+Alt+Up / Down | Next / Previous | Faster / Slower |
| Ctrl+Alt+Left Arrow | — | Back one visible page, while preserving Play / Pause |
| Ctrl+Alt+P | — | Play / Pause |
| Ctrl+Alt+Space | Show / Hide | Show / Hide |
| Ctrl+Alt+L | Configuration / Presentation | Configuration / Presentation |
| Ctrl+Alt+T | Toggle Click Through preference | Toggle Click Through preference |
| Ctrl+Alt+Add / Subtract | Text size | Text size |
| Ctrl+Alt+Plus / Minus | Text size, no numpad required | Text size, no numpad required |
| Ctrl+Alt+Home | First page | Restart, paused |

Windows can reserve a shortcut for another program. GhostPrompter reports registration status in Configuration; a failed shortcut does not disable valid shortcuts.

Use **Keyboard shortcuts…** in Configuration to view every shortcut with readable key names, capture new combinations, detect duplicates, and restore the defaults. Changes are applied when the shortcut window is saved.

Use **Edit text…** after loading a script to correct wording in a dark, multiline editor. **Cancel** discards the editor contents. **Save as…** accepts TXT destinations only and reloads the saved copy at its beginning.

Advanced users can also replace the `hotkeys` array in `%LOCALAPPDATA%\GhostPrompter\settings.json` while the application is closed. Changes take effect at the next launch. `modifiers` uses the Windows values `1` for Alt and `2` for Ctrl; `key` is a virtual-key code. For example, Ctrl+Alt+Up is:

```json
{ "action": "NextPage", "modifiers": 3, "key": 38 }
```

Duplicate combinations are rejected. Use **Restore defaults** in the Keyboard shortcuts window to return to the built-in set.

## Windows and capture

Configuration allows positioning, resizing, and precise settings. Presentation locks the overlay, keeps it topmost, and never deliberately activates it. Click Through applies in Presentation according to its stored preference. Use Reset window position if the overlay is off-screen. Background and text opacity are independent.

GhostPrompter asks Windows to apply `WDA_EXCLUDEFROMCAPTURE` to both windows. This is not DRM and cannot guarantee every capture method, camera, or HDMI acquisition device. Always view an actual recording; a preview or successful API return is insufficient.

## Local data and limits

Settings are stored at `%LOCALAPPDATA%\GhostPrompter\settings.json`. They include visual preferences, window geometry, mode, speed, starting height, Click Through preference, and the last source path. The last file is not loaded automatically. The minimal log at `%LOCALAPPDATA%\GhostPrompter\logs\GhostPrompter.log` is replaced on each launch.

Source files and extracted plain text are each limited to 10 MB. The application performs no network access, telemetry, cloud sync, or remote document loading.

## Build and publish

Use the included script to create the distributable executable:

```powershell
.\publish.ps1
```

It restores dependencies when necessary, then publishes the application as a self-contained,
single-file Windows x64 executable at `bin\publish\GhostPrompter.exe`. When dependencies
have already been restored, use `.\publish.ps1 -NoRestore` to skip the restore step.

For manual build and test steps:

```powershell
dotnet restore GhostPrompter.slnx --source 'C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\'
dotnet build GhostPrompter.slnx -m:1 --no-restore
dotnet test GhostPrompter.slnx -m:1 --no-build --no-restore
```

Release publishing produces a self-contained, single-file `GhostPrompter.exe` for Windows x64. WPF trimming is intentionally disabled.
