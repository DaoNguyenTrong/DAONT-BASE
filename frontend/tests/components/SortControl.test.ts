import { describe, expect, it } from 'vitest'
import SortControl from '@/components/SortControl.vue'
import { renderComponent } from '../helpers/render'

interface SortOption {
  label: string
  value: string
}

function makeOptions(): SortOption[] {
  return [
    { label: 'Name', value: 'name' },
    { label: 'Date', value: 'date' },
  ]
}

async function mountSortControl(
  props: Partial<{ modelValue: string; descending: boolean; options: SortOption[] }> = {},
) {
  return renderComponent(SortControl, {
    props: {
      modelValue: '',
      descending: false,
      options: makeOptions(),
      ...props,
    },
  })
}

describe('SortControl', () => {
  it('prepends the default sentinel option to the select options', async () => {
    const { wrapper } = await mountSortControl()

    const select = wrapper.findComponent({ name: 'Select' })
    const options = select.props('options') as SortOption[]

    expect(options[0]).toEqual({ label: 'Default', value: '__default__' })
    expect(options.slice(1)).toEqual(makeOptions())
  })

  it('maps an empty modelValue to the sentinel value on the select', async () => {
    const { wrapper } = await mountSortControl({ modelValue: '' })

    const select = wrapper.findComponent({ name: 'Select' })
    expect(select.props('value')).toBe('__default__')
  })

  it('passes a non-empty modelValue straight through to the select', async () => {
    const { wrapper } = await mountSortControl({ modelValue: 'name' })

    const select = wrapper.findComponent({ name: 'Select' })
    expect(select.props('value')).toBe('name')
  })

  it('emits an empty string for update:modelValue when the sentinel is selected', async () => {
    const { wrapper } = await mountSortControl({ modelValue: 'name' })

    const select = wrapper.findComponent({ name: 'Select' })
    await select.vm.$emit('update:value', '__default__')

    expect(wrapper.emitted('update:modelValue')).toEqual([['']])
  })

  it('emits the raw value for update:modelValue when a real option is selected', async () => {
    const { wrapper } = await mountSortControl()

    const select = wrapper.findComponent({ name: 'Select' })
    await select.vm.$emit('update:value', 'date')

    expect(wrapper.emitted('update:modelValue')).toEqual([['date']])
  })

  it('hides the direction-toggle button when modelValue is empty', async () => {
    const { wrapper } = await mountSortControl({ modelValue: '' })

    expect(wrapper.findComponent({ name: 'Button' }).exists()).toBe(false)
  })

  it('shows the direction-toggle button when modelValue is truthy', async () => {
    const { wrapper } = await mountSortControl({ modelValue: 'name' })

    expect(wrapper.findComponent({ name: 'Button' }).exists()).toBe(true)
  })

  it('emits the negated descending value when the direction toggle is clicked', async () => {
    const { wrapper } = await mountSortControl({ modelValue: 'name', descending: false })

    await wrapper.findComponent({ name: 'Button' }).trigger('click')

    expect(wrapper.emitted('update:descending')).toEqual([[true]])
  })

  it('emits false when toggling direction while already descending', async () => {
    const { wrapper } = await mountSortControl({ modelValue: 'name', descending: true })

    await wrapper.findComponent({ name: 'Button' }).trigger('click')

    expect(wrapper.emitted('update:descending')).toEqual([[false]])
  })
})
