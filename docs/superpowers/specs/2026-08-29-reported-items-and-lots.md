# The reported items, and the lots they were split into

**Date:** 2026-08-29
**Status:** the record. Lots A and B are merged; C and D are designed and planned, not built.

The fourteen items below are the user's report, quoted as written. The work was split into four
lots, and this file is the only place that mapping is written down — it was reconstructed on
2026-08-29 from the lot A and B commits after a session's context was lost, and is kept here so
it cannot be lost again.

---

## The lots

| Lot | Items | State |
|---|---|---|
| **A** | 1, 3, 4, 5, 6 (the Italian wording only), 7, 8 | merged — `3ad05ea`, `bb12cc2`, `f798a53` |
| **B** | 2, 11, 13 | merged — `00ddbaa` (nine tasks, `980f006`..`53e8c55`) |
| **C** | 9, 14 | planned in `plans/2026-08-30-not-printable-parts.md`; not yet built |
| **D** | 6 (the popup), 10, 12 | one plan each, listed in `2026-08-30-lot-d-what-is-left.md`; not yet built |

Lot B reversed part of item 2 on the strength of measurement: no *Repair* button, because after
the two repair corrections there is nothing left to ask of a part the run has not already tried.
See `2026-08-29-catalogue-actions-design.md`.

---

## The items, as reported

1. Se vado su eseuczioni vorrei un pulsante "Copia comando" per copiare il comando della CLI
   relativo a quella esecuzione
2. Nonostante --no-repair eè false di default molti pezzi non sono stati riparati (o almeno così
   diceva l'errore nel catalogo dei pezzi). In caso di mancata riparazione aggiungi un pulsante
   "Ripara" accanto a "Apri forma" e "Apri piano" e in cima aggiungi un pulsante "Ripara tutti"
   (solo se necessario)
3. quando cambi un'opzione compare il pulsante "Ripristina" e la description si disallinea
   (invece di nascondere il pulsante "Ripristina" a 0 pixel lascia lo spazio a prescindere
4. Anmche se ha creato tutti i piani nessuno dei pulsanti "Apri piano" è abilitato
5. sul file 6324712 rileva molte pagine che sono falsi positivi (in realtà le uniche pagine sono
   370-371. sull'altro file del set rileva molte pagine ma nessuna ha il catalogo pezzi
6. se chiedo pagina 372 del file 6324712 mi dice che "page 372 at" (in inglese nonostante avessi
   selezionato italiano) comunque dice che a pagina 372 alle coordinate (x1, y1)-(x2,y2) non è
   riuscito a decodificare. In questo caso aggiungi un pulsante lì vicino che apre un popup che
   ti fa vedere il pezzo di immagine non decodificata e chiede all'utente di inserire Codice
   pezzo, Colore e Quantità con sotto 3 pulsanti "Ok", "Salta", "Non è un codice Lego"
7. Le checbox delle opzioni vanno messe a destra delle loro etichette
8. Quando si apre l'applicazione "Solo ciò che ho cambiato" è abilitato di default, lo vorrei
   disabilitato per default
9. Tra il catalogo pezzi ci possono essere delle batterie o comunque dei pezzi che non possono
   essere stampati. In quel caso metti nel catalogo una foto del pezzo e aggiungi un pulsante per
   poterlo acquistare on line su uno dei siti che lo vendono (nei settings metti una lista di siti
   e l'utente sceglierà il suo preferito)
10. Sei in grado di capire se un pezzo può essere girato (in verticale invece che in orizzontale
    per diminuire il numero di "supporti" che verranno stampati assieme al pezzo?
11. Nel report ho visto "<codice> misura AxB e non entra in un piano. In questeo caso nel catalogo
    pezzi deve riportare il probmea e deve offirire la possibiltà di cambiare scala a tutto il set
    e ripartire (suggerisci il massimo di scala in base alle dimensioni del piatto per non uscire
    da esso)
12. Per farvore crea e inventa un'icona accattivante per l'applicazione. Falla in tutti i formati
    16x16, 32x32, 64x64, 128x128 e in formato .ico / .png / .svg
13. Nel catalogo aggiungere unaa dropdown per poter cambiare la visualizzazione dei codici dal
    formato BrickLink a quello Lego
14. Se ci sono file forma non trovati fammi vedere comunque l'immagine del PDF e dammi
    un'alternativa (tipo acquista) ni base al codice Lego

---

## Lot C — where the design lives

Designed on 2026-08-30 in `2026-08-30-not-printable-parts-design.md`. In short: the run asks a
local Rebrickable dump whether a part is printed at all, refuses to build the ones that are not —
rubber, cloth, card, foam, flexible plastic and metal by material; electronics and stickers by
kind — and the catalogue shows a picture of each and offers to buy it from a shop chosen in the
settings. Measured on run `6324712`, that stops three Powered Up components being printed as
hollow shells.

## Lot D — where the record lives

Recorded on 2026-08-30 in `2026-08-30-lot-d-what-is-left.md`, which holds the decisions taken for
each and what the code already provides — notably that `ReviewDirectory` and `OverridesPath` were
declared for item 6 years-of-commits ago and wired to nothing, and that `RunManifest.Unread`
keeps its entries as finished sentences, the same defect Lot B fixed for the parts that did not
fit. Three plans, one per item:

| Item | Plan |
|---|---|
| 6 — asking a person about a region the reader could not make out | `plans/2026-08-30-answering-what-was-not-read.md` |
| 10 — turning a part to need fewer supports | `plans/2026-08-30-turning-a-part-spike.md` (a spike: the output is an answer, not a feature) |
| 12 — the application icon | `plans/2026-08-30-the-application-icon.md` |
