import { expect, Page } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { apiFor, bearerToken, fullSync, me, sendCommand } from './api';
import { goTo } from './nav';
import { panel, savePanel } from './panels';

export async function goToInbox(page: Page) {
  await goTo(page, 'Inbox');
}

export async function capture(page: Page, text: string) {
  await page.getByRole('button', { name: 'Capture' }).first().click();
  await panel(page).getByLabel('Name').fill(text);
  await savePanel(page);
  await expect(inboxItem(page, text)).toBeVisible();
}

export async function captureWithEnter(page: Page, text: string) {
  await page.getByRole('button', { name: 'Capture' }).last().click();
  const name = panel(page).getByLabel('Name');
  await name.fill(text);
  await name.press('Enter');
  await expect(inboxItem(page, text)).toBeVisible();
}

export function inboxItem(page: Page, text: string) {
  return page.locator('.pspad-inbox-item').filter({ hasText: text });
}

export function inboxItemTexts(page: Page) {
  return page.getByRole('button').filter({ has: page.locator('.pspad-inbox-item-text') });
}

export async function openInboxItem(page: Page, text: string) {
  await inboxItem(page, text).click();
  await expect(panel(page).getByLabel('Name')).toHaveValue(text);
}

export async function renameInboxItem(page: Page, from: string, to: string) {
  const name = panel(page).getByLabel('Name');
  await expect(async () => {
    if ((await name.inputValue({ timeout: 2_000 })) !== to) {
      await name.fill(to, { timeout: 2_000 });
      await name.press('Tab', { timeout: 2_000 });
    }
    await expect(inboxItem(page, to)).toBeVisible({ timeout: 2_000 });
    await expect(inboxItem(page, from).filter({ hasNotText: to })).toHaveCount(0, { timeout: 2_000 });
  }).toPass({ timeout: 30_000 });
}

export async function captureFromAnotherDevice(page: Page, text: string) {
  const api = await apiFor(await bearerToken(page));
  const { userId } = await me(api);
  const [inbox] = (await fullSync(api)).documents['inboxes'] ?? [];
  const result = await sendCommand(api, 'CaptureToInbox', {
    userId, inboxId: inbox['id'], itemId: randomUUID(), text,
  });
  expect(result.accepted, result.rejection ?? '').toBe(true);
  await api.dispose();
}
