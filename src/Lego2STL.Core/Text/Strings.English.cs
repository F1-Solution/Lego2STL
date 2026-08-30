namespace Lego2STL.Core.Text;

public sealed partial class Strings
{
    /// <summary>Reached through a property so this table is set up on its own, on first use.</summary>
    private static IReadOnlyDictionary<TextKey, string> EnglishTable => EnglishWords.Table;

    private static class EnglishWords
    {
        internal static readonly Dictionary<TextKey, string> Table = new()
        {
            // The parts list's columns.
            [TextKey.CsvId] = "ID",
            [TextKey.CsvLegoCode] = "Lego Code",
            [TextKey.CsvBrickLinkCode] = "BrickLink Code",
            [TextKey.CsvColourName] = "Colour Name",
            [TextKey.CsvRgb] = "RGB Code",
            [TextKey.CsvQuantity] = "Quantity",

            // Shared words.
            [TextKey.Millimetres] = "millimetres",
            [TextKey.On] = "on",
            [TextKey.Off] = "off",
            [TextKey.Yes] = "yes",
            [TextKey.No] = "no",
            [TextKey.None] = "none",

            // Reports.
            [TextKey.ReportRunTitle] = "Lego2STL run report",
            [TextKey.ReportShapeTitle] = "Lego2STL shape report",
            [TextKey.ReportPlateTitle] = "Lego2STL plate report",
            [TextKey.ReportInput] = "Input",
            [TextKey.ReportPages] = "Pages",
            [TextKey.ReportColourNumbering] = "Colour numbers",
            [TextKey.ReportReading] = "Reading",
            [TextKey.ReportEntriesRead] =
                "{0} entries read; {1} quantity line(s) came from the learned lettering.",
            [TextKey.ReportPartsList] = "Parts list",
            [TextKey.ReportTotals] = "Totals",
            [TextKey.ReportTotalEntries] = "{0} entries",
            [TextKey.ReportTotalPieces] = "{0} pieces",
            [TextKey.ReportTotalDistinctParts] = "{0} distinct part numbers (one shape each)",
            [TextKey.ReportCouldNotBeRead] = "Could not be read",
            [TextKey.ReportRecogniserReturned] = "recogniser returned: \"{0}\"",
            [TextKey.ReportGeometryFrom] = "Geometry from",
            [TextKey.ReportUnits] = "Units",
            [TextKey.ReportStandingOnZero] = "standing on zero",
            [TextKey.ReportOriginalOrigin] = "original origin",
            [TextKey.ReportScale] = "Scale",
            [TextKey.ReportSeamRepair] = "Seam repair",
            [TextKey.ReportClearance] = "Clearance",
            [TextKey.ReportColumnPart] = "part",
            [TextKey.ReportColumnTriangles] = "tris",
            [TextKey.ReportColumnOpen] = "open",
            [TextKey.ReportColumnSeams] = "seams",
            [TextKey.ReportColumnGaps] = "gaps",
            [TextKey.ReportColumnSize] = "size (mm)",
            [TextKey.ReportShapesClosed] = "{0} of {1} shapes are closed.",
            [TextKey.ReportSeamsClosedSummary] =
                "Seam repair closed {0} edge(s) and completed {1} shape(s).",
            [TextKey.ReportGapsFilledSummary] =
                "Covering gaps closed {0} of them across {1} shape(s).",
            [TextKey.ReportRetiredNumbers] =
                "Retired numbers, where the shape comes from a replacement part:",
            [TextKey.ReportBuiltWithSomethingMissing] = "Parts built with something missing:",
            [TextKey.ReportProducedNothing] = "Produced nothing:",
            [TextKey.ReportPrintingNoteTitle] = "Note on printing",
            [TextKey.ReportPrintingNoteBody] =
                "These shapes are the catalogue's nominal dimensions, with no allowance for the " +
                "clearance a real moulded part has. Printed at true size they will be slightly " +
                "oversized on every face and will not clip together without a clearance allowance " +
                "calibrated for your own printer and material. Run the calibration command to find " +
                "that figure, then pass it with --clearance.",
            [TextKey.ReportPrintingNoteWithClearance] =
                "Every face has been taken in by {0} mm from the catalogue's nominal size. If parts " +
                "still bind, raise the figure; if they fall apart, lower it. The calibration command " +
                "prints a set at several values so the right one can be measured rather than guessed.",
            [TextKey.ReportPlateSummary] = "{0} plate(s) for {1} colour(s), {2} piece(s) in total.",
            [TextKey.ReportPlateLine] = "{0} x {1}",
            [TextKey.ReportPlateColumnPlate] = "plate",
            [TextKey.ReportPlateColumnColour] = "colour",
            [TextKey.ReportPlateColumnPieces] = "pieces",
            [TextKey.ReportPlateColumnFootprint] = "used (mm)",
            [TextKey.ReportPlateDidNotFit] = "Too big for the bed, so left off the plates:",
            [TextKey.ReportPlateMissingParts] =
                "{0} part(s) produced no shape and are on no plate. They are listed above; the "
                + "plates hold everything else.",

            // The shape library.
            [TextKey.LibraryFolder] = "folder {0}",
            [TextKey.LibraryArchive] = "archive {0}",
            [TextKey.LibraryWebsite] = "the LDraw library website, one file at a time",
            [TextKey.LibraryNone] = "no LDraw source",
            [TextKey.MsgLDrawUsingFolder] = "Using the LDraw library already on disk: {0}.",
            [TextKey.MsgLDrawNotALibrary] =
                "'{0}' does not look like an LDraw library (no 'parts' or 'p' folder); ignoring it.",
            [TextKey.MsgLDrawUsingDownloaded] =
                "Using the LDraw library downloaded earlier ({0} files).",
            [TextKey.MsgLDrawFetchingPerFile] =
                "Fetching LDraw files from the library website as they are needed.",
            [TextKey.MsgLDrawRefused] =
                "The library website refused {0} requests; downloading the whole library instead, "
                + "which is faster from here on.",
            [TextKey.MsgLDrawDownloading] =
                "Downloading the LDraw library from {0} (about 145 MB, once).",
            [TextKey.MsgLDrawReady] = "LDraw library ready: {0} files, cached at {1}.",

            // Running.
            [TextKey.MsgPagesInDocument] = "{0}: {1} pages.",
            [TextKey.MsgReadingPages] = "Reading pages {0} using {1} colour numbering.",
            [TextKey.MsgReadingPrintedPages] =
                "Reading pages {0} from the text the document itself carries.",
            [TextKey.MsgLookingUpElements] =
                "No element table to hand, so {0} element numbers will be looked up online, "
                + "about a second each. A local elements.csv given with --element-map does "
                + "the same instantly.",
            [TextKey.MsgNoPageRange] =
                "no page range given. Pass one, e.g. \"2-5\", or use --list-pages to see what is on " +
                "each page.",
            [TextKey.MsgNoSuchFile] = "no such file: {0}",
            [TextKey.MsgNoSuchPartsList] = "no such parts list: {0}",
            [TextKey.MsgPageIsCatalogueOne] = "page {0}  catalogue, {1} entry",
            [TextKey.MsgPageIsCatalogueMany] = "page {0}  catalogue, {1} entries",
            [TextKey.MsgNoCataloguePages] = "No catalogue pages found.",
            [TextKey.MsgNoCatalogueInThisBook] =
                "This document prints no parts catalogue. Official instructions that run to "
                + "several books carry the parts list in only one of them, usually the last; "
                + "try the others.",
            [TextKey.MsgCataloguePagesFound] = "Catalogue pages: {0}",
            [TextKey.MsgShapeClosed] = "closed",
            [TextKey.MsgShapeOpenEdges] = "{0} open edge(s)",
            [TextKey.MsgLookingUpSet] = "Looking up set {0}.",
            [TextKey.MsgSetSummary] = "Set {0}: {1} entries, {2} pieces.",
            [TextKey.MsgSuggestedRange] = "Suggested range: {0}",
            [TextKey.MsgEntriesSummary] = "{0} entries, {1} pieces, {2} distinct parts.",
            [TextKey.MsgPartsListWritten] = "Parts list: {0}",
            [TextKey.MsgReportWritten] = "Report:     {0}",
            [TextKey.MsgCouldNotReadEntriesOne] = "Could not read {0} entry:",
            [TextKey.MsgCouldNotReadEntriesMany] = "Could not read {0} entries:",
            [TextKey.MsgUnreadEntryAt] = "page {0} at {1}: {2}",
            [TextKey.ReasonCouldNotReadQuantity] = "could not read the quantity",
            [TextKey.ReasonCouldNotReadPartAndColour] =
                "could not read the part number and colour",
            [TextKey.ReasonCouldNotReadEither] =
                "could not read the quantity or the part number and colour",
            [TextKey.ReasonUnknownElement] =
                "element {0} could not be matched to a part and a colour",
            [TextKey.ReasonShapeHasNoSurfaces] = "the shape file contains no surfaces",
            [TextKey.ReasonNoShapeFile] = "no shape file for this part number",
            [TextKey.MsgWrittenWithoutThem] =
                "The parts list was written without them. Later stages are refused until they are settled.",
            [TextKey.MsgEntriesAndParts] = "{0} entries, {1} distinct parts.",
            [TextKey.MsgWroteShapes] = "Wrote {0} shape file(s) to {1}",
            [TextKey.MsgNotPrintedMaterial] = "{0} is {1}, so it was not built; buy it instead.",
            [TextKey.MsgNotPrintedKind] = "{0} is a part to buy rather than print, so it was not built.",
            [TextKey.ReportNotPrintedTitle] = "Not printed:",
            [TextKey.MsgClosedAndOpen] =
                "{0} closed, {1} with open edges (a slicer will repair those, and the report lists them).",
            [TextKey.MsgProducedNothing] = "{0} part(s) produced nothing:",
            [TextKey.MsgWrotePlates] = "Wrote {0} plate file(s) to {1}",
            [TextKey.MsgNoPlatesRequested] = "Plates were not asked for.",
            [TextKey.MsgPlatesMissingParts] =
                "{0} part(s) produced nothing, so they are on no plate. The rest were arranged "
                + "as usual and the report lists what is absent.",
            [TextKey.MsgCalibrationWritten] = "Wrote {0} calibration shape(s) to {1}",
            [TextKey.CalibrationTitle] = "Finding the clearance your printer needs",
            [TextKey.CalibrationHow] =
                "Here is {0}, once for each of these clearances in millimetres: {1}. Print the "
                + "whole set in the material you mean to use, on the machine you mean to use, "
                + "with the settings you mean to use. Nothing here can be carried over from "
                + "someone else's printer: the figure being looked for is smaller than the "
                + "difference between two machines of the same model.",
            [TextKey.CalibrationThen] =
                "Then try them. Work up from the smallest until parts go together without "
                + "forcing and stay together when shaken. That clearance is yours: pass it to "
                + "the build command as --clearance. Check it again if you change material, "
                + "nozzle or printer.",
            [TextKey.MsgClearanceApplied] =
                "Took every face in by {2} mm on {0} of {1} shape(s).",
            [TextKey.MsgClearanceRefusedOpen] =
                "{0} left at true size: their surfaces still have gaps, and pulling the faces in " +
                "would drag the edges of those gaps too. Drop --no-repair so the gaps are covered first.",
            [TextKey.MsgClearanceRefusedStillOpen] =
                "{0} left at true size: their surfaces have gaps that could not be covered, "
                + "because the surface branches at the edge of the gap rather than closing "
                + "into a ring. A slicer will still repair these when it prints them.",
            [TextKey.MsgClearanceRefusedThin] =
                "{0} left at true size: taking that much off every face would consume them.",
            [TextKey.MsgCancelled] = "Cancelled.",
            [TextKey.MsgError] = "Error",

            // Things that go wrong.
            [TextKey.ErrChooseDocument] = "Choose a document to read.",
            [TextKey.ErrChoosePartsList] = "Choose a parts list to build from.",
            [TextKey.ErrTypeSetNumber] = "Type a set number, for example 42100-1.",
            [TextKey.ErrNoFileAt] = "There is no file at {0}.",
            [TextKey.ErrScaleNotPositive] = "A scale has to be greater than zero.",
            [TextKey.ErrSpacingNegative] = "The gap between parts cannot be negative.",
            [TextKey.ErrNotABedSize] =
                "'{0}' is not a bed size. One looks like 220x220 or 300x300x400.",
            [TextKey.ErrClearanceNeedsClosedShape] =
                "{0} still has {1} open edge(s), so taking every face in by a fixed amount would move " +
                "the edges of the holes as well and distort the shape. It was written at true size " +
                "instead. Leave seam repair on, or exclude this part.",
            [TextKey.ErrClearanceNegative] = "A clearance cannot be negative; got {0} mm.",
            [TextKey.ErrPartTooSmallForClearance] =
                "{0} is only {1} mm across at its thinnest, so taking {2} mm off every face would " +
                "consume it. It was written at true size instead.",
            [TextKey.ErrPlateTooSmall] =
                "{0} measures {1} and does not fit a {2} bed, so it is not on any plate. Its shape " +
                "file is still there to place by hand.",
            [TextKey.ErrPartTooTallForBed] =
                "{0} stands {1} mm tall, more than the {2} mm this printer has.",
            [TextKey.ErrOcrUnavailable] =
                "Reading a document needs text recognition, and this system has none: the "
                + "recogniser the tool uses is part of Windows. Everything after the parts list "
                + "works here, so run 'extract' on Windows once and bring the parts list over, "
                + "or start from a set number instead.",
            [TextKey.ErrOcrWrongBuild] =
                "Reading a document needs text recognition, which this build does not have, "
                + "although Windows itself does: this is the plain build. Run the Windows one "
                + "instead - target framework {0} - which in Visual Studio is the framework "
                + "chosen next to the run button. Everything else works in this build.",
            [TextKey.ErrOpenScadNotFound] =
                "OpenSCAD was not found. Install it from openscad.org and either put it on the path " +
                "or name it with --openscad.",
            [TextKey.ErrUnknownColourCode] =
                "Page {0}: part {1} has colour {2}, which is not a known {3} colour.",
            [TextKey.ErrColourSchemeHintBrickLink] =
                "If the document uses a different numbering, say so with --color-scheme.",
            [TextKey.ErrColourSchemeHintOther] = "Is --color-scheme {0} right for this document?",
            [TextKey.ErrColourHasNoBrickLinkCode] =
                "Page {0}: part {1} is '{2}', which has no BrickLink colour number, so it cannot be "
                + "written to that column.",
            [TextKey.ErrSetCameBackEmpty] =
                "Set {0} came back with nothing usable. Check the number: Rebrickable wants the "
                + "variant suffix, so 42100 means 42100-1.",
            [TextKey.ErrChooseDocument] = "Choose a document to read.",
            [TextKey.ErrChoosePartsList] = "Choose a parts list to build from.",
            [TextKey.ErrTypeSetNumber] = "Type a set number, for example 42100-1.",
            [TextKey.ErrNoFileAt] = "There is no file at {0}.",
            [TextKey.ErrScaleNotPositive] = "A scale has to be greater than zero.",
            [TextKey.ErrNotABedSize] =
                "'{0}' is not a bed size. One looks like 220x220 or 300x300x400.",

            // The interface.
            [TextKey.UiTitle] = "Lego2STL",
            [TextKey.UiTabRun] = "Run",
            [TextKey.UiRailRuns] = "Runs",
            [TextKey.UiRailSetup] = "Setup",
            [TextKey.UiNewRun] = "New run",
            [TextKey.UiNext] = "Next",
            [TextKey.UiBack] = "Back",
            [TextKey.UiStart] = "Start",
            [TextKey.UiCancel] = "Cancel",
            [TextKey.UiBrowse] = "Browse...",
            [TextKey.UiInputKind] = "What are you starting from?",
            [TextKey.UiInputDocument] = "A document",
            [TextKey.UiInputPartsList] = "A parts list",
            [TextKey.UiInputSetNumber] = "A set number",
            [TextKey.UiChooseDocument] = "Document",
            [TextKey.UiChoosePartsList] = "Parts list",
            [TextKey.UiSetNumber] = "Set number",
            [TextKey.UiPageRange] = "Pages",
            [TextKey.UiPageRangeHint] = "for example 2-5, or 2-5,8,11-13",
            [TextKey.UiScanPages] = "Find the catalogue pages",
            [TextKey.UiScanning] = "Looking through the pages...",
            [TextKey.UiColourScheme] = "Colour numbering in the document",
            [TextKey.UiLanguage] = "Language",
            [TextKey.UiSettings] = "Settings",
            [TextKey.UiEquivalentCommand] = "The same thing from the command line",
            [TextKey.UiCopyCommand] = "Copy command",
            [TextKey.UiCopied] = "Copied",
            [TextKey.UiGroupStages] = "Stages",
            [TextKey.UiGroupOutput] = "Output",
            [TextKey.UiGroupGeometry] = "Geometry",
            [TextKey.UiGroupLibrary] = "Shape library",
            [TextKey.UiGroupPlates] = "Plates",
            [TextKey.UiGroupBehaviour] = "Behaviour",
            [TextKey.UiProgress] = "Progress",
            [TextKey.UiLog] = "Details",
            [TextKey.UiShowLog] = "Show details",
            [TextKey.UiHideLog] = "Hide details",
            [TextKey.UiCatalogueEmpty] = "Nothing yet. Run the pipeline and the parts appear here.",
            [TextKey.UiFilterByColour] = "Colour",
            [TextKey.UiAllColours] = "All colours",
            [TextKey.UiSortBy] = "Sort by",
            [TextKey.UiSortByPart] = "Part number",
            [TextKey.UiSortByQuantity] = "Quantity",
            [TextKey.UiSortByColour] = "Colour",
            [TextKey.UiOpenShapeFile] = "Open the shape file",
            [TextKey.UiOpenPlate] = "Open the plate",
            [TextKey.UiOpenFolder] = "Open the folder",
            [TextKey.UiWarningNotClosed] = "This shape has open edges; a slicer will repair it.",
            [TextKey.UiWarningSelfIntersects] =
                "Some of this shape's surfaces pass through each other; a slicer will still print it.",
            [TextKey.UiWarningThinFeature] = "This part has features thinner than a 0.4 mm nozzle.",
            [TextKey.UiDoesNotFitThePlate] = "Too big for the plate, so it is on none.",
            [TextKey.UiSomePartsDoNotFit] =
                "{0} part(s) do not fit the plate. Everything fits at {1}%.",
            [TextKey.UiTryASmallerScale] = "Start again at {0}%",
            [TextKey.UiNumbering] = "Numbering",
            [TextKey.UiNoElementNumber] = "no element number",
            [TextKey.UiNumberingBrickLink] = "BrickLink part number",
            [TextKey.UiNumberingLegoElement] = "LEGO element number",
            [TextKey.UiQuantity] = "Quantity",
            [TextKey.UiDone] = "Done",
            [TextKey.UiFailed] = "Failed",
            [TextKey.UiIdle] = "Ready",
            [TextKey.UiEveryOptionHere] =
                "Every option the command line takes is on this screen. The line at the foot of "
                + "the window is exactly what these settings amount to.",

            // How a run stands, and where it got to if it did not finish.
            [TextKey.UiStatusRunning] = "Running",
            [TextKey.UiStatusComplete] = "Complete",
            [TextKey.UiStatusNeedsDecision] = "Needs a decision",
            [TextKey.UiStatusFailed] = "Failed",
            [TextKey.UiStatusStopped] = "Stopped",
            [TextKey.UiStoppedAt] = "stopped while {0}, {1} of {2}",

            // The list of runs that have happened.
            [TextKey.UiRunsEmpty] = "No runs yet. Set one up and it appears here.",
            [TextKey.UiForget] = "Forget",
            [TextKey.UiForgetEverything] = "Forget every run",
            [TextKey.UiFolderMissing] = "This folder is no longer there.",

            // What a run's page says when its record is missing or from a newer build.
            [TextKey.UiNoManifest] =
                "This run was made before runs kept a record of themselves, so what its shapes " +
                "measured is not known. Run it again to fill it in.",
            [TextKey.UiNewerManifest] =
                "This run was recorded by a newer version of the program. What follows is the " +
                "part this one understands.",
            [TextKey.UiRunItAgain] = "Run it again",
            [TextKey.UiOpenPartsList] = "Open the parts list",
            [TextKey.UiContinueFromPartsList] = "Continue from the parts list",

            // Finding a way through the options.
            [TextKey.UiSearchOptions] = "Search the options",
            [TextKey.UiChangedOnly] = "Only what I changed",
            [TextKey.UiHiddenCount] = "{0} hidden",
            [TextKey.UiReset] = "Reset",

            // The open run.
            [TextKey.UiQuietNote] =
                "Nothing is being shown here because of --quiet. It is all in run.log.",
            [TextKey.UiWhen] = "When",
            [TextKey.UiRunFolder] = "Run folder",

            // The steps of a run, named for a screen rather than for a log.
            [TextKey.UiStageReadingDocument] = "Reading the document",
            [TextKey.UiStageLookingUpSet] = "Looking up the set",
            [TextKey.UiStageReadingPartsList] = "Reading the parts list",
            [TextKey.UiStageWritingPartsList] = "Writing the parts list",
            [TextKey.UiStageGatheringShapes] = "Gathering the shapes",
            [TextKey.UiStageBuildingShapes] = "Building the shapes",
            [TextKey.UiStageArrangingPlates] = "Arranging the plates",
            [TextKey.UiStageWritingReport] = "Writing the report",

            // What --help says.
            [TextKey.HelpRoot] =
                "Turn a LEGO parts catalogue into a parts list and shapes you can print.",
            [TextKey.HelpExtract] = "Read a catalogue out of a document and take it onward.",
            [TextKey.HelpBuild] =
                "Turn a parts list, or a set number, into shape files and coloured plates.",
            [TextKey.HelpCalibration] =
                "Write one small part at several clearances, to find the one your printer needs.",
            [TextKey.HelpBricks] =
                "Make a piece to a size, using your own OpenSCAD and your own copy of MachineBlocks.",
            [TextKey.HelpRefreshColors] =
                "Rebuild the colour cross-reference from the Rebrickable API.",
            [TextKey.HelpArgDocument] = "The document to read.",
            [TextKey.HelpArgPages] =
                "Which pages hold the catalogue, e.g. 2-5 or 2-5,8,11-13. Omit to search for them.",
            [TextKey.HelpArgPartsList] =
                "A parts list from an earlier run. Leave it out when using --set.",
            [TextKey.HelpArgSizes] =
                "Sizes to make, e.g. 2x4, or 2x4x6 to say how many plates tall.",
            [TextKey.HelpArgSteps] = "The clearances to try, in millimetres, separated by commas.",
            [TextKey.HelpOptLang] =
                "Language for messages, the help, the report, and the colour names in the parts list and the plate file names: en or it. Defaults to the machine's own.",
            [TextKey.HelpOptOutputDir] =
                "Where to put the run folder. Defaults to beside the input.",
            [TextKey.HelpOptAscii] = "Write the readable form of STL instead of the compact one.",
            [TextKey.HelpOptOffline] =
                "Never use the network; a part that is not already available is reported.",
            [TextKey.HelpOptLDrawDir] =
                "An existing LDraw library to use instead of downloading anything.",
            [TextKey.HelpOptApiKey] =
                "A Rebrickable key, for looking up a set. Also read from REBRICKABLE_API_KEY.",
            [TextKey.HelpOptListPages] =
                "Report what is on each page and stop, without reading anything.",
            [TextKey.HelpOptColorScheme] = "Whose colour numbering the document prints.",
            [TextKey.HelpOptElementMap] =
                "A Rebrickable elements.csv, or a folder holding one, for documents that print "
                + "element numbers. Found by itself beside the document when not given.",
            [TextKey.HelpOptSet] =
                "A set number to look up instead of reading a parts list, e.g. 42100-1.",
            [TextKey.HelpOptIncludeSpares] =
                "Keep a set's spare pieces as well as the ones the model uses.",
            [TextKey.HelpOptCsvOnly] = "Write the parts list and stop, without making any shapes.",
            [TextKey.HelpOptNoPlates] = "Write the shape files but no coloured plates.",
            [TextKey.HelpOptDelimiter] =
                "Separator for the parts list, or \"tab\". Defaults to a semicolon.",
            [TextKey.HelpOptKeepOrigin] =
                "Keep each part's own origin instead of standing it on the bed.",
            [TextKey.HelpOptScale] = "Scale as a percentage. 100 is true size.",
            [TextKey.HelpOptClearance] =
                "Take every face in by this many millimetres, so printed parts clip together. Use the calibration command to find the right figure for your printer.",
            [TextKey.HelpOptNoRepair] =
                "Leave the gaps a shape's surface arrives with, instead of covering them over. Covering is on by default, because a shape with gaps is not a solid and no clearance can be applied to it.",
            [TextKey.HelpOptNoSeamRepair] =
                "Skip closing seams where a corner lies part-way along another edge.",
            [TextKey.HelpOptPrintEverything] =
                "Build every part, including the ones that cannot be printed, such as electronics.",
            [TextKey.HelpOptWeldTolerance] =
                "How close two corners have to be to count as the same point, in source units where one unit is 0.4 mm. Not millimetres: corners are merged before the shape is converted.",
            [TextKey.HelpOptLDrawCache] = "Where to keep fetched shape files between runs.",
            [TextKey.HelpOptNoUnofficial] =
                "Use only the official shape library, not the unofficial collection.",
            [TextKey.HelpOptPrinter] = "Which printer's bed to lay plates out for: {0}.",
            [TextKey.HelpOptPlateSize] =
                "A bed size in millimetres instead of a printer name, e.g. 220x220 or 300x300x400.",
            [TextKey.HelpOptPlateSpacing] = "Millimetres to leave between parts on a plate.",
            [TextKey.HelpOptQuiet] =
                "Say only what matters: warnings, failures and where things were written.",
            [TextKey.HelpOptLog] = "Also write everything said during the run to this file.",
            [TextKey.HelpOptPart] = "Which part to print the set from. Repeat for several.",
            [TextKey.HelpOptKind] =
                "Brick (three plates), plate (one), or tile (one, smooth on top).",
            [TextKey.HelpOptNoKnobs] = "Leave the top smooth. A tile is already smooth.",
            [TextKey.HelpOptNoStudHoles] =
                "Make it solid underneath. The hollow tubes are what make a piece grip.",
            [TextKey.HelpOptOpenScad] = "Where OpenSCAD is, when it is not on the path.",
            [TextKey.HelpOptMachineBlocks] =
                "Your copy of the MachineBlocks library. Also read from MACHINEBLOCKS_DIR.",
            [TextKey.HelpOptScadOnly] =
                "Write the OpenSCAD description and stop, without making a shape.",
            [TextKey.HelpOptBricksOutputDir] =
                "Where to write. Defaults to a folder here named after the command.",
            [TextKey.HelpOptOut] =
                "Where to write the reference file. Defaults to the copy inside the source tree.",
            [TextKey.HelpOptRebrickableDump] =
                "A folder holding Rebrickable's downloadable tables, used instead of the API.",

            // What the window calls each option.
            [TextKey.LabelOptCsvOnly] = "Parts list only",
            [TextKey.LabelOptNoPlates] = "No print plates",
            [TextKey.LabelOptAscii] = "Readable STL",
            [TextKey.LabelOptKeepOrigin] = "Keep each part's own origin",
            [TextKey.LabelOptNoRepair] = "Leave gaps in the surface",
            [TextKey.LabelOptNoSeamRepair] = "Leave seams open",
            [TextKey.LabelOptPrintEverything] = "Print everything",
            [TextKey.LabelOptOffline] = "Work without the network",
            [TextKey.LabelOptNoUnofficial] = "Official library only",
            [TextKey.LabelOptScale] = "Scale",
            [TextKey.LabelOptClearance] = "Clearance",
            [TextKey.LabelOptWeldTolerance] = "Corner merging",
            [TextKey.LabelOptPlateSpacing] = "Gap between parts",
            [TextKey.LabelOptOutputDir] = "Run folder",
            [TextKey.LabelOptElementMap] = "Element table",
            [TextKey.LabelOptLDrawDir] = "Shape library",
            [TextKey.LabelOptLDrawCache] = "Downloaded shapes",
            [TextKey.LabelOptPlateSize] = "Bed size",
            [TextKey.LabelOptDelimiter] = "Parts list separator",
            [TextKey.LabelOptPrinter] = "Printer",
            [TextKey.LabelOptLang] = "Language",
            [TextKey.LabelOptApiKey] = "Rebrickable key",
            [TextKey.LabelOptLog] = "Log file",
            [TextKey.LabelOptQuiet] = "Say only what matters",
            [TextKey.LabelOptIncludeSpares] = "Include spare pieces",

            // Remarks a run makes about what it found along the way.
            [TextKey.NoteEntriesFound] = "Page {0}: {1} entries found.",
            [TextKey.NoteElementTable] = "Element table: {0} numbers, from {1}.",
            [TextKey.NoteNoElementTable] =
                "No element table found. Point --element-map at a Rebrickable elements.csv, or "
                + "give an API key, to have element numbers looked up rather than inferred.",
            [TextKey.NoteElementsResolved] = "Element numbers resolved: {0}.",
            [TextKey.NoteElementsFromTable] = "{0} from the table",
            [TextKey.NoteElementsFromRebrickable] = "{0} from Rebrickable",
            [TextKey.NoteElementsFromNumber] = "{0} inferred from the number itself",
            [TextKey.NoteLearnedLettering] =
                "Learned this document's lettering from {0} part line(s){1}. Shapes known: {2}.",
            [TextKey.NoteLearnedSkipped] =
                "; {0} skipped where the character count did not line up",
            [TextKey.NoteRecoveredQuantities] =
                "Recovered {0} quantity line(s) the text recogniser did not return, using the learned lettering.",
            [TextKey.NoteDuplicateEntry] =
                "{0} in {1} appears more than once; quantities added to give {2}.",
            [TextKey.NoteNoHeadingRow] = "The file has no heading row; reading it as data.",
            [TextKey.NoteRead] = "Read {0} entries, separated by '{1}'.",
            [TextKey.NoteSetColourUnknown] =
                "{0} is listed in colour {1} ({2}), which is not in the colour cross-reference; the "
                + "piece was left out. Regenerate it with 'lego2stl refresh-colors'.",
            [TextKey.NoteSetColourHasNoBrickLinkCode] =
                "{0} is '{1}', which has no BrickLink colour number, so it has no value for that "
                + "column; the piece was left out.",
            [TextKey.NoteSetSparesLeftOut] =
                "{0} spare piece(s) left out. Ask for --include-spares to keep them.",
            [TextKey.NoteSetLinesWithoutColour] =
                "{0} line(s) could not be given a BrickLink colour and were left out.",
            [TextKey.ReportPrinter] = "Printer",
        };
    }
}
