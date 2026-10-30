import { randomUUID } from 'node:crypto';
import { expect, test } from '../fixtures/accounts';
import { anotherDevice, idOf } from '../helpers/api';
import { createArea, openArea } from '../helpers/areas';
import { createList, listIdFromUrl, openList } from '../helpers/lists';
import { closePanel, openFabAction, panel } from '../helpers/panels';
import { goToSettings } from '../helpers/settings';
import { shellMenu } from '../helpers/shell';
import { signIn } from '../helpers/sign-in';
import { goOffline, goOnline, syncButton, syncNow } from '../helpers/sync';
import { addDays, shortDate, utcToday } from '../helpers/dates';
import { addTask, completeTaskRow, taskRow } from '../helpers/tasks';
import { passUtcMidnight } from '../helpers/today';

// Chromium's offline emulation skips requests a service worker makes, so this context runs without one.
test.use({ serviceWorkers: 'block' });

test('changes made offline reach the server after the next sync', async ({ page, browser, account }) => {
  const area = `Field ${account.suffix}`;
  const list = `Site visit ${account.suffix}`;
  const before = `Check the fence ${account.suffix}`;
  const offline = `Photograph the gate ${account.suffix}`;
  const shed = `Shed ${account.suffix}`;

  await signIn(page, account);
  await createArea(page, area);
  await createList(page, list);
  await openList(page, list);
  const listId = listIdFromUrl(page);
  await addTask(page, before);
  await syncNow(page);

  await goOffline(page);
  await expect(syncButton(page)).toHaveAccessibleDescription(/^Offline\b/, { timeout: 30_000 });
  await expect(page.getByRole('img', { name: 'Offline', exact: true }).filter({ visible: true })).toHaveCount(1);
  await addTask(page, offline);
  await completeTaskRow(page, before);
  await expect(page.getByText('Completed · 1')).toBeVisible();

  await test.step('offline, the list panel says snapshots need a connection', async () => {
    await openFabAction(page, 'Edit list');
    await expect(panel(page).getByRole('option', { name: /^Public snapshots/ })).toContainText('Needs a connection');
    await closePanel(page);
  });

  await test.step('offline, Settings holds the changes, and sign-out and deletion wait for a connection', async () => {
    await expect(shellMenu(page).getByRole('img', { name: 'Offline', exact: true })).toBeVisible();
    await expect(syncButton(page)).toBeDisabled();
    await goToSettings(page);
    await expect(page.getByText('Offline · updated just now')).toBeVisible();
    await expect(page.getByText('2 pending', { exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Sign out' })).toBeDisabled();
    await expect(page.getByText("You're offline, so this device can't be signed out securely. Connect to sign out.")).toBeVisible();
    await expect(page.getByRole('button', { name: 'Delete account' })).toBeDisabled();
    await expect(page.getByText('Requires a connection.')).toBeVisible();
  });

  await goOnline(page);
  await expect(page.getByRole('img', { name: /^(Offline|Server unreachable)/ })).toHaveCount(0, { timeout: 30_000 });
  await expect(syncButton(page)).toBeEnabled();
  await syncNow(page);

  await test.step('back online, sign-out is offered again and nothing is pending', async () => {
    await expect(page.getByRole('button', { name: 'Sign out' })).toBeEnabled();
    await expect(page.getByText("You're offline", { exact: false })).toHaveCount(0);
    await expect(page.getByText('Everything is synced.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Delete account' })).toBeEnabled();
  });

  const otherContext = await browser.newContext();
  const other = await otherContext.newPage();
  await signIn(other, account);
  await openArea(other, area);
  await openList(other, list);
  await expect(taskRow(other, offline)).toBeVisible();
  await expect(other.getByText('Completed · 1')).toBeVisible();
  await expect(taskRow(other, before)).toBeHidden();
  await otherContext.close();

  const device = await anotherDevice(page);

  await test.step('an unreachable server raises a belt whose Retry syncs once it answers again', async () => {
    await page.route('**/api/sync**', route => route.abort());
    await syncButton(page).click();
    const belt = page.getByRole('status').filter({ hasText: 'Can’t reach the server — working from local data. Changes sync when you’re back.' });
    await expect(belt).toBeVisible();
    await expect(shellMenu(page).getByRole('img', { name: 'Server unreachable', exact: true })).toBeVisible();
    await expect(page.getByText(/^Couldn't update · updated /)).toBeVisible();
    await expect(page.getByText("Couldn't sync.", { exact: true })).toBeVisible();
    await page.unroute('**/api/sync**');
    await belt.getByRole('button', { name: 'Retry' }).click();
    await expect(belt).toHaveCount(0);
    await expect(shellMenu(page).getByRole('img', { name: 'Server unreachable', exact: true })).toHaveCount(0);
    await expect(page.getByText('Everything is synced.')).toBeVisible();
  });

  await test.step('with the server silent, a reload still opens on stored data and the task in the address', async () => {
    const taskId = await idOf(device, 'todotasks', offline);
    let release = () => {};
    const released = new Promise<void>(resolve => { release = resolve; });
    const silent = async (route: import('@playwright/test').Route) => {
      await released;
      await route.abort();
    };
    await page.route('**/api/me', silent);
    await page.route('**/api/sync**', silent);
    await page.goto(`/lists/${listId}?task=${taskId}`);
    await expect(page.getByRole('heading', { name: list, level: 1 })).toBeVisible({ timeout: 20_000 });
    await expect(panel(page).getByPlaceholder('Task name')).toHaveValue(offline);
    await expect(shellMenu(page)).toContainText(account.displayName);
    await expect(shellMenu(page).getByRole('link', { name: area, exact: true })).toBeVisible();
    release();
    await page.unroute('**/api/me');
    await page.unroute('**/api/sync**');
    await closePanel(page);
  });

  await test.step('a change the server refuses is listed in Settings until it is discarded', async () => {
    const shedId = randomUUID();
    await device.send('CreateArea', { areaId: shedId, name: shed, position: 5 });
    await expect(shellMenu(page).getByRole('link', { name: shed, exact: true })).toBeVisible({ timeout: 20_000 });
    await openArea(page, shed);
    await goOffline(page);
    await openFabAction(page, 'Edit area');
    await panel(page).getByPlaceholder('Area name').fill(`${shed} renamed`);
    await panel(page).getByPlaceholder('Area name').press('Tab');
    await closePanel(page);
    await device.send('DeleteArea', { areaId: shedId });
    await goOnline(page);

    const belt = page.getByRole('status').filter({ hasText: '1 change couldn’t be saved.' });
    await expect(belt).toBeVisible();
    await belt.getByRole('button', { name: 'Details' }).click();
    await expect(page.getByRole('heading', { name: 'Settings', exact: true })).toBeVisible();
    await expect(page.getByText('Couldn’t be saved', { exact: true })).toBeVisible();
    await expect(page.getByText('That area no longer exists.', { exact: true })).toBeVisible();
    await expect(page.getByText('1 pending', { exact: true })).toBeVisible();
    await belt.getByRole('button', { name: 'Dismiss' }).click();
    await expect(belt).toHaveCount(0);
    await page.getByRole('button', { name: 'Discard this change' }).click();
    await expect(page.getByText('That area no longer exists.', { exact: true })).toHaveCount(0);
    await expect(page.getByText('Everything is synced.')).toBeVisible();
    await expect(shellMenu(page).getByRole('link', { name: new RegExp(shed) })).toHaveCount(0);
  });

  await test.step('an open My Day turns to the next day once midnight passes', async () => {
    const today = utcToday();
    await page.clock.install();
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'My Day' })).toBeVisible({ timeout: 30_000 });
    const label = page.getByText(/^Today · /);
    await expect(label).toContainText(shortDate(today));
    await passUtcMidnight(page);
    await expect(label).toContainText(shortDate(addDays(today, 1)), { timeout: 20_000 });
  });

  await device.dispose();
});
