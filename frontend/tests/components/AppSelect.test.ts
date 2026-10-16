import { flushPromises, mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import { NSelect } from 'naive-ui'
import AppSelect from '@/components/AppSelect.vue'

const options = [
  { label: 'Nguyễn Văn An', value: 'an' },
  { label: 'Trần Bình', value: 'binh' },
]

type Filter = (pattern: string, option: Record<string, unknown>) => boolean

describe('AppSelect', () => {
  it('emits null when the current single value is chosen again on a deselectable select', async () => {
    const wrapper = mount(AppSelect, { props: { value: 'an', options, deselectable: true } })

    await wrapper.findComponent(NSelect).vm.$emit('update:value', 'an', options[0])

    expect(wrapper.emitted('update:value')).toEqual([[null, null]])
  })

  // Opt-in: Naive highlights the current option when the menu opens, so Enter-to-open then
  // Enter-to-close re-picks it — a required form field must not be emptied by that.
  it('keeps the value when the current option is chosen again on an ordinary select', async () => {
    const wrapper = mount(AppSelect, { props: { value: 'an', options } })

    await wrapper.findComponent(NSelect).vm.$emit('update:value', 'an', options[0])

    expect(wrapper.emitted('update:value')).toEqual([['an', options[0]]])
  })

  it('forwards a new single value and its option unchanged', async () => {
    const wrapper = mount(AppSelect, { props: { value: 'an', options } })

    await wrapper.findComponent(NSelect).vm.$emit('update:value', 'binh', options[1])

    expect(wrapper.emitted('update:value')).toEqual([['binh', options[1]]])
  })

  it('forwards a multiple selection unchanged', async () => {
    const wrapper = mount(AppSelect, { props: { value: ['an'], multiple: true, options } })

    await wrapper.findComponent(NSelect).vm.$emit('update:value', ['an', 'binh'], options)

    expect(wrapper.emitted('update:value')).toEqual([[['an', 'binh'], options]])
  })

  it('passes other attributes through to the Naive UI select', () => {
    const wrapper = mount(AppSelect, {
      props: { value: null, options },
      attrs: { clearable: true },
    })

    expect(wrapper.findComponent(NSelect).props('clearable')).toBe(true)
    expect(wrapper.findComponent(NSelect).props('options')).toEqual(options)
  })

  it('filters accent-insensitively by default', () => {
    const wrapper = mount(AppSelect, {
      props: { value: null, options },
      attrs: { filterable: true },
    })
    const filter = wrapper.findComponent(NSelect).props('filter') as Filter

    expect(filter('nguyen an', options[0]!)).toBe(true)
    expect(filter('nguyen an', options[1]!)).toBe(false)
  })

  it('falls back to typo-tolerant matching across its options when nothing matches exactly', () => {
    const wrapper = mount(AppSelect, {
      props: { value: null, options },
      attrs: { filterable: true },
    })
    const filter = wrapper.findComponent(NSelect).props('filter') as Filter

    expect(filter('nguyn', options[0]!)).toBe(true)
    expect(filter('nguyn', options[1]!)).toBe(false)
  })

  // Options usually arrive after the first render; the fallback must see the loaded list.
  it('rebuilds its matcher when the options change', async () => {
    const wrapper = mount(AppSelect, {
      props: { value: null, options: [] },
      attrs: { filterable: true },
    })
    await wrapper.setProps({ options })
    const filter = wrapper.findComponent(NSelect).props('filter') as Filter

    expect(filter('nguyn', options[0]!)).toBe(true)
  })

  it('searches the options inside groups', () => {
    const grouped = [{ type: 'group', label: 'Members', key: 'members', children: options }]
    const wrapper = mount(AppSelect, {
      props: { value: null, options: grouped },
      attrs: { filterable: true },
    })
    const filter = wrapper.findComponent(NSelect).props('filter') as Filter

    expect(filter('nguyn', options[0]!)).toBe(true)
    expect(filter('nguyn', options[1]!)).toBe(false)
  })

  // Through the real Naive UI menu, not just the filter function: proves Naive calls it per option
  // with the objects AppSelect passed down.
  it.each([
    ['nguyn', ['Nguyễn Văn An']],
    ['BINH', ['Trần Bình']],
    ['xyzw', []],
    ['', ['Nguyễn Văn An', 'Trần Bình']],
  ])('shows the matching options in the open menu for %j', async (pattern, expected) => {
    const wrapper = mount(AppSelect, {
      props: { value: null, options },
      attrs: { filterable: true, virtualScroll: false },
      global: { stubs: { teleport: true } },
      attachTo: document.body,
    })
    await wrapper.find('.n-base-selection').trigger('click')
    await wrapper.find('input').setValue(pattern)
    await flushPromises()

    const shown = wrapper.findAll('.n-base-select-option').map((option) => option.text())
    expect(shown).toEqual(expected)
  })

  it('reads the label from labelField, falling back to a string value, like Naive UI', () => {
    const wrapper = mount(AppSelect, {
      props: { value: null, options: [] },
      attrs: { filterable: true, labelField: 'name' },
    })
    const filter = wrapper.findComponent(NSelect).props('filter') as Filter

    expect(filter('dien', { name: 'Điện Biên', value: 'x' })).toBe(true)
    expect(filter('ha noi', { value: 'Hà Nội' })).toBe(true)
    expect(filter('ha', { value: 7 })).toBe(false)
  })

  it('lets the caller replace the filter', () => {
    const custom: Filter = () => false
    const wrapper = mount(AppSelect, { props: { value: null, options }, attrs: { filter: custom } })

    expect(wrapper.findComponent(NSelect).props('filter')).toBe(custom)
  })
})
