// The home page's frontmatter: VitePress's own `features` cards plus the guide's `quickStart` steps
// (theme/components/HomeQuickStart.vue). Links are site paths: `/organizations/switch`, `/en/…`.

export type HomeFeature = {
  /** Inline SVG markup: VitePress renders a string icon as HTML. */
  icon?: string
  title: string
  details: string
  link: string
  linkText?: string
}

export type QuickStartStep = { title: string; details: string; link: string; image: string }

export type HomeFrontmatter = {
  layout?: string
  features?: HomeFeature[]
  quickStart?: { title: string; steps: QuickStartStep[] }
  [key: string]: unknown
}

function chapterOf(link: string): string {
  return link.replace(/^\/(en\/)?/, '').split(/[/#]/)[0] ?? ''
}

/**
 * A fork's hidden base chapters are not built, so a card or a step pointing into one would be a dead
 * link — config.mts drops them from the home page with this.
 */
export function filterHomeFrontmatter(home: HomeFrontmatter, hidden: string[]): HomeFrontmatter {
  if (hidden.length === 0) return home
  const shown = (link: string) => !hidden.includes(chapterOf(link))
  return {
    ...home,
    ...(home.features && { features: home.features.filter((f) => shown(f.link)) }),
    ...(home.quickStart && {
      quickStart: { ...home.quickStart, steps: home.quickStart.steps.filter((s) => shown(s.link)) },
    }),
  }
}
