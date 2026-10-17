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
const FIREFOX_LINUX = 'Mozilla/5.0 (X11; Linux x86_64; rv:120.0) Gecko/20100101 Firefox/120.0'
const SAFARI_MACOS =
  'Mozilla/5.0 (Macintosh; Intel Mac OS X 14_2) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Safari/605.1.15'
const SAFARI_IPHONE =
  'Mozilla/5.0 (iPhone; CPU iPhone OS 17_2 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Mobile/15E148 Safari/604.1'
const SAFARI_IPAD =
  'Mozilla/5.0 (iPad; CPU OS 17_2 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Mobile/15E148 Safari/604.1'

describe('formatDeviceInfo', () => {
  it.each([
    ['returns null for null input', null],
    ['returns null for empty string', ''],
    ['returns null for whitespace-only string', '   '],
    ['returns null for unrecognised UA instead of exposing raw string', 'UnknownAgent/1.0'],
  ])('%s', (_title, input) => {
    expect(formatDeviceInfo(input)).toBeNull()
  })

  it.each([
    ['detects Chrome on Linux', CHROME_LINUX, 'Chrome on Linux'],
    ['detects Chrome on Windows', CHROME_WINDOWS, 'Chrome on Windows'],
    ['detects Chrome on macOS', CHROME_MACOS, 'Chrome on macOS'],
    ['detects Chrome on Android', CHROME_ANDROID, 'Chrome on Android'],
    ['detects Edge on Windows (not misidentified as Chrome)', EDGE_WINDOWS, 'Edge on Windows'],
    ['detects Firefox on Linux', FIREFOX_LINUX, 'Firefox on Linux'],
    ['detects Safari on macOS (not Chrome despite Safari/ token)', SAFARI_MACOS, 'Safari on macOS'],
    ['detects Safari on iPhone', SAFARI_IPHONE, 'Safari on iPhone'],
    ['detects Safari on iPad', SAFARI_IPAD, 'Safari on iPad'],
  ])('%s', (_title, ua, label) => {
    expect(formatDeviceInfo(ua)).toBe(label)
  })
})
