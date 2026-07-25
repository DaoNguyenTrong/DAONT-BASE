import type { GlobalThemeOverrides } from 'naive-ui'

/**
 * Neutral starter theme for Naive UI's GlobalThemeOverrides. Naive UI switches
 * light/dark via a reactive `:theme` prop (`null` for light, `darkTheme` for
 * dark) rather than a CSS selector, so light and dark overrides are kept as
 * two separate objects here — must stay in sync with the `--color-primary-*`
 * / `--color-surface-*` bridge in `src/assets/styles/tailwind.css`.
 *
 * Brand primary is a single indigo/violet accent palette (`--color-accent-*`
 * in tailwind.css, derived from the weatherplus logo's #4e3d90 wordmark) —
 * swap the hex values here (and in tailwind.css) to rebrand. Light mode uses
 * accent-600/700/800 (darkens on hover/pressed) — 600 was promoted from the
 * original #3d3071 to `#5e4c9e` (same H253°/S35° hue family, just a lighter
 * point on it — originally a one-off sidebar background hex, since reused
 * as the actual primary so `AppSidebar.vue` could reference `bg-primary-600`
 * instead of an arbitrary hex) — 700/800 are darkened continuations of it,
 * not the old #3d3071-anchored steps, to avoid a hue seam on hover/pressed;
 * dark mode uses a custom
 * lighter+more-saturated purple trio (not the plain accent-200/100/50 steps)
 * — those steps needed meaningfully more lightness than teal did to clear
 * the same contrast ratio against the dark surface in the first place (WCAG
 * luminance weights green far more than blue, so a blue-violet needs to sit
 * lighter to read as equally bright), and were then tuned further toward a
 * vivid, more saturated violet (`#9c80dc`, H258°/S57%/L68%) rather than a
 * washed-out lilac, while still clearing WCAG AA against the dark body/card
 * (5.6:1 / 4.9:1). Only the `200` step (`--color-primary-200` in
 * tailwind.css) carries this custom value — it's the one step used
 * exclusively by `dark:` variants app-wide — and the two files must be
 * edited together or components (Naive UI) and utility classes
 * (`dark:text-primary-200`) will render two different purples.
 *
 * Surface mapping (light): base & card = #ffffff (surface-0), body = #efedf6,
 * popover/modal = #f9f8fb. Surface mapping (dark): base/body = #121527
 * (surface-950), card/popover/modal = #1e2235 (surface-900) — a navy-black
 * dark theme per explicit user-supplied hex values, not derived from the
 * brand hue like the rest of the surface ramp.
 *
 * `successColor` is retuned to the leaf-green accent (`--color-leaf-*` in
 * tailwind.css, from the logo's leaf glyph) instead of Naive UI's default
 * green, so success feedback reads as part of the same brand rather than a
 * generic library color. Unlike primary, this hue needed *no* dark-mode step
 * shift (leaf-400/300/200, same offsets as the old teal primary used) — green
 * already carries most of WCAG's luminance weight, so it doesn't suffer the
 * same "needs to sit lighter" problem the blue-violet primary did.
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
    primaryColor: '#5e4c9e',
    primaryColorHover: '#4e3f84',
    primaryColorPressed: '#41356d',
    primaryColorSuppl: '#5e4c9e',
    successColor: '#5b772a',
    successColorHover: '#4b6223',
    successColorPressed: '#3f521d',
    successColorSuppl: '#5b772a',
    baseColor: '#ffffff',
    bodyColor: '#efedf6',
    cardColor: '#ffffff',
    popoverColor: '#f9f8fb',
    modalColor: '#f9f8fb',
    inputColor: 'var(--fill-color)',
    inputColorDisabled: '#f3f2f8',
    tagColor: 'var(--fill-color)',
    buttonColor2: 'var(--fill-color)',
    buttonColor2Hover: '#d2cfdd',
    buttonColor2Pressed: '#9f9ab2',
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
    primaryColor: '#9c80dc',
    primaryColorHover: '#c1a4ff',
    primaryColorPressed: '#f6f2fb',
    primaryColorSuppl: '#9c80dc',
    successColor: '#91bd44',
    successColorHover: '#b0d078',
    successColorPressed: '#cfe3ac',
    successColorSuppl: '#91bd44',
    baseColor: '#121527',
    bodyColor: '#121527',
    cardColor: '#1e2235',
    popoverColor: '#1e2235',
    modalColor: '#1e2235',
    inputColor: 'var(--fill-color)',
    inputColorDisabled: '#121527',
    tagColor: 'var(--fill-color)',
    buttonColor2: 'var(--fill-color)',
    buttonColor2Hover: '#454250',
    buttonColor2Pressed: '#605c6e',
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
