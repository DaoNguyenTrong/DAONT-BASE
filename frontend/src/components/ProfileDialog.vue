<script setup lang="ts">
import { Logout } from '@vicons/tabler'
import type { FormInst, FormRules } from 'naive-ui'
import { getProfile } from '@/api/generated/profile/profile'
import type {
  ChangePasswordRequest,
  ProfileDto,
  ProfileUpdateRequest,
  SessionDto,
} from '@/api/types'
import { formatDeviceInfo } from '@/lib/format-device-info'

const visible = defineModel<boolean>('visible', { required: true })

const { t, locale } = useI18n()
const profileClient = getProfile()
const authStore = useAuthStore()

const loading = ref(false)
const savingProfile = ref(false)
const savingPassword = ref(false)
const loadError = ref<string | null>(null)
const profileFormRef = ref<FormInst | null>(null)
const passwordFormRef = ref<FormInst | null>(null)
const profileErrors = ref<Record<string, string>>({})
const passwordErrors = ref<Record<string, string>>({})
const activeTab = ref<'personalInfo' | 'changePassword' | 'sessions' | 'notifications'>(
  'personalInfo',
)
const hasPassword = ref(true)

const push = usePushNotifications()

async function togglePush(enabled: boolean) {
  if (enabled) {
    await push.subscribe()
  } else {
    await push.unsubscribe()
  }
}

const profileRules = computed<FormRules>(() => ({
  name: [{ required: true, message: t('profile.nameRequired'), trigger: ['input', 'blur'] }],
  email: [
    { required: true, message: t('profile.emailRequired'), trigger: ['input', 'blur'] },
    { type: 'email', message: t('profile.emailInvalid'), trigger: ['input', 'blur'] },
  ],
}))

const passwordRules = computed<FormRules>(() => ({
  currentPassword: [
    { required: true, message: t('profile.currentPasswordRequired'), trigger: ['input', 'blur'] },
  ],
  newPassword: [
    { required: true, message: t('profile.newPasswordRequired'), trigger: ['input', 'blur'] },
    { min: 8, message: t('profile.newPasswordTooShort'), trigger: ['input', 'blur'] },
  ],
}))

const sessions = ref<SessionDto[]>([])
const sessionsLoading = ref(true)
const revokingSessionId = ref<number | null>(null)
const revokingOthers = ref(false)
const hasOtherSessions = computed(() => sessions.value.some((session) => !session.isCurrent))

const profileForm = reactive<ProfileUpdateRequest>({
  name: '',
  phone: '',
  position: '',
  address: '',
  email: '',
})

const passwordForm = reactive<ChangePasswordRequest>({
  currentPassword: '',
  newPassword: '',
})

function fillProfileForm(data: ProfileDto) {
  profileForm.name = data.name
  profileForm.phone = data.phone ?? ''
  profileForm.position = data.position ?? ''
  profileForm.address = data.address ?? ''
  profileForm.email = data.email
  hasPassword.value = data.hasPassword
}

async function loadProfile() {
  loading.value = true
  loadError.value = null
  try {
    const data = await profileClient.profileGet()
    fillProfileForm(data)
  } catch (error) {
    loadError.value =
      error instanceof ApiError
        ? (error.problem.detail ?? error.problem.title)
        : t('errors.requestFailed')
  } finally {
    loading.value = false
  }
}

async function updateProfile() {
  if (savingProfile.value) return

  try {
    await profileFormRef.value?.validate()
  } catch {
    return
  }

  savingProfile.value = true
  profileErrors.value = {}
  try {
    const updated = await profileClient.profileUpdate({
      name: profileForm.name.trim(),
      phone: profileForm.phone?.trim() || null,
      position: profileForm.position?.trim() || null,
      address: profileForm.address?.trim() || null,
      email: profileForm.email.trim(),
    })
    fillProfileForm(updated)
    showSuccessMessage(t('profile.updateSuccess'))
  } catch (error) {
    if (error instanceof ApiError) {
      profileErrors.value = mapValidationErrors(error.problem.errors)
      showErrorMessage(error.problem.title, error.problem.detail ?? t('errors.requestFailed'))
    } else {
      showErrorMessage(t('errors.requestFailed'))
    }
  } finally {
    savingProfile.value = false
  }
}

