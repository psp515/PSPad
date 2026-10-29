import { expect, Page } from '@playwright/test';
import { Account } from '../fixtures/keycloak';
import { env } from '../env';
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
  const saved = page.waitForResponse(
    response => response.url().endsWith('/api/me/timezone') && response.request().method() === 'PUT' && response.ok(),
  );
  await page.locator('.mud-popover-open').getByText(id, { exact: true }).click();
  await saved;
  await expect(input).toHaveValue(id);
}

export async function setLanguage(page: Page, nativeName: 'English' | 'Polski') {
  await expect(page.locator('.mud-popover-open')).toHaveCount(0);
  await page.locator('.pspad-language').getByRole('combobox').click();
  const reloaded = page.waitForEvent('load');
  await page.locator('.mud-popover-open').getByText(nativeName, { exact: true }).click();
  await reloaded;
}

export async function deleteAccountDialog(page: Page) {
  await page.getByRole('button', { name: 'Delete account', exact: true }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByLabel('Email')).toBeVisible();
  return dialog;
}

export async function confirmAccountDeletion(page: Page) {
  const deleted = page.waitForResponse(response =>
    response.url().endsWith('/api/account') && response.request().method() === 'DELETE' && response.ok());
  await page.getByRole('dialog').getByRole('button', { name: 'Delete account', exact: true }).click();
  await deleted;
}

export async function keycloakLogIn(page: Page, account: Account) {
  const username = page.locator('#username');
  await expect(page.locator('#password')).toBeVisible({ timeout: 30_000 });
  if (await username.count() > 0 && await username.isEditable()) {
    await username.fill(account.username);
  }
  await page.locator('#password').fill(account.password);
  await page.locator('#kc-login').click();
}

export async function answerAccountDeletionOnce(page: Page, status: number, challenge?: string) {
  await page.route('**/api/account', route => route.fulfill({
    status,
    headers: {
      'access-control-allow-origin': env.appUrl,
      'access-control-expose-headers': 'WWW-Authenticate',
      ...(challenge ? { 'www-authenticate': challenge } : {}),
    },
  }), { times: 1 });
}
