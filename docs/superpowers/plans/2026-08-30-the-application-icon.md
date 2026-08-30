# An Icon For The Application — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Avalonia's default logo with an icon of this application's own, at 16, 32, 64
and 128 px, as `.svg`, `.png` and `.ico`, everywhere the old one appears. Item 12 of the reported
list.

**Architecture:** Draw two or three candidates as SVG, render each to all four sizes, and choose
by looking — an icon is judged at 16 px, not in an argument. Then wire the chosen one into the
window, the executable and the installers.

**Tech Stack:** SVG by hand, rendered with SkiaSharp (already a dependency of this solution, so
nothing new is installed), Avalonia 12.1.1, the WiX/Inno definitions under `packaging/`.

**Spec:** `docs/superpowers/specs/2026-08-30-lot-d-what-is-left.md`, item 12.

## Global Constraints

- The application currently ships `src/Lego2STL.Gui/Assets/avalonia-logo.ico`, and no project
  sets `ApplicationIcon`, so the built executable carries the framework's icon too. Both have to
  change, or the icon is only half replaced.
- **No new package.** SkiaSharp is already referenced and renders SVG through
  `SkiaSharp.Extended.Svg` or a plain path-drawing fallback; if neither is available without a
  new dependency, render the sizes with any tool at hand and commit the results — the renderer is
  not the deliverable.
- The 16 px version is hand-corrected after rendering. An outline that reads at 128 px turns to
  mud at 16, every time, and a downscale is a starting point rather than an answer.
- Commit messages: `<type>: <description>`, describing observable behaviour.
- There is a human gate in the middle of this plan. Do not choose the icon on your own.

---

### Task 1: Draw the candidates

**Files:**
- Create: `src/Lego2STL.Gui/Assets/icon-<name>.svg`, two or three of them

**Interfaces:**
- Consumes: nothing.
- Produces: the SVG sources. Task 2 renders them; Task 3 keeps one and deletes the rest.

- [ ] **Step 1: Draw two or three, on one square**

Each is a square SVG with a `viewBox="0 0 128 128"`, flat colour, no gradients and no text. Flat
because the icon is shown at 16 px on a taskbar, and a gradient at 16 px is a smudge.

Candidates worth drawing, and why each might be the one:

- **The brick that becomes layers.** A brick seen at a slight angle, its upper half solid and its
  lower half drawn as four or five printed layers. Says both halves of what the application does.
  The risk is that at 16 px the layers close up into a solid block, which makes it candidate
  three by accident.
- **The stud grid.** A plain brick face, four studs, one colour on a contrasting square. Reads at
  any size and looks like nothing else on a taskbar; says nothing about printing.
- **The nozzle over the stud.** A printer nozzle laying a line that ends in a stud. Says the whole
  idea in one image; the most likely of the three to fail at 16 px, which is exactly what the
  gate in Task 3 is for.

- [ ] **Step 2: Check each against a dark background as well as a light one**

The window has both themes and so does every taskbar. An icon that vanishes on one of them is not
finished. Give each candidate a colour that holds on both, or its own contrasting ground.

- [ ] **Step 3: Commit the candidates**

```bash
git add src/Lego2STL.Gui/Assets/icon-*.svg
git commit -m "docs: candidate icons for the application"
```

---

### Task 2: Render every candidate at every size

**Files:**
- Create: a throwaway renderer under the scratchpad
- Create: `<scratchpad>/icons/<name>-<size>.png`

- [ ] **Step 1: Render 128, 64, 32 and 16 for each candidate**

Nearest-neighbour is wrong here and so is a naive box filter; render each size **from the SVG**
rather than downscaling the 128, so every size gets the vector's own edges.

- [ ] **Step 2: Put them side by side**

Compose one sheet per candidate: the four sizes in a row on a light ground, and the same row
again on a dark one. Eight images per candidate on one page is what makes the choice possible.

