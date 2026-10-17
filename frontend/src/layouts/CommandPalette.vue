<script setup lang="ts">
import { Language, Logout, Settings, User } from '@vicons/tabler'
import {
  filterPaletteResults,
  registerPaletteAction,
  usePaletteActions,
  type PaletteResult,
} from '@/lib/command-palette'
import { flattenMenuLinks } from '@/layouts/sidebar-menu'

const { t } = useI18n()
const router = useRouter()
const auth = useAuthStore()
const layout = useLayoutStore()
const localeStore = useLocaleStore()
const confirmLogout = useConfirmLogout()
const menu = useSidebarMenu()
const registeredActions = usePaletteActions()

const query = ref('')
const activeIndex = ref(0)
const inputRef = ref<HTMLInputElement | null>(null)

const show = computed({
  get: () => layout.paletteOpen,
  set: (value) => (value ? layout.openPalette() : layout.closePalette()),
})

// The base actions live and die with this component, so a fork can add its own beside them.
const unregisterBase = [
  registerPaletteAction({
    id: 'base.open-settings',
    labelKey: 'layout.settings.title',
    icon: Settings,
    run: () => layout.openSettings(),
  }),
  registerPaletteAction({
    id: 'base.open-profile',
    labelKey: 'nav.profile',
    icon: User,
    run: () => layout.openProfile(),
  }),
  registerPaletteAction({
    id: 'base.switch-language',
    labelKey: 'layout.palette.switchLanguage',
    icon: Language,
    run: () => localeStore.toggleLocale(),
  }),
  registerPaletteAction({
    id: 'base.logout',
    labelKey: 'auth.logout',
    icon: Logout,
    run: confirmLogout,
  }),
]

const results = computed<PaletteResult[]>(() => {
  const pages: PaletteResult[] = flattenMenuLinks(menu.value).map((link) => ({
    kind: 'page',
    id: link.routeName,
    labelKey: link.labelKey,
    icon: link.icon,
    trailKeys: link.trailKeys,
  }))
  const actions: PaletteResult[] = registeredActions.value
    .filter((action) => !action.permission || auth.hasPermission(action.permission))
    .map((action) => ({
      kind: 'action',
      id: action.id,
      labelKey: action.labelKey,
      icon: action.icon,
      trailKeys: [],
      keywords: action.keywords,
    }))

  return filterPaletteResults([...pages, ...actions], query.value, t)
})

const activeResult = computed(() => results.value[activeIndex.value])
const optionId = (index: number) => `command-palette-option-${index}`
const activeDescendant = computed(() =>
  activeResult.value ? optionId(activeIndex.value) : undefined,
)

// A new query or a changed list (permissions, locale) restarts at the top.
watch(results, () => (activeIndex.value = 0))

watch(show, (open) => {
  if (open) {
    query.value = ''
    activeIndex.value = 0
  }
})

function focusInput() {
  inputRef.value?.focus()
}

function move(step: 1 | -1) {
  const count = results.value.length

  if (count > 0) {
    activeIndex.value = (activeIndex.value + step + count) % count
  }
}

async function choose(result: PaletteResult | undefined) {
  if (!result) {
    return
  }

  layout.closePalette()

  if (result.kind === 'page') {
    await router.push({ name: result.id })
    return
  }

  await registeredActions.value.find((action) => action.id === result.id)?.run()
}

function onInputKeydown(event: KeyboardEvent) {
  if (event.isComposing) {
    return
  }

  if (event.key === 'ArrowDown') {
    event.preventDefault()
    move(1)
  } else if (event.key === 'ArrowUp') {
    event.preventDefault()
    move(-1)
  } else if (event.key === 'Enter') {
    event.preventDefault()
    void choose(activeResult.value)
  } else if (event.key === 'Escape') {
    event.preventDefault()
    layout.closePalette()
  }
}

// Plain Ctrl/⌘ K only. Shift or Alt makes it another shortcut (Ctrl Shift K is Firefox's web console),
// a held key would re-fire it, and inside `[data-command-palette-ignore]` (the Markdown editor) the
// browser keeps it.
function onGlobalKeydown(event: KeyboardEvent) {
  if (
    !(event.ctrlKey || event.metaKey) ||
    event.shiftKey ||
    event.altKey ||
    event.repeat ||
    event.key.toLowerCase() !== 'k'
  ) {
    return
  }
  if (event.target instanceof Element && event.target.closest('[data-command-palette-ignore]')) {
    return
  }

  event.preventDefault()

  if (!layout.paletteOpen) {
    layout.openPalette()
  }
}

onMounted(() => window.addEventListener('keydown', onGlobalKeydown))
onUnmounted(() => {
  window.removeEventListener('keydown', onGlobalKeydown)
  unregisterBase.forEach((unregister) => unregister())
})
</script>

<template>
  <n-modal v-model:show="show" @after-enter="focusInput">
    <div
      role="dialog"
      aria-modal="true"
      :aria-label="t('layout.palette.trigger')"
      class="bg-surface-0 dark:bg-surface-900 fixed top-[15vh] left-1/2 w-[min(36rem,calc(100vw-2rem))] -translate-x-1/2 overflow-hidden rounded-xl shadow-2xl"
    >
      <input
        ref="inputRef"
        v-model="query"
        type="text"
        role="combobox"
        autofocus
        autocomplete="off"
        aria-expanded="true"
        aria-controls="command-palette-listbox"
        :aria-activedescendant="activeDescendant"
        :aria-label="t('layout.palette.trigger')"
        :placeholder="t('layout.palette.placeholder')"
        class="border-surface-200 dark:border-surface-700 w-full border-b bg-transparent px-4 py-3 text-base outline-none"
        data-testid="command-palette-input"
        @keydown="onInputKeydown"
      />

      <ul
        id="command-palette-listbox"
        role="listbox"
        class="max-h-[50vh] overflow-y-auto p-2"
        data-testid="command-palette-list"
      >
        <template v-for="(result, index) in results" :key="`${result.kind}:${result.id}`">
          <li
            v-if="index === 0 || results[index - 1]!.kind !== result.kind"
            role="presentation"
            class="text-surface-400 px-2 pt-2 pb-1 text-xs font-medium uppercase"
          >
            {{ result.kind === 'page' ? t('layout.palette.pages') : t('layout.palette.actions') }}
          </li>
          <li
            :id="optionId(index)"
            role="option"
            :aria-selected="index === activeIndex"
            class="flex cursor-pointer items-center gap-2 rounded-lg px-2 py-2 text-sm"
            :class="
              index === activeIndex
                ? 'bg-primary-50 text-primary-700 dark:bg-surface-800 dark:text-primary-200'
                : ''
            "
            @mousemove="activeIndex = index"
            @click="choose(result)"
          >
            <n-icon class="shrink-0"><component :is="result.icon" /></n-icon>
            <span class="min-w-0 flex-1 truncate">{{ t(result.labelKey) }}</span>
            <span v-if="result.trailKeys.length" class="text-surface-400 truncate text-xs">
              {{ result.trailKeys.map((key) => t(key)).join(' / ') }}
            </span>
          </li>
        </template>
        <li
          v-if="results.length === 0"
          role="presentation"
          class="text-surface-500 px-2 py-6 text-center text-sm"
        >
          {{ t('layout.palette.empty') }}
        </li>
      </ul>
    </div>
  </n-modal>
</template>
