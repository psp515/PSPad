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

export const pushedWithin = { timeout: 20_000 };

export function liveStreamOpened(page: Page) {
  return page.waitForResponse(response => response.url().endsWith('/api/live') && response.status() === 200, { timeout: 30_000 });
}

export async function settled(page: Page, edit: () => Promise<void>) {
  let committedAt = Number.POSITIVE_INFINITY;
  const committed = page.waitForResponse(response => {
    if (!response.url().includes('/api/commands') || !response.ok()) {
      return false;
    }
    const timing = response.request().timing();
    committedAt = timing.startTime + timing.responseEnd;
    return true;
  }, pushedWithin);
  const pulled = page.waitForResponse(
    response => response.url().includes('/api/sync') && response.ok() && response.request().timing().startTime >= committedAt,
    pushedWithin);
  await edit();
  await committed;
  await pulled;
}
