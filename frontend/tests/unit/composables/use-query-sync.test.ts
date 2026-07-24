import { describe, expect, it, afterEach, vi } from 'vitest'
import { ref } from 'vue'
import {
  boolQueryField,
  enumQueryField,
  numberQueryField,
  stringQueryField,
  useQuerySync,
} from '@/composables/use-query-sync'
import { withSetup } from '../../helpers/with-setup'

describe('query sync field factories', () => {
  it('numberQueryField parses positive integers and falls back to default', () => {
    const page = ref(1)
    const field = numberQueryField(page, 'page', 1)

    field.write('5')
    expect(page.value).toBe(5)

    field.write('abc')
    expect(page.value).toBe(1)

    field.write('0')
    expect(page.value).toBe(1)
    expect(field.isDefault()).toBe(true)
  })

  it('stringQueryField reads, writes, and detects default', () => {
    const q = ref('')
    const field = stringQueryField(q, 'q')

    field.write('hello')
    expect(q.value).toBe('hello')
    expect(field.read()).toBe('hello')
    expect(field.isDefault()).toBe(false)

    q.value = ''
    expect(field.isDefault()).toBe(true)
  })

  it('boolQueryField maps 1/0 to boolean', () => {
    const flag = ref(false)
    const field = boolQueryField(flag, 'flag', false)

    field.write('1')
    expect(flag.value).toBe(true)
    expect(field.read()).toBe('1')

    field.write('0')
    expect(flag.value).toBe(false)
    expect(field.isDefault()).toBe(true)
  })

  it('enumQueryField keeps allowed values and falls back to default', () => {
    const sort = ref<'a' | 'b'>('a')
    const field = enumQueryField(sort, 'sort', 'a', ['a', 'b'] as const)

    field.write('b')
    expect(sort.value).toBe('b')

    field.write('z')
    expect(sort.value).toBe('a')
    expect(field.isDefault()).toBe(true)
  })
})

describe('useQuerySync', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('hydrates state from route query on setup', async () => {
    const q = ref('')
    await withSetup(() => {
      useQuerySync([stringQueryField(q, 'q')])
    }, '/list?q=hello')

    expect(q.value).toBe('hello')
  })

  it('pushes query after debounce when state changes', async () => {
    vi.useFakeTimers()
    const q = ref('')
    const { router, app } = await withSetup(() => {
      useQuerySync([stringQueryField(q, 'q')])
    }, '/list')

    q.value = 'world'
    await vi.advanceTimersByTimeAsync(300)

    expect(router.currentRoute.value.query.q).toBe('world')
    app.unmount()
  })

  it('removes query key when state returns to default', async () => {
    vi.useFakeTimers()
    const q = ref('hello')
    const { router, app } = await withSetup(() => {
      useQuerySync([stringQueryField(q, 'q')])
    }, '/list?q=hello')

    q.value = ''
    await vi.advanceTimersByTimeAsync(300)

    expect(router.currentRoute.value.query.q).toBeUndefined()
    app.unmount()
  })
})
