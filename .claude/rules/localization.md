# Localization

Both the backend API and the frontend UI support Vietnamese (`vi`, default) and English (`en`) — as **two separate, unsynced localization systems**. There is no shared message catalog between them.

## Backend Message Files

| Type               | Location                                                              |
|--------------------|-----------------------------------------------------------------------|
| Validation errors  | `backend/src/FeedbackHub.Application/Resources/Messages.{vi,en}.resx`           |
| Domain exceptions  | `backend/src/FeedbackHub.Domain/DomainMessages.cs`                               |
| Application errors | `backend/src/FeedbackHub.Application/ApplicationMessages.cs`                     |

Validation messages are surfaced via DataAnnotations localization. Domain and application messages are string constants — add both `vi` and `en` entries whenever adding new messages.

## Frontend Message Files

vue-i18n, locale files at `frontend/src/locales/{vi,en}.ts` (plus `naive-ui.ts` for the component library's own locale). Add both `vi` and `en` keys whenever adding new user-facing UI text.

## Cross-Cutting Rule

Backend messages (Resx/exception strings) reach the user only when the frontend chooses to render them verbatim (e.g. an error `detail` shown as-is) — that path bypasses vue-i18n entirely. When a backend message is meant to be **displayed** in the UI rather than just logged, prefer having the frontend map the error to its own `locales/{vi,en}.ts` key (e.g. by error code) instead of rendering the backend string directly — otherwise the UI ends up mixing two independently-maintained translation systems. If you add a backend message and it needs a UI-facing counterpart, add it to both `Messages.{vi,en}.resx` and `frontend/src/locales/{vi,en}.ts`.
