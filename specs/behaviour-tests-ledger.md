# Behaviour tests: migration ledger

Condensed record of the test-suite migration decided in ADR-0080 (design: `specs/behaviour-tests-design.md`). The per-test ledgers lived in uncommitted `.superpowers/sdd/behaviour-tests/`; this keeps the answer to "why was this test dropped, and what covers it now". Rounds 1 (slices 01-11) and 2 (slices 21-23) are included. Test names are `Class.Method` of the removed unit tests; "e2e NN" is `test/e2e/scenarios/NN-*.spec.ts`. Counts are rows of the ledgers.

## 1. Counts per slice

| Slice | Kept | Moved | Dropped | Note |
|---|---|---|---|---|
| 01 Money (Api.Tests) | 0 | 21 | 7 |  |
| 02 Tasks module | 31 | 34 | 21 |  |
| 03 Statistics and Sharing modules | 2 | 46 | 33 | summary said 32/46/33; row count used |
| 04 Api | 46 | 24 | 17 |  |
| 05 Client (non-UI) | 715 | 3 | 3 |  |
| 06 Components (round 1) | 201 | 134 | 105 |  |
| 07 My Day and Inbox | 3 | 178 | 37 |  |
| 08 Areas, lists, goals | 0 | 216 | 74 |  |
| 09 Money pages | 8 | 146 | 49 |  |
| 10 Sharing pages | 2 | 85 | 10 |  |
| 11 Shell | 12 | 182 | 79 |  |
| 21 Theme and main layout (round 2) | 23 | 19 | 74 |  |
| 22 Components (round 2) | 36 | 114 | 56 |  |
| 23 State (round 2) | 212 | 0 | 72 |  |
| Total | 1291 | 1202 | 637 | |

Kept = still a unit/component test (some moved into `State/` as pure tests). Moved = replaced by an integration test or e2e step (including `BUG:` steps that are skipped, see the last section). Dropped = removed, see the dropped sections.

## 2. Moved: where coverage went

### 01 Money (Api.Tests)

- `BalanceHandlerTests` → BalanceCommandTests
- `BudgetHandlerTests` → BudgetCommandTests
- `CategoryHandlerTests` → CategoryCommandTests
- `MoneyEntryHandlerTests` → MoneyEntryCommandTests
- `MoneyImportRoundTripTests` → MoneyImportTests

### 02 Tasks module

- `ReferenceHandlerTests` → Api.Tests ReferenceCommandEndpointTests
- `DeleteCascadeTests` → Api.Tests DeleteCascadeEndpointTests
- `DeadContainerTests` → Api.Tests DeleteCascadeEndpointTests; Api.Tests InboxCommandTests; AreaTests
- `GoalTaskLinkTests` → Api.Tests GoalLinkCommandTests; GoalTests
- `SharedTaskTests` → Api.Tests MemberCommandTests
- `SharedReferenceTests` → Api.Tests MemberCommandTests
- `InboxTests` → Api.Tests InboxCommandTests
- `ReorderListsHandlerTests` → Api.Tests AreaViewSyncTests
- `PlaceListHandlerTests` → Api.Tests ListViewSyncTests

### 03 Statistics and Sharing modules

- `InboxRecordProjectionTests` → Api.Tests StatisticsOverviewTests; Api.Tests StatisticsReplayTests
- `StatisticsRecordProjectionTests` → Api.Tests StatisticsFeedTests; Api.Tests StatisticsReplayTests
- `StatisticsLabelProjectionTests` → Api.Tests StatisticsFeedTests; Api.Tests StatisticsOverviewTests; StatisticsFeedTests
- `StatisticsReplayTests` → Api.Tests StatisticsReplayTests; StatisticsRebuildTests
- `StatisticsReaderTests` → Api.Tests StatisticsFeedTests
- `StatisticsOverviewReaderTests` → Api.Tests StatisticsOverviewTests
- `SnapshotMarkingTests` → Api.Tests SnapshotMarkTests
- `SnapshotPublishingTests` → Api.Tests SnapshotEndpointTests

### 04 Api

- `CommandDispatcherTests` → CommandRefusalTests
- `CommandDispatcherLoggingTests` → CommandRefusalLoggingTests
- `ConflictRetryTests` → ConflictRetryTests
- `LiveAudienceTests` → LiveAudienceTests
- `LiveConnectionsTests` → LiveEndpointTests
- `ChannelDomainEventDispatcherTests` → DomainEventDeliveryTests
- `DomainEventPumpTests` → DomainEventDeliveryTests

### 05 Client (non-UI)

- `RedirectToLoginTests` → routed to a later page slice (07-11); coverage listed there
- `SharingPolishTests` → routed to a later page slice (07-11); coverage listed there

### 06 Components (round 1)

- `StepListTests` → routed to a later page slice (07-11); coverage listed there
- `SnapshotPublisherTests` → routed to a later page slice (07-11); coverage listed there
- `BootSplashReleaseTests` → routed to a later page slice (07-11); coverage listed there
- `EmptyStateTests` → routed to a later page slice (07-11); coverage listed there
- `SectionHeadingTests` → routed to a later page slice (07-11); coverage listed there
- `PageHeadingTests` → routed to a later page slice (07-11); coverage listed there
- `ThingCardTests` → routed to a later page slice (07-11); coverage listed there
- `QrCodeTests` → routed to a later page slice (07-11); coverage listed there
- `BudgetStatsChartsTests` → routed to a later page slice (07-11); coverage listed there
- `NetWorthByTypeChartTests` → routed to a later page slice (07-11); coverage listed there
- `ConsistencyHeatmapTests` → routed to a later page slice (07-11); coverage listed there
- `ListCardTests` → routed to a later page slice (07-11); coverage listed there
- `AreaChipsTests` → routed to a later page slice (07-11); coverage listed there
- `SnapshotMarkChipTests` → routed to a later page slice (07-11); coverage listed there
- `SidebarFooterTests` → routed to a later page slice (07-11); coverage listed there
- `InviteLinkCardTests` → routed to a later page slice (07-11); coverage listed there
- `InboxItemRowTests` → routed to a later page slice (07-11); coverage listed there
- `ReferenceRowTests` → routed to a later page slice (07-11); coverage listed there
- `ThingMenuTests` → routed to a later page slice (07-11); coverage listed there
- `ListToolbarTests` → routed to a later page slice (07-11); coverage listed there
- `DetailPanelTests` → routed to a later page slice (07-11); coverage listed there
- `TaskRowTests` → routed to a later page slice (07-11); coverage listed there

### 07 My Day and Inbox

- `WelcomeTests` → e2e 01
- `InboxPageTests` → e2e 02
- `InboxLocalizationTests` → e2e 18
- `InboxItemPanelTests` → e2e 02, 07
- `Routed: EmptyStateTests` → e2e 02
- `Routed: InboxItemRowTests` → e2e 02
- `Routed: DetailPanelTests` → e2e 02, 23, 24
- `TodayTests` → e2e 01, 04, 05, 06, 07, 17, 20, 21; DayQueryTests; DayViewPlanTests; WeekStripTests
- `TodayLocalizationTests` → e2e 18
- `Routed: SectionHeadingTests` → e2e 17
- `Routed: TaskRowTests` → e2e 02, 04, 05, 06, 07, 20, 21, 23, 24
- `SyncRefreshTests` → e2e 02, 20
- `TaskDetailPanelTests` → e2e 02, 04, 05, 06, 07, 11, 17, 19, 20, 23, 24
- `Routed: StepListTests` → e2e 19, 20

### 08 Areas, lists, goals

- `AreaBoardTests` → e2e 03, 25, 26, 29, 30, 31; AreaSummaryTests
- `Routed: ThingCardTests` → e2e 25
- `Routed: ListCardTests` → e2e 25, 30
- `Routed: ThingMenuTests` → e2e 25
- `Routed: EmptyStateTests` → e2e 03, 28
- `AreasIndexTests` → e2e 29
- `Routed: AreaChipsTests` → e2e 29
- `AreaDetailPanelTests` → e2e 03, 25, 29, 30; DeleteWarningTests
- `ListPageTests` → e2e 03, 22, 26, 27, 30, 31
- `Routed: ListToolbarTests` → e2e 22
- `ListDetailPanelTests` → e2e 03, 31
- `ListDetailPanelNavigationTests` → e2e 31
- `Routed (ledger-07): DetailPanelTests` → e2e 31
- `ReferenceItemPanelTests` → e2e 27
- `Routed: ReferenceRowTests` → e2e 30
- `GoalsPageTests` → e2e 28
- `GoalDetailPanelTests` → e2e 28
- `GoalPageTests` → e2e 07, 28; GoalPaceTests; GoalProgressTests

### 09 Money pages

- `BudgetsPageTests` → e2e 12, 32
- `BudgetsLocalizationTests` → e2e 18
- `BudgetPanelTests` → e2e 12, 32
- `BudgetPageTests` → e2e 04, 12, 13, 32, 33; BudgetTabsTests
- `BudgetOverviewTests` → e2e 12, 13, 32; MonthPaceTests
- `BudgetEntriesTests` → e2e 12; EntryFiltersTests; MonthTotalsTests
- `EntryPanelTests` → e2e 12, 13, 32; RateSuggestionTests
- `BudgetBalanceTests` → e2e 13, 32, 33; BalanceTotalsTests
- `HoldingPanelTests` → e2e 12, 13, 32; HoldingRateDayTests
- `BudgetImportPageTests` → e2e 33
- `BudgetStatsTests` → e2e 12, 32, 33; SummaryRangeTests; UsualMonthTests
- `Routed: BudgetStatsChartsTests` → e2e 33
- `Routed: NetWorthByTypeChartTests` → e2e 13, 33

### 10 Sharing pages

- `JoinPageTests` → e2e 10, 34
- `ListMembersViewTests` → e2e 10
- `Routed (06): InviteLinkCardTests` → e2e 10
- `Routed (06): QrCodeTests` → e2e 10, 11
- `Routed (06): ListCardTests` → e2e 10
- `Routed (06): AreaChipsTests` → e2e 10
- `Routed (08): AreaBoardTests` → e2e 10
- `Routed (08): AreasIndexTests` → e2e 10
- `Routed (08): ListPageTests` → e2e 10
- `Routed (08): ListDetailPanelNavigationTests` → e2e 10
- `Routed (08): GoalPageTests` → e2e 10
- `Routed (07): TodayTests` → e2e 10
- `Routed (07): TaskDetailPanelTests` → e2e 10
- `BudgetSharingPageTests` → e2e 34
- `BudgetMembersPanelTests` → e2e 34
- `SnapshotPageTests` → e2e 11
- `SnapshotsPageTests` → e2e 11; SnapshotVisitRowsTests
- `Routed (06): SnapshotPublisherTests` → e2e 11
- `Routed (06): SnapshotMarkChipTests` → e2e 11
- `Routed (06): ReferenceRowTests` → e2e 11
- `Routed (08): ReferenceItemPanelTests` → e2e 11
- `Routed (06): TaskRowTests` → e2e 11

