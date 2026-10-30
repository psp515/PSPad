import { expect, test } from '../fixtures/accounts';
import { anotherDevice, idOf } from '../helpers/api';
import { pageHeading, createArea } from '../helpers/areas';
import { addDays, utcToday } from '../helpers/dates';
import { createList, listIdFromUrl, openList } from '../helpers/lists';
import { signIn } from '../helpers/sign-in';
import { syncNow } from '../helpers/sync';
import { createTasksOn, taskRow } from '../helpers/tasks';

test('a list page shows its own tasks, with done and ended ones under Completed', async ({ page, account }) => {
  const home = `Home ${account.suffix}`;
  const errands = `Errands ${account.suffix}`;
  const desk = `Desk ${account.suffix}`;
  const today = utcToday();

  await signIn(page, account);
  await createArea(page, home);
  await createList(page, desk);
  await createList(page, errands);
  await openList(page, errands);
  const heading = pageHeading(page);

  const device = await anotherDevice(page);
  await test.step('tasks from another device appear without a reload, done and ended ones under Completed', async () => {
    await createTasksOn(device, await idOf(device, 'tasklists', desk), [{ name: 'Fix printer' }]);
    await createTasksOn(device, listIdFromUrl(page), [
      { name: 'Milk' },
      { name: 'Milk' },
      { name: 'Bread' },
      { name: 'Paid rent', completed: true },
      { name: 'Old habit', repeatsFrom: addDays(today, -7), until: addDays(today, -1) },
      { name: 'Dropped', deleted: true },
      { name: 'Water plants', repeatsFrom: addDays(today, -1), every: 2 },
      { name: 'Read a book', repeatsFrom: today, tickedToday: true },
    ]);
    await expect(heading).toHaveAccessibleDescription(`${home} · 5 open tasks`);
    await expect(page.getByText('No tasks yet.')).toHaveCount(0);
    await expect(page.getByText('Milk', { exact: true })).toHaveCount(2);
    await expect(page.getByText('Fix printer')).toHaveCount(0);
    await expect(page.getByText('Dropped')).toHaveCount(0);
    await page.getByText('Completed · 2').click();
    await expect(taskRow(page, 'Paid rent').getByRole('checkbox')).toBeChecked();
    await expect(taskRow(page, 'Old habit').getByRole('checkbox')).toBeChecked();
  });

  await test.step('ticking an ended repeat or a repeat on a day it skips saves nothing; a ticked repeat unticks today', async () => {
    await taskRow(page, 'Old habit').getByRole('checkbox').click();
    await taskRow(page, 'Water plants').getByRole('checkbox').click();
    await taskRow(page, 'Read a book').getByRole('checkbox').click();
    await expect(taskRow(page, 'Read a book').getByRole('checkbox')).not.toBeChecked();
    await syncNow(page);
    await page.reload();
    await expect(heading).toHaveAccessibleDescription(`${home} · 5 open tasks`);
    await page.getByText('Completed · 2').click();
    await expect(taskRow(page, 'Old habit').getByRole('checkbox')).toBeChecked();
    await expect(taskRow(page, 'Water plants').getByRole('checkbox')).not.toBeChecked();
    await expect(taskRow(page, 'Read a book').getByRole('checkbox')).not.toBeChecked();
  });

  await test.step('completing one of two same-named tasks leaves the other open on its own row', async () => {
    await taskRow(page, 'Milk').first().getByRole('checkbox').click();
    await expect(page.getByText('Completed · 3')).toBeVisible();
    await expect(heading).toHaveAccessibleDescription(`${home} · 4 open tasks`);
    await expect(taskRow(page, 'Milk').getByRole('checkbox', { checked: false })).toHaveCount(1);
    await expect(taskRow(page, 'Milk').getByRole('checkbox', { checked: true })).toHaveCount(1);
  });

  await device.dispose();
});
