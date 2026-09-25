# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `json` flag type support: `Flag.AsJson()` returns the decoded value as a `System.Text.Json.JsonElement`, covering both JSON objects and JSON arrays. Inline defaults (`IDictionary`, `IEnumerable`, or `JsonElement` holding an object/array) and `DefaultsCollection.Add(key, JsonElement)` now produce a json-typed flag instead of a stringified fallback.

### Fixed
- A flag whose `type` this SDK version doesn't recognize (e.g. a type introduced on the wire after this SDK shipped) no longer throws and drops the *entire* rules payload — it now parses as `FlagType.Unknown` and is treated like a missing flag, falling back to the caller's default while the rest of the flags evaluate normally.

## [1.0.0] - 2026-09-20

First public release.

### Added
- Flag client with builder pattern, async/await API, ASP.NET Core middleware and dependency injection.
- `CacheException`, raised when a cache backend fails to read or write cached data.
- `IZenmanageClient`/`ApiClient` now implement `IDisposable`, disposing the underlying `HttpClient` only when the SDK created it itself.
- `DefaultsCollection.All()` for enumerating configured defaults.

### Fixed
- `FlagManager.AllAsync()`/`SingleAsync()` now fall back to configured defaults (inline or `DefaultsCollection`) when rule loading fails outright — previously the exception propagated straight through, unlike the PHP SDK's documented fallback contract.
- `AllAsync()` now merges in any `DefaultsCollection` keys missing from the loaded flag set, matching PHP's `all()`.
- Rule conditions with multiple clause values (e.g. `equal` against a list) now check every value instead of only the first — previously only `clauseValues[0]` was ever compared.
- `not_in` now matches when *any* attribute value is outside the configured list, matching the PHP SDK's aggregation semantics — previously it required *all* attribute values to be outside the list.
- Added the `regex` operator to the rule engine; it was previously unimplemented and always evaluated to `false`.
- The CDN rules URL returned by the API is now required to use HTTPS, matching the PHP SDK's fail-closed behavior.
- `ReportUsageAsync` now checks the HTTP response status and retries transient failures with backoff instead of silently treating any response (including 4xx/5xx) as success.
- `GetRulesAsync` no longer retries on a malformed/invalid rules response — it fails fast, matching PHP.
- Fixed `dotnet pack` failing for the `Zenmanage.AspNetCore` package (missing packaged README), and updated CI/release workflows to pack and publish both packages instead of only `Zenmanage`.

### Changed
- `SdkInfo.Version` (used in the SDK's client-agent header) is now derived from the assembly's informational version at runtime instead of a hand-maintained constant, so it can't drift from the version actually shipped on each release.
