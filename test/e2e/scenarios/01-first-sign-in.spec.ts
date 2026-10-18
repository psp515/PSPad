import { expect, test } from '../fixtures/accounts';
import { signIn } from '../helpers/sign-in';

test('a fresh user signs in and sees an empty day', async ({ page, account }) => {
  await signIn(page, account);

  await expect(page.getByText('Nothing planned for today.')).toBeVisible();

  const badge = page.locator('.pspad-account-badge:visible');
  await expect(badge).toContainText(account.displayName);
  await expect(badge).toContainText(account.email);

  await page.goto('/settings');
  const card = page.locator('.pspad-account-card');
  await expect(card).toContainText(account.displayName);
  await expect(card).toContainText(account.email);
  await expect(page.getByLabel('Time zone')).toHaveValue('Etc/UTC');
});
