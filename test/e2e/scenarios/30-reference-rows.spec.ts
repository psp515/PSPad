import { expect, test } from '../fixtures/accounts';
import { anotherDevice, idOf } from '../helpers/api';
import { areaSubtitle, createArea, listCard, openCardMenu } from '../helpers/areas';
import { createList, openList } from '../helpers/lists';
import { awaitNoDrawer, closePanel, confirm, openFabAction, panel, savePanel } from '../helpers/panels';
import { addFieldsOn, createItemsOn, referenceRow } from '../helpers/references';
import { signIn } from '../helpers/sign-in';
import { createTasksOn } from '../helpers/tasks';

test('reference items show as rows with their labels as chips, on the board and on their list', async ({ page, account }) => {
  const area = `Pantry ${account.suffix}`;
  const shopping = `Shopping ${account.suffix}`;
  const recipes = `Recipes ${account.suffix}`;

  await signIn(page, account);
  await page.context().grantPermissions(['clipboard-read', 'clipboard-write']);
  await createArea(page, area);
  await createList(page, shopping);
  await createList(page, recipes, 'Reference');
  const device = await anotherDevice(page);
  await createTasksOn(device, await idOf(device, 'tasklists', shopping), [{ name: 'Flour' }]);
  const recipesId = await idOf(device, 'tasklists', recipes);

  await test.step('a reference card adds an item without opening it, previews five, and stars or opens a row', async () => {
    const card = listCard(page, recipes);
    await expect(card).toContainText('No items yet.');
    await expect(card.getByRole('button', { name: 'Add task', exact: true })).toHaveCount(0);
    await card.getByRole('button', { name: 'Add item' }).click();
    await expect(panel(page).getByText('New item', { exact: true })).toBeVisible();
    await panel(page).getByPlaceholder('Item name').fill('Bigos');
    await savePanel(page);
    await awaitNoDrawer(page);
    await expect(page).not.toHaveURL(/item=/);
    await expect(card).toContainText('Bigos');

    const ids = await createItemsOn(device, recipesId, ['Rosół', 'Pierogi', 'Żurek', 'Barszcz', 'Gołąbki', 'Flaki'], 1);
    await addFieldsOn(device, ids['Pierogi'], [
      ['Weight', '350g'],
      ['Recipe', 'https://example.com/pierogi'],
      ['Photo', '/home/me/pierogi.jpg'],
    ]);
    await expect(card.getByRole('link', { name: 'Show all 7' })).toBeVisible();
    await expect(card.getByRole('button', { name: 'Star', exact: true })).toHaveCount(5);
    await expect(card).not.toContainText('Gołąbki');

    await referenceRow(page, 'Barszcz').getByRole('button', { name: 'Star', exact: true }).click();
    await expect(card.getByRole('button', { name: 'Unstar' })).toHaveCount(1);
    await expect(card.getByText(/^(Bigos|Rosół|Pierogi|Żurek|Barszcz)$/).first()).toHaveText('Barszcz');

    await card.getByText('Bigos', { exact: true }).click();
    await expect(panel(page).getByPlaceholder('Item name')).toHaveValue('Bigos');
    await expect(panel(page).getByText('Reference item', { exact: true })).toBeVisible();
    await closePanel(page);
  });

  await test.step.skip('BUG: a reference card counts its items as "open tasks" (reported in PR #225)', async () => {
    await expect(listCard(page, recipes)).toContainText('7 items');
    await expect(listCard(page, recipes)).not.toContainText('open tasks');
  });

  await test.step('the board counts lists, open tasks and items, and deleting warns what goes with it', async () => {
    await expect(areaSubtitle(page)).toHaveText('2 lists · 1 open task · 7 items');
    await openCardMenu(page, recipes, 'Delete');
    await expect(page.getByRole('dialog')).toContainText(`Delete “${recipes}” and its 7 items? This can’t be undone.`);
    await confirm(page, 'Cancel');
    await openFabAction(page, 'Delete area');
    await expect(page.getByRole('dialog')).toContainText(
      `Delete “${area}” and its 2 lists, 1 task and 7 items? This can’t be undone.`);
    await confirm(page, 'Cancel');
  });

  await test.step('a reference list shows starred items first, then by position, with labels as chips in order', async () => {
    await openList(page, recipes);
    const names = page.getByText(/^(Bigos|Rosół|Pierogi|Żurek|Barszcz|Gołąbki|Flaki)$/);
    await expect(names).toHaveText(['Barszcz', 'Bigos', 'Rosół', 'Pierogi', 'Żurek', 'Gołąbki', 'Flaki']);
    await referenceRow(page, 'Flaki').getByRole('button', { name: 'Star', exact: true }).click();
    await expect(referenceRow(page, 'Flaki').getByRole('button', { name: 'Unstar' })).toBeVisible();
    await expect(names).toHaveText(['Barszcz', 'Flaki', 'Bigos', 'Rosół', 'Pierogi', 'Żurek', 'Gołąbki']);

    const pierogi = referenceRow(page, 'Pierogi');
    await expect(pierogi.getByText(/^(Weight: 350 g|Recipe|Photo)$/)).toHaveText(['Weight: 350 g', 'Recipe', 'Photo']);
    const link = pierogi.getByRole('link', { name: 'Recipe' });
    await expect(link).toHaveAttribute('href', 'https://example.com/pierogi');
    await expect(link).toHaveAttribute('target', '_blank');
    await pierogi.getByText('Photo', { exact: true }).click();
    await expect(page.getByText('Copied')).toBeVisible();
    expect(await page.evaluate(() => navigator.clipboard.readText())).toBe('/home/me/pierogi.jpg');
    await awaitNoDrawer(page);

    await pierogi.getByText('Pierogi', { exact: true }).click();
    await expect(panel(page).getByPlaceholder('Item name')).toHaveValue('Pierogi');
    await closePanel(page);
  });

  await test.step('deleting the list warns how many items go with it', async () => {
    await openFabAction(page, 'Delete list');
    await expect(page.getByRole('dialog')).toContainText(`Delete “${recipes}” and its 7 items? This can’t be undone.`);
    await confirm(page, 'Delete');
    await expect(page.getByRole('heading', { name: area, exact: true })).toBeVisible();
    await expect(listCard(page, recipes)).toHaveCount(0);
  });

  await device.dispose();
});
