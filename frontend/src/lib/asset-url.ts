import { getRuntimeConfig } from '@/lib/runtime-config'

/** Public file URLs are absolute (S3 providers) or rooted at the API origin (`/storage/...`, Local provider). */
export function resolveAssetUrl(url: string | null | undefined): string | undefined {
  if (!url) {
    return undefined
  }
  if (!url.startsWith('/')) {
    return url
  }
  return `${getRuntimeConfig().apiBaseUrl.replace(/\/+$/, '')}${url}`
}
