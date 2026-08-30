# Lot D — what is left, and what is already known about it

**Date:** 2026-08-30
**Status:** a record, not a design. Each of the three items needs its own brainstorming before it
is built.
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

**Open questions.** When is the question asked — during the run, which blocks a long run on a
person, or afterwards over a finished run's record, which is what `OverridesPath` implies? What
does *"Not a LEGO code"* record, so the same region is never offered again? Does answering
re-run anything, or only correct the parts list? Does the CLI get a way to answer too, or is this
the window's alone?

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

**Open questions.** What the icon should say — a brick, a printed layer, both — and whether it is
drawn as an SVG and rendered down to the four sizes, or drawn separately at 16 px, where an
outline that works at 128 px turns to mud.

---

## Suggested order

Item 6 first: it is the one with a design already written into the code, the one that unblocks a
real document the reader cannot fully manage, and the one whose machinery Lot C will have just
exercised. Item 12 next, being self-contained and short. Item 10 last, and as a spike whose
output is a recommendation rather than a feature.
