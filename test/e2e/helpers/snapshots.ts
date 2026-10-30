import { APIRequestContext, expect, Locator, Page, request } from '@playwright/test';
import { env } from '../env';
import { panel } from './panels';
import { calendarOpen, pickCalendarDay } from './today';

export type PublishedSnapshot = { id: string; token: string };

export async function publishSnapshotOn(api: APIRequestContext, listId: string, expiresAt: Date): Promise<PublishedSnapshot> {
  const response = await api.post(`/api/lists/${listId}/snapshots`, { data: { expiresAt: expiresAt.toISOString() } });
  if (!response.ok()) {
    throw new Error(`publish snapshot: ${response.status()}`);
  }
  return response.json();
}

export async function markOnSnapshot(token: string, entryId: string) {
  const visitor = await request.newContext({ baseURL: env.apiUrl });
  const response = await visitor.post(`/api/public/snapshots/${token}/marks`, { data: { entryId, stepId: null, marked: true } });
  expect(response.ok()).toBe(true);
  await visitor.dispose();
}

export async function showNewestSnapshot(page: Page): Promise<Locator> {
  const open = panel(page).getByRole('link', { name: 'Open', exact: true });
  await expect(async () => {
    if (!(await open.isVisible())) {
      await panel(page).getByRole('button', { name: 'Show QR code' }).first().click({ timeout: 2_000 });
    }
    await expect(open).toBeVisible({ timeout: 2_000 });
  }).toPass({ timeout: 20_000 });
  return open;
}

export function snapshotRows(page: Page) {
  return page.getByRole('option').filter({ hasText: ' · Opened ' });
}

export function snapshotDate(day: Date): string {
  return `${day.getUTCDate()} ${day.toLocaleString('en-GB', { month: 'short', timeZone: 'UTC' })} ${day.getUTCFullYear()}`;
}

export function recordedVisit(page: Page) {
  return page.waitForResponse(response =>
    response.url().endsWith('/api/me/snapshot-visits') && response.request().method() === 'POST' && response.ok());
}

export async function pickSnapshotDate(page: Page, day: Date) {
  await panel(page).getByRole('checkbox', { name: 'Date…', exact: true }).click();
  await panel(page).getByRole('textbox').click();
  await pickCalendarDay(page, day);
  await expect(calendarOpen(page)).toHaveCount(0);
}
