/** Returns a function that asks for confirmation, then logs the account out. */
export function useConfirmLogout(): () => void {
  const { t } = useI18n()

  return () => {
    requestConfirmationNaive({
      message: t('auth.logoutConfirmMessage'),
      header: t('auth.logout'),
      acceptLabel: t('common.confirm'),
      rejectLabel: t('common.cancel'),
      accept: async () => {
        // Ends in a full page reload to login (see the auth store's logout).
        await useAuthStore().logout()
      },
    })
  }
}
