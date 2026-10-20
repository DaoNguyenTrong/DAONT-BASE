<script setup lang="ts">
import { Bell, Check, Checks } from '@vicons/tabler'
import { showErrorMessage } from '@/lib/feedback'
import { ApiError } from '@/api/types'
import type { Notification } from '@/api/types'

// 'on-brand' is the bell on the purple menu bar (horizontal mode, brand color, light): a white icon there.
const props = withDefaults(defineProps<{ tone?: 'default' | 'on-brand' }>(), { tone: 'default' })

const { t, locale } = useI18n()
const notificationsStore = useNotificationsStore()
const { unreadCount } = storeToRefs(notificationsStore)

const showPopover = ref(false)
const items = ref<Notification[]>([])
const loading = ref(false)

async function loadList() {
  loading.value = true
  try {
    const result = await notificationsStore.fetchList(1, 10)
    items.value = result.items
  } catch (error) {
    reportError(error)
  } finally {
    loading.value = false
  }
}

function handleShowChange(show: boolean) {
  if (show) loadList()
}

function notificationText(item: Notification) {
  const data = item.data ? JSON.parse(item.data) : {}
  return t(`notifications.${item.type}`, data)
}

function formatDate(dateStr: string) {
  return new Intl.DateTimeFormat(locale.value, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(dateStr))
}

async function handleMarkAsRead(item: Notification) {
  try {
    await notificationsStore.markAsRead(item.id)
    item.isRead = true
  } catch (error) {
    reportError(error)
  }
}

async function handleMarkAllAsRead() {
  try {
    await notificationsStore.markAllAsRead()
    items.value = items.value.map((item) => ({ ...item, isRead: true }))
  } catch (error) {
    reportError(error)
  }
}

function reportError(error: unknown) {
  if (error instanceof ApiError) {
    showErrorMessage(error.problem.title, error.problem.detail ?? t('errors.requestFailed'))
  } else {
    showErrorMessage(t('errors.requestFailed'))
  }
}
</script>

<template>
  <!-- Anchored to the bell's right edge, which AppHeader keeps rightmost on phones: 2rem narrower than
       the viewport leaves a margin on both sides; 360px from ~392px up. -->
  <n-popover
    v-model:show="showPopover"
    trigger="click"
    placement="bottom-end"
    style="padding: 0; width: min(360px, calc(100vw - 2rem))"
    @update:show="handleShowChange"
  >
    <template #trigger>
      <n-badge :value="unreadCount" :max="99" :show="unreadCount > 0">
        <n-button
          :aria-label="t('notifications.title')"
          circle
          secondary
          :color="props.tone === 'on-brand' ? '#ffffff' : undefined"
          class="pointer-coarse:size-10!"
        >
          <template #icon
            ><n-icon><Bell /></n-icon
          ></template>
        </n-button>
      </n-badge>
    </template>

    <div class="flex max-h-[min(24rem,70dvh)] w-full flex-col">
      <div
        class="flex items-center justify-between border-b border-surface-200 px-3 py-2 dark:border-surface-800"
      >
        <span class="font-medium">{{ t('notifications.title') }}</span>
        <n-button
          text
          size="tiny"
          class="pointer-coarse:min-h-10"
          :disabled="unreadCount === 0"
          @click="handleMarkAllAsRead"
        >
          <template #icon
            ><n-icon><Checks /></n-icon
          ></template>
          {{ t('notifications.markAllRead') }}
        </n-button>
      </div>

      <n-spin :show="loading" class="flex-1 overflow-y-auto">
        <n-empty
          v-if="!loading && items.length === 0"
          :description="t('notifications.empty')"
          class="py-6"
        />
        <n-list v-else :show-divider="false">
          <n-list-item v-for="item in items" :key="item.id" class="items-start!">
            <div class="flex w-full items-start gap-2">
              <span
                class="mt-1.5 h-2 w-2 shrink-0 rounded-full"
                :class="item.isRead ? 'bg-transparent' : 'bg-primary-500'"
              />
              <div class="min-w-0 flex-1">
                <p class="text-sm">{{ notificationText(item) }}</p>
                <p class="text-xs text-surface-500">{{ formatDate(item.createdAt) }}</p>
              </div>
              <n-button
                v-if="!item.isRead"
                text
                size="tiny"
                class="shrink-0 pointer-coarse:min-h-10 pointer-coarse:min-w-10"
                :aria-label="t('notifications.markAsRead')"
                @click="handleMarkAsRead(item)"
              >
                <template #icon
                  ><n-icon><Check /></n-icon
                ></template>
              </n-button>
            </div>
          </n-list-item>
        </n-list>
      </n-spin>
    </div>
  </n-popover>
</template>
