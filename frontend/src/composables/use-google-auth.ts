export function useGoogleAuth() {
  const router = useRouter()
  const { t } = useI18n()
  const { isLoading, error, run } = useApiAction()
  const showResendVerification = ref(false)

  async function handleCredential(credential: string) {
    showResendVerification.value = false

    const result = await run(() => useAuthStore().externalLogin('google', { credential }), {
      onCode: {
        ExternalLoginEmailNotConfirmed: () => {
          error.value = t('auth.googleEmailNotConfirmed')
          showResendVerification.value = true
        },
        ExternalLoginEmailNotVerifiedByProvider: () => {
          error.value = t('auth.googleEmailNotVerifiedByProvider')
        },
      },
      fallbackMessage: t('auth.googleLoginFailed'),
    })

    if (result) {
      await router.push(useHomeRoute())
    }
  }

  return { isLoading, error, showResendVerification, handleCredential }
}
