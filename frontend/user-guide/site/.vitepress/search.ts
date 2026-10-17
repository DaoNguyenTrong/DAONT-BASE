import type { DefaultTheme } from 'vitepress'

// Strips Vietnamese diacritics at index and query time alike (MiniSearch uses one processTerm for
// both), so "ha noi" finds "Hà Nội". VitePress serialises this function to source text and rebuilds
// it in the browser: it must stay self-contained.
export function processTerm(term: string): string {
  return term.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd')
}

export const search: DefaultTheme.Config['search'] = {
  provider: 'local',
  options: {
    miniSearch: { options: { processTerm } },
    locales: {
      root: {
        translations: {
          button: { buttonText: 'Tìm kiếm', buttonAriaLabel: 'Tìm kiếm' },
          modal: {
            displayDetails: 'Hiện chi tiết',
            resetButtonTitle: 'Xoá từ khoá',
            backButtonTitle: 'Đóng tìm kiếm',
            noResultsText: 'Không tìm thấy kết quả cho',
            footer: {
              selectText: 'để mở',
              selectKeyAriaLabel: 'Enter',
              navigateText: 'để di chuyển',
              navigateUpKeyAriaLabel: 'Mũi tên lên',
              navigateDownKeyAriaLabel: 'Mũi tên xuống',
              closeText: 'để đóng',
              closeKeyAriaLabel: 'Esc',
            },
          },
        },
      },
    },
  },
}
