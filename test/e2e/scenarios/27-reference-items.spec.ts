import { expect, test } from '../fixtures/accounts';
import { anotherDevice, idOf } from '../helpers/api';
import { createArea } from '../helpers/areas';
import { shortDate, utcToday } from '../helpers/dates';
import { createList, openList } from '../helpers/lists';
import {
  awaitNoDrawer, chooseProperty, closePanel, confirm, dismissMenu, menuOptions, openFabAction, panel, propertyRow, savePanel,
} from '../helpers/panels';
import {
  addDraftField, dragRow, editFieldValue, fieldLabels, fieldRow, referenceRow, setFieldKind, storedItem,
} from '../helpers/references';
import { signIn } from '../helpers/sign-in';
import { settled } from '../helpers/sync';

test('reference items are drafted, edited, moved and listed with their labels as chips', async ({ page, account }) => {
  const home = `Home ${account.suffix}`;
  const work = `Work ${account.suffix}`;
  const recipes = `Recipes ${account.suffix}`;
  const notes = `Notes ${account.suffix}`;
  const manuals = `Manuals ${account.suffix}`;
  const shopping = `Shopping ${account.suffix}`;
  const today = utcToday();

  await signIn(page, account);
  await page.context().grantPermissions(['clipboard-read', 'clipboard-write']);
  await createArea(page, work);
  await createList(page, manuals, 'Reference');
  await createArea(page, home);
  await createList(page, shopping);
  await createList(page, notes, 'Reference');
  await createList(page, recipes, 'Reference');
  await openList(page, recipes);
  const device = await anotherDevice(page);

  await test.step('an empty reference list offers an item, never a task, a filter or a list view', async () => {
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Reference list');
    await expect(page.getByText('No items yet.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Add task' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Filter' })).toHaveCount(0);
    await openFabAction(page, 'Add item');
    await expect(panel(page).getByText('New item', { exact: true })).toBeVisible();
    await expect(panel(page).getByText('Labels', { exact: true })).toBeVisible();
    await expect(panel(page).getByText('Description', { exact: true })).toBeVisible();
    await expect(propertyRow(page, 'List')).toContainText(`${home} › ${recipes}`);
    await expect(panel(page).getByRole('button', { name: 'Delete item' })).toHaveCount(0);
  });

  await test.step('a new item keeps its draft labels in order with their kinds and its description, and is added without opening', async () => {
    await panel(page).getByPlaceholder('Item name').fill('Bigos');
    await addDraftField(page, 'Time', '45 min');
    await expect(panel(page).getByText('New item', { exact: true })).toBeVisible();
    await expect(referenceRow(page, 'Bigos')).toHaveCount(0);
    await addDraftField(page, 'Site', '/srv/recipes/bigos.md');
    await addDraftField(page, 'Serves', '4');
    await editFieldValue(page, 'Serves', '6');
    await expect(fieldRow(page, 'Serves')).toContainText('6');
    await panel(page).getByRole('button', { name: 'Remove Serves', exact: true }).click();
    await expect(panel(page).getByRole('button', { name: 'Remove Serves', exact: true })).toHaveCount(0);
    await setFieldKind(page, 'Site', 'Path');
    await dragRow(fieldRow(page, 'Site').getByLabel('Drag Site'), fieldRow(page, 'Time'));
    await expect.poll(() => fieldLabels(page)).toEqual(['Site', 'Time']);
    await panel(page).getByLabel('Description').fill('Slow cooked');
    await panel(page).getByLabel('Description').blur();
    await savePanel(page);
    await awaitNoDrawer(page);
    await expect(page).not.toHaveURL(/item=/);

    await expect.poll(async () => storedItem(device, 'Bigos')).toMatchObject({
      labels: ['Site', 'Time'],
      displays: ['path', null],
      description: 'Slow cooked',
    });

    await referenceRow(page, 'Bigos').getByText('Bigos', { exact: true }).click();
    await expect(panel(page).getByPlaceholder('Item name')).toHaveValue('Bigos');
    await expect(panel(page).getByText(`Created ${shortDate(today)} ${today.getUTCFullYear()}`)).toBeVisible();
    await expect(panel(page).getByRole('button', { name: 'Delete item' })).toBeVisible();
    await expect(panel(page).getByRole('button', { name: 'Add item' })).toHaveCount(0);
  });

  await test.step.skip('BUG: labels drafted on a new reference item are missing when it is opened, until a reload, though the server stored them (reported in PR #225)', async () => {
    await openFabAction(page, 'Add item');
    await panel(page).getByPlaceholder('Item name').fill('Gulasz');
    await addDraftField(page, 'Time', '90 min');
    await addDraftField(page, 'Serves', '4');
    await savePanel(page);
    await awaitNoDrawer(page);
    await referenceRow(page, 'Gulasz').getByText('Gulasz', { exact: true }).click();
    await expect.poll(() => fieldLabels(page)).toEqual(['Time', 'Serves']);
    await panel(page).getByRole('button', { name: 'Delete item' }).click();
    await confirm(page, 'Delete');
    await awaitNoDrawer(page);
  });

  await test.step('the panel renames and stars an item, and its list row offers reference lists only and moves it', async () => {
    await settled(page, async () => {
      await panel(page).getByPlaceholder('Item name').fill('Bigos myśliwski');
      await panel(page).getByPlaceholder('Item name').press('Tab');
    });
    await expect(referenceRow(page, 'Bigos myśliwski')).toBeVisible();
    await settled(page, () => panel(page).getByRole('button', { name: 'Star', exact: true }).click());
    await expect(panel(page).getByRole('button', { name: 'Unstar' })).toBeVisible();
    await expect(referenceRow(page, 'Bigos myśliwski').getByRole('button', { name: 'Unstar' })).toBeVisible();

    const lists = await menuOptions(page, 'List');
    await expect(lists.filter({ hasText: notes })).toHaveCount(1);
    await expect(lists.filter({ hasText: manuals })).toHaveCount(1);
    await expect(lists.filter({ hasText: shopping })).toHaveCount(0);
    await dismissMenu(page);
    await chooseProperty(page, 'List', manuals);
    await expect(propertyRow(page, 'List')).toContainText(`${work} › ${manuals}`);
    await closePanel(page);
    await expect(referenceRow(page, 'Bigos myśliwski')).toHaveCount(0);
  });

  await test.step('a new item can go to another list, and another draft starts empty', async () => {
    await openFabAction(page, 'Add item');
    await panel(page).getByPlaceholder('Item name').fill('Rosół');
    await chooseProperty(page, 'List', notes);
    await expect(propertyRow(page, 'List')).toContainText(`${home} › ${notes}`);
    await savePanel(page);
    await awaitNoDrawer(page);
    await expect(referenceRow(page, 'Rosół')).toHaveCount(0);
    const notesId = await idOf(device, 'tasklists', notes);
    await expect.poll(async () => (await storedItem(device, 'Rosół'))?.listId).toBe(notesId);

    await openFabAction(page, 'Add item');
    await panel(page).getByPlaceholder('Item name').fill('Draft');
    await addDraftField(page, 'Note', 'later');
    await closePanel(page);
    await openFabAction(page, 'Add item');
    await expect(panel(page).getByPlaceholder('Item name')).toHaveValue('');
    await expect(fieldRow(page, 'Note')).toHaveCount(0);
    await closePanel(page);
  });

  await device.dispose();
});
