import { expect, Page } from '@playwright/test';
import { Account } from '../fixtures/keycloak';

export async function signIn(page: Page, account: Account, landing = 'My Day') {
  await page.goto('/');
  await logInFromWelcome(page, account, landing);
}

export async function logInFromWelcome(page: Page, account: Account, landing?: string) {
  await page.getByRole('link', { name: 'Log in' }).click();
  await page.locator('#username').fill(account.username);
  await page.locator('#password').fill(account.password);
  await page.locator('#kc-login').click();
  if (landing) {
    await expect(page.getByRole('heading', { name: landing })).toBeVisible({ timeout: 30_000 });
    return;
  }
  await expect(page.getByRole('button', { name: /^Sync now/ }).first()).toBeVisible({ timeout: 30_000 });
}

export function signInDidNotFinish(page: Page) {
  return page.getByText('Signing in did not finish. Log in again to retry.');
}
