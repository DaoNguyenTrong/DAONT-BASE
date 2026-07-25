<script setup lang="ts">
const router = useRouter()
const { t } = useI18n()
const auth = useAuthStore()

const primaryLabel = computed(() => (auth.isAuthenticated ? t('common.goHome') : t('auth.login')))

async function handlePrimaryAction() {
  if (!auth.isAuthenticated) {
    await router.push({ name: 'login' })
    return
  }

  await router.push(useHomeRoute())
}
</script>

<template>
  <section
    class="relative min-h-screen overflow-hidden bg-surface-50 px-4 py-8 dark:bg-surface-950"
  >
    <div class="absolute right-4 top-4">
      <AppControls />
    </div>
    <div class="mx-auto flex min-h-[calc(100vh-4rem)] max-w-2xl items-center justify-center">
      <div
        class="relative w-full rounded-3xl border border-surface-200 bg-surface-0 px-6 py-10 text-center shadow-[0_20px_55px_rgba(0,39,67,0.08)] dark:border-surface-800 dark:bg-surface-900 sm:px-10"
      >
        <div
          class="mx-auto flex h-16 w-16 items-center justify-center rounded-2xl bg-primary-50 text-xl font-semibold text-primary-700 ring-1 ring-primary-100 dark:bg-primary-950/60 dark:text-primary-200 dark:ring-primary-900/70"
        >
          404
        </div>

        <div class="mt-6 space-y-3">
          <p
            class="text-sm font-semibold uppercase tracking-[0.24em] text-primary-600 dark:text-primary-200"
          >
            {{ t('errors.notFound') }}
          </p>
          <h1 class="text-3xl font-semibold tracking-tight text-surface-950 dark:text-surface-0">
            {{ t('errors.notFoundTitle') }}
          </h1>
          <p
            class="mx-auto max-w-lg text-sm leading-6 text-surface-600 dark:text-surface-300 sm:text-base"
          >
            {{ t('errors.notFoundDescription') }}
          </p>
        </div>

        <div class="mt-8 flex justify-center">
          <n-button
            type="primary"
            class="min-h-12 min-w-40"
            icon-placement="right"
            @click="handlePrimaryAction"
          >
            {{ primaryLabel }}
            <template #icon><SvgIcon name="arrow-right" /></template>
          </n-button>
        </div>
      </div>
    </div>
  </section>
</template>
