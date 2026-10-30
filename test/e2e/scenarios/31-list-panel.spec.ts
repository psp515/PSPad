import { randomUUID } from 'node:crypto';
import { expect, test } from '../fixtures/accounts';
import { anotherDevice } from '../helpers/api';
import { addDays, dayMonth, utcToday } from '../helpers/dates';
import { pageHeading, createArea, listCard, openCardMenu } from '../helpers/areas';
import { createList, listIdFromUrl, openList } from '../helpers/lists';
import { awaitNoDrawer, closePanel, confirm, openFabAction, panel } from '../helpers/panels';
import { openListSnapshots } from '../helpers/sharing';
import { signIn } from '../helpers/sign-in';
import { markOnSnapshot, pickSnapshotDate } from '../helpers/snapshots';
import { goOffline, goOnline } from '../helpers/sync';
import { createTasksOn } from '../helpers/tasks';

test.use({ serviceWorkers: 'block' });

test('the list panel edits, moves and deletes a list, and a gone list says so', async ({ page, account }) => {
  const home = `Home ${account.suffix}`;
  const work = `Work ${account.suffix}`;
  const errands = `Errands ${account.suffix}`;
  const renamed = `Weekly errands ${account.suffix}`;
  const spare = `Spare ${account.suffix}`;

  await signIn(page, account);
  await createArea(page, work);
  await createArea(page, home);
  await createList(page, errands);
  await openList(page, errands);
  const heading = pageHeading(page);

  await test.step('an empty task list names its kind and area and offers its first task from the FAB', async () => {
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Task list');
    await expect(page.getByText('No tasks yet.')).toBeVisible();
    await expect(heading).toHaveAccessibleDescription(`${home} · no open tasks`);
    await openFabAction(page, 'Add task');
    await expect(panel(page).getByText(`New task · ${home} › ${errands}`)).toBeVisible();
    await closePanel(page);
  });

  const device = await anotherDevice(page);
  const tasks = await createTasksOn(device, listIdFromUrl(page), [{ name: 'Milk' }, { name: 'Bread' }]);
  await expect(heading).toHaveAccessibleDescription(`${home} · 2 open tasks`);

  await test.step('the list panel names the list, its kind and area, renames as you type and moves it to another area', async () => {
    await openFabAction(page, 'Edit list');
    await expect(panel(page).getByText(`List · ${home}`, { exact: true })).toBeVisible();
    await expect(panel(page).getByPlaceholder('List name')).toHaveValue(errands);
    await expect(panel(page).getByText('Task list', { exact: true })).toBeVisible();
    await expect(panel(page).getByRole('button', { name: 'Add list' })).toHaveCount(0);
    await panel(page).getByPlaceholder('List name').fill(renamed);
    await panel(page).getByPlaceholder('List name').press('Tab');
    await expect(page.getByRole('heading', { level: 1 })).toContainText(renamed);

    await panel(page).getByLabel('Area').click();
    await page.getByRole('option', { name: work, exact: true }).click();
    await expect(heading).toHaveAccessibleDescription(`${work} · 2 open tasks`);
    await expect(panel(page).getByText(`List · ${work}`, { exact: true })).toBeVisible();
  });

  await test.step('Members opens a view whose back arrow returns to the details, and Public snapshots opens its own', async () => {
    await panel(page).getByRole('option', { name: /^Members/ }).click();
    await expect(panel(page).getByText('Members', { exact: true }).first()).toBeVisible();
    await expect(panel(page).getByPlaceholder('List name')).toHaveCount(0);
    await expect(panel(page).getByRole('button', { name: 'Delete list' })).toHaveCount(0);
    await panel(page).getByRole('button', { name: 'Back' }).click();
    await expect(panel(page).getByPlaceholder('List name')).toHaveValue(renamed);

    await panel(page).getByRole('option', { name: /^Public snapshots/ }).click();
    await expect(panel(page).getByPlaceholder('List name')).toHaveCount(0);
    await expect(panel(page).getByText('Public snapshots', { exact: true }).first()).toBeVisible();
    await panel(page).getByRole('button', { name: 'Back' }).click();
    await expect(panel(page).getByPlaceholder('List name')).toHaveValue(renamed);
    await closePanel(page);
  });

  await test.step('Public snapshots names the expiry it publishes, warns when publishing or revoking fails, counts days, ticks and entries, and needs a connection', async () => {
    const today = utcToday();
    const snapshots = '**/api/lists/*/snapshots';
    await openListSnapshots(page);
    const publish = panel(page).getByRole('button', { name: /^(Publish until|Pick a date)/ });
    const qr = panel(page).getByRole('img', { name: 'Snapshot link QR code' });
    await expect(publish).toHaveText(`Publish until ${dayMonth(addDays(today, 7))}`);
    await panel(page).getByRole('checkbox', { name: 'Date…', exact: true }).click();
    await expect(publish).toHaveText('Pick a date');
    await expect(publish).toBeDisabled();
    await pickSnapshotDate(page, addDays(today, 10));
    await expect(publish).toHaveText(`Publish until ${dayMonth(addDays(today, 10))}`);
    await panel(page).getByRole('checkbox', { name: '7 days', exact: true }).click();

    await page.route(snapshots, route => route.request().method() === 'POST' ? route.fulfill({ status: 500 }) : route.continue());
    await publish.click();
    await expect(page.getByText("Couldn't publish the snapshot.")).toBeVisible();
    await expect(panel(page).getByText('No live links. Published copies show up here.')).toBeVisible();
    await page.unroute(snapshots);

    await publish.click();
    await expect(panel(page).getByText('Live links · 1')).toBeVisible();
    await expect(panel(page).getByText('7', { exact: true })).toBeVisible();
    await expect(panel(page).getByText('days left', { exact: true })).toBeVisible();
    await expect(panel(page).getByText(`Published ${dayMonth(today)}`)).toBeVisible();
    await expect(panel(page).getByText('No ticks yet')).toBeVisible();
    await expect(panel(page).getByText('2 entries')).toBeVisible();
    const toggle = panel(page).getByRole('button', { name: 'Show QR code' });
    await expect(toggle).toHaveAttribute('aria-expanded', 'true');
    await expect(qr).toBeVisible();
    const token = (await panel(page).getByRole('link', { name: 'Open', exact: true }).getAttribute('href'))!.split('/').pop()!;
    await toggle.click();
    await expect(toggle).toHaveAttribute('aria-expanded', 'false');
    await expect(qr).toHaveCount(0);

    await markOnSnapshot(token, tasks['Milk']);
    await panel(page).getByRole('button', { name: 'Back' }).click();
    await page.route(snapshots, route => route.abort('connectionrefused'));
    await panel(page).getByRole('option', { name: /^Public snapshots/ }).click();
    await expect(panel(page).getByText('Live links need a connection.')).toBeVisible();
    await page.unroute(snapshots);
    await panel(page).getByRole('button', { name: 'Back' }).click();
    await panel(page).getByRole('option', { name: /^Public snapshots/ }).click();
    await expect(panel(page).getByText('1 ticked')).toBeVisible();

    await page.route('**/api/snapshots/*', route => route.abort('connectionrefused'));
    await panel(page).getByRole('button', { name: 'Revoke' }).click();
    await confirm(page, 'Revoke');
    await expect(page.getByText("Couldn't revoke the snapshot.")).toBeVisible();
    await expect(panel(page).getByText('Live links · 1')).toBeVisible();
    await page.unroute('**/api/snapshots/*');
    await panel(page).getByRole('button', { name: 'Revoke' }).click();
    await confirm(page, 'Revoke');
    await expect(panel(page).getByText('No live links. Published copies show up here.')).toBeVisible();

    await goOffline(page);
    await panel(page).getByRole('checkbox', { name: '1 day', exact: true }).click();
    await expect(publish).toBeDisabled();
    await expect(panel(page).getByText('Publishing needs a connection.')).toBeVisible();
    await goOnline(page);
    await panel(page).getByRole('checkbox', { name: '7 days', exact: true }).click();
    await expect(publish).toBeEnabled();
  });

  await test.step('Public snapshots disables Publish as soon as the connection drops and enables it when it returns', async () => {
    const publish = panel(page).getByRole('button', { name: /^Publish until/ });
    await goOffline(page);
    await expect(publish).toBeDisabled();
    await expect(panel(page).getByText('Publishing needs a connection.')).toBeVisible();
    await goOnline(page);
    await expect(publish).toBeEnabled();
  });

  await closePanel(page);

  await test.step('deleting from the panel stays on the board there, and on the list screen goes to its area', async () => {
    await page.getByRole('link', { name: 'Back to area' }).click();
    await expect(page.getByRole('heading', { name: work, exact: true })).toBeVisible();
    await createList(page, spare);
    await openCardMenu(page, spare, 'Edit');
    await panel(page).getByRole('button', { name: 'Delete list' }).click();
    await expect(page.getByRole('dialog')).toContainText(`Delete “${spare}”? This can’t be undone.`);
    await confirm(page, 'Delete');
    await awaitNoDrawer(page);
    await expect(page.getByRole('heading', { name: work, exact: true })).toBeVisible();
    await expect(listCard(page, spare)).toHaveCount(0);

    await openList(page, renamed);
    const listUrl = page.url();
    await openFabAction(page, 'Edit list');
    await panel(page).getByRole('button', { name: 'Delete list' }).click();
    await expect(page.getByRole('dialog')).toContainText(`Delete “${renamed}” and its 2 tasks? This can’t be undone.`);
    await confirm(page, 'Delete');
    await expect(page.getByRole('heading', { name: work, exact: true })).toBeVisible();
    await expect(listCard(page, renamed)).toHaveCount(0);

    await page.goto(new URL(listUrl).pathname);
    await expect(page.getByText('This list is not there anymore.')).toBeVisible();
    await page.goto(`/lists/${randomUUID()}`);
    await expect(page.getByText('This list is not there anymore.')).toBeVisible();
  });

  await device.dispose();
});
