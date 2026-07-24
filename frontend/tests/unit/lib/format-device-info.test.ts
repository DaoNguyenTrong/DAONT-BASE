import { describe, expect, it } from 'vitest'
import { formatDeviceInfo } from '@/lib/format-device-info'

const CHROME_LINUX =
  'Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36'
const CHROME_WINDOWS =
  'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
const CHROME_MACOS =
  'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
const CHROME_ANDROID =
  'Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Mobile Safari/537.36'
const EDGE_WINDOWS =
  'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0'
const FIREFOX_LINUX =
  'Mozilla/5.0 (X11; Linux x86_64; rv:120.0) Gecko/20100101 Firefox/120.0'
const SAFARI_MACOS =
  'Mozilla/5.0 (Macintosh; Intel Mac OS X 14_2) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Safari/605.1.15'
const SAFARI_IPHONE =
  'Mozilla/5.0 (iPhone; CPU iPhone OS 17_2 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Mobile/15E148 Safari/604.1'
const SAFARI_IPAD =
  'Mozilla/5.0 (iPad; CPU OS 17_2 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Mobile/15E148 Safari/604.1'

describe('formatDeviceInfo', () => {
  it('returns null for null input', () => {
    expect(formatDeviceInfo(null)).toBeNull()
  })

  it('returns null for empty string', () => {
    expect(formatDeviceInfo('')).toBeNull()
  })

  it('returns null for whitespace-only string', () => {
    expect(formatDeviceInfo('   ')).toBeNull()
  })

  it('detects Chrome on Linux', () => {
    expect(formatDeviceInfo(CHROME_LINUX)).toBe('Chrome on Linux')
  })

  it('detects Chrome on Windows', () => {
    expect(formatDeviceInfo(CHROME_WINDOWS)).toBe('Chrome on Windows')
  })

  it('detects Chrome on macOS', () => {
    expect(formatDeviceInfo(CHROME_MACOS)).toBe('Chrome on macOS')
  })

  it('detects Chrome on Android', () => {
    expect(formatDeviceInfo(CHROME_ANDROID)).toBe('Chrome on Android')
  })

  it('detects Edge on Windows (not misidentified as Chrome)', () => {
    expect(formatDeviceInfo(EDGE_WINDOWS)).toBe('Edge on Windows')
  })

  it('detects Firefox on Linux', () => {
    expect(formatDeviceInfo(FIREFOX_LINUX)).toBe('Firefox on Linux')
  })

  it('detects Safari on macOS (not Chrome despite Safari/ token)', () => {
    expect(formatDeviceInfo(SAFARI_MACOS)).toBe('Safari on macOS')
  })

  it('detects Safari on iPhone', () => {
    expect(formatDeviceInfo(SAFARI_IPHONE)).toBe('Safari on iPhone')
  })

  it('detects Safari on iPad', () => {
    expect(formatDeviceInfo(SAFARI_IPAD)).toBe('Safari on iPad')
  })

  it('returns null for unrecognised UA instead of exposing raw string', () => {
    expect(formatDeviceInfo('UnknownAgent/1.0')).toBeNull()
  })
})
