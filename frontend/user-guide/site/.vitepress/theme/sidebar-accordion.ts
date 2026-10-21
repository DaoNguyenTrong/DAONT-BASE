import { nextTick, onBeforeUnmount, onMounted, watch } from 'vue'
import { useRoute } from 'vitepress'

// One chapter open at a time. VitePress has no such mode and doesn't expose a group's open state,
// so a group is closed by clicking its own caret rather than by copying the theme's component.
// Only level-0 groups exist (chapter › page).
const GROUP = '.VPSidebar .VPSidebarItem.level-0.collapsible'

function caretOf(group: Element) {
  return group.querySelector<HTMLElement>(':scope > .item > .caret')
}

function collapseOpenGroups(keep: (group: Element) => boolean) {
  document.querySelectorAll(`${GROUP}:not(.collapsed)`).forEach((group) => {
    if (!keep(group)) caretOf(group)?.click()
  })
}

// The user opens a group with its caret (mouse or Enter) → close the others. Captured before
// VitePress flips the state: a group that is still `.collapsed` is about to open.
function onCaretInteraction(event: Event) {
  if (event instanceof KeyboardEvent && event.key !== 'Enter') return
  const caret = (event.target as Element | null)?.closest?.('.caret')
  const group = caret?.parentElement?.parentElement
  if (!group?.matches(`${GROUP}.collapsed`)) return
  collapseOpenGroups((other) => other === group)
}

export function useSidebarAccordion() {
  const route = useRoute()

  // A page in another chapter → VitePress opens that chapter; close the rest. flush 'post' so
  // `.has-active` already points at the current chapter. Pages outside any chapter (404) keep it.
  watch(
    () => route.path,
    async () => {
      await nextTick()
      if (!document.querySelector(`${GROUP}.has-active`)) return
      collapseOpenGroups((group) => group.classList.contains('has-active'))
    },
    { flush: 'post' },
  )

  onMounted(() => {
    document.addEventListener('click', onCaretInteraction, true)
    document.addEventListener('keydown', onCaretInteraction, true)
  })
  onBeforeUnmount(() => {
    document.removeEventListener('click', onCaretInteraction, true)
    document.removeEventListener('keydown', onCaretInteraction, true)
  })
}
