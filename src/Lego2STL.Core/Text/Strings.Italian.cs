namespace Lego2STL.Core.Text;

public sealed partial class Strings
{
    /// <summary>Reached through a property so this table is set up on its own, on first use.</summary>
    private static IReadOnlyDictionary<TextKey, string> ItalianTable => ItalianWords.Table;

    private static class ItalianWords
    {
        internal static readonly Dictionary<TextKey, string> Table = new()
        {
            // Le colonne dell'elenco pezzi. Questa e' la forma richiesta in origine, ed e'
            // quella che i file scritti dalle versioni precedenti portano gia'.
            [TextKey.CsvId] = "ID",
            [TextKey.CsvLegoCode] = "Codice Lego",
            [TextKey.CsvBrickLinkCode] = "Codice BrickLink",
            [TextKey.CsvColourName] = "Nome colore",
            [TextKey.CsvRgb] = "Codice RGB",
            [TextKey.CsvQuantity] = "Quantita",

            // Parole condivise.
            [TextKey.Millimetres] = "millimetri",
            [TextKey.On] = "attiva",
            [TextKey.Off] = "disattiva",
            [TextKey.Yes] = "sì",
            [TextKey.No] = "no",
            [TextKey.None] = "nessuno",

            // Resoconti.
            [TextKey.ReportRunTitle] = "Lego2STL - resoconto della lettura",
            [TextKey.ReportShapeTitle] = "Lego2STL - resoconto delle forme",
            [TextKey.ReportPlateTitle] = "Lego2STL - resoconto dei piani di stampa",
            [TextKey.ReportInput] = "File di partenza",
            [TextKey.ReportPages] = "Pagine",
            [TextKey.ReportColourNumbering] = "Numerazione colori",
            [TextKey.ReportReading] = "Lettura",
            [TextKey.ReportEntriesRead] =
                "{0} voci lette; {1} righe di quantità lette con i caratteri appresi dal documento.",
            [TextKey.ReportPartsList] = "Elenco pezzi",
            [TextKey.ReportTotals] = "Totali",
            [TextKey.ReportTotalEntries] = "{0} voci",
            [TextKey.ReportTotalPieces] = "{0} pezzi",
            [TextKey.ReportTotalDistinctParts] = "{0} codici pezzo distinti (una forma ciascuno)",
            [TextKey.ReportCouldNotBeRead] = "Non è stato possibile leggerle",
            [TextKey.ReportRecogniserReturned] = "il riconoscitore ha restituito: \"{0}\"",
            [TextKey.ReportGeometryFrom] = "Geometria da",
            [TextKey.ReportUnits] = "Unità",
            [TextKey.ReportStandingOnZero] = "appoggiate sullo zero",
            [TextKey.ReportOriginalOrigin] = "origine di partenza",
            [TextKey.ReportScale] = "Scala",
            [TextKey.ReportSeamRepair] = "Chiusura delle giunzioni",
            [TextKey.ReportClearance] = "Gioco",
            [TextKey.ReportColumnPart] = "pezzo",
            [TextKey.ReportColumnTriangles] = "tri",
            [TextKey.ReportColumnOpen] = "aperti",
            [TextKey.ReportColumnSeams] = "giunz",
            [TextKey.ReportColumnGaps] = "buchi",
            [TextKey.ReportColumnSize] = "dimensioni (mm)",
            [TextKey.ReportShapesClosed] = "{0} forme su {1} sono chiuse.",
            [TextKey.ReportSeamsClosedSummary] =
                "La chiusura delle giunzioni ha saldato {0} spigoli e completato {1} forme.",
            [TextKey.ReportGapsFilledSummary] =
                "La copertura dei buchi ne ha chiusi {0} su {1} forme.",
            [TextKey.ReportRetiredNumbers] =
                "Codici ritirati, la cui forma viene da un pezzo sostitutivo:",
            [TextKey.ReportBuiltWithSomethingMissing] = "Pezzi costruiti con qualcosa di mancante:",
            [TextKey.ReportProducedNothing] = "Non hanno prodotto nulla:",
            [TextKey.ReportPrintingNoteTitle] = "Nota sulla stampa",
            [TextKey.ReportPrintingNoteBody] =
                "Queste forme hanno le dimensioni nominali del catalogo, senza il gioco che un pezzo " +
                "stampato a iniezione ha davvero. Stampate a grandezza reale risultano leggermente " +
                "abbondanti su ogni faccia e non si incastrano finché non si toglie un gioco tarato " +
                "sulla propria stampante e sul proprio materiale. Il comando di taratura serve a " +
                "trovare quel valore, che poi si passa con --clearance.",
            [TextKey.ReportPrintingNoteWithClearance] =
                "Ogni faccia è stata rientrata di {0} mm rispetto alla dimensione nominale del " +
                "catalogo. Se i pezzi restano duri, aumentare il valore; se ballano, diminuirlo. Il " +
                "comando di taratura stampa una serie a valori diversi, così il valore giusto si " +
                "misura invece di indovinarlo.",
            [TextKey.ReportPlateSummary] = "{0} piani per {1} colori, {2} pezzi in tutto.",
            [TextKey.ReportPlateLine] = "{0} x {1}",
            [TextKey.ReportPlateColumnPlate] = "piano",
            [TextKey.ReportPlateColumnColour] = "colore",
            [TextKey.ReportPlateColumnPieces] = "pezzi",
            [TextKey.ReportPlateColumnFootprint] = "occupato (mm)",
            [TextKey.ReportPlateDidNotFit] = "Troppo grandi per il piano, quindi lasciati fuori:",

            // Esecuzione.
            [TextKey.MsgPagesInDocument] = "{0}: {1} pagine.",
            [TextKey.MsgReadingPages] = "Lettura delle pagine {0} con la numerazione colori {1}.",
            [TextKey.MsgNoPageRange] =
                "manca l'intervallo di pagine. Indicarlo, ad esempio \"2-5\", oppure usare " +
                "--list-pages per vedere che cosa c'è su ogni pagina.",
            [TextKey.MsgNoSuchFile] = "file inesistente: {0}",
            [TextKey.MsgNoSuchPartsList] = "elenco pezzi inesistente: {0}",
            [TextKey.MsgPageIsCatalogueOne] = "pagina {0}  catalogo, {1} voce",
            [TextKey.MsgPageIsCatalogueMany] = "pagina {0}  catalogo, {1} voci",
            [TextKey.MsgNoCataloguePages] = "Nessuna pagina di catalogo trovata.",
            [TextKey.MsgSuggestedRange] = "Intervallo suggerito: {0}",
            [TextKey.MsgEntriesSummary] = "{0} voci, {1} pezzi, {2} codici pezzo distinti.",
            [TextKey.MsgPartsListWritten] = "Elenco pezzi: {0}",
            [TextKey.MsgReportWritten] = "Resoconto:    {0}",
            [TextKey.MsgCouldNotReadEntriesOne] = "Non è stato possibile leggere {0} voce:",
            [TextKey.MsgCouldNotReadEntriesMany] = "Non è stato possibile leggere {0} voci:",
            [TextKey.MsgWrittenWithoutThem] =
                "L'elenco pezzi è stato scritto senza di esse. Le fasi successive restano bloccate " +
                "finché non sono risolte.",
            [TextKey.MsgEntriesAndParts] = "{0} voci, {1} codici pezzo distinti.",
            [TextKey.MsgWroteShapes] = "Scritte {0} forme in {1}",
            [TextKey.MsgClosedAndOpen] =
                "{0} chiuse, {1} con spigoli aperti (uno slicer le ripara, e il resoconto le elenca).",
            [TextKey.MsgProducedNothing] = "{0} pezzi non hanno prodotto nulla:",
            [TextKey.MsgWrotePlates] = "Scritti {0} piani di stampa in {1}",
            [TextKey.MsgNoPlatesRequested] = "I piani di stampa non sono stati richiesti.",
            [TextKey.MsgPlatesSkippedUnverified] =
                "I piani di stampa sono stati saltati perché alcuni pezzi non hanno prodotto nulla.",
            [TextKey.MsgCalibrationWritten] = "Scritte {0} forme di taratura in {1}",
            [TextKey.CalibrationTitle] = "Trovare il gioco che serve alla propria stampante",
            [TextKey.CalibrationHow] =
                "Qui c'è {0}, una volta per ciascuno di questi giochi in millimetri: {1}. "
                + "Stampare tutta la serie con il materiale che si intende usare, sulla "
                + "macchina che si intende usare, con le impostazioni che si intendono usare. "
                + "Niente di tutto questo si può prendere dalla stampante di qualcun altro: il "
                + "valore cercato è più piccolo della differenza fra due macchine dello stesso "
                + "modello.",
            [TextKey.CalibrationThen] =
                "Poi provarli. Salire dal più piccolo finché i pezzi si uniscono senza forzare "
                + "e restano uniti se si scuotono. Quel gioco è il proprio: passarlo al comando "
                + "build come --clearance, insieme a --repair perché possa essere applicato "
                + "anche ai pezzi con buchi nella superficie. Ricontrollarlo se si cambia "
                + "materiale, ugello o stampante.",
            [TextKey.MsgClearanceApplied] =
                "Ogni faccia rientrata di {2} mm su {0} forme di {1}.",
            [TextKey.MsgClearanceRefusedOpen] =
                "{0} lasciate a grandezza reale: la loro superficie ha ancora dei buchi, e " +
                "rientrare le facce trascinerebbe anche i bordi di quei buchi. Aggiungere " +
                "--repair per coprirli prima.",
            [TextKey.MsgClearanceRefusedStillOpen] =
                "{0} lasciate a grandezza reale: la loro superficie ha buchi che non è stato "
                + "possibile coprire, perché al bordo del buco la superficie si dirama invece "
                + "di chiudersi ad anello. Uno slicer le ripara comunque in fase di stampa.",
            [TextKey.MsgClearanceRefusedThin] =
                "{0} lasciate a grandezza reale: togliere tanto da ogni faccia le farebbe sparire.",
            [TextKey.MsgCancelled] = "Annullato.",
            [TextKey.MsgError] = "Errore",

            // Quando qualcosa non va.
            [TextKey.ErrClearanceNeedsClosedShape] =
                "{0} ha ancora {1} spigoli aperti: rientrare ogni faccia di una quantità fissa " +
                "sposterebbe anche i bordi dei buchi e deformerebbe la forma. È stata scritta a " +
                "grandezza reale. Lasciare attiva la chiusura delle giunzioni, oppure escludere " +
                "questo pezzo.",
            [TextKey.ErrClearanceNegative] = "Il gioco non può essere negativo; ricevuto {0} mm.",
            [TextKey.ErrPartTooSmallForClearance] =
                "{0} misura solo {1} mm nel punto più sottile, quindi togliere {2} mm da ogni faccia " +
                "lo farebbe sparire. È stato scritto a grandezza reale.",
            [TextKey.ErrPlateTooSmall] =
                "{0} misura {1} e non entra in un piano da {2}, quindi non è su nessun piano. La sua " +
                "forma resta comunque disponibile da sistemare a mano.",
            [TextKey.ErrOcrUnavailable] =
                "Leggere un documento richiede il riconoscimento del testo, che qui non è disponibile.",
            [TextKey.ErrOpenScadNotFound] =
                "OpenSCAD non è stato trovato. Installarlo da openscad.org e metterlo nel percorso " +
                "oppure indicarlo con --openscad.",

            // L'interfaccia.
            [TextKey.UiTitle] = "Lego2STL",
            [TextKey.UiTabInput] = "Partenza",
            [TextKey.UiTabOptions] = "Opzioni",
            [TextKey.UiTabRun] = "Esecuzione",
            [TextKey.UiTabCatalogue] = "Catalogo",
            [TextKey.UiNext] = "Avanti",
            [TextKey.UiBack] = "Indietro",
            [TextKey.UiStart] = "Avvia",
            [TextKey.UiCancel] = "Annulla",
            [TextKey.UiBrowse] = "Sfoglia...",
            [TextKey.UiInputKind] = "Da che cosa si parte?",
            [TextKey.UiInputDocument] = "Da un documento",
            [TextKey.UiInputPartsList] = "Da un elenco pezzi",
            [TextKey.UiInputSetNumber] = "Da un numero di set",
            [TextKey.UiChooseDocument] = "Documento",
            [TextKey.UiChoosePartsList] = "Elenco pezzi",
            [TextKey.UiSetNumber] = "Numero di set",
            [TextKey.UiPageRange] = "Pagine",
            [TextKey.UiPageRangeHint] = "ad esempio 2-5, oppure 2-5,8,11-13",
            [TextKey.UiScanPages] = "Trova le pagine del catalogo",
            [TextKey.UiScanning] = "Scorro le pagine...",
            [TextKey.UiColourScheme] = "Numerazione colori usata nel documento",
            [TextKey.UiLanguage] = "Lingua",
            [TextKey.UiSettings] = "Impostazioni",
            [TextKey.UiEquivalentCommand] = "Lo stesso comando da terminale",
            [TextKey.UiCopyCommand] = "Copia",
            [TextKey.UiCopied] = "Copiato",
            [TextKey.UiGroupStages] = "Fasi",
            [TextKey.UiGroupOutput] = "Risultato",
            [TextKey.UiGroupGeometry] = "Geometria",
            [TextKey.UiGroupLibrary] = "Libreria delle forme",
            [TextKey.UiGroupPlates] = "Piani di stampa",
            [TextKey.UiGroupBehaviour] = "Comportamento",
            [TextKey.UiProgress] = "Avanzamento",
            [TextKey.UiLog] = "Dettagli",
            [TextKey.UiShowLog] = "Mostra i dettagli",
            [TextKey.UiHideLog] = "Nascondi i dettagli",
            [TextKey.UiCatalogueEmpty] =
                "Ancora niente. Al termine di un'esecuzione i pezzi compaiono qui.",
            [TextKey.UiFilterByColour] = "Colore",
            [TextKey.UiAllColours] = "Tutti i colori",
            [TextKey.UiSortBy] = "Ordina per",
            [TextKey.UiSortByPart] = "Codice pezzo",
            [TextKey.UiSortByQuantity] = "Quantità",
            [TextKey.UiSortByColour] = "Colore",
            [TextKey.UiOpenShapeFile] = "Apri la forma",
            [TextKey.UiOpenPlate] = "Apri il piano",
            [TextKey.UiOpenFolder] = "Apri la cartella",
            [TextKey.UiWarningNotClosed] = "Questa forma ha spigoli aperti; uno slicer la ripara.",
            [TextKey.UiWarningThinFeature] =
                "Questo pezzo ha dettagli più sottili di un ugello da 0,4 mm.",
            [TextKey.UiQuantity] = "Quantità",
            [TextKey.UiDone] = "Fatto",
            [TextKey.UiFailed] = "Non riuscito",
            [TextKey.UiIdle] = "Pronto",
        };
    }
}
