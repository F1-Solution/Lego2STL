# Lego2STL

Turns a LEGO parts catalogue into a parts list and into shapes you can print.

Give it a set of building instructions, a parts list, or a set number. It produces a
spreadsheet of what the model is made of, one shape file per part, and printing plates grouped
by colour.

There is a window and there is a command line. They do exactly the same things, because they
are the same program with two ways in.

---

## Getting it

Download the package for your system, or build one — see [packaging](packaging/README.md).

| System | Install with | Or unpack |
|---|---|---|
| **Windows** | `Lego2STL-<version>-win-x64.exe` — no administrator unless .NET has to be installed. Adds a Start Menu entry and puts `lego2stl` on your path. | `…-win-x64.zip` |
| **Linux** | `Lego2STL-<version>-linux-x64.run` — `./…run` for you alone, `sudo ./…run --system` for everyone. `--uninstall` removes it again. | `…-linux-x64.tar.gz`, which holds an `install.sh` |
| **macOS** | `Lego2STL-<version>-osx-universal.pkg` — one file for any Mac, Intel or Apple silicon. It is not notarised, so the first open needs right-click → Open. | `…-osx-universal.zip` |

Lego2STL needs **.NET 10**. Each installer puts it in place when the machine has not got it,
and leaves it alone when it has. The zip and the tarball expect it to be there already.

---

## Using it

### The window

Start Lego2STL. Four screens, left to right:

1. **Input** — a document, a parts list, or a set number. For a document, type the pages the
   catalogue is on, or press *Find the catalogue pages* and let it look.
2. **Options** — every setting, grouped. The line along the bottom of the window is the
   command that would do the same thing, so the window teaches the command line rather than
   hiding it.
3. **Run** — a progress bar and everything the run has to say. *Copy command* takes the
   command that made it, ready to paste into a terminal; the list of past runs offers the
   same on every row.
4. **Catalogue** — every part in its real colour, with how many, and a warning on any shape
   that will not print cleanly. Buttons open a shape, a plate, or the folder.

The language can be changed at any time, from the top right or from Options. Everything
follows at once, including the column names the parts list is written with.

### The command line

```
lego2stl extract <document> [<pages>]   read a catalogue and carry on
lego2stl build   <parts-list>           start from a parts list
lego2stl build   --set 42100-1          start from a set number
lego2stl calibration                    find the clearance your printer needs
lego2stl bricks  2x4                    make a piece to a size
lego2stl refresh-colors                 rebuild the colour cross-reference
```

The whole thing, in one command:

```
lego2stl extract instructions.pdf 2-5 --clearance 0.15
```

Leave the pages off and they are worked out; `--list-pages` says what is on each one first.

`lego2stl --help`, or `lego2stl build --help`, lists every option. `--lang it` switches
language; without it the machine's own is used.

### Reading a document

Two kinds of document, and the tool tells them apart by itself.

**Official building instructions** carry their catalogue as real text, so it is quoted rather
than recognised: exact, quick, and no recogniser needed. The pages are found the same way — a
building step prints counts too, but never a part number beneath one, so there is nothing to
confuse. What such a book prints is an *element number* like `6177114`, a moulding and a
colour in one, which needs a table to take apart. Point `--element-map` at a Rebrickable
[`elements.csv`][dump], or leave it and one sitting beside the document is found by itself; a
Rebrickable key does the same online, and failing both, the older six-digit numbers can still
be read on their own.

Instructions that run to **several books** print the parts list in only one of them, usually
the last. Given one of the others, the tool says so rather than reporting hundreds of pages
that are not the catalogue.

**Scans and image-only exports** have no text to quote, and are read off the pixels by the
system's text recogniser, then checked against the lettering learned from the document itself.
That is the path that needs Windows.

[dump]: https://rebrickable.com/downloads/

---

## What comes out

One folder beside the input, named after it:

```
PistolaLego/
  PistolaLego.csv     what the model is made of
  report.txt          what was produced, and anything worth knowing about it
  stl/                one shape per part
  3mf/                printing plates, one per colour
```

