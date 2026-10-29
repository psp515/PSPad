import { expect, Page } from '@playwright/test';

export type NavTarget = 'My Day' | 'Inbox' | 'Goals' | 'Statistics' | 'Settings' | 'Budgets';

const paths: Record<NavTarget, string> = {
  'My Day': '/',
  Inbox: '/inbox',
  Goals: '/goals',
  Statistics: '/statistics',
  Settings: '/settings',
  Budgets: '/budgets',
};

export function navLink(page: Page, path: string) {
  return page.locator('.pspad-nav').locator(`a[href="${path}"]`);
}

export async function goTo(page: Page, target: NavTarget) {
  await expect(page.locator('.pspad-detail-panel.mud-drawer--open')).toHaveCount(0);
  await expect(page.locator('.mud-drawer-overlay--open')).toHaveCount(0);
  const link = navLink(page, paths[target]);
  if (await link.isVisible()) {
    await link.click({ timeout: 10_000 }).catch(() => page.goto(paths[target]));
  } else {
    await page.goto(paths[target]);
  }
  await expect(page.getByRole('heading', { name: target, exact: true })).toBeVisible();
}

export async function openInApp(page: Page, path: string) {
  await page.evaluate(target => (window as unknown as { Blazor: { navigateTo: (url: string) => void } }).Blazor.navigateTo(target), path);
}
