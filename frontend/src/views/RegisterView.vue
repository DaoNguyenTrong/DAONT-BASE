<script setup lang="ts">
import type { FormInst, FormRules } from 'naive-ui'

const { t } = useI18n()
const { wordmarkSrc } = useBrandWordmark()

const formRef = ref<FormInst | null>(null)
const form = reactive({
  name: '',
  username: '',
  email: '',
  password: '',
})

const registered = ref<RegisterResult | null>(null)

const {
  isLoading: isSubmitting,
  error: submitError,
  fieldErrors,
  run,
  clearFieldError,
} = useApiAction()

const rules = computed<FormRules>(() => ({
  name: [{ required: true, message: t('auth.nameRequired'), trigger: ['input', 'blur'] }],
  username: [{ required: true, message: t('auth.usernameRequired'), trigger: ['input', 'blur'] }],
  email: [
    { required: true, message: t('auth.emailRequired'), trigger: ['input', 'blur'] },
    { type: 'email', message: t('auth.emailInvalid'), trigger: ['input', 'blur'] },
  ],
  password: [
    { required: true, message: t('auth.passwordRequired'), trigger: ['input', 'blur'] },
    { min: 8, message: t('auth.passwordTooShort'), trigger: ['input', 'blur'] },
  ],
}))

async function handleSubmit() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }

  const result = await run(
    () =>
      useAuthStore().register({
        name: form.name.trim(),
        username: form.username.trim(),
        email: form.email.trim(),
        password: form.password,
      }),
    {
      onCode: {
        AccountUsernameAlreadyExists: (detail) => {
          fieldErrors.value = {
            ...fieldErrors.value,
            username: detail ?? t('auth.usernameAlreadyExists'),
          }
        },
        AccountEmailAlreadyExists: (detail) => {
          fieldErrors.value = {
            ...fieldErrors.value,
            email: detail ?? t('auth.emailAlreadyExists'),
          }
        },
      },
      fallbackMessage: t('auth.registerFailed'),
    },
  )

  if (result) {
    registered.value = result
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
              <img :alt="t('app.name')" class="h-12 w-auto" :src="wordmarkSrc" />
            </div>
            <h1 class="text-xl font-semibold text-surface-900 dark:text-surface-0">
              {{ t('auth.registerTitle') }}
            </h1>
          </div>

          <div v-if="registered" class="mt-8 space-y-5">
            <n-alert type="success" :show-icon="false">
              {{ t('auth.registerSuccessMessage', { email: registered.email }) }}
            </n-alert>
            <ResendVerificationForm />
            <RouterLink
              class="block text-center text-sm font-medium text-primary-600 hover:underline dark:text-primary-200"
              :to="{ name: 'login' }"
            >
              {{ t('auth.backToLogin') }}
            </RouterLink>
          </div>

          <template v-else>
            <n-form
              ref="formRef"
              :model="form"
              :rules="rules"
              class="mt-8 space-y-5"
              @submit.prevent="handleSubmit"
            >
              <n-form-item
                path="name"
                :show-label="false"
                first
                :feedback="fieldErrors.name || undefined"
                :validation-status="fieldErrors.name ? 'error' : undefined"
              >
                <div class="w-full space-y-3">
                  <label
                    class="text-sm font-semibold text-surface-800 dark:text-surface-100"
                    for="name"
                  >
                    {{ t('auth.name') }}<RequiredMark />
                  </label>
                  <n-input
                    v-model:value="form.name"
                    type="text"
                    class="w-full"
                    :placeholder="t('auth.namePlaceholder')"
                    :input-props="{ id: 'name', autocomplete: 'name' }"
                    @update:value="clearFieldError('name')"
                  />
                </div>
              </n-form-item>

              <n-form-item
                path="username"
                :show-label="false"
                first
                :feedback="fieldErrors.username || undefined"
                :validation-status="fieldErrors.username ? 'error' : undefined"
              >
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
                    @update:value="clearFieldError('username')"
                  />
                </div>
              </n-form-item>

              <n-form-item
                path="email"
                :show-label="false"
                first
                :feedback="fieldErrors.email || undefined"
                :validation-status="fieldErrors.email ? 'error' : undefined"
              >
                <div class="w-full space-y-3">
                  <label
                    class="text-sm font-semibold text-surface-800 dark:text-surface-100"
                    for="email"
                  >
                    {{ t('auth.email') }}<RequiredMark />
                  </label>
                  <n-input
                    v-model:value="form.email"
                    type="text"
                    class="w-full"
                    :placeholder="t('auth.emailPlaceholder')"
                    :input-props="{ id: 'email', autocomplete: 'email' }"
                    @update:value="clearFieldError('email')"
                  />
                </div>
              </n-form-item>

              <n-form-item
                path="password"
                :show-label="false"
                first
                :feedback="fieldErrors.password || undefined"
                :validation-status="fieldErrors.password ? 'error' : undefined"
              >
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
                    :input-props="{ id: 'password', autocomplete: 'new-password' }"
                    @update:value="clearFieldError('password')"
                  />
                </div>
              </n-form-item>

              <n-alert v-if="submitError" type="error" :show-icon="false">
                {{ submitError }}
              </n-alert>

              <n-button
                type="primary"
                attr-type="submit"
                class="min-h-12 w-full!"
                :loading="isSubmitting"
                :disabled="isSubmitting"
                >{{ isSubmitting ? t('common.loading') : t('auth.registerButton') }}</n-button
              >

              <RouterLink
                class="mt-5 block text-center text-sm font-medium text-primary-600 hover:underline dark:text-primary-200"
                :to="{ name: 'login' }"
              >
                {{ t('auth.backToLogin') }}
              </RouterLink>
            </n-form>

            <SocialLoginDivider class="mt-5" />
            <GoogleLoginButton class="mt-3" />
            <MicrosoftLoginButton class="mt-3" />
          </template>
        </div>
      </div>
    </div>
  </section>
</template>