### 11 Shell

- `AppInfoPageTests` → e2e 01
- `AuthenticationTests` → e2e 01, 15; KeycloakEndSessionTests; TokenRefresherTests
- `PageHeadingAdoptionTests` → e2e 01
- `SettingsPageTests` → e2e 01, 14, 15, 18; KeycloakEndSessionTests; LocalSignOutTests
- `StatisticsPageTests` → e2e 01, 15; ChartAxisTests
- `AccountBadgeTests` → e2e 01
- `AccountDrawerTests` → e2e 01
- `AppShellTests` → e2e 01, 14, 15, 18; LanguagePreferenceTests
- `BottomNavTests` → e2e 01; ActiveTabTests
- `ConnectionStatusTests` → e2e 01, 14
- `MobileTopBarTests` → e2e 01
- `NavSidebarTests` → e2e 01, 09, 14
- `PullToRefreshTests` → e2e 01
- `ShellLocalizationTests` → e2e 18
- `StatusBeltStackTests` → e2e 14
- `SyncButtonTests` → e2e 14
- `SyncStampTests` → routed to a later page slice (07-11); coverage listed there
- `Routed (05): RedirectToLoginTests` → e2e 01
- `Routed (05): SharingPolishTests` → e2e 18
- `Routed (06): BootSplashReleaseTests` → e2e 01
- `Routed (06): PageHeadingTests` → e2e 01, 14
- `Routed (06): ConsistencyHeatmapTests` → e2e 15
- `Routed (06): SidebarFooterTests` → e2e 01, 14
- `Routed (08): ListDetailPanelNavigationTests` → e2e 14

### 21 Theme and main layout (round 2)

- `TaskDetailPanelTests` → e2e 23; SyncServiceTests
- `TimeRowTypingTests` → routed to a later page slice (07-11); coverage listed there
- `TimeRowTests` → routed to a later page slice (07-11); coverage listed there

### 22 Components (round 2)

- `AddRowTests` → e2e 03
- `DayTimelineTests` → e2e 17; TimelineLayoutTests
- `DueDateMenuTests` → e2e 21
- `ImportEntriesTableToggleTests` → e2e 33
- `ListMonthViewTests` → e2e 22; WeekDaysTests
- `MarkdownFieldTests` → e2e 11, 23
- `MonthStripTests` → e2e 13
- `RecurrenceEditorTests` → e2e 06
- `ReferenceFieldListTests` → e2e 08, 27; DroppedTextTests
- `ReferenceFieldValueTests` → e2e 08; MarkdownRendererTests
- `SnapshotPublisherTests` → e2e 11, 31
- `StepListTests` → e2e 11, 19, 21, 24
- `TimeRowTests` → e2e 17, 23; TaskTimeTextTests
- `TimeRowTypingTests` → e2e 23; TimeTextTests
- `WeekStripTests` → e2e 05

## 3. Dropped without replacement

Rows whose reason is not "covered by": CSS or size only, test double, wiring, unobservable, no user path, domain-enforced. Where a reason names a rule or step that covers the visible result, it is quoted.

### 02 Tasks module (5)

