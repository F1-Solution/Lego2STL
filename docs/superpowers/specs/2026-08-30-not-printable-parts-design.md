# Parts that are not printed, and where to buy them instead

**Date:** 2026-08-30
**Status:** approved design, not yet implemented
**Covers:** items 9 and 14 of the reported list — "Lot C". See
`2026-08-29-reported-items-and-lots.md`.
**Follows:** Lot B (`00ddbaa`), merged.

## The problem, as reported

> *"Tra il catalogo pezzi ci possono essere delle batterie o comunque dei pezzi che non possono
> essere stampati. In quel caso metti nel catalogo una foto del pezzo e aggiungi un pulsante per
> poterlo acquistare on line su uno dei siti che lo vendono (nei settings metti una lista di siti
> e l'utente sceglierà il suo preferito)"* — item 9

> *"Se ci sono file forma non trovati fammi vedere comunque l'immagine del PDF e dammi
> un'alternativa (tipo acquista) in base al codice Lego"* — item 14

## Measured facts this design rests on

Everything below was measured on 2026-08-30 against the local dump in `DB Lego` and against
run `6324712` built at 200%.

### The run already prints things nobody can print

| Part | Category | Material | Shape built |
|---|---|---|---|
| `5102c13`, `5102c17`, `5102c21` | Tubes and Hoses | Rubber | no |
| `22127` Powered Up 4-port hub | **Electronics** | Plastic | **yes — 144 x 142.4 x 80 mm** |
| `22169` Powered Up Large motor | **Electronics** | Plastic | **yes — 48 x 128 x 62.4 mm** |
| `22172` Powered Up XL motor | **Electronics** | Plastic | **yes — 80 x 128 x 78.4 mm** |

The three rubber hoses fail loudly and are already reported. The hub and the two motors
**succeed**: the run builds them, plates them, and says nothing. Three hollow shells of
components that have to be bought, taking a large share of the plates.

This is what rules out fixing the problem in the catalogue alone: a window-only change would
still print them.

### Material alone does not identify them

| Group | Size | Materials it carries |
|---|---|---|
| Category `Electronics` | 615 parts | **`Plastic`, all of them** |
| Category `Stickers` | 4,631 parts | `Plastic`, all of them |
| Category `Wheels and Tyres` | 425 parts | 300 `Plastic`, 125 `Rubber` |

Across the whole dump the material column holds `Plastic` (58,767), `Cardboard/Paper` (4,060),
`Cloth` (991), `Rubber` (323), `Foam` (81), `Flexible Plastic` (48) and `Metal` (27). A battery
box is plastic. So the rule needs the category as well as the material — both columns sit in the
same `parts.csv`, joined to `part_categories.csv`.

### The dump answers for this document

All 223 entries of run `6324712` are present in `DB Lego/parts.csv` under the very part numbers
the parts list uses. The known mismatch between BrickLink and Rebrickable part numbers — `4265c`
is in neither `parts.csv` nor Rebrickable's API — therefore does not arise here, but it can
arise, which is why an unknown part number is never a reason to exclude anything.

### Both mechanisms already exist

- `RowCrop.Extract` takes a region out of a page at its own resolution and `RowCrop.ToPng`
  encodes it. Cropping a part's drawing is a choice of region, not a new mechanism.
- `RebrickableDump` already reads the dump's CSVs, tolerantly — *"optional input: never fail the
  run because of it"* — and `--element-map` already points at that folder.
- `ThumbnailCache` already downloads and caches a picture per part.
- `RunLayout.ReviewDirectory` is declared and used by nothing.

## Part 1 — What decides whether a part is printed

### 1.1 The question, asked in one place

A new component in Core answers one question: given a part number, is it printed? Four answers —
printed, not for its material, not for its kind, and not known.

The facts come from the dump through `RebrickableDump`, which gains one method reading
`parts.csv` and `part_categories.csv` into part number to category and material. No new setting:
`--element-map` already names that folder. Its wording changes, because it now names a dump
rather than one table inside it.

The rule:

| Condition | Answer |
|---|---|
| Material is `Rubber`, `Cloth`, `Cardboard/Paper`, `Foam`, `Flexible Plastic` or `Metal` | not for its material |
| Category is `Electronics` or `Stickers` | not for its kind |
| The part is not in the dump, or there is no dump | not known |
| Anything else | printed |

Material is tested before category so that a rubber tyre is reported as rubber rather than as a
wheel. "Not known" behaves exactly like "printed": nothing is excluded on an absence.

### 1.2 What the run does with the answer

The run asks before it builds. A part that is not printed is not built, is not plated, and is
recorded on the manifest with the reason as a token — `material` or `kind`, never a finished
sentence, so that changing the language afterwards re-words it. The report grows a section
listing them.

`--print-everything` puts it all back the way it is today, for anyone who wants the hub's shell
as an ornament. Off by default.

The parts-list CSV keeps its six columns. A part that is not printed is still a part of the set.

## Part 2 — The picture

### 2.1 From the document it was read from

While the pages are being read, the run crops the part's drawing — the band above its label —
and writes it to a new `images/` folder in the run, one PNG per part number. The catalogue looks
for it the way it already looks for `stl/<part>.stl`, so nothing new goes on the manifest.

**The one thing this design does not yet know is how tall that band is.** The run knows where the
label's *text* is, not where the drawing above it ends. Implementation begins by measuring it on
the reference document: first by following the ink upward with the connected components the
extraction already computes, stopping at the vertical gap that separates one entry from the row
above; failing that, by a fixed multiple of the label's height. The measurement decides; no
constant is invented here.

### 2.2 When there is no document

A run from a CSV or from a set number has no page to crop. There the picture is Rebrickable's
`part_img_url`, fetched and cached by the same mechanism that already caches the LDraw renders.
This is also the fallback for a part the crop failed on.

## Part 3 — The shops

The settings carry a list, editable: a name, the address of a part's page, and optionally the
address of a search. The placeholders are `{part}`, `{element}` and `{color}`. One entry is the
preferred one, and that is the one the button uses.

Three entries are written on first use, and can be changed or removed:

| Name | Part page | Search |
|---|---|---|
| BrickLink | `https://www.bricklink.com/v2/catalog/catalogitem.page?P={part}` | `https://www.bricklink.com/v2/search.page?q={part}` |
| Rebrickable | `https://rebrickable.com/parts/{part}/` | `https://rebrickable.com/search/?q={part}` |
| LEGO Pick a Brick | `https://www.lego.com/pick-and-build/pick-a-brick?query={element}` | the same address |

A shop whose part page needs `{element}` cannot be used for a run that has no element numbers;
the button then falls back to that shop's search on the part number. The element number is there
for runs read from a document, which is where Lot B put it.

## Part 4 — The card

| State | Picture | What it says | Button |
|---|---|---|---|
| Not printed, for its material | crop, or photo | which material, and that it has to be bought | Buy |
| Not printed, for its kind | crop, or photo | that it is an electronic or a sticker | Buy |
| No shape was built | crop, or photo | that the shape could not be built | Buy |
| The code was not recognised | crop, when there is one | that it was not recognised | Search |

The existing warnings — open edges, surfaces that pass through each other, features thinner than
the nozzle, too big for the plate — are unaffected and keep their place.

## Testing

- The rule, as a table of material and category against the four answers, including a part
  absent from the dump and a run with no dump at all.
- The dump reader, against a small fixture pair of CSVs, and against a missing file.
- The run: a part that is not printed is neither built nor plated, is recorded with its token,
  and comes back when `--print-everything` is given.
- The crop: a synthetic page, the band taken, the PNG written where the catalogue looks.
- The address of a shop: each placeholder, a template with none of them, a shop needing an
  element number for a run that has none.
- The shop list surviving a save and a re-read, including a file that cannot be parsed.
- The four states of the card, in both languages.

## What this design does not do

- It does not turn a part to need fewer supports. That is item 10, still a spike, in Lot D.
- It does not buy anything. It opens the browser at the page.
- It does not change the parts-list CSV.
- It does not guess about a part the dump has never heard of.
- It does not add categories beyond `Electronics` and `Stickers`. Others — `Non-System Parts`,
  `Gear Parts`, minifigure parts — are arguable, and the ones that matter are already caught by
  their material. Widening the list is a one-line change when a real set proves it necessary.

## Order of work

1. Part 1 first: until the run knows what it should not build, nothing else has an answer to show.
2. Part 2's measurement — the height of the band — before Part 2's code.
3. Part 3 and Part 4 are independent of each other and follow Part 1.
