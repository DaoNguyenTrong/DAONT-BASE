---
paths:
  - "backend/**"
  - "frontend/**"
---

# Localization

Both the backend API and the frontend UI support Vietnamese (`vi`, default) and English (`en`) — as **two separate, unsynced localization systems**. There is no shared message catalog between them.

## Backend Message Files

| What                          | Location                                                                 |
|-------------------------------|--------------------------------------------------------------------------|
| Message text — `vi` (default) | `backend/src/StarterKit.Application/Resources/Messages.resx`             |
| Message text — `en`           | `backend/src/StarterKit.Application/Resources/Messages.en.resx`          |
| Domain exception keys         | `backend/src/StarterKit.Domain/Exceptions/DomainMessages.cs`             |
| Application error keys        | `backend/src/StarterKit.Application/Resources/ApplicationMessages.cs`    |

`DomainMessages`/`ApplicationMessages` hold only **keys** (`const string X = nameof(X)`); the text lives in the two `.resx` files (the neutral `Messages.resx` is Vietnamese — `vi` is the default request culture in `Program.cs`). Validation messages are surfaced via DataAnnotations localization from the same files. Whenever you add a key, add its entry to **both** `Messages.resx` and `Messages.en.resx`.

## Frontend Message Files

vue-i18n, locale files at `frontend/src/locales/{vi,en}.ts` (plus `naive-ui.ts` for the component library's own locale). Add both `vi` and `en` keys whenever adding new user-facing UI text. Components read text via `useI18n()`; locale switching goes through `useLocaleStore` (`frontend/src/stores/locale-store.ts`), never `i18n.global.locale` directly.

## Cross-Cutting Rule

Backend messages (Resx/exception strings) reach the user only when the frontend chooses to render them verbatim (e.g. an error `detail` shown as-is) — that path bypasses vue-i18n entirely. When a backend message is meant to be **displayed** in the UI rather than just logged, prefer having the frontend map the error to its own `locales/{vi,en}.ts` key (e.g. by error code) instead of rendering the backend string directly — otherwise the UI ends up mixing two independently-maintained translation systems. If you add a backend message and it needs a UI-facing counterpart, add it to both `Messages.resx`/`Messages.en.resx` and `frontend/src/locales/{vi,en}.ts`.
