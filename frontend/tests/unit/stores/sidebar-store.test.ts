import { describe, expect, it, beforeEach } from 'vitest'
import { useSidebarStore } from '@/stores/sidebar-store'
import { setupTestPinia } from '../../helpers/pinia'

describe('useSidebarStore', () => {
  beforeEach(() => {
    setupTestPinia()
  })

  it('defaults to full mode when localStorage is empty', () => {
    const sidebar = useSidebarStore()

    expect(sidebar.mode).toBe('full')
    expect(sidebar.isMinimal).toBe(false)
    expect(sidebar.sidebarWidth).toBe('16rem')
  })

  it('restores mode from localStorage set before store creation', () => {
    localStorage.setItem('sidebar-mode', 'minimal')

    const sidebar = useSidebarStore()

    expect(sidebar.mode).toBe('minimal')
    expect(sidebar.isMinimal).toBe(true)
    expect(sidebar.sidebarWidth).toBe('5rem')
  })

  it('toggleMode flips mode and persists to localStorage', () => {
    const sidebar = useSidebarStore()

    sidebar.toggleMode()
    expect(sidebar.mode).toBe('minimal')
    expect(localStorage.getItem('sidebar-mode')).toBe('minimal')

    sidebar.toggleMode()
    expect(sidebar.mode).toBe('full')
    expect(localStorage.getItem('sidebar-mode')).toBe('full')
  })

  it('setMode sets mode explicitly and persists to localStorage', () => {
    const sidebar = useSidebarStore()

    sidebar.setMode('minimal')
    expect(sidebar.mode).toBe('minimal')
    expect(localStorage.getItem('sidebar-mode')).toBe('minimal')

    sidebar.setMode('full')
    expect(sidebar.mode).toBe('full')
    expect(localStorage.getItem('sidebar-mode')).toBe('full')
  })

  it('mobile menu starts closed and can be opened, closed, and toggled', () => {
    const sidebar = useSidebarStore()

    expect(sidebar.mobileOpen).toBe(false)

    sidebar.openMobile()
    expect(sidebar.mobileOpen).toBe(true)

    sidebar.closeMobile()
    expect(sidebar.mobileOpen).toBe(false)

    sidebar.toggleMobile()
    expect(sidebar.mobileOpen).toBe(true)

    sidebar.toggleMobile()
    expect(sidebar.mobileOpen).toBe(false)
  })
})
