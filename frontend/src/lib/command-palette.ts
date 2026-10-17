import type { Component, Ref } from 'vue'
import { matchesSearchText } from '@/lib/search-text'

export interface PaletteAction {
  id: string
  labelKey: string
  icon: Component
  /** Extra search words (already in the language the user types). */
  keywords?: string[]
  /** Permission code (`lib/permissions.ts`) the action is hidden without. */
  permission?: string
  run: () => void | Promise<void>
}

export interface PaletteResult {
  kind: 'page' | 'action'
  id: string
  labelKey: string
  icon: Component
  trailKeys: string[]
  keywords?: string[]
}

// shallowRef: identity of the registered object matters for unregistering.
const actions = shallowRef<PaletteAction[]>([])

/** Adds (or, for an existing `id`, replaces) an action. Returns the function that removes it. */
export function registerPaletteAction(action: PaletteAction): () => void {
  const index = actions.value.findIndex((existing) => existing.id === action.id)

  if (index === -1) {
    actions.value = [...actions.value, action]
  } else {
    actions.value = actions.value.map((existing, i) => (i === index ? action : existing))
  }

  return () => {
    // A later registration under the same id owns the slot; only remove our own.
    if (actions.value.includes(action)) {
      actions.value = actions.value.filter((existing) => existing !== action)
    }
  }
}

export function usePaletteActions(): Readonly<Ref<readonly PaletteAction[]>> {
  return actions
}

/** Pages first, then actions; an empty query keeps everything. */
export function filterPaletteResults(
  results: PaletteResult[],
  query: string,
  translate: (key: string) => string,
): PaletteResult[] {
  const matching = results.filter((result) => {
    const haystack = [
      translate(result.labelKey),
      ...result.trailKeys.map(translate),
      ...(result.keywords ?? []),
    ].join(' ')

    return matchesSearchText(haystack, query)
  })

  return [
    ...matching.filter((result) => result.kind === 'page'),
    ...matching.filter((result) => result.kind === 'action'),
  ]
}
