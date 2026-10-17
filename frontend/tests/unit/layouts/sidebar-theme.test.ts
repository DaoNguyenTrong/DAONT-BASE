import { describe, expect, it } from 'vitest'
import { sidebarThemeClasses } from '@/layouts/sidebar-theme'

describe('sidebarThemeClasses', () => {
  it('paints the brand menu purple in light mode with a translucent white active item', () => {
    const classes = sidebarThemeClasses('brand', false)

    expect(classes.surface).toContain('bg-primary-600')
    expect(classes.text).toContain('text-white')
    expect(classes.itemActive).toContain('bg-white/15')
  })

  it('keeps the brand menu on the dark surface in dark mode', () => {
    expect(sidebarThemeClasses('brand', true).surface).toContain('bg-surface-900')
  })

  it('blends the transparent menu into the light page with a white active pill', () => {
    const classes = sidebarThemeClasses('transparent', false)

    expect(classes.surface).toContain('bg-surface-100')
    expect(classes.itemIdle).toContain('text-surface-600')
    expect(classes.groupTitle).toContain('text-surface-400')
    expect(classes.itemActive).toContain('bg-white')
    expect(classes.itemActive).toContain('text-primary-600')
  })

  it('blends the transparent menu into the dark page', () => {
    const classes = sidebarThemeClasses('transparent', true)

    expect(classes.surface).toContain('bg-surface-950')
    expect(classes.itemActive).toContain('bg-surface-800')
    expect(classes.itemActive).toContain('text-primary-200')
  })

  it.each([
    [false, 'text-primary-600'],
    [true, 'text-primary-200'],
  ])(
    'sets a parent holding the current page apart from an idle item on the transparent menu (dark: %s)',
    (isDark, color) => {
      const classes = sidebarThemeClasses('transparent', isDark)
      const textColor = (value: string) => value.split(' ').find((c) => c.startsWith('text-'))

      expect(classes.text).toContain(color)
      expect(textColor(classes.text)).not.toBe(textColor(classes.itemIdle))
    },
  )
})
