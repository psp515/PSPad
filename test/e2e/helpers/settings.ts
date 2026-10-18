import { expect, Page } from '@playwright/test';
import { goTo } from './nav';

export async function goToSettings(page: Page) {
  await goTo(page, 'Settings');
}

export async function setTimeZone(page: Page, id: string) {
  const input = page.getByLabel('Time zone');
  await input.fill(id);
  await page.locator('.mud-popover-open').getByText(id, { exact: true }).click();
  await expect(input).toHaveValue(id);
}