async function changePassword() {
  if (savingPassword.value) return

  try {
    await passwordFormRef.value?.validate()
  } catch {
    return
  }

  savingPassword.value = true
  passwordErrors.value = {}
  try {
    await profileClient.profileChangePassword({ ...passwordForm })
    passwordForm.currentPassword = ''
    passwordForm.newPassword = ''
    showSuccessMessage(t('profile.passwordChanged'))
  } catch (error) {
    if (error instanceof ApiError) {
      passwordErrors.value = mapValidationErrors(error.problem.errors)
      if (error.status === 401) {
        passwordErrors.value.currentpassword = error.problem.detail ?? error.problem.title
      } else {
        showErrorMessage(error.problem.title, error.problem.detail ?? t('errors.requestFailed'))
      }
    } else {
      showErrorMessage(t('errors.requestFailed'))
    }
  } finally {
    savingPassword.value = false
  }
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

function reportSessionError(error: unknown) {
  if (error instanceof ApiError) {
    showErrorMessage(error.problem.title, error.problem.detail ?? t('errors.requestFailed'))
  } else {
    showErrorMessage(t('errors.requestFailed'))
  }
}

async function loadSessions() {
  sessionsLoading.value = true
  try {
    sessions.value = await authStore.getSessions()
  } catch (error) {
    reportSessionError(error)
  } finally {
    sessionsLoading.value = false
  }
}

function confirmRevokeSession(session: SessionDto) {
  requestConfirmation({
    header: t('common.confirm'),
    message: t('profile.signOutDeviceConfirm'),
    rejectLabel: t('common.cancel'),
    acceptLabel: t('common.confirm'),
    accept: async () => {
      revokingSessionId.value = session.id
      try {
        await authStore.revokeSession(session.id)
        sessions.value = sessions.value.filter((s) => s.id !== session.id)
        showSuccessMessage(t('profile.deviceSignedOut'))
      } catch (error) {
        reportSessionError(error)
      } finally {
        revokingSessionId.value = null
      }
    },
  })
}

function confirmRevokeOtherSessions() {
  requestConfirmation({
    header: t('common.confirm'),
    message: t('profile.signOutOtherDevicesConfirm'),
    rejectLabel: t('common.cancel'),
    acceptLabel: t('common.confirm'),
    accept: async () => {
      revokingOthers.value = true
      try {
        await authStore.revokeOtherSessions()
        sessions.value = sessions.value.filter((session) => session.isCurrent)
        showSuccessMessage(t('profile.otherDevicesSignedOut'))
      } catch (error) {
        reportSessionError(error)
      } finally {
        revokingOthers.value = false
      }
    },
  })
}

watch(visible, (open) => {
  if (open) {
    activeTab.value = 'personalInfo'
    void loadProfile()
    void loadSessions()
  }
})
</script>

<template>
  <n-modal
    v-model:show="visible"
    preset="card"
    :title="t('profile.title')"
    :aria-label="t('profile.title')"
    class="w-full max-w-2xl"
  >
    <div v-if="loading" class="space-y-4 pt-2">
      <n-skeleton height="2.5rem" style="border-radius: 0.5rem" />
      <n-skeleton height="2.5rem" style="border-radius: 0.5rem" />
      <n-skeleton height="2.5rem" style="border-radius: 0.5rem" />
      <n-skeleton height="2.5rem" style="border-radius: 0.5rem" />
    </div>

    <n-alert v-else-if="loadError" type="error" :show-icon="false" class="mt-2">
      {{ loadError }}
    </n-alert>

    <n-tabs v-else v-model:value="activeTab" type="line" class="pt-1">
      <n-tab-pane name="personalInfo" :tab="t('profile.personalInfo')">
        <n-form
          ref="profileFormRef"
          :model="profileForm"
          :rules="profileRules"
          class="grid gap-3 pt-3 sm:grid-cols-2"
          @submit.prevent="updateProfile"
        >
          <n-form-item
            path="name"
            :show-label="false"
            first
            :feedback="profileErrors.name || undefined"
            :validation-status="profileErrors.name ? 'error' : undefined"
          >
            <div class="w-full space-y-1.5">
              <label class="text-sm font-medium text-surface-700 dark:text-surface-200">
                {{ t('profile.name') }}<RequiredMark />
              </label>
              <n-input
                v-model:value="profileForm.name"
                type="text"
                class="w-full"
                :placeholder="t('profile.namePlaceholder')"
              />
            </div>
          </n-form-item>

          <n-form-item
            path="email"
            :show-label="false"
            first
            :feedback="profileErrors.email || undefined"
            :validation-status="profileErrors.email ? 'error' : undefined"
          >
            <div class="w-full space-y-1.5">
              <label class="text-sm font-medium text-surface-700 dark:text-surface-200">
                {{ t('profile.email') }}<RequiredMark />
              </label>
              <n-input
                v-model:value="profileForm.email"
                type="text"
                class="w-full"
                :placeholder="t('profile.emailPlaceholder')"
                :input-props="{ type: 'email' }"
              />
            </div>
          </n-form-item>

          <n-form-item
            :show-label="false"
            :feedback="profileErrors.phone || undefined"
            :validation-status="profileErrors.phone ? 'error' : undefined"
          >
            <div class="w-full space-y-1.5">
              <label class="text-sm font-medium text-surface-700 dark:text-surface-200">
                {{ t('profile.phone') }}
              </label>
              <n-input
                v-model:value="profileForm.phone"
                type="text"
                class="w-full"
                :placeholder="t('profile.phonePlaceholder')"
              />
            </div>
          </n-form-item>

          <n-form-item
            :show-label="false"
            :feedback="profileErrors.position || undefined"
            :validation-status="profileErrors.position ? 'error' : undefined"
          >
            <div class="w-full space-y-1.5">
              <label class="text-sm font-medium text-surface-700 dark:text-surface-200">
                {{ t('profile.position') }}
              </label>
              <n-input
                v-model:value="profileForm.position"
                type="text"
                class="w-full"
                :placeholder="t('profile.positionPlaceholder')"
              />
            </div>
          </n-form-item>

          <n-form-item
            class="sm:col-span-2"
            :show-label="false"
            :feedback="profileErrors.address || undefined"
            :validation-status="profileErrors.address ? 'error' : undefined"
          >
            <div class="w-full space-y-1.5">
              <label class="text-sm font-medium text-surface-700 dark:text-surface-200">
                {{ t('profile.address') }}
              </label>
              <n-input
                v-model:value="profileForm.address"
                type="text"
                class="w-full"
                :placeholder="t('profile.addressPlaceholder')"
              />
            </div>
          </n-form-item>

          <div class="sm:col-span-2">
            <n-button
              type="primary"
              attr-type="submit"
              :loading="savingProfile"
              class="min-h-11 w-full"
            >
              {{ t('common.save') }}
            </n-button>
          </div>
        </n-form>
      </n-tab-pane>

      <n-tab-pane v-if="hasPassword" name="changePassword" :tab="t('profile.changePassword')">
        <n-form
          ref="passwordFormRef"
          :model="passwordForm"
          :rules="passwordRules"
          class="grid gap-3 pt-3 sm:grid-cols-2"
          @submit.prevent="changePassword"
        >
          <n-form-item
            path="currentPassword"
            :show-label="false"
            first
            :feedback="passwordErrors.currentpassword || undefined"
            :validation-status="passwordErrors.currentpassword ? 'error' : undefined"
          >
            <div class="w-full space-y-1.5">
              <label class="text-sm font-medium text-surface-700 dark:text-surface-200">
                {{ t('profile.currentPassword') }}<RequiredMark />
              </label>
              <n-input
                v-model:value="passwordForm.currentPassword"
                type="password"
                show-password-on="click"
                class="w-full"
                :placeholder="t('profile.currentPasswordPlaceholder')"
                :input-props="{ autocomplete: 'current-password' }"
              />
            </div>
          </n-form-item>

          <n-form-item
            path="newPassword"
            :show-label="false"
            first
            :feedback="passwordErrors.newpassword || undefined"
            :validation-status="passwordErrors.newpassword ? 'error' : undefined"
          >
            <div class="w-full space-y-1.5">
              <label class="text-sm font-medium text-surface-700 dark:text-surface-200">
                {{ t('profile.newPassword') }}<RequiredMark />
              </label>
              <n-input
                v-model:value="passwordForm.newPassword"
                type="password"
                show-password-on="click"
                class="w-full"
                :placeholder="t('profile.newPasswordPlaceholder')"
                :input-props="{ autocomplete: 'new-password' }"
              />
            </div>
          </n-form-item>

          <div class="sm:col-span-2">
            <n-button
              type="primary"
              attr-type="submit"
              :loading="savingPassword"
              class="min-h-11 w-full"
            >
              {{ t('profile.changePassword') }}
            </n-button>
          </div>
        </n-form>
      </n-tab-pane>

      <n-tab-pane name="sessions" :tab="t('profile.sessionsTitle')">
        <div class="pt-3">
          <div class="flex items-center justify-end">
            <n-button
              v-if="hasOtherSessions"
              text
              type="error"
              class="min-h-11"
              :loading="revokingOthers"
              @click="confirmRevokeOtherSessions"
            >
              <template #icon
                ><n-icon><Logout /></n-icon
              ></template>
              {{ t('profile.signOutOtherDevices') }}
            </n-button>
          </div>

          <div v-if="sessionsLoading" class="mt-3 space-y-2">
            <n-skeleton v-for="i in 2" :key="i" height="4rem" style="border-radius: 0.75rem" />
          </div>

          <p
            v-else-if="sessions.length === 0"
            class="mt-3 text-sm text-surface-500 dark:text-surface-400"
          >
            {{ t('profile.sessionsEmpty') }}
          </p>

          <ul v-else class="mt-3 space-y-2">
            <li
              v-for="session in sessions"
              :key="session.id"
              class="flex items-start justify-between gap-3 rounded-xl border border-surface-200 p-3 dark:border-surface-800"
            >
              <div class="min-w-0 flex-1">
                <p class="truncate text-sm font-medium text-surface-800 dark:text-surface-100">
                  {{ formatDeviceInfo(session.deviceInfo) ?? t('profile.unknownDevice') }}
                </p>
                <div class="mt-1.5 flex flex-wrap gap-1.5">
                  <n-tag v-if="session.isCurrent" type="success" size="small" :bordered="false">
                    {{ t('profile.currentDevice') }}
                  </n-tag>
                  <n-tag v-if="session.isPersistent" size="small" :bordered="false">
                    {{ t('profile.rememberLogin') }}
                  </n-tag>
                </div>
                <p class="mt-1.5 truncate text-xs text-surface-500 dark:text-surface-400">
                  {{ t('profile.lastActive') }}: {{ formatDate(session.lastActiveAt) }} &middot; IP:
                  {{ session.ipAddress ?? t('profile.unknownIp') }}
                </p>
                <p class="mt-0.5 text-xs text-surface-500 dark:text-surface-400">
                  {{ t('profile.sessionLoginAt') }}: {{ formatDate(session.loginAt) }}
                </p>
              </div>
              <n-button
                v-if="!session.isCurrent"
                text
                circle
                type="error"
                class="min-h-11 min-w-11 shrink-0"
                :aria-label="t('profile.signOutDevice')"
                :loading="revokingSessionId === session.id"
                @click="confirmRevokeSession(session)"
              >
                <template #icon
                  ><n-icon><Logout /></n-icon
                ></template>
              </n-button>
            </li>
          </ul>
        </div>
      </n-tab-pane>

      <n-tab-pane name="notifications" :tab="t('pushNotifications.title')">
        <div class="pt-3">
          <p v-if="!push.isSupported.value" class="text-sm text-surface-500 dark:text-surface-400">
            {{ t('pushNotifications.notSupported') }}
          </p>
          <div v-else class="flex items-start justify-between gap-3">
            <div class="min-w-0 flex-1">
              <p class="text-sm font-medium text-surface-800 dark:text-surface-100">
                {{ t('pushNotifications.enable') }}
              </p>
              <p class="mt-1 text-xs text-surface-500 dark:text-surface-400">
                {{ t('pushNotifications.description') }}
              </p>
              <p
                v-if="push.permission.value === 'denied'"
                class="mt-1 text-xs text-error-600 dark:text-error-400"
              >
                {{ t('pushNotifications.permissionDenied') }}
              </p>
            </div>
            <n-switch
              :value="push.isSubscribed.value"
              :loading="push.isLoading.value"
              :disabled="push.permission.value === 'denied'"
              @update:value="togglePush"
            />
          </div>
        </div>
      </n-tab-pane>
    </n-tabs>
  </n-modal>
</template>
