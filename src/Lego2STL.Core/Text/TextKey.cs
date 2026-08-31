namespace Lego2STL.Core.Text;

/// <summary>
/// Every piece of text the tool shows.
/// </summary>
/// <remarks>
/// An enum rather than loose strings so that a missing translation is a test failure instead
/// of a phrase that silently stays English. <see cref="Strings"/> holds one table per
/// language, and the suite checks that every value here appears in all of them with matching
/// placeholders.
/// </remarks>
public enum TextKey
{
    // The parts list's column headings. These follow the display language, and every
    // wording the tool has ever written is still accepted on the way back in.
    CsvId,
    CsvLegoCode,
    CsvBrickLinkCode,
    CsvColourName,
    CsvRgb,
    CsvQuantity,

    // Units and small shared words.
    Millimetres,
    On,
    Off,
    Yes,
    No,
    None,

    // Reports.
    ReportRunTitle,
    ReportShapeTitle,
    ReportPlateTitle,
    ReportInput,
    ReportPages,
    ReportColourNumbering,
    ReportReading,
    ReportEntriesRead,
    ReportPartsList,
    ReportTotals,
    ReportTotalEntries,
    ReportTotalPieces,
    ReportTotalDistinctParts,
    ReportCouldNotBeRead,
    ReportRecogniserReturned,
    ReportGeometryFrom,
    ReportUnits,
    ReportStandingOnZero,
    ReportOriginalOrigin,
    ReportScale,
    ReportSeamRepair,
    ReportClearance,
    ReportColumnPart,
    ReportColumnTriangles,
    ReportColumnOpen,
    ReportColumnSeams,
    ReportColumnGaps,
    ReportColumnSize,
    ReportShapesClosed,
    ReportSeamsClosedSummary,
    ReportGapsFilledSummary,
    ReportRetiredNumbers,
    ReportBuiltWithSomethingMissing,
    ReportProducedNothing,
    ReportPrintingNoteTitle,
    ReportPrintingNoteBody,
    ReportPrintingNoteWithClearance,
    ReportPlateSummary,
    ReportPlateLine,
    ReportPlateColumnPlate,
    ReportPlateColumnColour,
    ReportPlateColumnPieces,
    ReportPlateColumnFootprint,
    ReportPlateDidNotFit,
    ReportPlateMissingParts,

    // The shape library: where a run's geometry came from, and how it was got.
    LibraryFolder,
    LibraryArchive,
    LibraryWebsite,
    LibraryNone,
    MsgLDrawUsingFolder,
    MsgLDrawNotALibrary,
    MsgLDrawUsingDownloaded,
    MsgLDrawFetchingPerFile,
    MsgLDrawRefused,
    MsgLDrawDownloading,
    MsgLDrawReady,

    // Running.
    MsgPagesInDocument,
    MsgReadingPages,
    MsgReadingPrintedPages,
    MsgLookingUpElements,
    MsgNoPageRange,
    MsgNoSuchFile,
    MsgNoSuchPartsList,
    MsgPageIsCatalogueOne,
    MsgPageIsCatalogueMany,
    MsgNoCataloguePages,
    MsgNoCatalogueInThisBook,
    MsgCataloguePagesFound,
    MsgShapeClosed,
    MsgShapeOpenEdges,
    MsgLookingUpSet,
    MsgSetSummary,
    MsgSuggestedRange,
    MsgEntriesSummary,
    MsgPartsListWritten,
    MsgReportWritten,
    MsgCouldNotReadEntriesOne,
    MsgCouldNotReadEntriesMany,
    MsgUnreadEntryAt,
    ReasonCouldNotReadQuantity,
    ReasonCouldNotReadPartAndColour,
    ReasonCouldNotReadEither,
    ReasonUnknownElement,
    ReasonShapeHasNoSurfaces,
    ReasonNoShapeFile,
    MsgWrittenWithoutThem,
    MsgEntriesAndParts,
    MsgWroteShapes,

    /// <summary>Said of a part left unbuilt because of what it is made of.</summary>
    MsgNotPrintedMaterial,

    /// <summary>Said of a part left unbuilt because of the kind of thing it is.</summary>
    MsgNotPrintedKind,

    /// <summary>Heading of the report's list of parts that were not built.</summary>
    ReportNotPrintedTitle,

    MsgClosedAndOpen,
    MsgProducedNothing,
    MsgWrotePlates,
    MsgWrotePrintSettings,
    MsgNoPlatesRequested,
    MsgPlatesMissingParts,
    MsgCalibrationWritten,
    CalibrationTitle,
    CalibrationHow,
    CalibrationThen,
    MsgClearanceApplied,
    MsgClearanceRefusedOpen,
    MsgClearanceRefusedStillOpen,
    MsgClearanceRefusedThin,
    MsgCancelled,
    MsgError,

