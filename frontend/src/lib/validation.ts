export function mapValidationErrors(errors?: Record<string, string[]>): Record<string, string> {
  if (!errors) {
    return {}
  }

  return Object.fromEntries(
    Object.entries(errors).map(([field, messages]) => [field.toLowerCase(), messages[0] ?? '']),
  )
}
