import { expect, test } from '../fixtures/accounts';
import { goToInbox } from '../helpers/inbox';
import { logInFromWelcome } from '../helpers/sign-in';
import { goToMyDay } from '../helpers/today';

test('a fresh user signs in and sees an empty day', async ({ page, account }) => {
  await test.step('Welcome greets a visitor and keeps where they were going', async () => {
    await page.goto('/inbox');
    await expect(page).toHaveURL(/\/welcome\?returnUrl=/);
    await expect(page.getByRole('heading', { name: 'Your day, planned.' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'An approach for GTD techniques' }))
      .toHaveAttribute('href', 'https://en.wikipedia.org/wiki/Getting_Things_Done');
    await expect(page.getByRole('link', { name: 'Docs' })).toHaveAttribute('href', 'https://psp515.com/pspad');
    await expect(page.getByRole('link', { name: 'Log in' })).toHaveAttribute('href', /returnUrl=.*inbox/);
    for (const preview of ['Renew the domain', 'Draft the release notes', 'Read a book', 'Water the plants']) {
      await expect(page.getByText(preview, { exact: true })).toBeVisible();
    }
    for (const gone of ['One Today screen', 'How it goes', 'Run it yourself', 'Install']) {
      await expect(page.getByText(gone)).toHaveCount(0);
    }
    await expect(page.getByRole('button', { name: /^Sync now/ })).toHaveCount(0);
    await logInFromWelcome(page, account, 'My Day');
  });

  await test.step('an empty inbox and an empty day say so', async () => {
    await goToInbox(page);
    await expect(page.getByText('Nothing captured.')).toBeVisible();
    await goToMyDay(page);
    await expect(page.getByText('Nothing planned for today.')).toBeVisible();
    await expect(page.getByText('Schedule empty for today.')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Overdue' })).toHaveCount(0);
    await expect(page.getByRole('heading', { name: 'Starred' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Add task' })).toBeVisible();
  });

  const areas = page.locator('.pspad-nav');
  for (const name of ['Personal', 'Work', 'Home']) {
    await expect(areas.getByRole('link', { name, exact: true })).toBeVisible();
  }

  const badge = page.locator('.pspad-account-badge:visible');
  await expect(badge).toContainText(account.displayName);
  await expect(badge).toContainText(account.email);

  await page.goto('/settings');
  const card = page.locator('.pspad-account-card');
  await expect(card).toContainText(account.displayName);
  await expect(card).toContainText(account.email);
  await expect(page.getByLabel('Time zone')).toHaveValue('Etc/UTC');
});
