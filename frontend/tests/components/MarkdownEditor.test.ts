import { describe, expect, it } from 'vitest'
import MarkdownEditor from '@/components/MarkdownEditor.vue'
import { renderComponent } from '../helpers/render'

describe('MarkdownEditor', () => {
  it('renders both editor and preview panes in split mode with empty content', async () => {
    const { wrapper } = await renderComponent(MarkdownEditor, {
      props: { modelValue: '' },
    })

    expect(wrapper.find('textarea').exists()).toBe(true)
    expect(wrapper.text()).toContain('Chưa có nội dung')
  })

  it('renders marked-parsed HTML in the preview pane for markdown content', async () => {
    const { wrapper } = await renderComponent(MarkdownEditor, {
      props: { modelValue: '**bold**' },
    })

    expect(wrapper.html()).toContain('<strong>bold</strong>')
  })

  it('emits update:modelValue when typing in the textarea', async () => {
    const { wrapper } = await renderComponent(MarkdownEditor, {
      props: { modelValue: 'hello' },
    })

    await wrapper.find('textarea').setValue('hello world')

    expect(wrapper.emitted('update:modelValue')).toBeTruthy()
    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['hello world'])
  })

  it('reflects modelValue updates from the parent via props round-trip', async () => {
    const { wrapper } = await renderComponent(MarkdownEditor, {
      props: { modelValue: 'first' },
    })

    await wrapper.setProps({ modelValue: '# Heading' })

    expect(wrapper.html()).toContain('<h1>Heading</h1>')
  })

  it('hides the preview pane when switching to editor mode', async () => {
    const { wrapper } = await renderComponent(MarkdownEditor, {
      props: { modelValue: '**bold**' },
    })

    expect(wrapper.find('.markdown-preview').exists()).toBe(true)

    // Buttons are rendered in modeOptions order: split, editor, preview.
    const editorButton = wrapper.findAll('button')[1]
    await editorButton.trigger('click')

    expect(wrapper.find('textarea').exists()).toBe(true)
    expect(wrapper.find('.markdown-preview').exists()).toBe(false)
  })

  it('hides the editor/textarea when switching to preview mode', async () => {
    const { wrapper } = await renderComponent(MarkdownEditor, {
      props: { modelValue: '**bold**' },
    })

    // Buttons are rendered in modeOptions order: split, editor, preview.
    const previewButton = wrapper.findAll('button')[2]
    await previewButton.trigger('click')

    expect(wrapper.find('textarea').exists()).toBe(false)
    expect(wrapper.find('.markdown-preview').exists()).toBe(true)
    expect(wrapper.html()).toContain('<strong>bold</strong>')
  })
})
