<script setup lang="ts">
import { ArrowRight } from '@vicons/tabler'
import { safeGuideTarget } from '@/lib/safe-redirect'

const route = useRoute()
const router = useRouter()
const { t } = useI18n()

const failed = ref(false)

// The guard already required a session, but nginx checks the cookie on this origin: ask the same
// endpoint nginx asks (relative URL, never apiBaseUrl) before sending the reader back, or a cookie
// the API doesn't accept would bounce them between nginx and this view forever.
onMounted(async () => {
  try {
    const response = await fetch('/api/auth/check', {
      credentials: 'same-origin',
      cache: 'no-store',
    })
    if (response.status === 204) {
      window.location.replace(safeGuideTarget(route.query.next))
      return
    }
  } catch {
    // Unreachable API: same outcome as a rejected session.
  }
  failed.value = true
})

async function goHome() {
  await router.push(useHomeRoute())
}
</script>

<template>
  <section class="relative min-h-dvh overflow-hidden bg-surface-50 px-4 py-8 dark:bg-surface-950">
    <div class="absolute right-4 top-4">
      <AppControls />
    </div>
    <div class="mx-auto flex min-h-[calc(100dvh-4rem)] max-w-2xl items-center justify-center">
      <div
        class="relative w-full rounded-3xl border border-surface-200 bg-surface-0 px-6 py-10 text-center shadow-[0_20px_55px_rgba(0,39,67,0.08)] dark:border-surface-800 dark:bg-surface-900 sm:px-10"
      >
        <template v-if="failed">
          <p class="mx-auto max-w-lg text-base leading-6 text-surface-700 dark:text-surface-200">
            {{ t('guideAuth.failed') }}
          </p>
          <div class="mt-8 flex justify-center">
            <n-button
              type="primary"
              class="min-h-12 min-w-40"
              icon-placement="right"
              @click="goHome"
            >
              {{ t('common.goHome') }}
              <template #icon
                ><n-icon><ArrowRight /></n-icon
              ></template>
            </n-button>
          </div>
        </template>
        <div v-else class="flex flex-col items-center gap-4" role="status">
          <n-spin size="medium" />
          <p class="text-sm text-surface-600 dark:text-surface-300">
            {{ t('guideAuth.opening') }}
          </p>
        </div>
      </div>
    </div>
  </section>
</template>