    // Things that go wrong, said in a way that says what to do next.
    ErrChooseDocument,
    ErrChoosePartsList,
    ErrTypeSetNumber,
    ErrNoFileAt,
    ErrScaleNotPositive,
    ErrSpacingNegative,
    ErrNotABedSize,
    ErrClearanceNeedsClosedShape,
    ErrClearanceNegative,
    ErrPartTooSmallForClearance,
    ErrPlateTooSmall,
    ErrPartTooTallForBed,
    ErrOcrUnavailable,
    ErrOcrWrongBuild,
    ErrOpenScadNotFound,
    ErrUnknownColourCode,
    ErrColourSchemeHintBrickLink,
    ErrColourSchemeHintOther,
    ErrColourHasNoBrickLinkCode,
    ErrSetCameBackEmpty,

    // The interface.
    UiTitle,
    UiTabRun,
    UiRailRuns,
    UiRailSetup,
    UiNewRun,
    UiNext,
    UiBack,
    UiStart,
    UiCancel,
    UiBrowse,
    UiInputKind,
    UiInputDocument,
    UiInputPartsList,
    UiInputSetNumber,
    UiChooseDocument,
    UiChoosePartsList,
    UiSetNumber,
    UiPageRange,
    UiPageRangeHint,
    UiScanPages,
    UiScanning,
    UiColourScheme,
    UiLanguage,
    UiSettings,

    /// <summary>The settings card holding the list of shops.</summary>
    UiShops,

    /// <summary>The button that adds a row to it.</summary>
    UiAddShop,

    /// <summary>The button that takes one away.</summary>
    UiRemoveShop,

    /// <summary>Marks the shop whose button the catalogue shows.</summary>
    UiPreferredShop,

    /// <summary>Explains what a shop's address may contain.</summary>
    UiShopHelp,
    UiEquivalentCommand,
    UiCopyCommand,
    UiCopied,
    UiGroupStages,
    UiGroupOutput,
    UiGroupGeometry,
    UiGroupLibrary,
    UiGroupPlates,
    UiGroupBehaviour,
    UiProgress,
    UiLog,
    UiShowLog,
    UiHideLog,
    UiCatalogueEmpty,
    UiFilterByColour,
    UiAllColours,
    UiSortBy,
    UiSortByPart,
    UiSortByQuantity,
    UiSortByColour,
    UiOpenShapeFile,
    UiOpenPlate,
    UiOpenFolder,
    UiWarningNotClosed,

    /// <summary>Said of a shape whose surfaces pass through each other rather than gape.</summary>
    UiWarningSelfIntersects,
    UiWarningThinFeature,

    /// <summary>Said of a part no plate could take.</summary>
    UiDoesNotFitThePlate,

    /// <summary>Said of a part left unbuilt because of what it is made of.</summary>
    UiNotPrintedMaterial,

    /// <summary>Said of a part left unbuilt because of the kind of thing it is.</summary>
    UiNotPrintedKind,

    /// <summary>Said of a part whose shape the run could not build.</summary>
    UiNoShapeWasBuilt,

    /// <summary>Said of a code neither the parts database nor the shape library knows.</summary>
    UiPartNotRecognised,

    /// <summary>The button that opens a shop at this part.</summary>
    UiBuy,

    /// <summary>The same button when all it can do is search.</summary>
    UiSearchForIt,

    /// <summary>The band offering a scale at which everything would fit.</summary>
    UiSomePartsDoNotFit,

    /// <summary>The button on that band.</summary>
    UiTryASmallerScale,

    /// <summary>The panel that asks about an entry the reader could not make out.</summary>
    UiCouldNotRead,

    /// <summary>Asks for the part number of the piece in the picture.</summary>
    UiWhichPart,

    /// <summary>Asks for its colour code.</summary>
    UiWhichColour,

    /// <summary>Asks how many.</summary>
    UiHowMany,

    /// <summary>Accepts the answer.</summary>
    UiAnswerOk,

    /// <summary>Leaves the question for later.</summary>
    UiAnswerSkip,

    /// <summary>Says the region was never an entry at all.</summary>
    UiAnswerNotALegoCode,
    /// <summary>The catalogue's choice of which numbering to show.</summary>
    UiNumbering,

    /// <summary>Shown in place of an element number for a list that has none.</summary>
    UiNoElementNumber,

    /// <summary>The numbering menu's first choice.</summary>
    UiNumberingBrickLink,

    /// <summary>The numbering menu's second choice.</summary>
    UiNumberingLegoElement,

    UiQuantity,
    UiDone,
    UiFailed,
    UiIdle,
    UiEveryOptionHere,

    // How a run stands, and where it got to if it did not finish.
    UiStatusRunning,
    UiStatusComplete,
    UiStatusNeedsDecision,
    UiStatusFailed,
    UiStatusStopped,
    UiStoppedAt,

    // The list of runs that have happened.
    UiRunsEmpty,
    UiForget,
    UiForgetEverything,
    UiFolderMissing,

