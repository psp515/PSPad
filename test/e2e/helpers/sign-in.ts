import { expect, Page } from '@playwright/test';
import { Account } from '../fixtures/keycloak';

export async function signIn(page: Page, account: Account) {
  await page.goto('/');
  await page.getByRole('link', { name: 'Log in' }).click();
  await page.locator('#username').fill(account.username);
  await page.locator('#password').fill(account.password);
  await page.locator('#kc-login').click();
  await expect(page.getByRole('heading', { name: 'My Day' })).toBeVisible({ timeout: 30_000 });
}
