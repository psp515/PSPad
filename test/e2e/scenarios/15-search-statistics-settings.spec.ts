import { expect, test } from '../fixtures/accounts';
import { anotherDevice, apiFor, bearerToken, fullSync, idOf } from '../helpers/api';
import { createArea } from '../helpers/areas';
import { utcToday } from '../helpers/dates';
import { createGoalOn, linkTasksOn } from '../helpers/goals';
import { captureFromAnotherDevice } from '../helpers/inbox';
import { createList, listIdFromUrl, openList } from '../helpers/lists';
import { goTo } from '../helpers/nav';
import { closePanel, panel } from '../helpers/panels';
import { goToSettings } from '../helpers/settings';
import { signIn } from '../helpers/sign-in';
import {
  capturesTable, heatmapDay, heatmapDays, heatmapMonthLabel, readoutDay, recordFeed, statRange, statTile,
} from '../helpers/statistics';
import { syncNow } from '../helpers/sync';
import { addTask, completeTaskRow, createTasksOn } from '../helpers/tasks';
import { shellMenu } from '../helpers/shell';

test.use({ serviceWorkers: 'block' });

test('search finds a task, statistics count it, settings change the look and sign-out returns to Welcome', async ({ page, account }) => {
  const list = `Reading ${account.suffix}`;
  const task = `Finish the novel ${account.suffix}`;
  const gone = `Return the atlas ${account.suffix}`;
  const goal = `Read more ${account.suffix}`;
  const unsynced = `Buy a bookmark ${account.suffix}`;
  const today = utcToday();

  await signIn(page, account);
  await createArea(page, `Leisure ${account.suffix}`);
  await createList(page, list);
  await openList(page, list);
  await addTask(page, task);
  const device = await anotherDevice(page);
  const taskId = await idOf(device, 'todotasks', task);
  await linkTasksOn(device, await createGoalOn(device, goal), [taskId]);
  await createTasksOn(device, listIdFromUrl(page), [{ name: gone, completed: true, deleted: true }]);
  await captureFromAnotherDevice(page, `A new shelf ${account.suffix}`);
  await completeTaskRow(page, task);
  await syncNow(page);
  await device.send('ReopenTask', { taskId });
  await device.send('CompleteTask', { taskId });

  await test.step('search finds the task, its result opens the task, and the address keeps it', async () => {
    await page.goto(`/search?q=${encodeURIComponent(account.suffix)}`);
    await expect(page.getByRole('heading', { level: 1 })).toContainText(`Search: ${account.suffix}`);
    await page.getByRole('option', { name: task }).click();
    await expect(panel(page).getByPlaceholder('Task name')).toHaveValue(task);
    await expect(panel(page).getByRole('checkbox', { name: 'Done' })).toBeChecked();
    await page.reload();
    await expect(panel(page).getByPlaceholder('Task name')).toHaveValue(task);
    await page.goBack();
    await expect(page).toHaveURL(/\/search\?q=[^&]*$/);
    await expect(page.getByRole('heading', { level: 1 })).toContainText(`Search: ${account.suffix}`);
    await expect(panel(page).getByPlaceholder('Task name')).toHaveCount(0);
  });

  await test.step('statistics count today, by goal and by week, and the heatmap answers a tap', async () => {
    await goTo(page, 'Statistics');
    await expect(statTile(page, 'Done today')).toHaveText('3');
    await expect(statTile(page, 'Last 7 days')).toHaveText('3');
    await expect(statRange(page, 30)).toBeChecked();
    await expect(statRange(page, 90)).not.toBeChecked();
    await expect(page.getByRole('progressbar', { name: goal })).toHaveAttribute('aria-valuenow', '2');
    await expect(page.getByRole('progressbar', { name: 'No goal' })).toHaveAttribute('aria-valuenow', '1');
    await expect(capturesTable(page).getByRole('row').last()).toHaveText(/ 1$/);

    await expect(heatmapDays(page)).toHaveCount(30);
    await expect(heatmapDay(page, today)).toHaveAccessibleName(/: 3 completed$/);
    await expect(heatmapMonthLabel(page, today)).toHaveCount(0);
    await expect(page.getByText('Each square is one day; darker means more tasks finished that day.')).toBeVisible();
    await expect(page.getByText('Less', { exact: true })).toBeVisible();
    await expect(page.getByText('More', { exact: true })).toBeVisible();
    const hint = page.getByText('Tap a square for the day and its count.');
    await expect(hint).toBeVisible();
    await heatmapDay(page, today).click();
    await expect(page.getByText(`${readoutDay(today)} — 3 completed`)).toBeVisible();
    await expect(hint).toHaveCount(0);
    await heatmapDay(page, today).click();
    await expect(hint).toBeVisible();

    await statRange(page, 90).click();
    await expect(statRange(page, 90)).toBeChecked();
    await expect(statRange(page, 30)).not.toBeChecked();
    await expect(heatmapDays(page)).toHaveCount(90);
    await expect(heatmapMonthLabel(page, today)).toBeVisible();
  });

  await test.step('the record feed opens on demand, opens a task, and a deleted task stays put', async () => {
    const completed = page.getByText(`Completed — ${task}`, { exact: true }).first();
    await expect(completed).toBeHidden();
    await recordFeed(page).click();
    await expect(completed).toBeVisible();
    await expect(page.getByText(`Completed — ${task}`, { exact: true })).toHaveCount(2);
    await expect(page.getByText('#2', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'load older' }).click();
    await expect(completed).toBeVisible();
    await page.getByText(`Deleted — ${gone}`, { exact: true }).click();
    await expect(page).toHaveURL(/\/statistics$/);
    await completed.click();
    await expect(panel(page).getByPlaceholder('Task name')).toHaveValue(task);
    await closePanel(page);
    await recordFeed(page).click();
    await recordFeed(page).click();
    await expect(completed).toBeVisible();
  });

  await test.step('an old History address leads to Statistics', async () => {
    await page.goto('/history');
    await expect(page).toHaveURL(/\/statistics$/);
  });

  await test.step.skip('BUG: an old History address lands on an empty Statistics page (reported in PR #225)', async () => {
    await page.goto('/history');
    await expect(statTile(page, 'Done today')).toHaveText('3');
  });

  await test.step('offline, Statistics keeps the last numbers it had', async () => {
    await page.goto('/statistics');
    await expect(statTile(page, 'Done today')).toHaveText('3');
    await page.route('**/api/statistics/**', route => route.abort());
    await page.reload();
    await expect(page.getByText('Offline — showing the last numbers we have.')).toBeVisible();
    await expect(statTile(page, 'Done today')).toHaveText('3');
    await page.unroute('**/api/statistics/**');
  });

  await test.step('settings change theme, accent, currency and time zone, and they survive a reload', async () => {
    await goToSettings(page);
    await page.getByRole('combobox', { name: 'Theme' }).click();
    const modes = page.getByRole('option');
    await expect(modes).toHaveText(['System', 'Light', 'Dark']);
    await modes.getByText('Dark', { exact: true }).click();
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');

    const accents = page.getByRole('group', { name: 'Accent colour' });
    await expect(accents.getByRole('button', { name: 'Green accent' })).toHaveAttribute('aria-pressed', 'true');
    await accents.getByRole('button', { name: 'Purple accent' }).click();
    await expect(accents.getByRole('button', { name: 'Purple accent' })).toHaveAttribute('aria-pressed', 'true');
    await expect(accents.getByRole('button', { name: 'Green accent' })).toHaveAttribute('aria-pressed', 'false');
    await accents.getByRole('button', { name: 'Custom accent' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Cancel' }).click();
    await expect(page.getByRole('dialog')).toBeHidden();
    await expect(accents.getByRole('button', { name: 'Purple accent' })).toHaveAttribute('aria-pressed', 'true');
    await accents.getByRole('button', { name: 'Custom accent' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Apply' }).click();
    await expect(accents.getByRole('button', { name: 'Custom accent' })).toHaveAttribute('aria-pressed', 'true');
    await expect(accents.getByRole('button', { name: 'Purple accent' })).toHaveAttribute('aria-pressed', 'false');

    const currency = page.getByRole('combobox', { name: 'Default currency' });
    await expect(currency).toHaveText('PLN');
    await currency.click();
    await page.getByRole('option', { name: 'EUR', exact: true }).click();
    await expect(currency).toHaveText('EUR');

    await page.getByLabel('Time zone').fill('warsaw');
    await expect(page.getByRole('option', { name: 'Europe/Warsaw', exact: true })).toBeVisible();
    await expect(page.getByRole('option', { name: 'Pacific/Kiritimati', exact: true })).toHaveCount(0);
    await page.keyboard.press('Escape');

    await page.reload();
    await expect(page.getByRole('heading', { name: 'Settings', exact: true })).toBeVisible();
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
    await expect(accents.getByRole('button', { name: 'Custom accent' })).toHaveAttribute('aria-pressed', 'true');
    await expect(page.getByRole('combobox', { name: 'Default currency' })).toHaveText('EUR');
    await expect(page.getByLabel('Time zone')).toHaveValue('Etc/UTC');
  });

  await test.step('the Sync card says all is synced, and an unreachable sign-in server keeps the user signed in', async () => {
    await expect(page.getByText('Everything is synced.')).toBeVisible();
    await expect(page.getByText(/^Last synced (just now|\d+ min ago)$/)).toBeVisible();
    await page.route('**/protocol/openid-connect/revoke', route => route.abort());
    await page.getByRole('button', { name: 'Sign out' }).click();
    await expect(page.getByText("Couldn't reach the sign-in server, so you're still signed in.", { exact: false })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Settings', exact: true })).toBeVisible();
    await page.unroute('**/protocol/openid-connect/revoke');
  });

  await test.step('signing out with an unsynced change asks first, and confirming drops it and ends the Keycloak session', async () => {
    const api = await apiFor(await bearerToken(page));
    await page.route('**/api/commands', route => route.abort());
    await shellMenu(page).getByRole('link', { name: `Leisure ${account.suffix}`, exact: true }).click();
    await openList(page, list);
    await addTask(page, unsynced);
    await goToSettings(page);
    await expect(page.getByText(/1 pending$/)).toBeVisible();

    await page.getByRole('button', { name: 'Sign out' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText("1 unsynced change hasn't reached the server yet.");
    await dialog.getByRole('button', { name: 'Cancel' }).click();
    await expect(dialog).toBeHidden();
    await expect(page.getByText(/1 pending$/)).toBeVisible();

    await page.getByRole('button', { name: 'Sign out' }).click();
    await dialog.getByRole('button', { name: 'Sign out' }).click();
    await expect(page.getByRole('heading', { name: 'Your day, planned.' })).toBeVisible({ timeout: 30_000 });
    await page.unroute('**/api/commands');
    const tasks = (await fullSync(api)).documents['todotasks'] ?? [];
    expect(tasks.map(doc => doc['name'])).not.toContain(unsynced);
    await api.dispose();

    await page.getByRole('link', { name: 'Log in' }).click();
    await expect(page.locator('#password')).toBeVisible({ timeout: 30_000 });
  });

  await device.dispose();
});
