<script setup lang="ts">
const { t } = useI18n()

const email = ref('')
const hasSubmitted = ref(false)
const isSubmitting = ref(false)
const isSent = ref(false)
const submitError = ref<string | null>(null)

const emailError = computed(() => {
  if (!hasSubmitted.value || email.value.trim().length > 0) {
    return null
  }

  return t('auth.emailRequired')
})

async function handleSubmit() {
  hasSubmitted.value = true
  submitError.value = null

  if (isSubmitting.value || emailError.value) {
    return
  }

  isSubmitting.value = true

  try {
    await authApi.resendVerification({ email: email.value.trim() })
    isSent.value = true
  } catch (error) {
    submitError.value =
      error instanceof ApiError
        ? (error.problem.detail ?? t('auth.resendVerificationFailed'))
        : t('auth.resendVerificationFailed')
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <div class="space-y-3">
    <n-alert v-if="isSent" type="success" :show-icon="false">
      {{ t('auth.resendVerificationSuccess') }}
    </n-alert>
    <form v-else class="space-y-3" @submit.prevent="handleSubmit">
      <div class="space-y-2">
        <label
          class="text-sm font-medium text-surface-800 dark:text-surface-100"
          for="resend-verification-email"
        >
          {{ t('auth.email') }}<RequiredMark />
        </label>
        <n-input
          v-model:value="email"
          type="text"
          class="w-full"
          :status="emailError ? 'error' : undefined"
          :placeholder="t('auth.emailPlaceholder')"
          :input-props="{ id: 'resend-verification-email', autocomplete: 'email' }"
        />
        <n-alert v-if="emailError" type="error" :show-icon="false">
          {{ emailError }}
        </n-alert>
      </div>

      <n-alert v-if="submitError" type="error" :show-icon="false">
        {{ submitError }}
      </n-alert>

      <n-button
        type="primary"
        attr-type="submit"
        class="w-full"
        :loading="isSubmitting"
        :disabled="isSubmitting"
        >{{ t('auth.resendVerification') }}</n-button
      >
    </form>
  </div>
</template>
