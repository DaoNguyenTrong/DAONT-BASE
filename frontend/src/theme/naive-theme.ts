import type { GlobalThemeOverrides } from 'naive-ui'

/**
 * Neutral starter theme for Naive UI's GlobalThemeOverrides. Naive UI switches
 * light/dark via a reactive `:theme` prop (`null` for light, `darkTheme` for
 * dark) rather than a CSS selector, so light and dark overrides are kept as
 * two separate objects here — must stay in sync with the `--color-primary-*`
 * / `--color-surface-*` bridge in `src/assets/styles/tailwind.css`.
 *
 * Brand primary is a single teal accent palette (`--color-accent-*` in
 * tailwind.css), reused as-is in both light and dark mode — swap the hex
 * values here (and in tailwind.css) to rebrand.
 *
 * Surface mapping (light): base & card = #ffffff (surface-0), body = #eef1f5,
 * popover/modal = #f8f9fb. Surface mapping (dark): base/body = #121527
 * (surface-950), card/popover/modal = #1e2235 (surface-900).
 *
 * Form/filled controls (Input, Select, Button `secondary`, Tag) all share one
 * fill tone via the `--fill-color` CSS variable (defined in
 * `tailwind.css`, aliasing surface-200 light / surface-800 dark — a step off
 * the surface ramp between `bodyColor` and `cardColor`). `inputColor`,
 * `tagColor` and `buttonColor2` all point at that single variable so they
 * can't drift out of sync with each other again; many of these controls also
 * render unbordered directly on the page, so matching either bodyColor or
 * cardColor exactly would make them invisible — the in-between tone stays
 * visible against both. `borderRadius` / `borderRadiusSmall` are bumped from
 * Naive UI's square 3px/2px defaults to match the rounded-lg/xl look already
 * used elsewhere in the app.
 *
 * `Dialog.padding` is bumped from Naive UI's default `1rem 1.75rem 1.25rem
 * 1.75rem` for more breathing room around dialog content — applies globally
 * to every `useDialog()`-created dialog, not just one call site.
 *
 * `Dialog.borderRadius` (1.25rem) is set well above the base `borderRadius`
 * (0.625rem) it would otherwise inherit — a plain surface this large looks
 * flat/boxy at the same radius as its own small inner controls. 1.25rem
 * matches the app's existing `rounded-2xl` (large-surface) convention and
 * comfortably nests around the inner corner radius plus padding instead of
 * looking disproportionately tight against it.
 *
 * `Alert` is shrunk from Naive UI's default (13px uniform padding, 1.6
 * line-height, 14px font) — every `<n-alert>` in this app is a one-line
 * inline validation/error message under a form field (`:show-icon="false"`
 * throughout), not a multi-line banner, so the default sizing reads as
 * oversized next to the field it's attached to. `NAlert` has no `size` prop
 * (unlike `NButton`/`NInput`), so this is the only way to shrink it —
 * per-instance Tailwind classes can't reach these CSS-variable-driven
 * dimensions.
 *
 * All sizing here is in `rem`, not `px`, so it scales with the root font size
 * (see `useFontSize`'s `<html>` `font-size` toggle) instead of staying fixed.
 */

const lightThemeOverrides: GlobalThemeOverrides = {
  common: {
    primaryColor: '#0d9488',
    primaryColorHover: '#0f766e',
    primaryColorPressed: '#115e59',
    primaryColorSuppl: '#0d9488',
    baseColor: '#ffffff',
    bodyColor: '#eef1f5',
    cardColor: '#ffffff',
    popoverColor: '#f8f9fb',
    modalColor: '#f8f9fb',
    inputColor: 'var(--fill-color)',
    inputColorDisabled: '#f1f5f9',
    tagColor: 'var(--fill-color)',
    buttonColor2: 'var(--fill-color)',
    buttonColor2Hover: '#cbd5e1',
    buttonColor2Pressed: '#94a3b8',
    borderRadius: '0.625rem',
    borderRadiusSmall: '0.375rem',
  },
  Dialog: {
    padding: '1.5rem 2rem 1.75rem 2rem',
    borderRadius: '1.25rem',
  },
  Alert: {
    padding: '0.5rem 0.75rem',
    fontSize: '0.8125rem',
    lineHeight: '1.4',
  },
}

const darkThemeOverrides: GlobalThemeOverrides = {
  common: {
    primaryColor: '#2dd4bf',
    primaryColorHover: '#5eead4',
    primaryColorPressed: '#99f6e4',
    primaryColorSuppl: '#2dd4bf',
    baseColor: '#121527',
    bodyColor: '#121527',
    cardColor: '#1e2235',
    popoverColor: '#1e2235',
    modalColor: '#1e2235',
    inputColor: 'var(--fill-color)',
    inputColorDisabled: '#121527',
    tagColor: 'var(--fill-color)',
    buttonColor2: 'var(--fill-color)',
    buttonColor2Hover: '#3b4357',
    buttonColor2Pressed: '#555f75',
    borderRadius: '0.625rem',
    borderRadiusSmall: '0.375rem',
  },
  Dialog: {
    padding: '1.5rem 2rem 1.75rem 2rem',
    borderRadius: '1.25rem',
  },
  Alert: {
    padding: '0.5rem 0.75rem',
    fontSize: '0.8125rem',
    lineHeight: '1.4',
  },
}

export default { light: lightThemeOverrides, dark: darkThemeOverrides }
