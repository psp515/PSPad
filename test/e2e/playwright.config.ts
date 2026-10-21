import { defineConfig } from '@playwright/test';
import { env } from './env';

export default defineConfig({
  testDir: './scenarios',
  globalSetup: './global-setup.ts',
  fullyParallel: true,
  workers: process.env.E2E_WORKERS ? Number(process.env.E2E_WORKERS) : 4,
  retries: process.env.CI ? 1 : 0,
  timeout: 90_000,
  expect: { timeout: 10_000 },
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : [['list']],
  use: {
    baseURL: env.appUrl,
    viewport: { width: 1280, height: 800 },
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
});
