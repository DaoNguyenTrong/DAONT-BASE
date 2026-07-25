export function useMicrosoftAuth() {
  const router = useRouter()
  const { t } = useI18n()
  const { isLoading, error, run } = useApiAction()
  const showResendVerification = ref(false)

  async function handleCredential(credential: string) {
    showResendVerification.value = false

    const result = await run(() => useAuthStore().externalLogin('microsoft', { credential }), {
      onCode: {
        ExternalLoginEmailNotConfirmed: () => {
          error.value = t('auth.microsoftEmailNotConfirmed')
          showResendVerification.value = true
        },
        ExternalLoginEmailNotVerifiedByProvider: () => {
          error.value = t('auth.microsoftEmailNotVerifiedByProvider')
        },
      },
      fallbackMessage: t('auth.microsoftLoginFailed'),
    })

    if (result) {
      await router.push(useHomeRoute())
    }
  }

  return { isLoading, error, showResendVerification, handleCredential }
}
