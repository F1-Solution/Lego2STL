# Lot D — what is left, and what is already known about it

**Date:** 2026-08-30
**Status:** the decisions for all three were taken on 2026-08-30 and each now has its own plan:
`plans/2026-08-30-answering-what-was-not-read.md` (item 6),
`plans/2026-08-30-turning-a-part-spike.md` (item 10) and
`plans/2026-08-30-the-application-icon.md` (item 12).
**Covers:** items 6, 10 and 12 of the reported list — see
`2026-08-29-reported-items-and-lots.md`.

Written so that the three remaining items survive a lost session with the groundwork already
done: what was asked, what the code turns out to have, and what the open questions are. None of
this is approved design. Do not implement from this file.

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

**Still open, for its plan to settle.** How a corrected entry reaches the parts list when the run
folder has been reopened weeks later, and what the dialogue does with an entry whose part number
reads cleanly but whose colour does not.

---

## Item 10 — Turning a part so it needs fewer supports

> *"Sei in grado di capire se un pezzo può essere girato (in verticale invece che in orizzontale
> per diminuire il numero di 'supporti' che verranno stampati assieme al pezzo?"*

Still a spike: nobody has measured whether it can be decided from the geometry the pipeline
already holds. It was named as a spike in the Lot B design and has not moved since.

**What the code already has.** `MeshPipeline.StandUp` already turns every shape from the source's
axes onto a print bed, and `SitOnBed` centres it and drops it onto zero — so a rotation stage has
somewhere obvious to live and an existing convention about which way is up. `MeshAnalysis`
measures the surfaces; `ClearanceOffset.ThinnestSpan` already walks the geometry looking for a
measurement, so the machinery for "look at every face and score it" exists.

**The question the spike must answer first.** Can overhang be scored usefully from the triangles
alone — the area of faces whose normal points below the bed beyond the angle a slicer would
support — and does rotating to minimise that score actually agree with what a slicer does? If the
score does not predict the slicer, the feature is not worth building, and that answer is the
spike's whole output.

**A caution.** Turning a part changes what fits the plate, which Lot B's fitting scale now
computes. The two interact: a part that does not fit lying down may fit standing up. Whatever is
designed must say which of the two decides.

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

---

## Suggested order

Item 6 first: it is the one with a design already written into the code, the one that unblocks a
real document the reader cannot fully manage, and the one whose machinery Lot C will have just
exercised. Item 12 next, being self-contained and short. Item 10 last, and as a spike whose
output is a recommendation rather than a feature.
