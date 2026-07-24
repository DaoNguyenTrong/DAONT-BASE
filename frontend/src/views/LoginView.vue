<script setup lang="ts">
const router = useRouter()
const { t } = useI18n()

const username = ref('')
const password = ref('')
const keepLogin = ref(getKeepLoginPreference(true))
const hasSubmitted = ref(false)
const isSubmitting = ref(false)
const submitError = ref<string | null>(null)
const showResendVerification = ref(false)

const usernameError = computed(() => {
  if (!hasSubmitted.value || username.value.trim().length > 0) {
    return null
  }

  return t('auth.usernameRequired')
})

const passwordError = computed(() => {
  if (!hasSubmitted.value || password.value.length > 0) {
    return null
  }

  return t('auth.passwordRequired')
})

const hasValidationErrors = computed(() => Boolean(usernameError.value || passwordError.value))

async function handleSubmit() {
  hasSubmitted.value = true
  submitError.value = null
  showResendVerification.value = false

  if (isSubmitting.value || hasValidationErrors.value) {
    return
  }

  isSubmitting.value = true

  try {
    await authApi.login({
      username: username.value.trim(),
      password: password.value,
      keepLoggedIn: keepLogin.value,
    })
    setKeepLoginPreference(keepLogin.value)
    await router.push(useHomeRoute())
  } catch (error) {
    if (error instanceof ApiError) {
      if (error.problem.code === 'EmailNotConfirmed') {
        submitError.value = t('auth.emailNotConfirmed')
        showResendVerification.value = true
      } else if (error.status === 401) {
        submitError.value = t('auth.invalidCredentials')
      } else {
        submitError.value = error.problem.detail ?? t('auth.loginFailed')
      }
    } else {
      submitError.value = t('auth.loginFailed')
    }
  } finally {
    isSubmitting.value = false
  }
}
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
          <div class="space-y-4 text-center">
            <div class="flex justify-center">
              <img :alt="t('app.name')" class="h-14 w-14" src="/icons/android-chrome-192x192.png" />
            </div>
            <div class="space-y-1">
              <h1 class="text-xl font-semibold text-surface-900 dark:text-surface-0">
                {{ t('app.name') }}
              </h1>
            </div>
          </div>

          <form class="mt-8 space-y-5" @submit.prevent="handleSubmit">
            <div class="space-y-3">
              <label
                class="text-sm font-semibold text-surface-800 dark:text-surface-100"
                for="username"
              >
                {{ t('auth.username') }}<RequiredMark />
              </label>
              <n-input
                v-model:value="username"
                type="text"
                class="w-full"
                :status="usernameError ? 'error' : undefined"
                :placeholder="t('auth.usernamePlaceholder')"
                :input-props="{ id: 'username', autocomplete: 'username' }"
              />
              <n-alert v-if="usernameError" type="error" :show-icon="false">
                {{ usernameError }}
              </n-alert>
            </div>

            <div class="space-y-3">
              <label
                class="text-sm font-semibold text-surface-800 dark:text-surface-100"
                for="password"
              >
                {{ t('auth.password') }}<RequiredMark />
              </label>
              <n-input
                v-model:value="password"
                type="password"
                show-password-on="click"
                class="w-full"
                :status="passwordError ? 'error' : undefined"
                :placeholder="t('auth.passwordPlaceholder')"
                :input-props="{ id: 'password', autocomplete: 'current-password' }"
              />
              <n-alert v-if="passwordError" type="error" :show-icon="false">
                {{ passwordError }}
              </n-alert>
            </div>

            <div class="flex items-end gap-2 rounded-2xl px-4 dark:border-surface-800">
              <n-checkbox v-model:checked="keepLogin">{{ t('auth.keepLogin') }}</n-checkbox>
            </div>

            <n-alert v-if="submitError" type="error" :show-icon="false">
              {{ submitError }}
            </n-alert>

            <ResendVerificationForm v-if="showResendVerification" />

            <n-button
              type="primary"
              attr-type="submit"
              class="min-h-12 w-full!"
              :loading="isSubmitting"
              :disabled="isSubmitting"
              >{{ isSubmitting ? t('common.loading') : t('auth.loginButton') }}</n-button
            >

            <RouterLink
              class="mt-5 block text-center text-sm font-medium text-primary-600 hover:underline dark:text-primary-400"
              :to="{ name: 'register' }"
            >
              {{ t('auth.registerLink') }}
            </RouterLink>
          </form>

          <GoogleLoginButton class="mt-5" />
        </div>
      </div>
    </div>
  </section>
</template>