- `ListAccessTests.LoadingWithoutAListFallsBackToOwnerAccess` — wiring of an extension over a fake store; covered by every owner command that loads no list (e.g. Api.Tests DeleteCascadeEndpointTests.DeletingAnAreaDeletesItsListsAndTas…
- `AreaTests.ARejectedCommandCommitsNothing` — rule covered by AreaTests.CreatingAnAreaWithABlankNameIsRejected
- `ReorderListsHandlerTests.ARejectionCommitsNothing` — rule covered by AreaViewTests.AListMissingFromTheOrderIsRejected
- `CommandDispatcherTests.AnEnvelopeReachesTheRightHandler` — used FakeUnitOfWork; covered by Api.Tests CommandEndpointTests.AnAcceptedCommandComesBackAcceptedAndIsStored
- `CommandDispatcherTests.AnAcceptedCommandIsNotMarkedUnrecoverable` — used FakeUnitOfWork; covered by CommandEndpointTests.AnAcceptedCommandComesBackAcceptedAndIsStored (now also asserts Unrecoverable is false)

### 03 Statistics and Sharing modules (9)

- `StatisticsRecordProjectionTests.ARecordIsKeyedBySeqAndUser` — key composition is storage shape; seq ordering and per-user isolation covered by StatisticsEndpointTests.PagingWithBeforeWalksBackwards and AnotherUsersWorkIsInvisibleInT…
- `StatisticsLabelProjectionTests.AnAreaIsRememberedRenamedAndDeleted` — no reader or endpoint consumes area labels; unobservable
- `StatisticsLabelProjectionTests.ARenameForALabelThatWasNeverCreatedIsIgnored` — unobservable guard: no view lists a label that was never created, and commands cannot produce the case
- `StatisticsLabelProjectionTests.ADeleteForALabelThatWasNeverCreatedIsIgnored` — unobservable guard, as above
- `StatisticsLabelProjectionTests.AnUnrelatedEventIsIgnored` — unobservable: an ignored event changes no view; every other feed test sends unrelated events
- `StatisticsReaderTests.OneLookupCoversEveryDistinctTaskOnThePage` — counts calls on a fake of our own port; not a result
- `StatisticsReaderTests.AnEmptyPageNeverReachesForSnapshots` — counts calls on a fake of our own port; an empty feed is covered by AnotherUsersWorkIsInvisibleInTheFeedAndTheOverview
- `StatisticsOverviewReaderTests.BothHalvesOfTheWindowAreReadFromTheSameInstant` — asserts arguments passed to a fake store; the split instant is unobservable
- `StatisticsOverviewReaderTests.TheInboxCapturesSplitOnTheSameInstantAsTheTaskCharts` — asserts arguments passed to a fake store

### 04 Api (11)

- `CommandDispatcherTests.ACommandWithNoRegisteredHandlerIsRejectedAsUnrecoverable` — wiring only: an empty service provider; every command in every integration test proves its handler is registered
- `AuthenticationLoggingTests.AForbiddenRequestIsLoggedWithTheSubjectAndPath` — no endpoint authorizes beyond "signed in", so no request can reach a forbid; the callback was only exercised with hand-built contexts
- `LiveAudienceTests.AnEmptyIdIsNeverInTheAudience` — guard on an id no request can carry; not observable
- `LiveConnectionsTests.NudgesBetweenTwoReadsCollapseIntoOne` — channel coalescing detail; clients refetch on any nudge, so the count is not observable behaviour
- `LiveConnectionsTests.NudgingAUserWithNoStreamIsANoOp` — every command in every integration test commits with no open stream
- `SseFramesTests.TheFramesAreExactlyTheWireFormat` — byte-level framing; LiveEndpointTests read ready, sync and heartbeat frames through the real client reader
- `ChannelDomainEventDispatcherTests.PublishedEventsQueueUpInOrder` — queue internals; ordering shows in StatisticsFeedTests completion numbers
- `ChannelDomainEventDispatcherTests.TheCallersListMayBeClearedAsSoonAsPublishReturns` — internal copy semantics, not observable through the API
- `ChannelDomainEventDispatcherTests.DrainingWaitsUntilEverythingPublishedBeforeItIsHandled` — race not deterministically observable
- `ChannelDomainEventDispatcherTests.EventsPublishedAfterTheDrainStartedDoNotHoldItUp` — race not deterministically observable
- `DomainEventPumpTests.NothingIsDeliveredUntilReplayHasCaughtUp` — end state covered by StatisticsReplayTests.AStartCatchesUpEverythingPastTheMarker; the in-between ordering is not observable

### 05 Client (non-UI) (1)

- `BudgetSharingApiTests.EachChangeHitsItsEndpoint` — request shape only (method and path); covered by Api.Tests BudgetSharingEndpointTests.LeavingAndRemovalEndAccess and OnlyTheOwnerShares

### 06 Components (round 1) (105)

- `TimeRowTests.TheClearButtonLinesUpWithTheOtherProperties` — styling class only
- `TimeRowTests.EachPickerEndsInAClockIcon` — icon glyph is shape; the clock button opening the picker is TheClockButtonOpensThePicker
- `TimeRowTests.FromAndToSitOnTwoRowsBesideTheTimeLabel` — layout only (row structure and icons)
- `TimeRowTests.TheClearButtonSitsOnTheFromRow` — layout only
- `ListMonthViewTests.AnOverdueEntryIsMarked` — colour class only; the overdue rule is ListCalendarTests
- `ListMonthViewTests.ACellCarriesBothTheNamesAndThePhoneDot` — responsive markup only
- `DayTimelineTests.TheTimelineAsksForOnlyTheLengthItsDayNeeds` — inline CSS length; axis maths is State/TimelineAxisTests
- `StepListTests.ACheckedStepNameStaysStruckThrough` — strike-through class only
- `ReferenceFieldListTests.FieldRowsShareTheAddRowsLeadColumn` — layout only
- `AddRowTests.TheIconAndTheLabelSitOnOneLineUnderTheCheckboxColumn` — layout only (classes)
- `BrandLoaderTests.ItDrawsTheBrandCheck` — shape only (draws the brand check)
- `BootSplashReleaseTests.AFailedTeardownDoesNotBreakTheScreenBeneathIt` — failure path of a JS teardown; invisible to a user
- `EmptyStateTests.ItsIconSitsInATintedCircle` — shape only (tint, plain card without callback)
- `EmptyStateTests.WithoutOnCreateTheCardIsNotAButton` — shape only (tint, plain card without callback)
- `SectionHeadingTests.ItRendersTheTitleAsAnH2` — shape only (tag, muted hint, pill markup)
- `SectionHeadingTests.NoCountMeansNoPill` — shape only (tag, muted hint, pill markup)
- `SectionHeadingTests.AHintFollowsTheTitleMuted` — shape only (tag, muted hint, pill markup)
- `SectionHeadingTests.TheColourAddsTheMatchingTextClass` — shape only (tag, muted hint, pill markup)
- `PageHeadingTests.ItDrawsTheTitleOnDesktopOnly` — shape only (layout classes, element tags, colours)
- `PageHeadingTests.TheDesktopRowCentresItsItemsAndLeavesTwentyPixelsAboveThePage` — shape only (layout classes, element tags, colours)
- `PageHeadingTests.AnEmptyTitleDrawsNothingButStillClearsTheShell` — shape only (layout classes, element tags, colours)
- `PageHeadingTests.TheTitleIsThePagesOnlyTopLevelHeading` — shape only (layout classes, element tags, colours)
- `PageHeadingTests.TheTitleIsNotInThePrimaryColour` — shape only (layout classes, element tags, colours)
- `PageHeadingTests.TheSubtitleSitsUnderTheTitleInOneColumn` — shape only (layout classes, element tags, colours)
- `PageHeadingTests.TheRowCentresItsPartsVertically` — shape only (layout classes, element tags, colours)
- `PageHeadingTests.AnIconSitsInADecorativeTileBeforeTheTitle` — shape only (layout classes, element tags, colours)
- `PageHeadingTests.NoIconMeansNoTile` — shape only (layout classes, element tags, colours)
- `PageHeadingTests.NoSubtitleMeansNoSubtitleElement` — shape only (layout classes, element tags, colours)
- `SkeletonTests.ARowSkeletonRendersTheRequestedNumberOfRows` — shape only (counts and wrappers of loading placeholders); a loading region is exercised by every page load
- `SkeletonTests.ACardSkeletonRendersTheRequestedNumberOfCards` — shape only (counts and wrappers of loading placeholders); a loading region is exercised by every page load
- `SkeletonTests.ARowSkeletonSitsInsideOneRowGroupAndACardSkeletonInsideTheGrid` — shape only (counts and wrappers of loading placeholders); a loading region is exercised by every page load
- `SkeletonTests.ARowSkeletonMarksItselfAsALiveLoadingRegion` — shape only (counts and wrappers of loading placeholders); a loading region is exercised by every page load
- `SkeletonTests.ACardSkeletonMarksItselfAsALiveLoadingRegion` — shape only (counts and wrappers of loading placeholders); a loading region is exercised by every page load
- `MasonryGridTests.ItemsAreDealtAcrossColumnsInOrder` — layout only (column dealing is presentation of a card grid, seen on any screen with cards)
- `MasonryGridTests.FewerItemsThanColumnsLeaveTheRestEmpty` — layout only (column dealing is presentation of a card grid, seen on any screen with cards)
- `MasonryGridTests.ItRendersOneStackPerColumnForTheCurrentBreakpoint` — layout only (column dealing is presentation of a card grid, seen on any screen with cards)
- `MasonryGridTests.ABreakpointChangeReflowsTheColumns` — layout only (column dealing is presentation of a card grid, seen on any screen with cards)
- `ThingCardTests.FiveTasksNeedNoShowAll` — shape only (icon tile, dividers, slots, caller functions)
- `ThingCardTests.TheIconTileIsTheCollapseToggleAndThereIsNoChevron` — shape only (icon tile, dividers, slots, caller functions)
- `ThingCardTests.WithoutAnIconTheTileShowsTheDefaultListIcon` — shape only (icon tile, dividers, slots, caller functions)
- `ThingCardTests.TheCardHasNoDividers` — shape only (icon tile, dividers, slots, caller functions)
- `ThingCardTests.WithoutOnAddAnEmptyTaskCardOnlySaysSo` — shape only (icon tile, dividers, slots, caller functions)
- `ThingCardTests.ACardWithOpenTasksHasNoEmptyRow` — shape only (icon tile, dividers, slots, caller functions)
- `ThingCardTests.AnEmptyTextReplacesTheEmptyRow` — shape only (icon tile, dividers, slots, caller functions)
- `ThingCardTests.RowCaptionsComeFromTheCallersFunctions` — shape only (icon tile, dividers, slots, caller functions)
- `ThingCardTests.SubtitleAndActionsRenderInTheHeader` — shape only (icon tile, dividers, slots, caller functions)
- `ThingCardTests.RowsOverridesTaskRowsWhenSet` — shape only (icon tile, dividers, slots, caller functions)
- `ThingCardTests.EmptyTextShowsInsteadOfRowsWhenRowCountIsZero` — shape only (icon tile, dividers, slots, caller functions)
- `MoneyHeroTests.ItsTopRowIsLaidOutByItsOwnStylesNotByImportantUtilityClasses` — shape only (utility classes)
- `PanelSectionTests.ItShowsItsTitleAboveItsContentAfterADivider` — shape only (title, divider markup)
- `RowGroupTests.ItIsOneOutlinedPaperHoldingItsRows` — shape only (paper, column class)
- `RowGroupTests.ColumnsMarkTheGroupSoRowsFlowIntoColumnsOnWideScreens` — shape only (paper, column class)
- `RowGroupTests.ItTakesAnExtraClass` — shape only (paper, column class)
- `QrCodeTests.ADifferentValueGivesADifferentImage` — a different link giving a different image is the QR library
- `BudgetStatsChartsTests.TheMonthlyYAxisScalesToTheLargestMonth` — chart axis scaling is SVG geometry; shape only
- `NetWorthByTypeChartTests.TheYAxisScalesToTheLargestTotal` — axis scaling is chart geometry; shape only
- `ConsistencyHeatmapTests.AYearOfDaysStillGetsOneCellPerDay` — shape only (shade classes, grid CSS, swatch counting, hover titles, aria-live plumbing)
- `ConsistencyHeatmapTests.ADayWithNoCompletionsGetsTheEmptyShade` — shape only (shade classes, grid CSS, swatch counting, hover titles, aria-live plumbing)
- `ConsistencyHeatmapTests.TheBusiestDayGetsTheStrongestShade` — shape only (shade classes, grid CSS, swatch counting, hover titles, aria-live plumbing)
- `ConsistencyHeatmapTests.ACellStillCarriesItsFullAccessibleLabelAlongsideTheVisibleDayNumber` — shape only (shade classes, grid CSS, swatch counting, hover titles, aria-live plumbing)
- `ConsistencyHeatmapTests.ARangeWhereEveryDayIsZeroRendersWithoutDividingByZero` — shape only (shade classes, grid CSS, swatch counting, hover titles, aria-live plumbing)
- `ConsistencyHeatmapTests.EachCellShowsItsDayOfMonthNumber` — shape only (shade classes, grid CSS, swatch counting, hover titles, aria-live plumbing)
- `ConsistencyHeatmapTests.TheGridIsAFlowingAutoFitGridSoItFillsThePanelWidth` — shape only (shade classes, grid CSS, swatch counting, hover titles, aria-live plumbing)
- `ConsistencyHeatmapTests.SundaysAreMarkedAndOtherDaysAreNot` — shape only (shade classes, grid CSS, swatch counting, hover titles, aria-live plumbing)
- `ConsistencyHeatmapTests.TheKeysSwatchesAreNotCountedAsDays` — shape only (shade classes, grid CSS, swatch counting, hover titles, aria-live plumbing)
- `ConsistencyHeatmapTests.EachMonthSectionHoldsOnlyItsOwnDays` — shape only (shade classes, grid CSS, swatch counting, hover titles, aria-live plumbing)
- `ConsistencyHeatmapTests.AYearSplitsIntoAMonthSectionPerMonthItTouches` — shape only (shade classes, grid CSS, swatch counting, hover titles, aria-live plumbing)
- `ConsistencyHeatmapTests.EveryDayCellIsAButtonSoATapReachesItOnTouch` — shape only (shade classes, grid CSS, swatch counting, hover titles, aria-live plumbing)
- `ConsistencyHeatmapTests.TheTappedCellIsMarkedAsTheSelectedOne` — shape only (shade classes, grid CSS, swatch counting, hover titles, aria-live plumbing)
- `ConsistencyHeatmapTests.ATapLeavesEveryCellsHoverTitleAndAccessibleLabelInPlace` — shape only (shade classes, grid CSS, swatch counting, hover titles, aria-live plumbing)
- `ConsistencyHeatmapTests.TheReadoutIsAnnouncedToScreenReadersWhenItChanges` — shape only (shade classes, grid CSS, swatch counting, hover titles, aria-live plumbing)
- `ConsistencyHeatmapTests.OnlyAnAnsweredReadoutIsMarkedForSticking` — shape only (shade classes, grid CSS, swatch counting, hover titles, aria-live plumbing)
- `ListCardTests.TheCardIsOutlinedRatherThanElevated` — shape only (outlined vs elevated, missing inline field, aria-label of the add icon is covered by the role query)
- `ListCardTests.NoInlineAddTaskFieldRemainsInTheCard` — shape only (outlined vs elevated, missing inline field, aria-label of the add icon is covered by the role query)
- `ListCardTests.TheAddItemButtonHasAnAriaLabel` — shape only (outlined vs elevated, missing inline field, aria-label of the add icon is covered by the role query)
- `SnapshotMarkChipTests.TheCompactChipIsAnIconNamedForScreenReaders` — compact chip aria markup is shape only
- `InboxItemRowTests.TheFullTextRidesInTheTitleForALongCapture` — long capture title tooltip is hover markup only
- `ReferenceRowTests.AnItemWithoutFieldsHasNoChipLine` — shape only (row alignment, star colour, no-chip-line, no checkbox)
- `ReferenceRowTests.ALinkChipIsARealLinkThatSharesNoOpener` — shape only (row alignment, star colour, no-chip-line, no checkbox)
- `ReferenceRowTests.AnItemWithoutAMarkShowsNoChip` — shape only (row alignment, star colour, no-chip-line, no checkbox)
- `ReferenceRowTests.ItSharesTheRowShapeWithAOneLineName` — shape only (row alignment, star colour, no-chip-line, no checkbox)
- `ReferenceRowTests.ThereIsNoCheckbox` — shape only (row alignment, star colour, no-chip-line, no checkbox)
- `ReferenceRowTests.AnUnstarredStarIsQuiet` — shape only (row alignment, star colour, no-chip-line, no checkbox)
- `ReferenceRowTests.AStarredStarIsPrimary` — shape only (row alignment, star colour, no-chip-line, no checkbox)
- `ListToolbarTests.NoBadgeWithoutAFilter` — no badge without a filter is the empty case of the count row
- `ListToolbarTests.TheSortMenuNamesTheDirectionForItsOrder` — no badge without a filter is the empty case of the count row
- `DetailPanelTests.WithoutCallbacksThereIsNoActionBar` — shape only (column width, full-screen on phones, pinned footer, dividers, no action bar without callbacks)
- `DetailPanelTests.OnADesktopViewportItKeepsAFixedColumnWidth` — shape only (column width, full-screen on phones, pinned footer, dividers, no action bar without callbacks)
- `DetailPanelTests.OnASmallViewportItFillsTheScreen` — shape only (column width, full-screen on phones, pinned footer, dividers, no action bar without callbacks)
- `DetailPanelTests.TheFooterIsPinnedBelowTheScrollingContent` — shape only (column width, full-screen on phones, pinned footer, dividers, no action bar without callbacks)
- `DetailPanelTests.DividersDoNotStretchIntoGaps` — shape only (column width, full-screen on phones, pinned footer, dividers, no action bar without callbacks)
- `TaskRowTests.ATaskWithNoStepsShowsNoProgress` — shape only (one-line clamp, spacer, round checkbox, stripe, row height, colours, no-meta-line, no-dot)
- `TaskRowTests.ATaskWithNoDescriptionShowsNoNotesIcon` — shape only (one-line clamp, spacer, round checkbox, stripe, row height, colours, no-meta-line, no-dot)
- `TaskRowTests.TheNameIsOneLineWithTheFullNameOnHover` — shape only (one-line clamp, spacer, round checkbox, stripe, row height, colours, no-meta-line, no-dot)
- `TaskRowTests.ATaskWithNothingToShowHasNoMetaLine` — shape only (one-line clamp, spacer, round checkbox, stripe, row height, colours, no-meta-line, no-dot)
- `TaskRowTests.ThePriorityIsAStripeAtTheRowStartNotADotInTheMetaLine` — shape only (one-line clamp, spacer, round checkbox, stripe, row height, colours, no-meta-line, no-dot)
- `TaskRowTests.ATaskWithoutPriorityKeepsASpacerSoNamesStayAligned` — shape only (one-line clamp, spacer, round checkbox, stripe, row height, colours, no-meta-line, no-dot)
- `TaskRowTests.ARowWithAMetaLineIsTallerThanABareOne` — shape only (one-line clamp, spacer, round checkbox, stripe, row height, colours, no-meta-line, no-dot)
- `TaskRowTests.ATaskWithoutASnapshotMarkShowsNoMarkChip` — shape only (one-line clamp, spacer, round checkbox, stripe, row height, colours, no-meta-line, no-dot)
- `TaskRowTests.TheCheckboxIsRound` — shape only (one-line clamp, spacer, round checkbox, stripe, row height, colours, no-meta-line, no-dot)
- `TaskRowTests.AnUnstarredStarIsQuiet` — shape only (one-line clamp, spacer, round checkbox, stripe, row height, colours, no-meta-line, no-dot)
- `TaskRowTests.AStarredStarIsPrimary` — shape only (one-line clamp, spacer, round checkbox, stripe, row height, colours, no-meta-line, no-dot)
- `TaskRowTests.ATaskWithoutPriorityShowsNoDot` — shape only (one-line clamp, spacer, round checkbox, stripe, row height, colours, no-meta-line, no-dot)
- `TaskRowTests.TheGoalCarriesItsFullNameAsATitle` — shape only (one-line clamp, spacer, round checkbox, stripe, row height, colours, no-meta-line, no-dot)
- `TaskRowTests.AGoalAloneGivesTheRowASecondLine` — shape only (one-line clamp, spacer, round checkbox, stripe, row height, colours, no-meta-line, no-dot)

### 07 My Day and Inbox (30)

- `InboxPageTests.ThereIsNoInlineCaptureFieldAnymoreOnlyTheFab` — absence of a removed input is shape; capturing through the FAB is 02-capture-and-triage.spec.ts › Enter or Add captures; the newest item comes first with its capture time
- `InboxPageTests.EachItemIsARowInOneSharedGroup` — row-group class is shape; both items listed is 02-capture-and-triage.spec.ts › Enter or Add captures; the newest item comes first with its capture time
- `InboxPageTests.ItDoesNotClaimNothingIsCapturedBeforeItHasLoaded` — transient loading skeleton before the replica answers; not reachable deterministically end-to-end
- `InboxItemPanelTests.ConvertingWithoutStepsOrDescriptionLeavesThemEmpty` — default absence; OrganiseInboxItem without steps is Api.Tests InboxCommandTests.OrganisingCreatesTheTaskAndEmptiesTheItem
- `TodayTests.EachTaskIsARowInASectionGroup` — row-group shape; tomorrow's task hidden is 04-today-rule-and-time-zone.spec.ts journey
- `TodayTests.ItLaysTodaysTasksOutInOneRowGroup` — row-group shape
- `TodayTests.ItDoesNotClaimNothingIsDueBeforeItHasLoaded` — transient loading skeleton before the replica answers; not reachable deterministically end-to-end
- `TodayTests.TheNowLineIsOnlyOnToday` — the now label is the live clock time, not stably locatable; DayTimelineTests.TheNowLabelShowsOnlyWhenNowIsGiven keeps the component rule
- `TodayTests.TaskSectionsAreLedgerGroups` — CSS class only
- `TodayTests.TheTimelineLeavesTheGoalOut` — shape: the timeline block has no goal slot
- `Routed: TaskRowTests.ACompletedTaskWithAPastDueDateIsNotOverdue` — TaskRow.IsOverdue delegates to TodayRule; covered by DayPlanTests.ACompletedTaskIsNotCarriedAsOverdue and TodayRuleTests.ACompletedTaskDropsOffToday
- `Routed: TaskRowTests.TheMetaLineRunsDueStepsRepeatDescriptionThenList` — layout order
- `Routed: TaskRowTests.TheTimeCanBeHidden` — no caller sets ShowTime=false; the schedule block shows its own time (DayTimelineTests.ABlockShowsItsNameTimesAndList)
- `Routed: TaskRowTests.TheGoalSitsBeforeTheListName` — layout order
- `Routed: TaskRowTests.APastDueStepIsRed` — colour is style; overdue through a step is 21-step-deadline.spec.ts journey
- `Routed: TaskRowTests.TheMarkComesAfterTheDueDateAndBeforeTheStepProgress` — layout order
- `SyncRefreshTests.EveryReplicaBackedPageListensForTheSyncRevision` — wiring (reflection over a cascading parameter, Theory, 9 pages); pages refreshing on sync are 02-capture-and-triage.spec.ts › an item captured on another device shows up …
- `SyncRefreshTests.EveryRoutablePageIsAccountedForOnOneSideOrTheOther` — wiring guard over route attributes
- `SyncRefreshTests.ARenderThatBringsNoNewSyncDoesNotSendThePageBackToTheReplica` — counts reads on our own replica port (performance wiring)
- `TaskDetailPanelTests.WithNoSnapshotMarksThereIsNoSnapshotSection` — absence of a section; 11-public-snapshot.spec.ts covers it appearing and clearing
- `TaskDetailPanelTests.CompletingATaskWhileDisabledDoesNotChangeItsState` — Disabled only while the shell boots (!_ready); transient, not reachable end-to-end
- `TaskDetailPanelTests.OnADesktopViewportTheDrawerKeepsItsFixedColumnWidth` — layout (drawer width)
- `TaskDetailPanelTests.OnASmallViewportTheDrawerFillsTheFullWidthInsteadOfAFixedColumn` — layout (drawer width)
- `TaskDetailPanelTests.StepsComeFirstThenThePropertyRowsThenTheDescriptionSection` — layout order
- `TaskDetailPanelTests.SwitchingTaskIdWhileEditingShowsTheOtherTasksDescriptionRatherThanTheDraft` — not reachable by a user: the open panel's overlay blocks opening another task; MarkdownField is keyed on the task id
- `TaskDetailPanelTests.InViewModeEveryControlIsDisabled` — Disabled only while the shell boots (!_ready); transient
- `TaskDetailPanelTests.TheNameIsALargeUnboxedFieldBesideTheDoneCheckbox` — typography and layout; the name value is read by every openTask
- `TaskDetailPanelTests.StepsFollowTheNameAndPropertyRowsFollowTheSteps` — layout order
- `TaskDetailPanelTests.ANewTaskIsNotCreatedWhileDisabled` — Disabled only while the shell boots (!_ready); transient
- `TaskDetailPanelTests.ANewTaskShowsTheStepListBeforeThePropertyRows` — layout order

### 08 Areas, lists, goals (44)

- `AreaBoardTests.TheHeadingShowsAFolderIcon` — icon (shape)
- `AreaBoardTests.TheSharedBoardShowsAPeopleIcon` — icon (shape)
- `AreaBoardTests.EachCardShowsItsKindIcon` — icon (shape); kind behaviour is the card's Add task vs Add item, 25 and 30
- `AreaBoardTests.ADeletedAreasScreenDoesNotRenderItsName` — an area's own delete navigates home (03 journey); a deleted area's address is not reachable from the UI
- `AreaBoardTests.ItLaysListCardsOutInTheGrid` — layout
- `AreaBoardTests.ItShowsCardSkeletonsBeforeItHasLoaded` — transient loading skeleton
- `AreaBoardTests.ARejectedStarFromACardSurfacesTheRejection` — a rejection end-to-end needs a stub of our own handler; snackbar wiring for rejections is CommandSender/ReferenceFieldList unit tests
- `AreaBoardTests.TheAreaHasExactlyOneFabMenu` — DOM count (shape)
- `AreaBoardTests.TheFabMenuOffersThreeIconOnlyItems` — icon-only rendering (shape); the items are used by name in 03, 25, 30
- `AreaDetailPanelTests.TheNameIsTheUnboxedHeadingFieldTasksUse` — typography; the "Area name" placeholder is read by every createArea
- `AreaDetailPanelTests.ANewAreaHasNoListsSection` — absence of a section on a draft; 03 checks the new-area panel offers only the name and Add area
- `AreaDetailPanelTests.ClosingInvokesOnClose` — callback wiring; closePanel in every scenario
- `ListPageTests.TheHeaderShowsTheListsKindIcon` — icon (shape)
- `ListPageTests.ARejectedStarSurfacesTheRejection` — a rejection end-to-end needs a stub of our own handler
- `ListPageTests.ARecurringTaskIsNeverOverdueHereEither` — the list page shows overdue only as a CSS class; recurring never overdue is 06-recurring-task.spec.ts and TodayRule unit tests
- `ListPageTests.ItLaysOpenTasksOutInOneRowGroup` — layout
- `ListPageTests.TheListHasExactlyOneFabMenu` — DOM count (shape)
- `ListPageTests.ItDoesNotClaimTheListIsGoneBeforeItHasLoaded` — transient loading state
- `ListPageTests.ItShowsATitleSkeletonBeforeItHasLoaded` — transient loading skeleton (size style)
- `ListPageTests.EachTaskIsARowInTheGroup` — DOM structure; rows are counted by name in 26
- `ListPageTests.TheInlineAddTaskFieldAndHeaderMenuAreGone` — absence of retired markup
- `ListDetailPanelTests.TheNameComesFirstAndTheKindBelowIt` — layout order
- `ListDetailPanelNavigationTests.ReRenderingTheSameListLoadsTheSummaryOnce` — counts calls on our own ISnapshotsApi fake (performance wiring)
- `ListDetailPanelNavigationTests.ComingBackFromSnapshotsReloadsTheSummaryOnce` — counts calls on our own ISnapshotsApi fake (performance wiring)
- `ReferenceItemPanelTests.WithNoSnapshotMarksThereIsNoSnapshotSection` — absence of a section; appearing and clearing go with the Task 11 row above
- `ReferenceItemPanelTests.TheOldAddFieldButtonIsGone` — absence of retired markup
- `ReferenceItemPanelTests.DroppingTheLastFieldAtOneSendsToIndexOneOfThree` — index math covered by PositionsTests.MoveShiftsTheItemAndLeavesNoGaps and ReferenceFieldTests; drag is 08
- `ReferenceItemPanelTests.DroppingTheFirstFieldAtOneSendsToIndexOneOfThree` — index math covered by PositionsTests.MoveShiftsTheItemAndLeavesNoGaps and ReferenceFieldTests; drag is 08
- `ReferenceItemPanelTests.ARejectedStarStaysUnstarredAndSurfacesTheRejection` — a rejection end-to-end needs a stub of our own handler
- `ReferenceItemPanelTests.TheListRowSitsBetweenTheFieldsAndTheDescription` — layout order
- `ReferenceItemPanelTests.AnItemWithoutACreatedDateShowsNoCreatedText` — legacy documents without CreatedAt are not reachable end-to-end (MongoBackfill fills them)
- `ReferenceItemPanelTests.ARejectedCreateWarnsAndKeepsTheDrafts` — a rejection end-to-end needs a stub of our own handler
- `ReferenceItemPanelTests.ARejectedFieldAfterCreateWarnsAndStillCloses` — a rejection end-to-end needs a stub of our own handler
- `ReferenceItemPanelTests.ASecondAddWhileTheFirstIsInFlightSendsNothing` — in-flight window needs a gated handler of our own; not reachable by a user
- `GoalsPageTests.TheInlineNewGoalFieldIsGone` — absence of retired markup
- `GoalsPageTests.EachGoalIsARow` — component count (shape); rows are located by name in 28
- `GoalsPageTests.ItShowsRowSkeletonsBeforeItHasLoaded` — transient loading skeleton
- `GoalDetailPanelTests.TheNameIsTheUnboxedHeadingFieldTasksUse` — typography; the "Goal name" placeholder is read by createGoal and 28
- `GoalDetailPanelTests.ClosingInvokesOnClose` — callback wiring; closePanel in every scenario
- `GoalPageTests.TheHeadingShowsAFlagIcon` — icon (shape)
- `GoalPageTests.EachTaskIsARowInOneGroup` — DOM structure
- `GoalPageTests.ItShowsASkeletonAndNoVerdictBeforeItHasLoaded` — transient loading skeleton
- `GoalPageTests.TogglingAnEndedRecurringTaskDoesNothing` — GoalPage uses the same ended-repeat guard as ListPage, covered by 26-list-page.spec.ts › ticking an ended repeat or a repeat on a day it skips saves nothing; a ticked rep…
- `GoalPageTests.ChartsSitBetweenTheSummaryAndTheTasks` — layout order

### 09 Money pages (39)

- `BudgetPanelTests.ARejectedCreateStaysOnThePanelAndShowsTheRejection` — a rejection end-to-end needs a value the UI cannot type (the field caps it) or a stub of our own handler; the domain rejection is Module.Money.Tests and the snackbar wiri…
- `BudgetPanelTests.TheNameFieldCapsAtSixtyFourCharacters` — maxlength attribute (markup); the limit itself is Module.Money.Tests and content-limit integration tests
- `BudgetPanelTests.ARejectedRenameShowsTheRejection` — a rejection end-to-end needs a value the UI cannot type (the field caps it) or a stub of our own handler; the domain rejection is Module.Money.Tests and the snackbar wiri…
- `BudgetPageTests.EveryViewCarriesItsIcon` — icons (shape)
- `BudgetPageTests.TheHeadingHasNoCategoryCountSubtitle` — absence of a subtitle (shape)
- `BudgetPageTests.TheStripMarksTheMonthsThatHaveEntriesOrBalances` — the "has entries" mark is covered by MonthStripTests (kept component test) and every month chip in 12/13 is found through monthButton, which accepts the ", has entries" s…
- `BudgetPageTests.TheSettingsTabHasNoFab` — the add FAB is phone-only; 12-budget-month.spec.ts › on a phone the add button offers an expense or an income on Entries, and Stats has none shows Stats without it, and s…
- `BudgetPageTests.ABudgetOwnedByAnotherUserDoesNotExist` — another user's budget never reaches the replica: Api.Tests budget sharing/sync tests (sync is per membership) and MembershipReconcileTests
- `BudgetPageTests.ARejectedCategoryKeepsTheInputOpenWithItsTextAndShowsTheRejection` — a rejection end-to-end needs a value the UI cannot type (the field caps it) or a stub of our own handler; the domain rejection is Module.Money.Tests and the snackbar wiri…
- `BudgetPageTests.TheCategoryInputCapsAtFortyCharacters` — maxlength attribute (markup); the limit is Module.Money.Tests CategoryName tests
- `BudgetPageTests.SettingsPairsTheCardsUnderTheListsUntilWide` — grid layout (shape)
- `BudgetPageTests.ARejectedArchiveSaysWhy` — a rejection end-to-end needs a stub of our own handler
- `EntryPanelTests.TheNameAndNoteStopAtTheirLimits` — maxlength attributes (markup); limits are Module.Money.Tests
- `EntryPanelTests.AnNbpRateForAnotherCurrencyThanNowIsDropped` — late-answer race needs a gated NBP; guarded by RateFields' currency/date check, not reachable deterministically end-to-end
- `EntryPanelTests.AnNbpRateForAnotherDateThanNowIsDropped` — late-answer race needs a gated NBP; not reachable deterministically end-to-end
- `EntryPanelTests.ALateNbpAnswerForAnotherEntryIsDropped` — late-answer race needs a gated NBP; not reachable deterministically end-to-end
- `EntryPanelTests.ANbpFailureKeepsTheRateAndSaysSo` — the failure snackbar is one string; NBP down is exercised for the balance tiles and import (13-balance-month.spec.ts › holdings group by type with their amount, rate and …
- `EntryPanelTests.TwoRapidSavesSendOneCommand` — double-submit guard needs a gated handler of our own; Playwright's click waits for the panel to close
- `EntryPanelTests.SwitchingFromANewExpenseToANewIncomeStartsAFreshDraft` — the open panel's overlay covers both add buttons, so a user cannot switch kinds with a draft open
- `EntryPanelTests.AForeignEntryLeavesThePanelClosed` — another user's entries never reach the replica (Api.Tests sync per membership, MembershipReconcileTests.AnEntryOfABudgetIDoNotHoldIsDropped)
- `EntryPanelTests.AForeignBudgetLeavesTheNewPanelClosed` — another user's budget never reaches the replica (as above)
- `EntryPanelTests.ARejectedSaveStaysOpenAndSaysWhy` — a rejection end-to-end needs a value the UI cannot type (the field caps it) or a stub of our own handler; the domain rejection is Module.Money.Tests and the snackbar wiri…
- `BudgetBalanceTests.ALowerRateTodayShowsANegativeDifference` — sign formatting is MoneyTextTests.SignedPlnShowsTheSign and BalanceTotalsTests.ValueTodayUsesTodaysRateRoundedHalfAwayFromZero
- `BudgetBalanceTests.LeavingThePageUnsubscribesFromConnectivity` — event unsubscription is wiring with no visible effect
- `BudgetBalanceTests.AnAnswerForAnotherCurrencyIsDropped` — a mismatched NBP answer needs a stub answering another currency than asked; the API never does
- `BudgetBalanceTests.TheNetWorthColumnSitsBesideTheHoldingsOnlyWhenWide` — grid layout (shape)
- `HoldingPanelTests.TheRateFieldIsHiddenForPln` — same RateFields as the entry panel: 12-budget-month.spec.ts › a new expense starts in the default currency, waits for its fields, suggests the latest rate, takes NBP's an…
- `HoldingPanelTests.ALateNbpAnswerForAnotherCurrencyIsDropped` — late-answer race needs a gated NBP; not reachable deterministically end-to-end
- `HoldingPanelTests.ALateNbpAnswerForAnotherHoldingIsDropped` — late-answer race needs a gated NBP; not reachable deterministically end-to-end
- `HoldingPanelTests.AFailedNbpLookupKeepsTheRateAndSaysSo` — the failure snackbar is one string; NBP down is exercised in 13-balance-month.spec.ts › holdings group by type with their amount, rate and PLN value, and a failed NBP loo…
- `HoldingPanelTests.ARejectionIsShownAndNothingIsQueued` — a rejection end-to-end needs a value the UI cannot type (the field caps it) or a stub of our own handler; the domain rejection is Module.Money.Tests and the snackbar wiri…
- `HoldingPanelTests.AnUnknownHoldingOpensNothing` — only reachable by a hand-made URL; HoldingQueryTests cover the query
- `HoldingPanelTests.ADeletedMonthOpensNothing` — only reachable by a hand-made URL; after Delete month no holding row is left to open (13-balance-month.spec.ts › deleting the month asks first, and Cancel keeps it)
- `BudgetImportPageTests.AnArchivedBudgetShowsTheBannerAndNoPickers` — an archived budget hides its import cards (32-budgets-and-settings.spec.ts › an archived budget reads only: no adding, editing, starting or importing, and it waits behind…
- `BudgetImportPageTests.OverlappingRateFetchesResolvingInReverseOrderLeaveNoRowWaiting` — a reverse-order race needs gated NBP answers; covered by ImportRatesTests.RetryingReplacesAnEarlierFailure and ImportProgressTests
- `BudgetImportPageTests.AnEntriesFileShowsNoTabs` — absence of tabs (shape)
- `BudgetImportPageTests.LeavingWithPickedDataAndConfirmingLeaves` — the confirm side of the same NavigationLock dialog; 33-budget-history.spec.ts › an entries file previews ready and error rows, new categories and rate sources, retries a …
- `BudgetStatsTests.TheListsAndTheChartsPackIntoTwoColumns` — grid layout (shape)
- `BudgetStatsTests.ARateThatRoundsToZeroIsNotRed` — colour (shape); MoneyText.PercentIsNegative rounding is MoneyTextTests

### 10 Sharing pages (10)

- `JoinPageTests.AnExpiredAnswerSaysSoAndOffersMyDay` — unreachable end-to-end: an invite expires after 30 minutes of server time; the server's 410 is Api.Tests JoinListTests.AnExpiredInviteWithTheRightCodeAnswersGone / Budget…
- `ListMembersViewTests.StoppingSharingRemovesEveryone` — not covered end-to-end for lists: ListMembersView's own Stop sharing loop (ListMembersView.razor:133-160) has no step; 10 has one member, who is removed by Remove. The bu…
- `Routed (06): AreaChipsTests.TheSharedWithMeChipSpeaksPolishUnderThePolishCulture` — a resource string only; the shared chip in Polish would need a shared list inside 18 (three contexts already, over a minute); Polish resources are kept by the localizatio…
- `BudgetMembersPanelTests.AStrangerSeesNoPanel` — a stranger's replica never holds the budget: sync is per membership (Api.Tests BudgetSharingEndpointTests.LeavingAndRemovalEndAccess, MembershipReconcileTests.ABudgetThat…
- `SnapshotPageTests.TheHeroUsesThePrimaryTheme` — CSS class only
- `SnapshotPageTests.ABlankOwnerNameFallsBackToTheOwner` — unreachable: the server freezes the publisher's display name into every snapshot, and an account always has one; the fallback is two resource strings
- `SnapshotPageTests.ADoneTaskIsStruckThrough` — inline style only; the done task's section is covered by DoneTasksSitInTheirOwnSection
- `SnapshotPageTests.ATickRefusedForTooManyChangesSaysSo` — a real 429 needs 100 marks on one list in a day or the per-address public limit, which parallel scenarios share; the server side is Api.Tests SnapshotMarkTests.MarkingALi…
- `SnapshotPageTests.ASignedInVisitorCachesTheVisitTimeNotTheSnapshotsCreatedAt` — not covered: what SnapshotPage writes as the opening time has no step (end-to-end the publish and the visit fall on the same day); State/SnapshotVisitRowsTests.ACachedSna…
- `SnapshotPageTests.AnAnonymousVisitorNeitherRecordsNorCaches` — nothing to observe: a visitor without an account has no List snapshots; recording a visit needs a signed-in caller (Api.Tests snapshot visit endpoint under /api, which re…

### 11 Shell (68)

- `AuthenticationTests.TheLoginCallbackRouteNeverMountsAppShell` — wiring (a layout attribute); the race it prevents would leave a signed-in user anonymous, which every signIn landing on My Day with the account in the sidebar disproves, …
- `AuthenticationTests.ItShowsTheBrandMarkInsteadOfTheLibraryText` — markup of a transient callback screen (the brand mark is the shared "Loading PSPad" image); 01-first-sign-in.spec.ts › logging in again holds the app until the first pull…
- `AuthenticationTests.ItStoresTheRotatedRefreshTokenRatherThanTheCapturedOne` — not observable: the realm does not revoke reused refresh tokens; TokenRefresherTests covers Renewed carrying the rotated token
- `AuthenticationTests.ItWritesNothingWhenNoRefreshTokenIsCaptured` — unreachable end-to-end (Keycloak always issues one for this client); same outcome as 01-first-sign-in.spec.ts › a sign-in whose profile fetch fails leaves the visitor on …
- `AuthenticationTests.ItWritesNothingWhenTheProfileCarriesNoUserId` — the server never answers an empty id; MeResponseExtensionsTests.AnEmptyUserIdIsNoAccountAtAll keeps the rule, 01-first-sign-in.spec.ts › a sign-in whose profile fetch fai…
- `AuthenticationTests.ACompletedLogOutClearsTheLocalSessionAndTheReplica` — unreachable: Sign out goes to Keycloak end-session and back to /welcome, never through /authentication/logout-callback; clearing is LocalSignOutTests.ClearingRemovesTheOu…
- `SearchPageTests.AReferenceListHitShowsTheLibraryBooksIcon` — icon only; ListIconTests.AReferenceListShowsTheLibraryBooksIcon keeps the rule; 15-search-statistics-settings.spec.ts › search finds the task, its result opens the task, …
- `SearchPageTests.ATasksListHitShowsTheChecklistIcon` — icon only; ListIconTests.ATasksListShowsTheChecklistIcon keeps the rule
- `SettingsPageTests.WhenTheServerIsUnreachableTheChoiceStaysPending` — pending write and retry are LanguagePreferenceTests.APendingChoiceIsNotOverwrittenByTheServer and SyncCoordinator retries; no step takes the server down while choosing
- `SettingsPageTests.ARejectedCurrencyLeavesTheSelectOnTheOldValue` — unreachable: the select offers only supported currencies; the rejection is MoneyPreferences Decide unit tests
- `SettingsPageTests.ItsHeaderCarriesAnIconTileAndASubtitleLikeTheAreaScreen` — shape (icon tile); the subtitle text is a resource
- `SettingsPageTests.ANullTimeZoneFromTheServerDoesNotCrashTheSettingsPage` — the server never answers null; MeResponseExtensionsTests.ANullOrEmptyTimeZoneFallsBackToUtc
- `SettingsPageTests.ATimeZoneTheBrowserCannotResolveFallsBackToUtcInsteadOfThrowing` — the picker lists only zones the runtime has; LocalToday unit tests keep the fallback
- `SettingsPageTests.TheSyncSectionSaysWhenNothingHasSyncedYet` — unreachable: the shell holds the app until the first pull lands (01-first-sign-in.spec.ts › logging in again holds the app until the first pull lands, then shows the acco…
- `SettingsPageTests.ARejectionAlreadyDroppedOffersNoDiscard` — not covered end-to-end: no step makes an unrecoverable rejection; the drop is SyncServiceTests (structural rejections leave the outbox) and the Discard button is shown on…
- `SettingsPageTests.SigningOutClearsBeforeLeavingForKeycloakSoAnInterruptedSignOutStillSignsOut` — ordering inside one click, not observable in a browser; LocalSignOutTests.SigningOutClearsEverythingAndHandsBackTheIdTokenAsTheEndSessionHint
- `SettingsPageTests.TimeZoneThemeAndAccentShareOneApplicationSettingsCard` — layout only
- `SettingsPageTests.OnAPhoneTheColourPickerFillsTheScreen` — layout only (full-screen dialog option)
- `SettingsPageTests.TheThemeControlIsASelectNotAButtonGroup` — shape (component kind); 15-search-statistics-settings.spec.ts › settings change theme, accent, currency and time zone, and they survive a reload drives it as a combobox
- `SettingsPageTests.TheCardsSitInAResponsiveGrid` — layout only
- `SettingsPageTests.TheSignOutButtonLivesInsideTheAccountCard` — layout only
- `SettingsPageTests.TheDeleteConfirmButtonStaysDisabledWhenTheAccountHasNoEmailToConfirmAgainst` — unreachable: Keycloak accounts here always carry an email
- `StatisticsPageTests.CompletionsChartRendersThePlannedAndUnplannedSeriesPerDay` — chart series wiring; the numbers are Api.Tests StatisticsOverviewTests, the tiles and heatmap in 15-search-statistics-settings.spec.ts › statistics count today, by goal a…
- `StatisticsPageTests.OpenedChartRendersTheOpenedCountsPerDay` — chart series wiring; Api.Tests StatisticsOverviewTests
- `StatisticsPageTests.OutstandingChartRendersTheOutstandingCountsPerDay` — chart series wiring; Api.Tests StatisticsOverviewTests.ATaskOpenedBeforeTheWindowCountsOnEveryDayUntilItIsFinished
- `StatisticsPageTests.CompletionsChartYAxisScalesToTheStackedTotal` — the stacked total is a Max over two fields in the page; the step rule is ChartAxisTests.TheYAxisStepIsARoundNumberForTheLargestValue
- `AccountBadgeTests.TheAvatarFallsBackToTheEmailWhenThereIsNoName` — Keycloak accounts here always have a name; AvatarColorTests.TheInitialIsTheFirstLetterOfTheAddress keeps the rule
- `AccountBadgeTests.TheFullBadgeHasGenerousTopAndBottomPadding` — layout only
- `AccountBadgeTests.ItHasNoDropdownArrowOrMenu` — shape only
- `AppShellTests.OnTheDesktopThePageInsetIsPaddingSoNoMarginCollapsesAboveTheShell` — layout only
- `AppShellTests.ThereIsNoHamburgerAnymore` — shape only
- `AppShellTests.PageContentUsesTheFullWidth` — layout only
- `AppShellTests.ANullDisplayNameOrEmailFromApiMeDoesNotCrashTheShell` — the server never answers null; MeResponseExtensionsTests.ANullDisplayNameOrEmailBecomesEmpty
- `AppShellTests.ANullTimeZoneFromApiMeDoesNotCrashTheShell` — the server never answers null; MeResponseExtensionsTests.ANullOrEmptyTimeZoneFallsBackToUtc
- `AppShellTests.TheSyncCoordinatorStartsEvenWhenTheAccountFetchFails` — wiring (a read count on a spy)
- `AppShellTests.AThrowingSessionStoreLeavesTheShellReadyAndAnonymousRatherThanThrowing` — unreachable (IndexedDB failure); SessionBootstrapperTests.ItReportsNoSessionWhenTheStoreThrows
- `AppShellTests.ItBecomesReadyWhenLoadingTheLocalReplicaFails` — unreachable (IndexedDB failure); the shell's try/catch only
- `AppShellTests.ItKeepsTheTokenAndContactTimeWrittenWhileTheAccountFetchWasInFlight` — internal race, not observable; SessionAuthorizationHandlerTests cover rotation
- `AppShellTests.TheSameDayLeavesTheRevisionAlone` — wiring (revision counter)
- `AppShellTests.ItStaysReadyOnATimeZoneTheRuntimeDoesNotCarry` — unreachable: the server stores only zones from the picker; LocalToday falls back to UTC (unit)
- `AppShellTests.TheBeltsSitAboveThePageInFlow` — layout only
- `AppShellTests.ANewVersionOffersAReloadBeltThatActivatesIt` — not covered: needs a second deployed version for the service worker; BrowserAppUpdatesTests and StatusBeltsTests keep the parts
- `AppShellTests.AVersionAlreadyWaitingWhenTheShellMountsIsOfferedOnce` — not covered (same reason); BrowserAppUpdatesTests.AWaitingVersionIsAnnouncedOnce
- `BottomNavTests.TheMyDayLabelIsInsideTheLinkAndNotAnnouncedTwice` — shape (aria-hidden on the FAB); the link is found once by its name in 01-first-sign-in.spec.ts › on a phone the top bar names the screen, the bottom bar switches screens …
- `MobileTopBarTests.ItIsNotDenseSoTheStylesheetSetsItsHeight` — layout only
- `MobileTopBarTests.AFailedSyncTurnsTheButtonRed` — colour only; the failed label text is SyncStampTextTests.AFailedSyncSaysSoAndKeepsTheLastGoodTime
- `MobileTopBarTests.TheAvatarIsThirtySixPixels` — layout only
- `MobileTopBarTests.ThePageIconSitsInATileBeforeTheTitle` — shape (decorative icon tile)
- `MobileTopBarTests.NoIconMeansNoTile` — shape
- `MobileTopBarTests.TheTitleIsNotInThePrimaryColour` — colour only
- `NavSidebarTests.AreasLoadingIsMarkedAsALiveLoadingRegion` — transient loading markup (role status, aria-busy) while the replica loads
- `NavSidebarTests.ClickingNewAreaWhileDisabledDoesNotRaiseOnNewArea` — transient (only before the shell is ready)
- `NavSidebarTests.NoAreaRowCarriesAMenuAnymore` — shape only
- `NavSidebarTests.TheFooterSplitsTheDateAndTheSignatureAcrossTwoLines` — layout only
- `NavSidebarTests.TheNewAreaRowCarriesAnAddIcon` — icon only
- `NavSidebarTests.NewBudgetIsDisabledUntilTheShellIsReady` — transient (only before the shell is ready)
- `PublicLayoutTests.ThePublicScreensGetTheAppTheme` — theme CSS variables are shape; 01-first-sign-in.spec.ts › Welcome greets a visitor and keeps where they were going and 11-public-snapshot.spec.ts render the public screen…
- `PullToRefreshTests.APullOfflineDoesNothing` — not covered (touch gesture); a call count on our own trigger
- `PullToRefreshTests.APullDuringASyncDoesNotStartAnother` — call count on our own trigger; SyncCoordinatorTests cover overlapping syncs
- `StatusBeltStackTests.EachKindCarriesItsSeverity` — colour only
- `StatusBeltStackTests.TheNewestBeltIsOnTop` — order rule is StatusBeltsTests.TheNewestBeltComesFirst; no step raises two belts at once
- `StatusBeltStackTests.ABeltWithoutAnActionHasNoButtonForIt` — every belt the app raises has an action; StatusBeltsTests.ABeltNeedsNoAction
- `SyncButtonTests.ItIsDisabledWhileASyncRuns` — transient; a sync lasts a few hundred ms
- `SyncButtonTests.AFailedSyncTurnsTheIconButtonRed` — colour only; 14-offline-capture-and-sync.spec.ts › an unreachable server raises a belt whose Retry syncs once it answers again shows the failure in text
- `SyncButtonTests.AFailedSyncTurnsTheLabelledButtonRed` — colour only
- `SyncButtonTests.ItTurnsRedWhenASyncFailsWhileOpen` — colour only; 14-offline-capture-and-sync.spec.ts › an unreachable server raises a belt whose Retry syncs once it answers again follows the failure while open
- `SyncStampTests.TheExactTimeIsOnHover` — hover title only
- `Routed (06): PageHeadingTests.ItHandsTheIconToTheShell` — decorative icon; shape

### 21 Theme and main layout (round 2) (74)

- `PSPadThemeTests.CornersAreTwelvePixels` — size or stylesheet text only
- `PSPadThemeTests.TheBeltTintAndInkMatchTheStylesheet` — size or stylesheet text only
- `PSPadThemeTests.TheMessageGroundMatchesTheStylesheet` — size or stylesheet text only
- `PSPadThemeTests.TheRaisedGroundMatchesTheStylesheet` — size or stylesheet text only
- `StylesheetRulesTests.TheCardTileShowsWhereKeyboardFocusIs` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.TheCardTileDeepensItsTintOnHover` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.ThePhoneWeekStripOutranksTheCardLookAndStaysEdgeToEdge` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.WeekDaysAndShowAllKeepTheirOwnShapeInsteadOfThePill` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.NoButtonPillRuleCatchesTheWeekDays` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.CardsInsideADrawerSitOnTheDrawerSurfaceNotBelowIt` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.RowsInAGroupAreSeparatedByAnInsetDivider` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.RowsInsideAnAreaCardGetTheSameDivider` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.ARowHasTheSamePaddingOnBothSides` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.AGroupSpansTheContentWidth` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.AColumnGroupFlowsRowsIntoTwoThenThreeColumns` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.RowsInAColumnGroupDrawTheirOwnBottomDividerAndLeftHairline` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.ALedgerRowGoesOnOneLineOnlyWhenItsListIsWideEnough` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.MyDayIsTwoPanesFrom1280AndTabsBelow` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.TheTimelinePlacesEverythingOnItsOwnLength` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.TheTimelineFillsTheHeightWithoutScrolling` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.ThePhoneScheduleTabHasNoAddButtonSinceTheScheduleAddsOnClick` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.AWideScreenRunsTheDayLeftToRightAboveTheTasks` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.TheDayTabsArePillShapedLikeTheirButtons` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.AGroupSitsInItsLaneLikeABlock` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.BlocksLeaveARailAndTheGutterFreeForAddingATask` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.VeryWideMyDayFlowsItsRowsIntoTwoColumnsBelowTheSchedule` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.AnEmptyStarIsHiddenUntilHoverOnlyWhereHoverExists` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.ARowNoLongerHasAFixedHeight` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.AFocusableRowShowsKeyboardFocus` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.ARowShowsKeyboardFocusLikeHover` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.ThePersistentSidebarIsPinnedToTheTopInsteadOfItsStaticPosition` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.OnTheDesktopTheMainAreaContainsItsChildrensMargins` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.ThePageIconIsATintedTile` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.ThePhoneTopBarIconTileIsSmaller` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.TheDesktopTitleIsTwentyPixelsSemiBoldWithTheSubtitleTwoPixelsUnderIt` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.TheDesktopHeadingRowIsAsTallAsTheAccountBadgeRowWithFourteenPixelGaps` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.ThePhoneTopBarIsSixtyFourPixelsOnTheSurfaceWithAHairline` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.TheTopBarHeightRuleOutranksMudBlazorsAppBarHeights` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.PhoneContentStartsUnderTheTallerTopBar` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.ThePullIndicatorSitsJustUnderTheTopBar` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.TheTimeRowStaysOnOneLineWithItsLabelAtEveryWidth` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.ThePhoneTopBarTitleIsEighteenPixelsInTheTextColour` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.ThePhoneTopBarButtonsAreFortyFourPixels` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.TheFromAndToLabelsAreSmallAndFixedWidth` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.EachTimePickerFillsTheRestOfItsRow` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.StatusBeltsStackInFlowSoTheyPushThePageDown` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.OnTheDesktopBeltsAreRoundedAndSpacedAboveTheHeading` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.OnAPhoneBeltsRunEdgeToEdgeUnderTheTopBar` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.ABeltWritesInBodyTextOnItsTint` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.OnAPhoneAMessageSitsAboveTheBottomNav` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.OnTheDesktopAMessageMovesToTheLeftOfTheContent` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.AMessageIsCompact` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.TheBudgetViewsShareTheRowEvenly` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.TheMonthStripFillsItsRowWithWholeChipsCentred` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.MonthChipsStackTheLabelOverTheDot` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.OnAPhoneTheMonthPickerIsABottomSheet` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.TheMonthPickerLaysTheYearOutInFourColumns` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.TheSummaryPresetsScrollSidewaysInsteadOfWrapping` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.OnAPhoneEachViewStacksItsIconOverItsLabel` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.TheCategoryNamesKeepTheirCase` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.TheBudgetViewsKeepTheirCase` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.TheBudgetPageTakesTheFullWidth` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.NothingFloatsBottomLeft` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.OnDesktopTheViewsAreNotStretched` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.OnDesktopTheHeroFiguresSitBesideTheHeadline` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.TheWholeBudgetCardIsTheLink` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.AHoveredBudgetCardOutlinesInThePrimary` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.TheHeroFiguresWrapRatherThanOverflow` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.CategoryRowsReadAsListRowsNotButtons` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.TheHoldingsBoxMeasuresItself` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.HoldingsLineUpInColumnsWhenTheirBoxIsWide` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.TheHoldingsHeadDoesNotWrap` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.HoldingsHideTheirTableCellsInANarrowBox` — CSS text only; layout is seen in the e2e run
- `StylesheetRulesTests.LongEntryNamesEndInAnEllipsis` — CSS text only; layout is seen in the e2e run

### 22 Components (round 2) (23)

- `DayTimelineTests.WithoutAnAddHandlerThereIsNothingToClick` — no user path: My Day is the only timeline and always passes OnAdd
- `DueDateMenuTests.ChoosingTheCurrentDaySendsNothing` — no observable result: picking the shown day changes nothing on screen or on the server
- `DueDateRowRenderTests.WithoutADateItSpeaksPolishUnderThePolishCulture` — resource text only; Localization/ResourceGuardTests.EveryLanguageHasEveryKeyAndNoEmptyValue keeps the Polish key
- `MarkdownFieldTests.TheEditorStopsAtTheDescriptionLimit` — the limit is enforced by the domain (Module.Tasks.Tests ContentLimitsTests); maxlength is markup
- `MarkdownFieldTests.ARejectedSaveRefocusesTheEditor` — no user path reaches a rejected or throwing save: Decide rejects only text over the limit (the field stops at it) or a missing aggregate (the panel closes)
- `MarkdownFieldTests.DisabledOffersNoEdit` — no user path: a panel is Disabled only while the shell is not ready, and AppShell opens panels only after it is ready (ReadOpenPanels after _ready)
- `MarkdownFieldTests.AThrowingSaveStaysInEditKeepingTheDraft` — no user path reaches a rejected or throwing save: Decide rejects only text over the limit (the field stops at it) or a missing aggregate (the panel closes)
- `MarkdownFieldTests.ARejectedSaveStaysInEditKeepingTheDraft` — no user path reaches a rejected or throwing save: Decide rejects only text over the limit (the field stops at it) or a missing aggregate (the panel closes)
- `MarkdownFieldTests.ASaveInFlightIsNotSentTwice` — internal guard; a second blur has no observable result
- `MarkdownFieldTests.TheEditButtonAndTextareaCarryAriaLabels` — every description step locates the Edit description button and the Description textbox by these names
- `ReferenceFieldListTests.TheAddRowStopsAtTheLabelAndValueLimits` — the limit is enforced by the domain (Module.Tasks.Tests ContentLimitsTests, reference labels and values); maxlength is markup
- `ReferenceFieldListTests.ARejectedAddSurfacesTheRejectionAndKeepsTheText` — no user path reaches a rejected or throwing save: Decide rejects only text over the limit (the field stops at it) or a missing aggregate (the panel closes)
- `ReferenceFieldListTests.ARejectedMoveDoesNotReportAChange` — no user path reaches a rejected or throwing save: Decide rejects only text over the limit (the field stops at it) or a missing aggregate (the panel closes)
- `ReferenceFieldListTests.ARejectedRemoveSurfacesTheRejection` — no user path reaches a rejected or throwing save: Decide rejects only text over the limit (the field stops at it) or a missing aggregate (the panel closes)
- `ReferenceFieldListTests.EachRowShowsItsLabelAboveItsValue` — layout only
- `ReferenceFieldListTests.DisabledOffersNoAddOrRemove` — no user path: a panel is Disabled only while the shell is not ready, and AppShell opens panels only after it is ready (ReadOpenPanels after _ready)
- `ReferenceFieldValueTests.RemoveIsHiddenForANewField` — no user path: the editor opens only on an existing field
- `StepListTests.TheAddFieldStopsAtTheNameLimit` — the limit is enforced by the domain (Module.Tasks.Tests ContentLimitsTests); maxlength is markup
- `StepListTests.AnUnchangedStepNameChangesNothing` — no observable result
- `StepListTests.TheStepNameFieldIsLabelledAndCapped` — every step finds the field by Rename …; the cap is the limit is enforced by the domain (Module.Tasks.Tests ContentLimitsTests); maxlength is markup
- `StepListTests.ADisabledListCannotRenameAStep` — no user path: a panel is Disabled only while the shell is not ready, and AppShell opens panels only after it is ready (ReadOpenPanels after _ready)
- `StepListTests.DisabledShowsTheDateAsTextOnly` — no user path: a panel is Disabled only while the shell is not ready, and AppShell opens panels only after it is ready (ReadOpenPanels after _ready)
- `TimeRowTests.ThePickersStayTypeable` — every time step types into the Start and End fields

### 23 State (round 2) (11)

- `PageHeaderTests.SettingAHeaderAnnouncesIt` — asserts our own event fired (wiring); visible result covered by 01-first-sign-in › on a phone the top bar names the screen, the bottom bar switches screens and the avatar…
- `PageHeaderTests.SettingTheSameHeaderAgainStaysQuiet` — asserts our own event fired (wiring); visible result covered by 01-first-sign-in › on a phone the top bar names the screen, the bottom bar switches screens and the avatar…
- `SnapshotCacheTests.ASavedSnapshotComesBackByToken` — tests a test double, not production code
- `SnapshotCacheTests.AnUnknownTokenComesBackNull` — tests a test double, not production code
- `SnapshotCacheTests.AllAsyncListsEverySavedSnapshot` — tests a test double, not production code
- `SnapshotCacheTests.AllAsyncCarriesTheVisitTimeSeparatelyFromTheSnapshotsCreatedAt` — tests a test double, not production code
- `SnapshotCacheTests.SavingTheSameTokenAgainOverwritesIt` — tests a test double, not production code
- `SnapshotCacheTests.PruneAsyncDropsOnlyExpiredSnapshots` — tests a test double, not production code
- `SnapshotCacheTests.ClearAsyncDropsEverything` — tests a test double, not production code
- `StatusBeltsTests.EveryChangeIsAnnounced` — asserts our own event fired (wiring); visible result covered by 14-offline-capture-and-sync › an unreachable server raises a belt whose Retry syncs once it answers again
- `StatusBeltsTests.NothingThatChangesNothingIsAnnounced` — asserts our own event fired (wiring); visible result covered by 14-offline-capture-and-sync › an unreachable server raises a belt whose Retry syncs once it answers again

## 4. Dropped as already covered

Class level: `Class` (rows dropped) → covering test or scenario.

### 01 Money (Api.Tests)

- `BalanceHandlerTests` (4) → BalanceSyncTests
- `BudgetHandlerTests` (1) → BudgetSyncTests
- `CategoryHandlerTests` (1) → CategoryCommandTests; MoneyEntrySyncTests
- `MoneyEntryHandlerTests` (1) → MoneyEntrySyncTests

### 02 Tasks module

- `ReferenceHandlerTests` (3) → Api.Tests ReferenceCommandEndpointTests; ListKindTests; ReferenceSyncTests
- `DeleteCascadeTests` (1) → Api.Tests DeleteCascadeEndpointTests
- `DeadContainerTests` (4) → Api.Tests DeleteCascadeEndpointTests; Api.Tests ListViewSyncTests
- `GoalTaskLinkTests` (2) → GoalTests; SharedStatisticsTests
- `SharedTaskTests` (1) → Api.Tests MemberCommandTests
- `ListAccessTests` (1) → Api.Tests MemberCommandTests; ListAccessTests
- `AreaTests` (1) → Api.Tests DeleteCascadeEndpointTests
- `ReorderListsHandlerTests` (1) → Api.Tests AreaViewSyncTests
- `PlaceListHandlerTests` (2) → Api.Tests ListViewSyncTests

### 03 Statistics and Sharing modules

- `InboxRecordProjectionTests` (4) → StatisticsEndpointTests
- `StatisticsRecordProjectionTests` (3) → SharedStatisticsTests; StatisticsEndpointTests
- `StatisticsReplayTests` (4) → Api.Tests StatisticsReplayTests; StatisticsRebuildTests; StatisticsReplayTests
- `StatisticsReaderTests` (1) → StatisticsEndpointTests
- `StatisticsOverviewReaderTests` (3) → InboxCapturesTests; SharedStatisticsTests; StatisticsEndpointTests; StatisticsOverviewTests
- `SnapshotMarkingTests` (6) → MongoMarkBudgetTests; SnapshotMarkTests
- `SnapshotPublishingTests` (3) → PublicSnapshotTests; SnapshotEndpointTests

### 04 Api

- `AuthenticationLoggingTests` (1) → SecurityLoggingTests
- `LiveAudienceTests` (2) → LiveEndpointTests
- `LiveConnectionsTests` (2) → LiveEndpointTests
- `SseFramesTests` (1) → LiveEndpointTests

### 05 Client (non-UI)

- `LiveUpdatesTests` (2) → e2e 16

### 07 My Day and Inbox

- `TodayTests` (2) → DayTimelineTests; TimeRowTests
- `Routed: TaskRowTests` (3) → EarliestDueStepTests; StepListTests; TimeRowTests; TodayRuleTests
- `TaskDetailPanelTests` (2) → MarkdownFieldTests; StepTests

### 08 Areas, lists, goals

- `AreaBoardTests` (5) → e2e 07, 09, 30; AreaSummaryTests; DeleteWarningTests
- `Routed: ThingCardTests` (2) → e2e 07
- `AreaDetailPanelTests` (4) → e2e 09
- `ListPageTests` (7) → e2e 03, 07, 22; ListMonthViewTests
- `ListDetailPanelTests` (1) → see ledger text
- `ReferenceItemPanelTests` (6) → e2e 08
- `GoalsPageTests` (3) → e2e 07
- `GoalDetailPanelTests` (1) → see ledger text
- `GoalPageTests` (1) → e2e 07

### 09 Money pages

- `BudgetOverviewTests` (1) → InsightTextTests; InsightsTests
- `BudgetEntriesTests` (1) → e2e 12; EntryFiltersTests
- `EntryPanelTests` (3) → RateSuggestionTests
- `BudgetBalanceTests` (1) → BalanceTotalsTests
- `BudgetImportPageTests` (1) → e2e 33; ImportRatesTests
- `BudgetStatsTests` (3) → e2e 04; MoneySummaryTests; MoneyTextTests; SummaryRangeTests

### 11 Shell

- `SettingsPageTests` (1) → e2e 04
- `AppShellTests` (7) → e2e 03, 04, 16, 24, 27, 30
- `NavSidebarTests` (3) → e2e 10, 29

### 22 Components (round 2)

- `DayTimelineTests` (6) → e2e 17; TimelineAxisTests; TimelineLayoutTests
- `DueDateMenuTests` (1) → e2e 21
- `DueDateRowRenderTests` (2) → e2e 05, 23, 24
- `ListMonthViewTests` (1) → e2e 22
- `MarkdownFieldTests` (2) → e2e 23
- `MonthStripTests` (2) → e2e 12, 13
- `ReferenceFieldListTests` (4) → e2e 08, 27
- `ReferenceFieldValueTests` (6) → e2e 08, 27, 30; FieldDisplayTests
- `StepListTests` (5) → e2e 19, 21, 24
- `WeekStripTests` (3) → e2e 04, 05
- `TimeRowTests` (1) → e2e 17

### 23 State (round 2)

- `AreaLinksTests` (2) → e2e 10
- `AreaQueryTests` (2) → e2e 03
- `BudgetOrderTests` (1) → e2e 34
- `BudgetQueryTests` (2) → e2e 32
- `BudgetTabsTests` (1) → e2e 12
- `CardCollapseStateTests` (4) → e2e 25
- `DayQueryTests` (2) → e2e 04
- `DeleteWarningTests` (2) → e2e 25, 30
- `DroppedTextTests` (1) → e2e 08
- `EntryDefaultDateTests` (1) → e2e 12
- `EntryQueryTests` (2) → e2e 12
- `FieldDisplayTests` (1) → e2e 27
- `GoalQueryTests` (2) → e2e 28
- `HoldingQueryTests` (1) → e2e 13
- `ImportRatesTests` (2) → e2e 33
- `InboxQueryTests` (2) → e2e 02
- `InsightTextTests` (1) → e2e 12
- `InviteCodeTests` (2) → e2e 10, 34
- `InviteTokenTests` (1) → e2e 10
- `LastAreaTests` (1) → e2e 29
- `ListPlacementTests` (4) → e2e 03, 10
- `ListQueryTests` (2) → e2e 31
- `MoneyTextTests` (4) → e2e 12, 33
- `NavGroupStateTests` (1) → e2e 01
- `PageHeaderTests` (1) → e2e 01
- `ReferenceQueryTests` (2) → e2e 27, 30
- `SidebarCountsTests` (2) → e2e 01, 04
- `SnapshotVisitRowsTests` (1) → e2e 11
- `StatusBeltsTests` (2) → e2e 14
- `SummaryRangeTests` (3) → e2e 33
- `TaskDoneTests` (2) → e2e 26
- `TaskQueryTests` (4) → e2e 03, 05, 14

## 5. Skipped BUG steps

Bugs found during the migration are listed in `specs/behaviour-tests-design.md` (skipped `BUG:` steps). Not duplicated here.
