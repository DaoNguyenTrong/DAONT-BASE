import { defineComponent, h } from 'vue'
import { useData, withBase, type Theme } from 'vitepress'
// The variant without Inter: the guide uses Roboto like the app, self-hosted (no font CDN).
import DefaultTheme from 'vitepress/theme-without-fonts'
import '@fontsource/roboto/400.css'
import '@fontsource/roboto/500.css'
import '@fontsource/roboto/700.css'
import './brand-tokens.generated.css'
import './brand.css'
import './custom.css'
import { createAuthGuard, reloadThroughServer } from './auth-guard'
import AppLink from './components/AppLink.vue'
import HeroTitle from './components/HeroTitle.vue'
import HomeQuickStart from './components/HomeQuickStart.vue'
import PermissionNote from './components/PermissionNote.vue'
import { useSidebarAccordion } from './sidebar-accordion'
import { localeOf } from '../paths'
import { strings } from './strings'

const product = __GUIDE__

// Two wordmarks because the text colour is part of the image; custom.css picks one per background.
function brand(subtitle: string) {
  return h('span', { class: 'guide-brand' }, [
    h('img', {
      class: 'guide-brand__logo guide-brand__logo--color',
      src: withBase(`/brand/${product.brand.wordmarkLight}`),
      alt: product.publisher,
    }),
    h('img', {
      class: 'guide-brand__logo guide-brand__logo--white',
      src: withBase(`/brand/${product.brand.wordmarkDark}`),
      alt: '',
      'aria-hidden': 'true',
    }),
    h('span', { class: 'guide-brand__sub' }, subtitle),
  ])
}

function versionBadge(placement: string, title: string) {
  return h(
    'span',
    { class: ['guide-version', placement], title: `${title} ${product.version}` },
    product.version,
  )
}

export default {
  extends: DefaultTheme,
  // The version badge sits by the site title on narrow screens; from 960px the title block is capped
  // at the sidebar's width, so the badge moves to the right-hand group (see custom.css).
  Layout: defineComponent(() => {
    useSidebarAccordion()
    const { page } = useData()
    return () => {
      const text = strings[localeOf(page.value.relativePath)]
      return h(DefaultTheme.Layout, null, {
        'nav-bar-title-before': () => brand(`${text.guide} ${product.name}`),
        'nav-bar-title-after': () => versionBadge('guide-version--title', text.versionTitle),
        'nav-bar-content-before': () => h(AppLink),
        'nav-bar-content-after': () => versionBadge('guide-version--nav', text.versionTitle),
        'home-hero-info': () => h(HeroTitle),
        'home-hero-actions-after': () => h(AppLink, { variant: 'button' }),
        'home-features-after': () => h(HomeQuickStart),
      })
    }
  }),
  enhanceApp({ app, router }) {
    // Inserted under each page's title by the guide-permission-note markdown rule.
    app.component('PermissionNote', PermissionNote)
    // onBeforePageLoad, not onBeforeRouteChange: the Back button loads pages without the latter.
    // The dev server has no nginx in front of it, so no gate either.
    if (!import.meta.env.SSR && !import.meta.env.DEV) {
      router.onBeforePageLoad = createAuthGuard({
        mode: () =>
          fetch(withBase('/_mode'), { cache: 'no-store' }).then((r) => (r.ok ? r.text() : '')),
        check: () =>
          fetch('/api/auth/check', { credentials: 'same-origin', cache: 'no-store' }).then(
            (r) => r.status,
          ),
        navigate: (to) => reloadThroughServer(to, window.location),
      })
    }
  },
} satisfies Theme
