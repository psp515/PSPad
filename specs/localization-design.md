# Localization — design

Status: Built. English and Polish, structured so a third language is a
resource file per feature plus the six steps in §7.

## 1. Problem

Every user-facing string is hardcoded English in `.razor` files. Dates mix
`CultureInfo.InvariantCulture` and whatever culture the browser reports, so a
Polish user sees "18 Oct" next to "1 234,56". There is no language setting,
no place to put one, and no way for a contributor to add a language without
touching components.

## 2. Decisions

| # | Decision |
|---|----------|
| D1 | Language is a property of the `User` aggregate (`Language`, BCP-47 primary tag), next to `TimeZone`. It syncs across devices through `/me`. |
| D2 | Supported tags live once in `PSPad.Abstractions.SupportedLanguages` (`en`, `pl`; `en` default). Identity validates against it, the client builds its picker from it, the API rejects anything else. |
| D3 | Strings use the .NET built-ins: `Microsoft.Extensions.Localization`, `IStringLocalizer<TMarker>`, `.resx` per feature. No third-party localization library. |
| D4 | Language sets `CultureInfo.CurrentCulture` and `CurrentUICulture` in the WASM host. Dates, numbers, first day of week follow the culture. `InvariantCulture` stays only where a format is a wire or storage format, never to force English display. |
| D5 | Domain rejection messages (`DomainRejectedException`) stay English. Modules reference `Abstractions` only and carry no resources. |
| D6 | Keycloak is untouched. No realm flags, no `kc_locale`, nothing stored there. |
| D7 | Public snapshot pages (`PublicLayout`) render in the viewer's browser language; a snapshot carries no owner language. |
| D8 | Every resource entry is a whole sentence. Placeholders hold values (counts, names, dates), never words. Plurals are separate whole sentences per form. |
| D9 | The docs site stays English. |

Reasoning and rejected alternatives: `adr/0075`.

## 3. Data model and contracts

### 3.1 Identity

- `User.Language` (`string`, default `"en"`). A Mongo document without the
  field deserialises to `"en"`; no migration.
- `ProvisionUser` and `UserProvisioned` gain `Language`.
- New command `SetUserLanguage(CommandId, UserId, Language)` and event
  `UserLanguageSet`, folder `Identity/SetLanguage/`, mirroring
  `SetTimeZone/`. `Decide` validates via `SupportedLanguages.Require`, returns
  no event when unchanged.
- `ProvisioningPlan.SeedAreaNames(language)` returns the seed area names in
  the provisioning language (`Personal/Work/Home`, `Prywatne/Praca/Dom`).
  This is the only Polish text inside a module and it is seed data, not UI.

### 3.2 Abstractions

```csharp
public static class SupportedLanguages
{
    public const string Default = "en";
    public static IReadOnlyList<string> All { get; } = ["en", "pl"];
    public static bool IsSupported(string tag);
    public static string Require(string tag);      // throws DomainRejectedException
    public static string FromBrowser(string? tag); // "pl-PL" → "pl", "de" → "en", null → "en"
}
```

`FromBrowser` matches on the primary subtag, case-insensitive.

### 3.3 Contracts and API

- `MeResponse` gains `Language`.
- `SetLanguageRequest(string Language)`; `PUT api/me/language` next to
  `PUT api/me/timezone`, same shape, 400 on unsupported tag,
  `UserProvisioner.SetLanguageAsync`.
- First `GET api/me` provisions with
  `SupportedLanguages.FromBrowser(Accept-Language first tag)`; `CurrentUser`
  gains `LanguageHint` read from the request header, not the token.

## 4. Client

### 4.1 `LanguagePreference` (`App/Localization/`)

Modeled on `ThemePreference`. Holds `Language`, persists to `localStorage`
key `pspad.language`, plus `pspad.language.pending` when a server write
failed. Raises `Changed`.

Boot (`Program.cs`, before `host.RunAsync()`):

1. Read `pspad.language`; if absent, `SupportedLanguages.FromBrowser(navigator.language)`.
2. Set `CultureInfo.DefaultThreadCurrentCulture`, `DefaultThreadCurrentUICulture`,
   `CurrentCulture`, `CurrentUICulture` to `CultureInfo.GetCultureInfo(tag)`.

First paint is already localized, offline or not, with no replica involved.
English uses the `en-GB` culture (week starts Monday, 24-hour time); it
abbreviates September as "Sept".

When `/me` arrives in `AppState`: if `me.Language` differs from the cached
value and no pending write exists, cache it and reload
(`NavigationManager.NavigateTo(uri, forceLoad: true)`). WASM cannot switch
culture live; reload is the standard pattern and happens at most once per
device per change.

