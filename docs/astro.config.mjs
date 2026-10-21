import { defineConfig } from 'astro/config'

export default defineConfig({
  site: 'https://psp515.com',
  base: '/pspad',
  redirects: { '/install': '/pspad/self-host/' }
})
