# Prototype capture validation

Status: validated on the target workstation on 9 September 2026.

The prototype is built from the current source and asks Windows to exclude both
`MainWindow` and `PrompterWindow` from capture with `WDA_EXCLUDEFROMCAPTURE`.
Windows reporting success is not itself evidence of compatible capture output.

Complete this table before moving to the playback and import milestones.

| Item | Camtasia | OBS |
| --- | --- | --- |
| GhostPrompter version/commit |  |  |
| Windows version/build and DPI |  |  |
| Capture software and version |  |  |
| Capture source/method and parameters |  |  |
| Prompter visible to presenter |  |  |
| Prompter absent from recorded video |  |  |
| No black rectangle, flash, or artefact |  |  |
| Underlying app retains focus after hotkeys |  |  |
| Click, double-click, right-click, drag and wheel pass through |  |  |
| Result |  |  |

Recorded result: the presenter confirmed that Click Through works and that both
GhostPrompter windows are absent from real Camtasia and OBS recordings. The
prototype's capture-exclusion concept is therefore approved. Exact software,
Windows and capture-method versions should still be filled in before release.

Use a real recording, then view the resulting file. Test again after changing
opacity, moving/resizing, hiding/showing, and switching configuration/presentation.
