<script setup lang="ts">
interface SortOption {
  label: string
  value: string
}

const DEFAULT_VALUE = '__default__'

const props = defineProps<{
  modelValue: string
  descending: boolean
  options: SortOption[]
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
  'update:descending': [value: boolean]
}>()

const { t } = useI18n()

const selectOptions = computed(() => [
  { label: t('common.sortDefault'), value: DEFAULT_VALUE },
  ...props.options,
])

const selectValue = computed(() => props.modelValue || DEFAULT_VALUE)

function onSelectChange(value: string) {
  emit('update:modelValue', value === DEFAULT_VALUE ? '' : value)
}

function toggleDirection() {
  emit('update:descending', !props.descending)
}
</script>

<template>
  <div class="flex shrink-0 items-center gap-1">
    <n-select
      :value="selectValue"
      :options="selectOptions.map((o) => ({ label: o.label, value: o.value }))"
      :consistent-menu-width="false"
      :aria-label="t('common.sortBy')"
      @update:value="onSelectChange"
    />
    <n-button
      v-if="modelValue"
      text
      circle
      :aria-label="descending ? t('common.sortDescending') : t('common.sortAscending')"
      @click="toggleDirection"
    >
      <template #icon>
        <SvgIcon :name="descending ? 'sort-descending' : 'sort-ascending'" />
      </template>
    </n-button>
  </div>
</template>
