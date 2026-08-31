# Lot D — what is left, and what is already known about it

**Date:** 2026-08-30, closed 2026-08-31
**Status:** all three are settled. Items 6 and 12 were built, from
`plans/2026-08-30-answering-what-was-not-read.md` and
`plans/2026-08-30-the-application-icon.md`. Item 10 was a spike,
`plans/2026-08-30-turning-a-part-spike.md`, and its answer — written into this file below — is
that the feature should not be built.
**Covers:** items 6, 10 and 12 of the reported list — see
`2026-08-29-reported-items-and-lots.md`.

Written so that the three remaining items survive a lost session with the groundwork already
done: what was asked, what the code turns out to have, and what the open questions were. What
follows each item's original notes is what actually happened.

---

## Item 6 — Asking a person what the reader could not make out

> *"se chiedo pagina 372 del file 6324712 mi dice che 'page 372 at' (in inglese nonostante avessi
> selezionato italiano) comunque dice che a pagina 372 alle coordinate (x1, y1)-(x2,y2) non è
> riuscito a decodificare. In questo caso aggiungi un pulsante lì vicino che apre un popup che ti
> fa vedere il pezzo di immagine non decodificata e chiede all'utente di inserire Codice pezzo,
> Colore e Quantità con sotto 3 pulsanti 'Ok', 'Salta', 'Non è un codice Lego'"*

Lot A fixed only the first half — the sentence appearing in English inside an Italian run. The
dialogue was never built.

**What the code already has.** More than expected, and none of it wired up:

- `UnresolvedReading(Page, Bounds, RawText, Quantity?, PartNumber?, ColorCode?, Reason)` carries
  everything the dialogue needs to ask its question, including the region on the page.
- `RunLayout.ReviewDirectory` — *"crops of anything that could not be read, for checking by
  eye"* — is declared and **used by nothing**.
- `RunLayout.OverridesPath` — *"answers given during review, so the same question is not asked
  twice"* — is declared and **used by nothing**.
- `RowCrop.Extract` and `RowCrop.ToPng` already produce exactly the picture the dialogue must
  show. Lot C will already be writing crops to a run folder, on the same machinery.

So the shape of the answer was designed once and left unbuilt. Whoever picks this up should read
those three declarations first: they are the intended design, written by the person who saw the
problem.

**One thing more, found on 2026-08-30.** A run's record keeps its unread entries as
`IReadOnlyList<string>` of finished sentences — `RunManifest.Unread`, formatted at the moment the
manifest is built — so nothing downstream can say which page, which region, or what was read. It
is the same defect Lot B found in the list of parts too big for the plate and fixed by turning
strings into data. Item 6 has to do that first; the dialogue cannot be built over a sentence.

**Decisions, taken 2026-08-30.**

- **Asked afterwards, over the finished run.** The run never stops for a person: a run of several
  hours left going overnight has to finish on its own. The catalogue shows the unread entries
  with their crops and they are answered one at a time. This is what `OverridesPath` was declared
  for.
- **"Not a LEGO code" marks a region, not a run.** Page and bounds are recorded as "not an
  entry", so the same document read again — and a second run over the same pages — does not
  ask again. It does not touch the parts list, because nothing was ever added to it.
- **Answering corrects the parts list and nothing else.** The way back to shapes and plates is
  the road that already exists: *continue from the parts list*, which lands in the same folder.
  No new kind of run, and no partial re-run.
- **The window's alone.** The command line already reports what it could not read; answering a
  question is a conversation, and a report is a file.

**Both open questions, as built on 2026-08-31.** A corrected entry reaches the parts list by the
catalogue rewriting the run's own CSV in place, so a run reopened weeks later is corrected where
it stands and *continue from the parts list* picks the correction up. An entry whose part number
read cleanly and whose colour did not arrives with the part number already filled in and the
colour blank; Ok stays grey until all three fields are given, so a half answer is visibly a half
answer rather than a silent one.

---

## Item 10 — Turning a part so it needs fewer supports

> *"Sei in grado di capire se un pezzo può essere girato (in verticale invece che in orizzontale
> per diminuire il numero di 'supporti' che verranno stampati assieme al pezzo?"*

Measured on 2026-08-31, over the 175 shapes of run 6324712, by a throwaway probe that has since
been deleted. **The recommendation is: do not build this.** Not because too little overhangs, and
not because turning does not help — both turned out otherwise — but because overhang area is the
wrong thing to minimise, and minimising it gives advice nobody would act on.

