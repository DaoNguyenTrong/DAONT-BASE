// Placeholder until the branded theme lands: sync-brand writes into this folder, and VitePress
// treats any .vitepress/theme/ folder as a custom theme that must have an index.
import DefaultTheme from 'vitepress/theme'
import './brand-tokens.generated.css'

export default DefaultTheme
