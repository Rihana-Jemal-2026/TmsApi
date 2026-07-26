# API Versioning Policy

## Breaking Changes

A breaking change is any modification that can cause existing API clients to stop working or change their expected behavior.

Examples of breaking changes:
- Removing an existing response field
- Renaming an existing field
- Changing an HTTP status code clients depend on
- Tightening validation rules so previously valid requests fail
- Changing the default sorting order of results

Breaking changes require a new API version.

## Additive (Non-Breaking) Changes

An additive change adds functionality without affecting existing clients.

Examples:
- Adding a new optional response field
- Adding a new endpoint
- Adding a new optional query parameter

These changes can be released without creating a new API version.

## Sunset Window

When V2 is released, V1 will continue running for a minimum of 6 months.

This gives TMS partners, including rural training centres that follow quarterly maintenance schedules, enough time to test and migrate their systems.

## Communication

From the first day of V2 release, deprecated versions will communicate migration information using:

- Deprecation header
- Sunset header
- Link header pointing to the successor version

Every version change will also include:
- A CHANGELOG entry
- An email notification to every team that owns an API key
- A calendar invitation for the V1 shutdown date

## Skipping Versions

Clients are not required to migrate through every intermediate version.

For example, migration directly from V1 to V3 is allowed when needed.