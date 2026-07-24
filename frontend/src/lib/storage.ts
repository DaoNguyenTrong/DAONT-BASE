function isAvailable() {
  return typeof window !== 'undefined'
}

export function getItem(key: string): string | null {
  return isAvailable() ? window.localStorage.getItem(key) : null
}

export function setItem(key: string, value: string): void {
  if (isAvailable()) window.localStorage.setItem(key, value)
}

export function removeItem(key: string): void {
  if (isAvailable()) window.localStorage.removeItem(key)
}
