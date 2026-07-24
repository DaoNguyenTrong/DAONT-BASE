<script setup lang="ts">
import type { Account, CreateAccountRequest, UpdateAccountRequest } from '@/api/types'
import AccountForm from '@/components/AccountForm.vue'

const { t, locale } = useI18n()
const { open } = useAppDialogNaive()

const searchQuery = ref('')

let searchTimeout: ReturnType<typeof setTimeout> | undefined

useQuerySync([stringQueryField(searchQuery, 'q')])

const list = useLazyList<Account>({
  pageSize: 10,
  fetchPage: (pageNumber, pageSize) =>
    accountApi.getAll(pageNumber, pageSize, {
      search: searchQuery.value.trim() || undefined,
    }),
})

function formatDate(dateStr: string | null) {
  if (!dateStr) return '-'
  return new Intl.DateTimeFormat(locale.value, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(dateStr))
}

function onSearchInput() {
  clearTimeout(searchTimeout)
  searchTimeout = setTimeout(() => void list.reset(), 300)
}

function clearSearch() {
  searchQuery.value = ''
  void list.reset()
}

function onScroll(event: Event) {
  const el = event.target as HTMLElement
  if (list.hasMore.value && el.scrollTop + el.clientHeight >= el.scrollHeight - 200) {
    void list.loadMore()
  }
}

function openCreateDialog() {
  const state = reactive<CreateAccountRequest>({
    name: '',
    phone: '',
    position: '',
    address: '',
    username: '',
    email: '',
    password: '',
    status: true,
  })
  open(AccountForm, {
    header: t('accounts.createTitle'),
    data: { state, isEditing: false },
    dialogClass: 'w-full! max-w-2xl!',
    onConfirm: async (close) => {
      await accountApi.create({
        ...state,
        phone: state.phone?.trim() || null,
        position: state.position?.trim() || null,
        address: state.address?.trim() || null,
      })
      showSuccessMessage(t('accounts.created'))
      close()
      await list.reset()
    },
  })
}

function openEditDialog(account: Account) {
  const state = reactive<CreateAccountRequest>({
    name: account.name,
    phone: account.phone ?? '',
    position: account.position ?? '',
    address: account.address ?? '',
    username: account.username,
    email: account.email,
    password: '',
    status: account.status,
  })
  open(AccountForm, {
    header: t('accounts.editTitle'),
    data: { state, isEditing: true },
    dialogClass: 'w-full! max-w-2xl!',
    onConfirm: async (close) => {
      const payload: UpdateAccountRequest = {
        name: state.name,
        phone: state.phone?.trim() || null,
        position: state.position?.trim() || null,
        address: state.address?.trim() || null,
        username: state.username,
        email: state.email,
        status: state.status,
      }
      await accountApi.update(account.id, payload)
      showSuccessMessage(t('accounts.updated'))
      close()
      await list.reset()
    },
  })
}

function confirmDelete(account: Account) {
  requestConfirmation({
    header: t('common.confirm'),
    message: t('accounts.deleteConfirm'),
    rejectLabel: t('common.cancel'),
    acceptLabel: t('common.confirm'),
    accept: async () => {
      await accountApi.remove(account.id)
      showSuccessMessage(t('accounts.deleted'))
      await list.reset()
    },
  })
}

onMounted(async () => {
  await list.reset()
})
</script>

<template>
  <div class="mx-auto container flex h-full flex-col space-y-5">
    <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
      <h1 class="text-lg font-semibold text-surface-800 dark:text-surface-100">
        {{ t('accounts.title') }}
      </h1>
      <NButton type="primary" class="min-h-11" @click="openCreateDialog">
        <template #icon><SvgIcon name="plus" /></template>
        {{ t('accounts.createNew') }}
      </NButton>
    </div>

    <div class="flex flex-col gap-3 sm:flex-row sm:items-center">
      <NInput
        v-model:value="searchQuery"
        :placeholder="t('accounts.searchPlaceholder')"
        class="w-full flex-1"
        @input="onSearchInput"
      >
        <template v-if="searchQuery" #suffix>
          <SvgIcon name="times" class="cursor-pointer" @click="clearSearch" />
        </template>
      </NInput>
    </div>

    <div class="min-h-0 flex-1 overflow-hidden">
      <div v-if="list.loading.value" />

      <div
        v-else-if="list.items.value.length === 0"
        class="flex flex-col items-center justify-center rounded-xl border border-surface-200 bg-surface-0 px-6 py-16 dark:border-surface-800 dark:bg-surface-900"
      >
        <div
          class="mb-4 flex h-14 w-14 items-center justify-center rounded-2xl bg-primary-50 dark:bg-primary-400/10"
        >
          <SvgIcon name="users" class="text-2xl text-primary-500 dark:text-primary-400" />
        </div>
        <p class="text-base font-medium text-surface-700 dark:text-surface-200">
          {{ t('accounts.empty') }}
        </p>
        <p class="mt-1 text-sm text-surface-500 dark:text-surface-400">
          {{ t('accounts.emptyHint') }}
        </p>
      </div>

      <div v-else class="flex h-full w-full flex-col">
        <NVirtualList
          :items="list.items.value"
          :item-size="104"
          key-field="id"
          class="min-h-0 w-full flex-1 overflow-y-auto"
          @scroll="onScroll"
        >
          <template #default="{ item: account }">
            <div class="pb-2">
              <div
                class="flex items-center justify-between gap-3 rounded-xl border border-surface-200 bg-surface-0 p-4 dark:border-surface-800 dark:bg-surface-900"
              >
                <div class="min-w-0 flex-1">
                  <div class="flex items-center gap-2">
                    <p class="truncate text-sm font-medium text-surface-800 dark:text-surface-100">
                      {{ account.name }}
                    </p>
                    <NTag :type="account.status ? 'success' : 'error'" :bordered="false">
                      {{ account.status ? t('accounts.active') : t('accounts.inactive') }}
                    </NTag>
                  </div>
                  <p class="mt-1 truncate text-xs text-surface-500 dark:text-surface-400">
                    {{ account.username }} &middot; {{ account.email }}
                  </p>
                  <p class="mt-1 truncate text-xs text-surface-500 dark:text-surface-400">
                    {{ t('accounts.updatedAtLabel') }}:
                    {{ formatDate(account.updatedAt ?? account.createdAt) }}
                  </p>
                </div>
                <div class="flex shrink-0 items-center gap-1">
                  <NButton
                    text
                    circle
                    class="min-h-11 min-w-11"
                    :aria-label="t('accounts.editTitle')"
                    @click="openEditDialog(account)"
                  >
                    <template #icon><SvgIcon name="pencil" /></template>
                  </NButton>
                  <NButton
                    type="error"
                    text
                    circle
                    class="min-h-11 min-w-11"
                    :aria-label="t('accounts.deleteConfirm')"
                    @click="confirmDelete(account)"
                  >
                    <template #icon><SvgIcon name="trash" /></template>
                  </NButton>
                </div>
              </div>
            </div>
          </template>
        </NVirtualList>
        <div v-if="list.loadingMore.value" class="flex shrink-0 items-center justify-center py-3">
          <SvgIcon name="loader" class="animate-spin text-lg text-surface-400" />
        </div>
      </div>
    </div>
  </div>
</template>
