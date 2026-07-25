<script setup lang="ts">
const { t } = useI18n()
const { wordmarkSrc } = useBrandWordmark()

const EMAIL_FORMAT = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

const form = reactive({
  name: '',
  username: '',
  email: '',
  password: '',
})

const hasSubmitted = ref(false)
const registered = ref<RegisterResult | null>(null)

const {
  isLoading: isSubmitting,
  error: submitError,
  fieldErrors,
  run,
  clearFieldError,
} = useApiAction()

const nameClientError = computed(() => {
  if (!hasSubmitted.value || form.name.trim().length > 0) {
    return null
  }

  return t('auth.nameRequired')
})

const usernameClientError = computed(() => {
  if (!hasSubmitted.value || form.username.trim().length > 0) {
    return null
  }

  return t('auth.usernameRequired')
})

const emailClientError = computed(() => {
  if (!hasSubmitted.value) {
    return null
  }

  if (form.email.trim().length === 0) {
    return t('auth.emailRequired')
  }

  if (!EMAIL_FORMAT.test(form.email.trim())) {
    return t('auth.emailInvalid')
  }

  return null
})

const passwordClientError = computed(() => {
  if (!hasSubmitted.value) {
    return null
  }

  if (form.password.length === 0) {
    return t('auth.passwordRequired')
  }

  if (form.password.length < 8) {
    return t('auth.passwordTooShort')
  }

  return null
})

const nameError = computed(() => fieldErrors.value.name ?? nameClientError.value)
const usernameError = computed(() => fieldErrors.value.username ?? usernameClientError.value)
const emailError = computed(() => fieldErrors.value.email ?? emailClientError.value)
const passwordError = computed(() => fieldErrors.value.password ?? passwordClientError.value)

const hasValidationErrors = computed(() =>
  Boolean(
    nameClientError.value ||
    usernameClientError.value ||
    emailClientError.value ||
    passwordClientError.value,
  ),
)

async function handleSubmit() {
  hasSubmitted.value = true

  if (hasValidationErrors.value) {
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
            <form class="mt-8 space-y-5" @submit.prevent="handleSubmit">
              <div class="space-y-3">
                <label
                  class="text-sm font-semibold text-surface-800 dark:text-surface-100"
                  for="name"
                >
                  {{ t('accounts.name') }}<RequiredMark />
                </label>
                <n-input
                  v-model:value="form.name"
                  type="text"
                  class="w-full"
                  :status="nameError ? 'error' : undefined"
                  :placeholder="t('accounts.namePlaceholder')"
                  :input-props="{ id: 'name', autocomplete: 'name' }"
                  @update:value="clearFieldError('name')"
                />
                <n-alert v-if="nameError" type="error" :show-icon="false">
                  {{ nameError }}
                </n-alert>
              </div>

              <div class="space-y-3">
                <label
                  class="text-sm font-semibold text-surface-800 dark:text-surface-100"
                  for="username"
                >
                  {{ t('accounts.username') }}<RequiredMark />
                </label>
                <n-input
                  v-model:value="form.username"
                  type="text"
                  class="w-full"
                  :status="usernameError ? 'error' : undefined"
                  :placeholder="t('accounts.usernamePlaceholder')"
                  :input-props="{ id: 'username', autocomplete: 'username' }"
                  @update:value="clearFieldError('username')"
                />
                <n-alert v-if="usernameError" type="error" :show-icon="false">
                  {{ usernameError }}
                </n-alert>
              </div>

              <div class="space-y-3">
                <label
                  class="text-sm font-semibold text-surface-800 dark:text-surface-100"
                  for="email"
                >
                  {{ t('accounts.email') }}<RequiredMark />
                </label>
                <n-input
                  v-model:value="form.email"
                  type="text"
                  class="w-full"
                  :status="emailError ? 'error' : undefined"
                  :placeholder="t('accounts.emailPlaceholder')"
                  :input-props="{ id: 'email', autocomplete: 'email' }"
                  @update:value="clearFieldError('email')"
                />
                <n-alert v-if="emailError" type="error" :show-icon="false">
                  {{ emailError }}
                </n-alert>
              </div>

              <div class="space-y-3">
                <label
                  class="text-sm font-semibold text-surface-800 dark:text-surface-100"
                  for="password"
                >
                  {{ t('accounts.password') }}<RequiredMark />
                </label>
                <n-input
                  v-model:value="form.password"
                  type="password"
                  show-password-on="click"
                  class="w-full"
                  :status="passwordError ? 'error' : undefined"
                  :placeholder="t('accounts.passwordPlaceholder')"
                  :input-props="{ id: 'password', autocomplete: 'new-password' }"
                  @update:value="clearFieldError('password')"
                />
                <n-alert v-if="passwordError" type="error" :show-icon="false">
                  {{ passwordError }}
                </n-alert>
              </div>

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
            </form>

            <SocialLoginDivider class="mt-5" />
            <GoogleLoginButton class="mt-3" />
            <MicrosoftLoginButton class="mt-3" />
          </template>
        </div>
      </div>
    </div>
  </section>
</template>
