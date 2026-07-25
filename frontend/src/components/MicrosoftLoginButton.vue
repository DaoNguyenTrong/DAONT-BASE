<script setup lang="ts">
import { PublicClientApplication } from '@azure/msal-browser'

const { t } = useI18n()
const clientId = import.meta.env.VITE_MICROSOFT_CLIENT_ID
const tenantId = import.meta.env.VITE_MICROSOFT_TENANT_ID || 'common'
const ready = ref(false)
const { error, showResendVerification, handleCredential } = useMicrosoftAuth()

let msalInstance: PublicClientApplication | null = null

onMounted(async () => {
  if (!clientId) {
    return
  }

  try {
    msalInstance = new PublicClientApplication({
      auth: {
        clientId,
        authority: `https://login.microsoftonline.com/${tenantId}`,
        // Back to the SPA root — main.ts detects an MSAL OAuth-response hash and skips
        // mounting the app/router on that load, so the hash reaches MSAL's popup monitor
        // untouched (see main.ts for why a separate static redirect page isn't used).
        redirectUri: window.location.origin,
      },
    })

    await msalInstance.initialize()
    ready.value = true
  } catch {
    error.value = t('auth.microsoftLoginFailed')
  }
})

async function signIn() {
  if (!msalInstance) {
    return
  }

  try {
    const result = await msalInstance.loginPopup({ scopes: ['openid', 'profile', 'email'] })
    await handleCredential(result.idToken)
  } catch {
    error.value = t('auth.microsoftLoginFailed')
  }
}
</script>

<template>
  <div v-if="clientId" class="space-y-3">
    <button class="ms-button w-full" @click="signIn" :disabled="!ready">
      <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 21 21">
        <rect x="1" y="1" width="9" height="9" fill="#f25022" />
        <rect x="11" y="1" width="9" height="9" fill="#7fba00" />
        <rect x="1" y="11" width="9" height="9" fill="#00a4ef" />
        <rect x="11" y="11" width="9" height="9" fill="#ffb900" />
      </svg>
      <span>{{ t('auth.continueWithMicrosoft') }}</span>
    </button>
    <n-alert v-if="error" type="error" :show-icon="false">
      {{ error }}
    </n-alert>
    <ResendVerificationForm v-if="showResendVerification" />
  </div>
</template>

<style scoped>
.ms-button {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  height: 40px;
  padding: 0 12px;
  border: 1px solid #8c8c8c;
  /* Microsoft's sign-in button redlines specify square corners — do not round. */
  border-radius: 0;
  background-color: #ffffff;
  color: #5e5e5e;
  font-family:
    'Segoe UI Semibold',
    'Segoe UI',
    Arial,
    sans-serif;
  font-size: 15px;
  font-weight: 600;
  cursor: pointer;
  user-select: none;
  transition:
    background-color 0.218s,
    border-color 0.218s;
}

.ms-button:disabled {
  cursor: default;
  opacity: 0.5;
}

.ms-button:not(:disabled):hover {
  background-color: #f2f2f2;
}

.ms-button:not(:disabled):active {
  background-color: #e6e6e6;
}
</style>
