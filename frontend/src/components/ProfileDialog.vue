<script setup lang="ts">
import type {
  ChangePasswordRequest,
  ProfileDto,
  ProfileUpdateRequest,
  SessionDto,
} from '@/api/types'
import { formatDeviceInfo } from '@/lib/format-device-info'

const visible = defineModel<boolean>('visible', { required: true })

const { t, locale } = useI18n()

const loading = ref(false)
const savingProfile = ref(false)
const savingPassword = ref(false)
const loadError = ref<string | null>(null)
const profileErrors = ref<Record<string, string>>({})
const passwordErrors = ref<Record<string, string>>({})
const activeTab = ref<'personalInfo' | 'changePassword' | 'sessions'>('personalInfo')
const hasPassword = ref(true)

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
    const data = await profileApi.getProfile()
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
  savingProfile.value = true
  profileErrors.value = {}
  try {
    const updated = await profileApi.updateProfile({
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
  savingPassword.value = true
  passwordErrors.value = {}
  try {
    await profileApi.changePassword({ ...passwordForm })
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
    sessions.value = await authApi.getSessions()
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
        await authApi.revokeSession(session.id)
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
        await authApi.revokeOtherSessions()
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
        <form class="grid gap-3 pt-3 sm:grid-cols-2" @submit.prevent="updateProfile">
          <div class="space-y-1.5">
            <label class="text-sm font-medium text-surface-700 dark:text-surface-200">
              {{ t('accounts.name') }}<RequiredMark />
            </label>
            <n-input
              v-model:value="profileForm.name"
              type="text"
              class="w-full"
              :placeholder="t('accounts.namePlaceholder')"
              :status="profileErrors.name ? 'error' : undefined"
            />
            <small v-if="profileErrors.name" class="text-red-500">{{ profileErrors.name }}</small>
          </div>

          <div class="space-y-1.5">
            <label class="text-sm font-medium text-surface-700 dark:text-surface-200">
              {{ t('accounts.email') }}<RequiredMark />
            </label>
            <n-input
              v-model:value="profileForm.email"
              type="text"
              class="w-full"
              :placeholder="t('accounts.emailPlaceholder')"
              :status="profileErrors.email ? 'error' : undefined"
              :input-props="{ type: 'email' }"
            />
            <small v-if="profileErrors.email" class="text-red-500">{{ profileErrors.email }}</small>
          </div>

          <div class="space-y-1.5">
            <label class="text-sm font-medium text-surface-700 dark:text-surface-200">
              {{ t('accounts.phone') }}
            </label>
            <n-input
              v-model:value="profileForm.phone"
              type="text"
              class="w-full"
              :placeholder="t('accounts.phonePlaceholder')"
              :status="profileErrors.phone ? 'error' : undefined"
            />
            <small v-if="profileErrors.phone" class="text-red-500">{{ profileErrors.phone }}</small>
          </div>

          <div class="space-y-1.5">
            <label class="text-sm font-medium text-surface-700 dark:text-surface-200">
              {{ t('accounts.position') }}
            </label>
            <n-input
              v-model:value="profileForm.position"
              type="text"
              class="w-full"
              :placeholder="t('accounts.positionPlaceholder')"
              :status="profileErrors.position ? 'error' : undefined"
            />
            <small v-if="profileErrors.position" class="text-red-500">{{
              profileErrors.position
            }}</small>
          </div>

          <div class="space-y-1.5 sm:col-span-2">
            <label class="text-sm font-medium text-surface-700 dark:text-surface-200">
              {{ t('accounts.address') }}
            </label>
            <n-input
              v-model:value="profileForm.address"
              type="text"
              class="w-full"
              :placeholder="t('accounts.addressPlaceholder')"
              :status="profileErrors.address ? 'error' : undefined"
            />
            <small v-if="profileErrors.address" class="text-red-500">{{
              profileErrors.address
            }}</small>
          </div>

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
        </form>
      </n-tab-pane>

      <n-tab-pane v-if="hasPassword" name="changePassword" :tab="t('profile.changePassword')">
        <form class="grid gap-3 pt-3 sm:grid-cols-2" @submit.prevent="changePassword">
          <div class="space-y-1.5">
            <label class="text-sm font-medium text-surface-700 dark:text-surface-200">
              {{ t('profile.currentPassword') }}<RequiredMark />
            </label>
            <n-input
              v-model:value="passwordForm.currentPassword"
              type="password"
              show-password-on="click"
              class="w-full"
              :placeholder="t('profile.currentPasswordPlaceholder')"
              :status="passwordErrors.currentpassword ? 'error' : undefined"
              :input-props="{ autocomplete: 'current-password' }"
            />
            <small v-if="passwordErrors.currentpassword" class="text-red-500">
              {{ passwordErrors.currentpassword }}
            </small>
          </div>

          <div class="space-y-1.5">
            <label class="text-sm font-medium text-surface-700 dark:text-surface-200">
              {{ t('profile.newPassword') }}<RequiredMark />
            </label>
            <n-input
              v-model:value="passwordForm.newPassword"
              type="password"
              show-password-on="click"
              class="w-full"
              :placeholder="t('profile.newPasswordPlaceholder')"
              :status="passwordErrors.newpassword ? 'error' : undefined"
              :input-props="{ autocomplete: 'new-password' }"
            />
            <small v-if="passwordErrors.newpassword" class="text-red-500">
              {{ passwordErrors.newpassword }}
            </small>
          </div>

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
        </form>
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
              <template #icon><SvgIcon name="sign-out" /></template>
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
                <template #icon><SvgIcon name="sign-out" /></template>
              </n-button>
            </li>
          </ul>
        </div>
      </n-tab-pane>
    </n-tabs>
  </n-modal>
</template>
