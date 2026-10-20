import { expect, Page } from '@playwright/test';
import { goTo } from './nav';

export async function goToSettings(page: Page, label = 'Settings') {
  if (label === 'Settings') {
    await goTo(page, 'Settings');
    return;
  }
  await page.locator('.pspad-nav').getByRole('link', { name: label, exact: true }).click();
  await expect(page.getByRole('heading', { name: label, exact: true })).toBeVisible();
}

export async function setTimeZone(page: Page, id: string) {
  const input = page.getByLabel('Time zone');
  await input.fill(id);
  await page.locator('.mud-popover-open').getByText(id, { exact: true }).click();
  await expect(input).toHaveValue(id);
}

export async function setLanguage(page: Page, nativeName: 'English' | 'Polski') {
  await expect(page.locator('.mud-popover-open')).toHaveCount(0);
  await page.locator('.pspad-language').getByRole('combobox').click();
  const reloaded = page.waitForEvent('load');
  await page.locator('.mud-popover-open').getByText(nativeName, { exact: true }).click();
  await reloaded;
}
