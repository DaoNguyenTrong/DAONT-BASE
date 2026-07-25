<script setup lang="ts">
const route = useRoute()
const router = useRouter()
const { t } = useI18n()
const { wordmarkSrc } = useBrandWordmark()

const status = ref<'verifying' | 'error'>('verifying')
const { error: errorMessage, run } = useApiAction()

function extractToken(raw: unknown): string | null {
  return typeof raw === 'string' && raw.trim().length > 0 ? raw : null
}

// A watcher (not onMounted) so a query change is re-processed even if vue-router
// reuses this component instance across two /verify-email visits within the
// same SPA session (no full page reload) — e.g. two verification links opened
// back to back. onMounted alone would only ever run once per mount.
watch(
  () => route.query.token,
  async (rawToken) => {
    status.value = 'verifying'

    const token = extractToken(rawToken)

    if (!token) {
      status.value = 'error'
      errorMessage.value = t('auth.verifyEmailMissingToken')
      return
    }

    const result = await run(() => useAuthStore().verifyEmail({ token }), {
      onCode: {
        EmailVerificationTokenInvalidOrExpired: () => {
          errorMessage.value = t('auth.verifyEmailInvalidToken')
        },
      },
      fallbackMessage: t('auth.verifyEmailFailed'),
    })

    if (result) {
      await router.push(useHomeRoute())
    } else {
      status.value = 'error'
    }
  },
  { immediate: true },
)
</script>

<template>
  <section class="relative min-h-screen bg-surface-50 px-4 py-8 dark:bg-surface-950">
    <div class="absolute right-4 top-4">
      <AppControls />
    </div>
    <div class="mx-auto flex min-h-[calc(100vh-4rem)] max-w-md items-center justify-center">
      <div
        class="w-full overflow-hidden rounded-2xl border border-surface-200 bg-surface-0 dark:border-surface-800 dark:bg-surface-900"
      >
        <div class="h-1 bg-primary-500" />

        <div class="p-6 sm:p-8">
          <div class="flex justify-center">
            <img :alt="t('app.name')" class="h-12 w-auto" :src="wordmarkSrc" />
          </div>

          <div v-if="status === 'verifying'" class="mt-8 flex flex-col items-center gap-4 py-4">
            <n-spin size="large" />
            <p class="text-sm text-surface-600 dark:text-surface-300">
              {{ t('auth.verifyingEmail') }}
            </p>
          </div>

          <div v-else class="mt-8 space-y-5">
            <n-alert type="error" :show-icon="false">
              {{ errorMessage }}
            </n-alert>
            <ResendVerificationForm />
            <RouterLink
              class="block text-center text-sm font-medium text-primary-600 hover:underline dark:text-primary-200"
              :to="{ name: 'login' }"
            >
              {{ t('auth.backToLogin') }}
            </RouterLink>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>
