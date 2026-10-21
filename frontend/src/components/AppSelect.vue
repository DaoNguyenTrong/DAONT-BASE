<script setup lang="ts">
import { NSelect } from 'naive-ui'
import type { SelectOption, SelectProps } from 'naive-ui'

/**
 * Drop-in for every `<n-select>` (via the `vite.config.ts` resolver); `filterable` uses
 * `createSearchTextMatcher` unless a `filter` is passed. `deselectable` is opt-in: Naive highlights
 * the current option on open, so Enter-to-open, Enter-to-close would otherwise empty a form field.
 */
defineOptions({ inheritAttrs: false })

// options and the two field names are props, not attrs, so the matcher below rebuilds when they
// change: `useAttrs()` isn't reactive, and options are often loaded after the first render.
const props = defineProps<{
  value?: string | number | Array<string | number> | null
  multiple?: boolean
  options?: SelectProps['options']
  labelField?: string
  valueField?: string
  childrenField?: string
  /** Re-picking the selected option clears it — for filters and sorts, not required fields. */
  deselectable?: boolean
}>()

/**
 * Naive UI's own handler type, not a wide union: its intersection parameters keep narrow handlers
 * (`(value: string) => void`) assignable. It can't express the `null` a `deselectable` select
 * emits, so that handler must accept `null` itself.
 */
type NaiveOnUpdateValue = Extract<
  NonNullable<SelectProps['onUpdateValue']>,
  (...args: never[]) => unknown
>

const emit = defineEmits<{
  'update:value': Parameters<NaiveOnUpdateValue>
}>()

// Same fields Naive UI's default filter reads: the label, else a string value.
function searchTextOf(option: SelectOption): string | null {
  const label = option[props.labelField ?? 'label']
  const value = option[props.valueField ?? 'value']
  return typeof label === 'string' ? label : typeof value === 'string' ? value : null
}

// Group headers aren't matched themselves; Naive filters their children.
function selectableOptions(options: SelectProps['options'] = []): SelectOption[] {
  const childrenField = props.childrenField ?? 'children'
  return options.flatMap((option) => {
    const children = (option as Record<string, unknown>)[childrenField]
    return option.type === 'group' && Array.isArray(children)
      ? selectableOptions(children as SelectProps['options'])
      : [toRaw(option) as SelectOption]
  })
}

// Naive calls `filter` once per option, but falling back to typo tolerance is decided over the
// whole list — so the matcher is built from the options, once per options change. Options are
// compared raw: a reactive parent hands over proxies, and a proxy never equals its target.
const searchMatcher = computed(() =>
  createSearchTextMatcher(selectableOptions(props.options), searchTextOf),
)

function accentInsensitiveFilter(pattern: string, option: SelectOption): boolean {
  return searchMatcher.value(pattern, toRaw(option))
}

function onUpdateValue(
  next: string | number | Array<string | number> | null,
  option: SelectOption | SelectOption[] | null,
) {
  const resolved = props.deselectable
    ? resolveSelectUpdate(props.value, next, props.multiple === true)
    : next

  // The real payload is the wide union; the public type above only narrows per call site, so it
  // is cast once here rather than at every consumer.
  emit(
    'update:value',
    ...([resolved, resolved === null ? null : option] as unknown as Parameters<NaiveOnUpdateValue>),
  )
}
</script>

<template>
  <!-- `filter` comes before `v-bind="$attrs"` so a caller's own filter replaces it. -->
  <NSelect
    :filter="accentInsensitiveFilter"
    v-bind="$attrs"
    :value="value"
    :multiple="multiple"
    :options="options"
    :label-field="labelField"
    :value-field="valueField"
    :children-field="childrenField"
    @update:value="onUpdateValue"
  />
</template>
