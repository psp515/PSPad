import { expect, test } from '../fixtures/accounts';
import { createArea } from '../helpers/areas';
import { createList, openList } from '../helpers/lists';
import { goTo } from '../helpers/nav';
import { closePanel, panel } from '../helpers/panels';
import { goToSettings } from '../helpers/settings';
import { signIn } from '../helpers/sign-in';
import { syncNow } from '../helpers/sync';
import { addTask, completeTaskRow } from '../helpers/tasks';

test('search finds a task, statistics count it, the theme sticks and sign-out returns to Welcome', async ({ page, account }) => {
  const list = `Reading ${account.suffix}`;
  const task = `Finish the novel ${account.suffix}`;

  await signIn(page, account);
  await createArea(page, `Leisure ${account.suffix}`);
  await createList(page, list);
  await openList(page, list);
  await addTask(page, task);
  await completeTaskRow(page, task);
  await syncNow(page);

  await page.goto(`/search?q=${encodeURIComponent(account.suffix)}`);
  await expect(page.getByRole('heading', { level: 1 })).toContainText(`Search: ${account.suffix}`);
  await page.getByRole('option', { name: task }).click();
  await expect(panel(page).getByPlaceholder('Task name')).toHaveValue(task);
  await expect(panel(page).getByRole('checkbox', { name: 'Done' })).toBeChecked();
  await closePanel(page);

  await goTo(page, 'Statistics');
  const doneToday = page.locator('.mud-paper').filter({ hasText: 'Done today' }).first();
  await expect(doneToday).toContainText('1');

  await goToSettings(page);
  await expect(page.locator('.mud-popover-open')).toHaveCount(0);
  await page.getByRole('combobox', { name: 'Theme' }).click();
  await page.locator('.mud-popover-open').getByText('Dark', { exact: true }).click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Settings', exact: true })).toBeVisible();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');

  await page.locator('.pspad-sign-out').click();
  const dialog = page.getByRole('dialog');
  if (await dialog.isVisible()) {
    await dialog.getByRole('button', { name: 'Sign out' }).click();
  }
  await expect(page.getByRole('heading', { name: 'Your day, planned.' })).toBeVisible({ timeout: 30_000 });
});