Settings change: write cache, set pending, `PUT api/me/language`. On
success clear pending; on failure keep pending. `SyncCoordinator` retries
the pending write after each successful sync. Then reload. The device works
in the chosen language meanwhile, offline included.

### 4.2 Globalization data

`PSPad.App.csproj`: `<BlazorWebAssemblyLoadAllGlobalizationData>false</…>`,
default ICU sharding. `pl` resolves to `icudt_no_CJK.dat` (≈1.4 MB). The
service worker caches it with the other framework assets.

### 4.3 Resources

- `builder.Services.AddLocalization()`.
- `App/Resources/<Feature>Strings.resx` + `<Feature>Strings.pl.resx`, with an
  empty marker class `<Feature>Strings` per file. Features: `Shared`, `Shell`,
  `Auth`, `Settings`, `Today`, `Tasks`, `Lists`, `Inbox`, `Goals`,
  `Reference`, `Budgets`, `Sharing`, `Statistics`, `Mud`.
- Usage: `@inject IStringLocalizer<TodayStrings> T`, then `@T["NothingDue"]`.
  Keys are stable identifiers in `PascalCase`; the English `.resx` holds the
  text. A missing key renders the key, which the resource guard test (§6)
  prevents.
- Formatted strings use indexed placeholders: `T["DueInDays", count]`.
- Plurals: `Plural` helper in `App/Localization/`:
  `T.Plural(count, "TasksLeft")` looks up `TasksLeft.One`, `TasksLeft.Few`,
  `TasksLeft.Many` (and `.Zero` when present) by `PluralRules.For(culture)`.
  English: One / Many. Polish: One (1), Few (2–4 except 12–14),
  Many (0, 5–21, 12–14, …). Each form is a complete sentence with `{0}`.
- MudBlazor: `MudLocalizer` implementation backed by
  `IStringLocalizer<MudStrings>` registered after `AddMudServices`, covering
  the built-in strings the app actually renders (date picker, dialog buttons).
- `Presentation` module stays text-free. Any display text found there moves
  to App resources.

### 4.4 Formats

`CultureInfo.InvariantCulture` is removed from display formatting
(`DueDateRow`, `WeekStrip`, `SharePeople`, `RecurrenceEditor`, `TaskRow`,
`SnapshotPublisher`) and kept for CSV import/export and any other wire or
storage format. Week start comes from
`CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek`; hardcoded
`DayOfWeek.Monday` is replaced where it meant "start of week".

### 4.5 Settings page

"Language" `MudSelect` under "Application settings", options from
`SupportedLanguages.All` shown as native names (`English`, `Polski`).
Selecting one follows §4.1. No other settings surface (`ui-spec` §/settings).

## 5. Keycloak and server text

Nothing changes in `docker/keycloak/realm-psplace.json`. The login page
stays in Keycloak's default language. Server-side text (rejection messages,
logs) stays English.

## 6. Testing

`[UnitTest]`:

- `User.Decide`: accepts `en`/`pl`, rejects `de`, no event when unchanged,
  provisioning carries language, document without `Language` loads as `en`.
- `SupportedLanguages.FromBrowser`: `pl-PL`, `PL`, `en-GB`, `de`, `null`.
- `LanguagePreference`: cache precedence over browser, server value wins
  over cache when nothing is pending, pending set on failed write and cleared
  on retry.
- `PluralRules`: Polish 1 / 2 / 4 / 5 / 12 / 22 / 25 / 0; English 1 / 0 / 2.
- Resource guard: for every `<Feature>.resx`, the `.pl.resx` has the same
  key set and no empty value, and every `.Few` key has `.One` and `.Many`.
- `ProvisioningPlan.SeedAreaNames("pl")` returns Polish names.

`[IntegrationTest]`:

- `PUT api/me/language` persists and `GET api/me` echoes it; unsupported tag
  returns 400.
- First `GET api/me` with `Accept-Language: pl-PL,en;q=0.8` provisions `pl`
  and Polish seed areas; without the header provisions `en`.

Manual: every page walked in Polish in the browser pane before merge.

## 7. Adding a language (contributor path)

1. Add the tag to `SupportedLanguages.All`.
2. Copy each `App/Resources/<Feature>Strings.resx` to
   `<Feature>Strings.<tag>.resx` and translate every value as a whole sentence.
3. Add plural forms to `PluralRules` if the language is not One/Many.
4. Add seed area names to `ProvisioningPlan`.
5. Add the native name in `SettingsPage`.
6. Run the unit tests; `ResourceGuardTests` reports any missing key.

Documented in `docs/src/pages/features.astro` ("Speaks your language" card
and "Adding a language" section).

## 8. Out of scope

Keycloak login theme, docs site translation, localized server errors,
per-user region separate from language, right-to-left layouts.
