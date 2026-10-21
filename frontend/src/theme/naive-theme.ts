import type { GlobalThemeOverrides } from 'naive-ui'

// Naive UI switches light/dark through the `:theme` prop, not a CSS selector, so each mode is its
// own object. Keep the hex values in sync with tailwind.css (accent-600/700/800, the dark-only
// --color-primary-200, the surface steps), or Naive components and utility classes diverge.

// Sizes are in rem so they follow useFontSize's root font-size toggle.
const lightThemeOverrides: GlobalThemeOverrides = {
  common: {
    primaryColor: '#6428f4',
    primaryColorHover: '#5017d3',
    primaryColorPressed: '#4418aa',
    primaryColorSuppl: '#6428f4',
    successColor: '#5b772a',
    successColorHover: '#4b6223',
    successColorPressed: '#3f521d',
    successColorSuppl: '#5b772a',
    baseColor: '#ffffff',
    bodyColor: '#daeefd',
    cardColor: '#ffffff',
    popoverColor: '#eef7fe',
    modalColor: '#eef7fe',
    // One fill tone for unbordered controls, between body and card so it shows on both.
    inputColor: 'var(--fill-color)',
    inputColorDisabled: '#eef7fe',
    tagColor: 'var(--fill-color)',
    buttonColor2: 'var(--fill-color)',
    buttonColor2Hover: '#b3cde3',
    buttonColor2Pressed: '#94a3b8',
    borderRadius: '0.625rem',
    borderRadiusSmall: '0.375rem',
  },
  Dialog: {
    padding: '1.5rem 2rem 1.75rem 2rem',
    borderRadius: '1.25rem',
  },
  // Every n-alert here is a one-line message under a form field; NAlert has no `size` prop.
  Alert: {
    padding: '0.5rem 0.75rem',
    fontSize: '0.8125rem',
    lineHeight: '1.4',
  },
}

const darkThemeOverrides: GlobalThemeOverrides = {
  common: {
    // Lighter than the accent ramp's steps to clear WCAG AA on the navy body/card.
    primaryColor: '#a27aff',
    primaryColorHover: '#bfa3ff',
    primaryColorPressed: '#e2d6ff',
    primaryColorSuppl: '#a27aff',
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
