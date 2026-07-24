interface QuerySyncField {
  key: string
  read: () => string
  write: (raw: string) => void
  isDefault: () => boolean
}

const SYNC_DEBOUNCE_MS = 300

export function useQuerySync(fields: QuerySyncField[]) {
  const route = useRoute()
  const router = useRouter()
  let timeout: ReturnType<typeof setTimeout> | undefined

  for (const field of fields) {
    const raw = route.query[field.key]
    if (typeof raw === 'string') field.write(raw)
  }

  watch(
    fields.map((field) => field.read),
    () => {
      clearTimeout(timeout)
      timeout = setTimeout(() => {
        const query = { ...route.query }
        for (const field of fields) {
          if (field.isDefault()) delete query[field.key]
          else query[field.key] = field.read()
        }
        void router.replace({ query })
      }, SYNC_DEBOUNCE_MS)
    },
  )
}

export function numberQueryField(
  state: Ref<number>,
  key: string,
  defaultValue: number,
): QuerySyncField {
  return {
    key,
    read: () => String(state.value),
    write: (raw) => {
      const parsed = Number(raw)
      state.value = Number.isFinite(parsed) && parsed > 0 ? parsed : defaultValue
    },
    isDefault: () => state.value === defaultValue,
  }
}

export function stringQueryField(
  state: Ref<string>,
  key: string,
  defaultValue = '',
): QuerySyncField {
  return {
    key,
    read: () => state.value,
    write: (raw) => {
      state.value = raw
    },
    isDefault: () => state.value === defaultValue,
  }
}

export function boolQueryField(
  state: Ref<boolean>,
  key: string,
  defaultValue: boolean,
): QuerySyncField {
  return {
    key,
    read: () => (state.value ? '1' : '0'),
    write: (raw) => {
      state.value = raw === '1'
    },
    isDefault: () => state.value === defaultValue,
  }
}

export function enumQueryField<T extends string>(
  state: Ref<T>,
  key: string,
  defaultValue: T,
  allowed: readonly T[],
): QuerySyncField {
  return {
    key,
    read: () => state.value,
    write: (raw) => {
      state.value = (allowed as readonly string[]).includes(raw) ? (raw as T) : defaultValue
    },
    isDefault: () => state.value === defaultValue,
  }
}
