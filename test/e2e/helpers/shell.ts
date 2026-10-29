import { expect, Page } from '@playwright/test';

export const phoneViewport = { width: 390, height: 844 };
export const desktopViewport = { width: 1280, height: 800 };

export function shellMenu(page: Page) {
  return page.getByRole('complementary')
    .filter({ has: page.locator('a[href="/app-info"]') })
    .filter({ visible: true });
}

export function bootSplash(page: Page) {
  return page.getByRole('img', { name: 'Loading PSPad', exact: true });
}

export function bottomNav(page: Page) {
  return page.getByRole('navigation', { name: 'Main', exact: true });
}

export function topBar(page: Page) {
  return page.getByRole('banner');
}

export async function openAccountDrawer(page: Page) {
  const statistics = shellMenu(page).getByRole('link', { name: 'Statistics', exact: true });
  await expect(async () => {
    if (!(await statistics.evaluate(link => {
      const box = link.getBoundingClientRect();
      return box.right > 0 && box.left < window.innerWidth;
    }))) {
      await topBar(page).getByRole('button', { name: 'Account', exact: true }).click({ timeout: 2_000 });
    }
    await expect(statistics).toBeInViewport({ timeout: 2_000 });
  }).toPass({ timeout: 20_000 });
}

export async function collapseGroup(page: Page, group: 'areas' | 'budgets') {
  await shellMenu(page).getByRole('button', { name: `Collapse ${group}`, exact: true }).click();
}

export async function expandGroup(page: Page, group: 'areas' | 'budgets') {
  await shellMenu(page).getByRole('button', { name: `Expand ${group}`, exact: true }).click();
}
