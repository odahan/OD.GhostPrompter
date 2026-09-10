# Functional validation checklist

Run this checklist against the published `GhostPrompter.exe`, after completing the capture checklist in `prototype-capture-validation.md`.

## Environment

Record the GhostPrompter version, Windows build, display DPI, monitor layout, keyboard model, and whether the test was run as a standard Windows user.

## Blocks

1. Load `Docs/sample/blocks-demo.txt`.
2. Confirm that `[Opening]` is bold and that comments are italic without `//`.
3. Make the overlay small enough to create several pages in a long block.
4. Navigate forward and backward through every page and block. Confirm that no text is skipped, duplicated except for an intentional repeated title, or truncated.
5. Confirm Next stops at the final page and Previous stops at the first page.
6. Change font size, resize the overlay, move it between 100%, 125%, and 150% DPI displays if available, then confirm the current passage remains reachable.

## Scroll

1. Load `Docs/sample/scroll-demo.txt` and select Scroll mode.
2. Verify the first line starts at 50% of useful height by default, with later lines visible below it.
3. Play, pause, increase/decrease speed, hide/show, and restart.
4. Change Starting height to 60%. Confirm scrolling pauses and the current passage moves to the new reference point.
5. Let the final line reach the reference point. Confirm that progress is 100%, playback stops, and Play does not restart implicitly.
6. Disable Show titles for a title-only script and confirm the no-visible-content state is safe.

## Window and controls

1. In Configuration, drag and resize the overlay. Restart and verify its position, dimensions, visual settings, and last source path are restored; the source must not auto-load.
2. In Presentation, confirm the overlay cannot be moved or resized and does not take activation.
3. Enable Click Through. Test click, double-click, right-click, drag, and wheel against another process below the overlay.
4. Use all global shortcuts while typing in another application. Continue typing without clicking back into that application.
5. Force an external shortcut collision if practical and confirm unaffected shortcuts remain usable.
6. Use Reset window position after moving the overlay off-screen or disconnecting a secondary monitor.

## Imports and limits

1. Load UTF-8 TXT with and without BOM, Markdown, and DOCX variants of a prepared script; their parsed logical blocks must match.
2. Confirm UTF-16 TXT produces a readable error and retains the previous document.
3. Confirm a DOCX containing a table loads its paragraphs and reports that the table was ignored.
4. Attempt unsupported, missing, corrupt, locked, and over-limit files. The previous document and position must remain available.
5. Load a representative large script, cancel an in-progress import, then load another source. Only the newest successful request may become active.

## Quick text editing

1. Confirm **Edit text…** is disabled before a document is loaded, then load TXT, Markdown, and DOCX sources and verify the editor contains their current plain text and GhostPrompter markers.
2. Correct a few words, choose **Save as…**, and confirm the dialog only offers TXT, writes readable UTF-8 text, loads the saved copy as the current source, and restarts at the beginning.
3. Edit again and choose **Cancel** or close the editor. Confirm the current source and displayed prompter text remain unchanged.
4. While the editor has focus, type combinations assigned to global shortcuts and confirm they enter text without controlling the prompter. Confirm the shortcuts work again after closing the editor.

Do not mark the release as capture-compatible unless the real Camtasia and OBS recordings have been viewed and recorded in the separate capture validation file.
