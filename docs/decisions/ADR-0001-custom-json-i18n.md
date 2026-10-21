# ADR-0001: Custom JSON-based i18n instead of .resx resources

- Status: Accepted
- Date: 2026-10-20

## Context

v1.0.0 required a multilingual UI (EN, ES, DE, FR, zh-Hans) over an existing codebase with
hardcoded Spanish strings in both XAML and C#. .resx satellite assemblies are the WPF
standard but are verbose to edit, hard to diff, painful for bulk translation by agents, and
require `x:Uid` plumbing or generated designer classes.

## Decision

A small custom layer: flat JSON files per language under `Assets/i18n/<lang>/`
(`mainui`, `maincode`, `views`, `core` — 288 keys), merged at startup with English as base
and fallback. Static `Loc.T(key, args)` for C#; `{local:Loc key}` MarkupExtension for XAML.
Language resolved from a `language` setting ("auto" → OS culture); changing it requires an
app restart (strings are read once, no dynamic resource swap).

## Consequences

- Translations are plain JSON: trivial to diff, validate 1:1, and generate with agents.
- No satellite assemblies; files are copied to output and into both distributions.
- Restart-on-change is a accepted UX cost (a localized notice explains it).
- Every new string must be added to all five languages; English must always have the key
  (it is the fallback). Tests initialize `Loc` explicitly (`TestFactory`).
