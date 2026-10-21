import { expect, Page } from '@playwright/test';
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

export function inboxItem(page: Page, text: string) {
  return page.locator('.pspad-inbox-item').filter({ hasText: text });
}

export async function openInboxItem(page: Page, text: string) {
  await inboxItem(page, text).click();
  await expect(panel(page).getByLabel('Name')).toHaveValue(text);
}
