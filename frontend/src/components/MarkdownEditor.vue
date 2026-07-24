<script setup lang="ts">
import { marked } from 'marked'

type ViewMode = 'split' | 'editor' | 'preview'

const props = withDefaults(
  defineProps<{
    modelValue: string
    placeholder?: string
  }>(),
  {
    placeholder: '',
  },
)

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

const viewMode = ref<ViewMode>('split')

const modeOptions: Array<{ value: ViewMode; icon: string; label: string }> = [
  { value: 'split', icon: 'columns', label: 'Chia đôi' },
  { value: 'editor', icon: 'pencil', label: 'Soạn thảo' },
  { value: 'preview', icon: 'eye', label: 'Xem trước' },
]

const renderedMarkdown = computed(() => {
  return marked.parse(props.modelValue ?? '') as string
})
</script>

<template>
  <div
    class="overflow-hidden rounded-xl border border-surface-200 bg-surface-0 dark:border-surface-800 dark:bg-surface-900"
  >
    <!-- Toolbar -->
    <div
      class="flex items-center justify-between border-b border-surface-200 bg-surface-50 px-4 py-2 dark:border-surface-800 dark:bg-surface-900/50"
    >
      <div class="flex items-center gap-2">
        <SvgIcon name="file" class="text-xs text-surface-400 dark:text-surface-500" />
        <span class="text-xs font-medium text-surface-400 dark:text-surface-500">Markdown</span>
      </div>

      <!-- Segmented mode switcher -->
      <div
        class="flex overflow-hidden rounded-lg border border-surface-200 text-xs dark:border-surface-600"
      >
        <n-button
          v-for="option in modeOptions"
          :key="option.value"
          :class="
            viewMode === option.value
              ? 'bg-primary-500 text-white'
              : 'bg-transparent text-surface-500 hover:bg-surface-100 dark:text-surface-400 dark:hover:bg-surface-800'
          "
          @click="viewMode = option.value"
          text
        >
          <SvgIcon :name="option.icon" class="text-xs" />
          <span class="hidden sm:inline">{{ option.label }}</span>
        </n-button>
      </div>
    </div>

    <!-- Editor panes -->
    <div class="flex min-h-120" :class="viewMode === 'split' ? 'flex-col md:flex-row' : 'flex-col'">
      <!-- Editor pane -->
      <div
        v-if="viewMode !== 'preview'"
        class="flex flex-1 flex-col"
        :class="
          viewMode === 'split'
            ? 'border-b border-surface-200 dark:border-surface-800 md:border-b-0 md:border-r'
            : ''
        "
      >
        <div
          class="border-b border-surface-100 px-4 py-1 text-xs text-surface-400 dark:border-surface-800/60 dark:text-surface-500"
        >
          Soạn thảo
        </div>
        <n-input
          type="textarea"
          :value="modelValue"
          :placeholder="placeholder"
          class="markdown-editor__textarea w-full flex-1"
          @update:value="emit('update:modelValue', $event)"
        />
      </div>

      <!-- Preview pane -->
      <div v-if="viewMode !== 'editor'" class="flex flex-1 flex-col">
        <div
          class="border-b border-surface-100 px-4 py-1 text-xs text-surface-400 dark:border-surface-800/60 dark:text-surface-500"
        >
          Xem trước
        </div>
        <div class="flex-1 overflow-auto p-5">
          <div
            v-if="!modelValue"
            class="flex min-h-40 flex-col items-center justify-center gap-2 text-center"
          >
            <SvgIcon name="file" class="text-2xl text-surface-300 dark:text-surface-600" />
            <span class="text-sm text-surface-400 dark:text-surface-500">Chưa có nội dung</span>
          </div>
          <div
            v-else
            class="markdown-preview text-sm text-surface-700 dark:text-surface-200"
            v-html="renderedMarkdown"
          />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped lang="scss">
.markdown-editor__textarea {
  :deep(textarea) {
    border: none;
    border-radius: 0;
    box-shadow: none;
    background: transparent;
    min-height: 26rem;
    resize: vertical;
    font-family: 'IBM Plex Mono', 'Cascadia Code', 'Fira Code', ui-monospace, monospace;
    font-size: 0.875rem;
    line-height: 1.75;
    padding: 1rem;

    &:focus,
    &:focus-visible {
      box-shadow: none;
      outline: none;
    }
  }
}

.markdown-preview {
  line-height: 1.75;
  word-break: break-word;

  :deep(h1),
  :deep(h2),
  :deep(h3),
  :deep(h4) {
    font-weight: 600;
    line-height: 1.3;
    margin-bottom: 0.6rem;
    margin-top: 1.25rem;

    &:first-child {
      margin-top: 0;
    }
  }

  :deep(h1) {
    font-size: 1.4rem;
  }
  :deep(h2) {
    font-size: 1.2rem;
  }
  :deep(h3) {
    font-size: 1.05rem;
  }

  :deep(p),
  :deep(ul),
  :deep(ol),
  :deep(blockquote),
  :deep(pre) {
    margin-bottom: 1rem;

    &:last-child {
      margin-bottom: 0;
    }
  }

  :deep(ul),
  :deep(ol) {
    padding-left: 1.5rem;
  }

  :deep(li + li) {
    margin-top: 0.25rem;
  }

  :deep(code) {
    border-radius: 0.3rem;
    background: color-mix(in srgb, var(--color-surface-200) 80%, transparent);
    padding: 0.125rem 0.375rem;
    font-family: 'IBM Plex Mono', monospace;
    font-size: 0.85em;
  }

  :deep(pre) {
    overflow-x: auto;
    border-radius: 0.625rem;
    background: color-mix(in srgb, var(--color-surface-100) 90%, transparent);
    padding: 1rem 1.25rem;
    border: 1px solid var(--color-surface-200);

    code {
      background: transparent;
      padding: 0;
    }
  }

  :deep(blockquote) {
    border-left: 3px solid var(--color-primary-500);
    padding: 0.25rem 0 0.25rem 1rem;
    color: var(--color-surface-500);
    font-style: italic;
  }

  :deep(hr) {
    border: none;
    border-top: 1px solid var(--color-surface-200);
    margin: 1.5rem 0;
  }

  :deep(a) {
    color: var(--color-primary-500);
    text-decoration: underline;
  }

  :deep(strong) {
    font-weight: 600;
  }
}

.dark .markdown-preview {
  :deep(code) {
    background: color-mix(in srgb, var(--color-surface-700) 75%, transparent);
  }

  :deep(pre) {
    background: color-mix(in srgb, var(--color-surface-800) 80%, transparent);
    border-color: var(--color-surface-700);
  }

  :deep(hr) {
    border-color: var(--color-surface-700);
  }
}
</style>
