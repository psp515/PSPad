import { randomUUID } from 'node:crypto';
import { expect, test } from '../fixtures/accounts';
import { anotherDevice } from '../helpers/api';
import { areaSubtitle, createArea, listCard, openCardMenu } from '../helpers/areas';
import { createList, listIdFromUrl, openList } from '../helpers/lists';
import { awaitNoDrawer, closePanel, confirm, openFabAction, panel } from '../helpers/panels';
import { signIn } from '../helpers/sign-in';
import { createTasksOn } from '../helpers/tasks';

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
  const subtitle = areaSubtitle(page);
  const device = await anotherDevice(page);
  await createTasksOn(device, listIdFromUrl(page), [{ name: 'Milk' }, { name: 'Bread' }]);
  await expect(subtitle).toHaveText(`${home} · 2 open tasks`);

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
    await expect(subtitle).toHaveText(`${work} · 2 open tasks`);
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