The parts list has six columns — id, part number, BrickLink colour, colour name, RGB and
quantity — separated by semicolons, so it opens straight into a spreadsheet. It is also an
input: correct it and feed it back in, in any language: the colour names follow `--lang`, and
a list written in one is read in another.

Each plate is named after its colour, in the same language, and carries that colour so a
slicer opens it already looking like the finished model.

**Read the report.** It names every shape that is not closed, every retired part number whose
shape came from a replacement, and every part too big for the plate. None of that should be
discovered at the printer.

---

## Before you print

**Printed parts will not clip together at true size.** The shapes are the catalogue's nominal
dimensions and have none of the clearance a moulded part is made with. `--clearance` takes
every face in; `lego2stl calibration` prints an axle and its bush at a range of values so you
can measure which one your printer needs. No figure can be supplied in advance — the one being
looked for is smaller than the difference between two machines of the same model.

**Some parts are at the edge of what a printer can do.** Technic pins, axles and bushes have
walls at or below the width of a 0.4 mm nozzle. Beams come out well; the small connecting
pieces are marginal to failing. The catalogue screen marks them.

**Most shapes arrive with gaps in their surface.** They are covered over by default, which is
also what a clearance needs before it can be applied. On the reference set that takes the
shapes that are complete from 11 of 44 to 33. `--no-repair` leaves the surfaces exactly as
they arrived, and a slicer then repairs them silently instead; either way the report says
which shapes had gaps and how many.

**No gcode.** Nothing here talks to a slicer. Open the plates in yours.

What open edges, clearance and calibration actually are, at length and in Italian:
[docs/geometria-3d.md](docs/geometria-3d.md).

---

## What runs where

| | Windows | Linux | macOS |
|---|:---:|:---:|:---:|
| Read a catalogue a document prints as text | yes | yes | yes |
| Read a catalogue off the pixels | yes | — | — |
| Start from a parts list or a set number | yes | yes | yes |
| Shapes, plates, clearance, calibration | yes | yes | yes |
| The window | yes | yes | yes |

Only the second row needs text recognition, and the recogniser used is part of Windows. A
document that carries its own text — which official building instructions do — is read
anywhere. Asked to recognise one elsewhere, the program says so and points at what does work:
read that document on a Windows machine once and bring the parts list over, or start from a
set number.

---

## Building it

```
dotnet build
dotnet test
```

.NET 10 or later. Four projects: the logic, a console program, a window, and the tests.
`PistolaLego.pdf` and the two books of set 42100 are a third party's copyrighted instructions
and are not in the repository; the tests that need them skip with a clear message when they
are absent.

Two target frameworks are built: `net10.0-windows10.0.19041.0`, which carries the recogniser,
and plain `net10.0`, which is everything else and runs anywhere. On Windows the Windows one is
listed first, so Visual Studio's run button and `dotnet run` take it without being asked;
choose the plain one from the framework dropdown to see what the tool does elsewhere.

The window has tests of its own that draw it off-screen and read back what it shows —
including that every command-line option is reachable from it.

Packages are built by [packaging/](packaging/README.md), and the workflow that builds them
can be run on your own machine in Docker rather than on GitHub — see
[README-act.md](README-act.md).

---

## Where things come from

Part geometry is the [LDraw Parts Library](https://library.ldraw.org), fetched as needed and
kept. Colour and set information is [Rebrickable](https://rebrickable.com); a key is only
needed for `--set` and `refresh-colors`, and is read from `--api-key`, `REBRICKABLE_API_KEY`,
or `%APPDATA%\Lego2STL\config.json`.

`lego2stl bricks` drives [MachineBlocks](https://github.com/pks5/machineblocks), which
is published under CC BY-NC-SA. It is neither included nor downloaded: it needs your own copy
and your own OpenSCAD, and what it produces carries that licence.

LEGO is a trademark of the LEGO Group, which does not sponsor or endorse this.
