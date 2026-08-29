<script setup lang="ts">
import type { FormInst, FormRules } from 'naive-ui'

const router = useRouter()
const route = useRoute()
const { t } = useI18n()

// Set by the router guard when it bounces an unauthenticated visitor here from a
// protected route; validated to an app-internal path before we follow it.
function goToDestination() {
  return router.push(safeRedirectTarget(route.query.redirect) ?? useHomeRoute())
}
const { wordmarkSrc } = useBrandWordmark()

const formRef = ref<FormInst | null>(null)
const form = reactive({
  username: '',
  password: '',
})
const keepLogin = ref(getKeepLoginPreference(true))
const isSubmitting = ref(false)
const submitError = ref<string | null>(null)
const showResendVerification = ref(false)

const rules = computed<FormRules>(() => ({
  username: [{ required: true, message: t('auth.usernameRequired'), trigger: ['input', 'blur'] }],
  password: [{ required: true, message: t('auth.passwordRequired'), trigger: ['input', 'blur'] }],
}))

async function handleSubmit() {
  submitError.value = null
  showResendVerification.value = false

  try {
    await formRef.value?.validate()
  } catch {
    return
  }

  if (isSubmitting.value) {
    return
  }

  isSubmitting.value = true

  try {
    await useAuthStore().login({
      username: form.username.trim(),
      password: form.password,
      keepLoggedIn: keepLogin.value,
    })
    setKeepLoginPreference(keepLogin.value)
    await goToDestination()
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
          <div class="flex justify-center">
            <img :alt="t('app.name')" class="h-12 w-auto" :src="wordmarkSrc" />
          </div>

          <n-form
            ref="formRef"
            :model="form"
            :rules="rules"
            class="mt-8 space-y-5"
            @submit.prevent="handleSubmit"
          >
            <n-form-item path="username" :show-label="false" first>
              <div class="w-full space-y-3">
                <label
                  class="text-sm font-semibold text-surface-800 dark:text-surface-100"
                  for="username"
                >
                  {{ t('auth.username') }}<RequiredMark />
                </label>
                <n-input
                  v-model:value="form.username"
                  type="text"
                  class="w-full"
                  :placeholder="t('auth.usernamePlaceholder')"
                  :input-props="{ id: 'username', autocomplete: 'username' }"
                />
              </div>
            </n-form-item>

            <n-form-item path="password" :show-label="false" first>
              <div class="w-full space-y-3">
                <label
                  class="text-sm font-semibold text-surface-800 dark:text-surface-100"
                  for="password"
                >
                  {{ t('auth.password') }}<RequiredMark />
                </label>
                <n-input
                  v-model:value="form.password"
                  type="password"
                  show-password-on="click"
                  class="w-full"
                  :placeholder="t('auth.passwordPlaceholder')"
                  :input-props="{ id: 'password', autocomplete: 'current-password' }"
                />
              </div>
            </n-form-item>

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
              class="mt-5 block text-center text-sm font-medium text-primary-600 hover:underline dark:text-primary-200"
              :to="{ name: 'register' }"
            >
              {{ t('auth.registerLink') }}
            </RouterLink>
          </n-form>

          <SocialLoginDivider class="mt-5" />
          <GoogleLoginButton class="mt-3" />
          <MicrosoftLoginButton class="mt-3" />
        </div>
      </div>
    </div>
  </section>
</template>
