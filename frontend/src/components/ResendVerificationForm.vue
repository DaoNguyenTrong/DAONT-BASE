<script setup lang="ts">
import type { FormInst, FormRules } from 'naive-ui'

const { t } = useI18n()

const formRef = ref<FormInst | null>(null)
const form = reactive({ email: '' })
const isSubmitting = ref(false)
const isSent = ref(false)
const submitError = ref<string | null>(null)

const rules = computed<FormRules>(() => ({
  email: [
    { required: true, message: t('auth.emailRequired'), trigger: ['input', 'blur'] },
    { type: 'email', message: t('auth.emailInvalid'), trigger: ['input', 'blur'] },
  ],
}))

async function handleSubmit() {
  submitError.value = null

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
    await useAuthStore().resendVerification({ email: form.email.trim() })
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
    <n-form
      v-else
      ref="formRef"
      :model="form"
      :rules="rules"
      class="space-y-3"
      @submit.prevent="handleSubmit"
    >
      <n-form-item path="email" :show-label="false" first>
        <div class="w-full space-y-2">
          <label
            class="text-sm font-medium text-surface-800 dark:text-surface-100"
            for="resend-verification-email"
          >
            {{ t('auth.email') }}<RequiredMark />
          </label>
          <n-input
            v-model:value="form.email"
            type="text"
            class="w-full"
            :placeholder="t('auth.emailPlaceholder')"
            :input-props="{ id: 'resend-verification-email', autocomplete: 'email' }"
          />
        </div>
      </n-form-item>

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
    </n-form>
  </div>
</template>
