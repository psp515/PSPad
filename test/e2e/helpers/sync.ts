import { expect, Page } from '@playwright/test';

export function syncButton(page: Page) {
  return page.getByRole('button', { name: /^Sync now/ }).first();
}

export async function goOffline(page: Page) {
  await page.context().setOffline(true);
  await page.evaluate(() => window.dispatchEvent(new Event('offline')));
}

export async function goOnline(page: Page) {
  await page.context().setOffline(false);
  await page.evaluate(() => window.dispatchEvent(new Event('online')));
}

export async function syncNow(page: Page) {
  const synced = page.waitForResponse(response => response.url().includes('/api/sync') && response.ok());
  await syncButton(page).click();
  await synced;
  await expect(page.locator('.pspad-sync-stamp').first()).toContainText(/Updated/);
}
