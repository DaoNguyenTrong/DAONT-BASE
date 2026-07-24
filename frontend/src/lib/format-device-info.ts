import { UAParser } from 'ua-parser-js'

export function formatDeviceInfo(userAgent: string | null | undefined): string | null {
  if (!userAgent?.trim()) return null

  const parser = new UAParser(userAgent)
  const browser = normalizeBrowser(parser.getBrowser().name ?? null)
  const os = normalizeOs(parser.getOS().name ?? null, parser.getDevice().model ?? null)

  if (browser && os) return `${browser} on ${os}`
  return browser ?? os ?? null
}

/** Strip the "Mobile " prefix that ua-parser-js adds for mobile browsers. */
function normalizeBrowser(name: string | null): string | null {
  if (!name) return null
  return name.startsWith('Mobile ') ? name.slice(7) : name
}

/** For iOS, prefer the device model ("iPhone", "iPad") over the generic "iOS" OS name. */
function normalizeOs(osName: string | null, deviceModel: string | null): string | null {
  if (!osName) return null
  if (osName === 'Mac OS') return 'macOS'
  if (osName === 'iOS' && deviceModel) return deviceModel
  return osName
}