- [ ] **Step 3: Look at the 16 px versions on their own, at their own size**

This is the only size that decides anything. A candidate that needs to be explained at 16 px has
already lost.

---

### Task 3: Choose — with the user

**Files:**
- Delete: the candidates not chosen

- [ ] **Step 1: Show the sheets and ask**

Present the sheets and ask which to keep. **Stop here and wait.** Do not pick one and carry on;
the whole point of drawing three was that the choice is the user's.

- [ ] **Step 2: Correct the chosen one at 16 px by hand**

Nudge the paths until the 16 px render is crisp: whole-pixel edges, one detail removed rather
than one added, contrast raised if it needs it. Keep it as a second file,
`icon-16.svg`, when the correction diverges enough that one source can no longer serve both.

- [ ] **Step 3: Delete the candidates that lost**

```bash
git rm src/Lego2STL.Gui/Assets/icon-<the others>.svg
```

- [ ] **Step 4: Commit the chosen icon**

```bash
git add -A
git commit -m "feat: the application has an icon of its own"
```

---

### Task 4: Put it everywhere the old one was

**Files:**
- Create: `src/Lego2STL.Gui/Assets/icon.ico`, `icon-16.png`, `icon-32.png`, `icon-64.png`,
  `icon-128.png`
- Modify: `src/Lego2STL.Gui/Lego2STL.Gui.csproj`
- Modify: `src/Lego2STL.Gui/Views/MainWindow.axaml`
- Modify: whatever under `packaging/` names an icon
- Delete: `src/Lego2STL.Gui/Assets/avalonia-logo.ico`
- Test: `tests/Lego2STL.UiTests/WindowTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `tests/Lego2STL.UiTests/WindowTests.cs`:

```csharp
    /// <summary>The window carries its own icon rather than the framework's.</summary>
    [AvaloniaFact]
    public void The_window_has_an_icon_of_its_own()
    {
        var window = new MainWindow();

        window.Icon.Should().NotBeNull();
    }
```

- [ ] **Step 2: Build the `.ico` and the four `.png`s**

The `.ico` carries all four sizes in one file, largest first. Windows picks the size it wants
from inside it, which is why one `.ico` with four images beats four files.

- [ ] **Step 3: Point the window and the executable at it**

In `src/Lego2STL.Gui/Lego2STL.Gui.csproj`:

```xml
    <ApplicationIcon>Assets/icon.ico</ApplicationIcon>
```

In `MainWindow.axaml`, on the `Window` element:

```xml
             Icon="/Assets/icon.ico"
```

Check the assets are included in the project: `avalonia-logo.ico` was, and the new files must be
in the same way.

- [ ] **Step 4: Point the installers at it**

```
grep -rn "avalonia-logo\|\.ico" packaging/
```

Every hit gets the new file. A shortcut still carrying the Avalonia logo is the most visible
place a half-done job shows.

- [ ] **Step 5: Delete the old one**

```bash
git rm src/Lego2STL.Gui/Assets/avalonia-logo.ico
```

Then build: anything still referring to it fails now rather than shipping.

- [ ] **Step 6: Run the whole suite, and look at the executable**

```
dotnet test Lego2STL.slnx
dotnet build src/Lego2STL.Gui/Lego2STL.Gui.csproj -c Release
```

Open the output folder in Explorer at Large Icons and again at Details, and look at the taskbar
with the window running. Three places, because they draw three different sizes out of the `.ico`.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: the window, the program and the installers all carry the new icon"
```

---

## Notes for whoever executes this

- The gate in Task 3 is the plan. Everything before it is preparation and everything after it is
  wiring; choosing the icon without asking wastes both.
- If the renderer in Task 2 turns into a project of its own, stop and render the sizes by any
  means available. The images are the deliverable; the renderer is not.
- Record `PHASE:LOT-D WAVE:12 STATUS:complete TS:<ISO-8601-UTC>` in `PROGRESS.md` when the icon
  is in place.
