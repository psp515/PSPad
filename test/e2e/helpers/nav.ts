import { expect, Page } from '@playwright/test';
import { awaitNoDrawer } from './panels';
import { shellMenu } from './shell';
import { escapeRegExp } from './text';

export type NavTarget = 'My Day' | 'Inbox' | 'Goals' | 'Statistics' | 'Settings' | 'Budgets';

export const paths: Record<NavTarget, string> = {
  'My Day': '/',
  Inbox: '/inbox',
  Goals: '/goals',
  Statistics: '/statistics',
  Settings: '/settings',
  Budgets: '/budgets',
};

export function navLink(page: Page, target: NavTarget) {
  return shellMenu(page).getByRole('link', { name: new RegExp(`^${escapeRegExp(target)}( ?\\d+)?$`) });
}

export async function goTo(page: Page, target: NavTarget) {
  await awaitNoDrawer(page);
  const link = navLink(page, target);
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