**What the code already has.** `MeshPipeline.StandUp` already turns every shape from the source's
axes onto a print bed, and `SitOnBed` centres it and drops it onto zero — so a rotation stage has
somewhere obvious to live and an existing convention about which way is up. `MeshAnalysis`
measures the surfaces; `ClearanceOffset.ThinnestSpan` already walks the geometry looking for a
measurement, so the machinery for "look at every face and score it" exists.

**How much of a real set overhangs at all.** Counting the area of faces pointing more than 45°
below horizontal, and excluding the face a part rests on:

| overhanging more than | parts |
|---|---|
| 5% of their surface | 155 of 175 |
| 10% | 136 of 175 |
| 25% | 12 of 175 |

So the question is a real one about this set, not a corner case.

**How much turning helps.** Scoring each shape in the six axis-aligned orientations and keeping
the best: 95 of 175 improve by more than 5 percentage points and 26 by more than 20. The worst
offenders shed almost all of it — a 12L axle goes from 24.7% to 0.3%.

**What turning costs.** Not the plate: none of the 95 stops fitting a 256 × 256 mm bed, so the
interaction with Lot B's fitting scale that this record warned about does not bite on this set.
The cost is the shape of the print. **50 of those 95 end up taller than three times their
narrowest footprint side, against 2 as built.** The extreme cases are the axles: the score's
advice for a 12L axle is to stand 191 mm of it on a 9.6 × 9.6 mm base. That is a print that
topples, or that needs a brim and more support than it saved.

**A defect worth recording, for whoever tries again.** The first version of the score counted
every downward-facing triangle, including the flat underside a part is resting on — and therefore
advised standing plates on their edge, which is exactly backwards. A face within a layer height of
the lowest point has to be excluded before any of the numbers above mean anything.

**What was not done.** The comparison against a real slicer. Bambu Studio is installed on the
build machine but is a windowed program that writes nothing to a console, so it cannot be driven
headlessly; the comparison needs a person. It is moot for the decision: even if the score agreed
with the slicer on support material to the gram, acting on it would stand half the improved parts
on end.

**The recommendation.** Build nothing from this score. Overhang area is honest about overhang and
blind to everything else that decides an orientation — whether the part stands up, how much plate
it occupies, how many layers it becomes and therefore how long it takes, and where the seam lands.
A suggestion that is wrong about half the parts it speaks up for is worse than no suggestion,
because someone will act on it. If this is picked up again, it should not start from triangle
scoring at all: it should start from the slicer's own support figure for a handful of real
orientations, and the objective it optimises has to include stability and plate cost from the
first line, not as a filter bolted on afterwards.

---

## Item 12 — An icon for the application

> *"Per favore crea e inventa un'icona accattivante per l'applicazione. Falla in tutti i formati
> 16x16, 32x32, 64x64, 128x128 e in formato .ico / .png / .svg"*

**What the code already has.** Nothing of its own: the window still ships Avalonia's default,
`src/Lego2STL.Gui/Assets/avalonia-logo.ico`. No `ApplicationIcon` is set in any project file, so
the built executable carries the framework's icon too.

**What has to be touched, beyond drawing it.** The GUI project's `ApplicationIcon`, the window's
own icon, the assets folder, and the installers under `packaging/` — a shortcut with the default
Avalonia logo is the most visible place the old icon would survive.

**Decision, taken 2026-08-30.** Two or three proposals are drawn as SVG, rendered to all four
sizes, and looked at before one is chosen — an icon is judged by eye at 16 px, not by argument.
The plan therefore has a gate in the middle rather than a subject settled up front.

**Chosen 2026-08-31.** Four candidates were drawn, not three: the fourth — a nozzle printing a
brick that is being laid down in layers — was asked for at the gate, after the first three had
been rendered and looked at, and it is the one that was kept. The sizes changed at the same
gate: 16 px was dropped and the set runs 32 to 1024, for the application stores. It is carried by
the window, the executable, the MSI (which takes its icon out of the executable), the Windows
bootstrapper, the Linux menu entry — which had always named an icon that was never installed —
and the Mac bundle, whose `Resources` folder had been sitting empty for exactly this.

---

## Suggested order

Item 6 first: it is the one with a design already written into the code, the one that unblocks a
real document the reader cannot fully manage, and the one whose machinery Lot C will have just
exercised. Item 12 next, being self-contained and short. Item 10 last, and as a spike whose
output is a recommendation rather than a feature.

Done in that order on 2026-08-31. One thing is left for a person: driving the real window against
pages 370-372 of `6324712.pdf` and answering the question page 372 raises. Everything a headless
run can check is covered by the suite.
