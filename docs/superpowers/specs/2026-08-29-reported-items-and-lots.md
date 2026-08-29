# The reported items, and the lots they were split into

**Date:** 2026-08-29
**Status:** the record. Lots A and B are merged; C is being designed; D is untouched.

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
| **C** | 9, 14 | designed here; not yet built |
| **D** | 6 (the popup), 10, 12 | not started |

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

## Lot C — decisions taken so far

Being brainstormed on 2026-08-29. The design document will supersede this section; until it
exists, these are the answers already given.

- **Two faults, not one.** The material says "this cannot be printed at all" (item 9); the
  absence of a shape says "this could not be built" (item 14). Different messages on the card and
  different offers. Measured on run `6324712`: of the four parts that produced nothing,
  `5102c13/17/21` are rubber hoses and `40918` is a plastic linear actuator that LDraw has no
  file for — the same symptom from two different causes.
- **The material comes from the local dump.** `DB Lego/parts.csv` carries `part_material`
  (Plastic, Cardboard/Paper, Cloth, Rubber, Foam, Flexible Plastic, Metal) and no code reads it
  today. Rebrickable's API does not expose it. Reading the dump works offline.
- **The picture comes from two places.** A crop from the PDF when the run came from one — it
  works offline and is the very part the book shows — and Rebrickable's `part_img_url` for runs
  from a CSV or a set number, and as a fallback. Note that the run today knows where the label's
  *text* is, not where the drawing above it is, so the crop is new work.

Still open: which shops the settings list offers, and what the card does for a part that is
neither printable nor buyable.

---

## Lot D — what is left

- **Item 6, the popup.** Lot A fixed only the wording ("page 372 at" appearing in English in an
  Italian run). The dialogue that shows the unread crop and asks for part, colour and quantity —
  with *Ok*, *Skip* and *Not a LEGO code* — was never built.
- **Item 10, turning a part to need fewer supports.** Still a spike: nobody has measured whether
  it can be decided from the geometry the pipeline already has.
- **Item 12, the application icon.** 16, 32, 64 and 128 px, as `.ico`, `.png` and `.svg`.
