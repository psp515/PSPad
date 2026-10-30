import { expect, test } from '../fixtures/accounts';
import { anotherDevice, idOf } from '../helpers/api';
import { pageHeading, createArea, listCard, openCardMenu } from '../helpers/areas';
import { addDays, utcToday } from '../helpers/dates';
import { createList } from '../helpers/lists';
import { awaitNoDrawer, closePanel, confirm, openFabAction, openMenuItems, panel, savePanel } from '../helpers/panels';
import { signIn } from '../helpers/sign-in';
import { syncNow } from '../helpers/sync';
import { createTasksOn, taskRow } from '../helpers/tasks';

test('an area board previews each list as a card', async ({ page, account }) => {
  const area = `Kitchen ${account.suffix}`;
  const groceries = `Groceries ${account.suffix}`;
  const chores = `Chores ${account.suffix}`;
  const items = Array.from({ length: 7 }, (_, index) => `Item ${index + 1} ${account.suffix}`);
  const today = utcToday();

  await signIn(page, account);
  await createArea(page, area);
  await createList(page, groceries);
  await createList(page, chores);

  await test.step('an empty task card offers to add one, and its header button adds to that list too', async () => {
    const card = listCard(page, chores);
    await expect(card).toContainText('No open tasks');
    await card.getByRole('button', { name: `Add a task to ${chores}` }).click();
    await expect(panel(page).getByText(`New task · ${area} › ${chores}`)).toBeVisible();
    await panel(page).getByPlaceholder('Task name').fill('Sweep');
    await savePanel(page);
    await awaitNoDrawer(page);
    await expect(card).toContainText('Sweep');
    await expect(card).not.toContainText('No open tasks');

    await card.getByRole('button', { name: 'Add task', exact: true }).click();
    await expect(panel(page).getByText(`New task · ${area} › ${chores}`)).toBeVisible();
    await closePanel(page);
  });

  const device = await anotherDevice(page);
  await test.step('tasks sent from another device show up on their card without a reload', async () => {
    await createTasksOn(device, await idOf(device, 'tasklists', groceries), [
      ...items.map(name => ({ name })),
      { name: 'Bought bread', completed: true },
      { name: 'Dropped idea', deleted: true },
      { name: 'Old habit', repeatsFrom: addDays(today, -7), until: addDays(today, -1) },
    ]);
    await createTasksOn(device, await idOf(device, 'tasklists', chores), [
      { name: 'Water plants', repeatsFrom: addDays(today, -1), every: 2 },
      { name: 'Read a book', repeatsFrom: today, tickedToday: true },
    ]);
    await expect(listCard(page, groceries)).toContainText('7 open tasks');
    await expect(listCard(page, chores)).toContainText('Read a book');
  });

  await test.step('a card previews five open tasks, counts every one, and Show all opens the list', async () => {
    const card = listCard(page, groceries);
    await expect(card.getByText(/^Item \d /)).toHaveCount(5);
    for (const hidden of ['Bought bread', 'Dropped idea', 'Old habit']) {
      await expect(card).not.toContainText(hidden);
    }
    await expect(pageHeading(page)).toHaveAccessibleDescription('2 lists · 10 open tasks');
    await expect(listCard(page, chores).getByRole('link', { name: /^Show all/ })).toHaveCount(0);
    await card.getByRole('link', { name: 'Show all 7' }).click();
    await expect(page.getByRole('heading', { level: 1 })).toContainText(groceries);
    await expect(page.getByText(items[6])).toBeVisible();
    await page.getByRole('link', { name: 'Back to area' }).click();
    await expect(page.getByRole('heading', { name: area, exact: true })).toBeVisible();
  });

  await test.step('ticking a repeat on a day it skips saves nothing, and ticking a ticked repeat unticks today', async () => {
    const skipped = taskRow(page, 'Water plants').getByRole('checkbox');
    const ticked = taskRow(page, 'Read a book').getByRole('checkbox');
    await expect(ticked).toBeChecked();
    await skipped.click();
    await ticked.click();
    await expect(ticked).not.toBeChecked();
    await syncNow(page);
    await page.reload();
    await expect(page.getByRole('heading', { name: area, exact: true })).toBeVisible();
    await expect(skipped).not.toBeChecked();
    await expect(ticked).not.toBeChecked();
  });

  await test.step('a repeat ticked on a day it skips does not show ticked', async () => {
    const skipped = taskRow(page, 'Water plants').getByRole('checkbox');
    await skipped.click();
    await syncNow(page);
    await expect(skipped).not.toBeChecked();
  });

  await test.step('collapsing a card hides its tasks until it is expanded, also across a reload', async () => {
    await page.getByRole('button', { name: `Collapse ${groceries}` }).click();
    await expect(listCard(page, groceries).getByText(/^Item \d /).first()).toBeHidden();
    await page.reload();
    await expect(page.getByRole('button', { name: `Expand ${groceries}` })).toBeVisible();
    await expect(listCard(page, groceries).getByText(/^Item \d /).first()).toBeHidden();
    await page.getByRole('button', { name: `Expand ${groceries}` }).click();
    await expect(listCard(page, groceries).getByText(/^Item \d /).first()).toBeVisible();
  });

  await test.step('the board counts lists and open tasks, and a card menu edits or deletes its list', async () => {
    await listCard(page, groceries).getByRole('button', { name: 'Actions' }).click();
    await expect(openMenuItems(page)).toHaveText(['Edit', 'Delete']);
    await openMenuItems(page).getByText('Edit', { exact: true }).click();
    await expect(panel(page).getByText(`List · ${area}`, { exact: true })).toBeVisible();
    await expect(panel(page).getByPlaceholder('List name')).toHaveValue(groceries);
    await closePanel(page);

    await openFabAction(page, 'Delete area');
    await expect(page.getByRole('dialog')).toContainText(`Delete “${area}” and its 2 lists and 12 tasks? This can’t be undone.`);
    await confirm(page, 'Cancel');

    await openCardMenu(page, chores, 'Delete');
    await expect(page.getByRole('dialog')).toContainText(`Delete “${chores}” and its 3 tasks? This can’t be undone.`);
    await confirm(page, 'Delete');
    await expect(listCard(page, chores)).toHaveCount(0);
    await expect(listCard(page, groceries)).toBeVisible();
    await expect(pageHeading(page)).toHaveAccessibleDescription('1 list · 7 open tasks');
  });

  await device.dispose();
});
