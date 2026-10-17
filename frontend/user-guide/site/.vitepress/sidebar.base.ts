import type { Chapter } from './sidebar-types'

// [base] The base's chapters. Forks don't edit this file: they add chapters in sidebar.app.ts and
// hide hideable base chapters with product.hiddenBaseChapters.
export const baseChapters: Chapter[] = [
  {
    slug: 'getting-started',
    text: { vi: 'Bắt đầu', en: 'Getting started' },
    items: [
      { slug: 'index', text: { vi: 'Giới thiệu', en: 'Introduction' } },
      { slug: 'register', text: { vi: 'Đăng ký tài khoản', en: 'Create an account' } },
      { slug: 'verify-email', text: { vi: 'Xác minh email', en: 'Verify your email' } },
      { slug: 'sign-in', text: { vi: 'Đăng nhập', en: 'Sign in' } },
      { slug: 'sign-out', text: { vi: 'Đăng xuất', en: 'Sign out' } },
      { slug: 'interface', text: { vi: 'Làm quen giao diện', en: 'Find your way around' } },
    ],
  },
  {
    slug: 'account',
    text: { vi: 'Tài khoản', en: 'Your account' },
    hideable: true,
    items: [
      { slug: 'profile', text: { vi: 'Cập nhật hồ sơ', en: 'Update your profile' } },
      { slug: 'change-password', text: { vi: 'Đổi mật khẩu', en: 'Change your password' } },
      { slug: 'sessions', text: { vi: 'Phiên đăng nhập', en: 'Manage your sessions' } },
      {
        slug: 'push-notifications',
        text: { vi: 'Thông báo đẩy', en: 'Push notifications' },
      },
    ],
  },
  { slug: 'faq', text: { vi: 'Câu hỏi thường gặp', en: 'FAQ' } },
]