    // What a run's page says when its record is missing or from a newer build.
    UiNoManifest,
    UiNewerManifest,
    UiRunItAgain,
    UiOpenPartsList,
    UiContinueFromPartsList,

    // Finding a way through the options.
    UiSearchOptions,
    UiChangedOnly,
    UiHiddenCount,
    UiReset,

    // The open run.
    UiQuietNote,
    UiWhen,
    UiRunFolder,

    // The steps of a run, named for a screen rather than for a log.
    UiStageReadingDocument,
    UiStageLookingUpSet,
    UiStageReadingPartsList,
    UiStageWritingPartsList,
    UiStageGatheringShapes,
    UiStageBuildingShapes,
    UiStageArrangingPlates,
    UiStageWritingReport,

    // Remarks a run makes about what it found along the way.
    NoteEntriesFound,
    NoteElementTable,
    NoteNoElementTable,
    NoteElementsResolved,
    NoteElementsFromTable,
    NoteElementsFromRebrickable,
    NoteElementsFromNumber,
    NoteLearnedLettering,
    NoteLearnedSkipped,
    NoteRecoveredQuantities,
    NoteDuplicateEntry,
    NoteNoHeadingRow,
    NoteRead,
    NoteSetColourUnknown,
    NoteSetColourHasNoBrickLinkCode,
    NoteSetSparesLeftOut,
    NoteSetLinesWithoutColour,
    ReportPrinter,

    // What --help says. Kept here with everything else so that a language cannot be
    // half-applied: messages in one language and the help in another is the state these
    // tables exist to prevent.
    HelpRoot,
    HelpExtract,
    HelpBuild,
    HelpCalibration,
    HelpBricks,
    HelpRefreshColors,
    HelpArgDocument,
    HelpArgPages,
    HelpArgPartsList,
    HelpArgSizes,
    HelpArgSteps,
    HelpOptLang,
    HelpOptOutputDir,
    HelpOptAscii,
    HelpOptOffline,
    HelpOptLDrawDir,
    HelpOptApiKey,
    HelpOptListPages,
    HelpOptColorScheme,
    HelpOptElementMap,
    HelpOptSet,
    HelpOptIncludeSpares,
    HelpOptCsvOnly,
    HelpOptNoPlates,
    HelpOptDelimiter,
    HelpOptKeepOrigin,
    HelpOptScale,
    HelpOptClearance,
    HelpOptNoRepair,
    HelpOptNoSeamRepair,
    HelpOptPrintEverything,
    HelpOptWeldTolerance,
    HelpOptLDrawCache,
    HelpOptNoUnofficial,
    HelpOptPrinter,
    HelpOptPlateSize,
    HelpOptPlateSpacing,
    HelpOptQuiet,
    HelpOptLog,
    HelpOptPart,
    HelpOptKind,
    HelpOptNoKnobs,
    HelpOptNoStudHoles,
    HelpOptOpenScad,
    HelpOptMachineBlocks,
    HelpOptScadOnly,
    HelpOptBricksOutputDir,
    HelpOptOut,
    HelpOptRebrickableDump,

    // What the window calls each option. The flag names the option for a terminal; this names
    // it for a reader, so the window is not a list of switches only its author can use. Both
    // are shown, because a label nobody can turn into a command is half an answer.
    LabelOptCsvOnly,
    LabelOptNoPlates,
    LabelOptAscii,
    LabelOptKeepOrigin,
    LabelOptNoRepair,
    LabelOptNoSeamRepair,
    LabelOptPrintEverything,
    LabelOptOffline,
    LabelOptNoUnofficial,
    LabelOptScale,
    LabelOptClearance,
    LabelOptWeldTolerance,
    LabelOptPlateSpacing,
    LabelOptOutputDir,
    LabelOptElementMap,
    LabelOptLDrawDir,
    LabelOptLDrawCache,
    LabelOptPlateSize,
    LabelOptDelimiter,
    LabelOptPrinter,
    LabelOptLang,
    LabelOptApiKey,
    LabelOptLog,
    LabelOptQuiet,
    LabelOptIncludeSpares,

    /// <summary>The heading of the sheet that goes beside the plates.</summary>
    PrintNotesTitle,

    /// <summary>Says the preset is a starting point and calibration beats it.</summary>
    PrintNotesStartingPoint,

    /// <summary>Tells the reader to import the preset file beside the plates.</summary>
    PrintNotesImport,

    /// <summary>Says this printer has no profiles of its own and whose it borrows.</summary>
    PrintNotesBorrowedProfile,

    /// <summary>Says no preset could be written for this printer.</summary>
    PrintNotesNoPreset,

    /// <summary>Says which nozzle the preset is for.</summary>
    PrintNotesNozzle,

    /// <summary>The heading over the table of settings.</summary>
    PrintNotesSettings,

    /// <summary>The heading over the calibration sequence.</summary>
    PrintNotesCalibration,

    /// <summary>The calibration sequence itself.</summary>
    PrintNotesCalibrationSteps,
}
