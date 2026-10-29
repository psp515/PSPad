import { expect, test } from '../fixtures/accounts';
import { createArea, openArea } from '../helpers/areas';
import { addDays, shortDate, utcToday } from '../helpers/dates';
import { createList, listRowNames, openList } from '../helpers/lists';
import {
  awaitNoDrawer, chooseProperty, clearProperty, confirm, dismissMenu, menuOptions, panel, propertyRow,
} from '../helpers/panels';
import { signIn } from '../helpers/sign-in';
import { anotherDevice, idOf } from '../helpers/api';
import { addTask, closeTask, openTask, renameTask, taskRow } from '../helpers/tasks';
import { setCustomLeadTime, setDueDate, setTaskTime } from '../helpers/today';

test('the task panel edits a task in place, and deleting asks first', async ({ page, account }) => {
  const home = `Home ${account.suffix}`;
  const office = `Office ${account.suffix}`;
  const shopping = `Shopping ${account.suffix}`;
  const desk = `Desk ${account.suffix}`;
  const recipes = `Recipes ${account.suffix}`;
  const task = `Buy milk ${account.suffix}`;
  const renamed = `Buy oat milk ${account.suffix}`;
  const today = utcToday();
  const later = addDays(today, 12);

  await signIn(page, account);
  const device = await anotherDevice(page);
  await createArea(page, office);
  await createList(page, desk);
  await createArea(page, home);
  await createList(page, shopping);
  await createList(page, recipes, 'Reference');
  await openList(page, shopping);
  await addTask(page, task);

  await test.step('an open task names where it lives and when it was made, and shows only the rows that apply', async () => {
    await openTask(page, task);
    await expect(panel(page).getByText(`${home} › ${shopping}`, { exact: true }).first()).toBeVisible();
    await expect(propertyRow(page, 'List')).toContainText(`${home} › ${shopping}`);
    await expect(panel(page).getByText(`Created ${shortDate(today)} ${today.getUTCFullYear()}`)).toBeVisible();
    await expect(panel(page).getByRole('button', { name: 'Delete task' })).toBeVisible();
    await expect(panel(page).getByRole('button', { name: 'Add task', exact: true })).toHaveCount(0);
    await expect(propertyRow(page, 'Due')).toContainText('No due date');
    await expect(propertyRow(page, 'Remind me')).toHaveCount(0);
    await expect(panel(page).getByRole('textbox', { name: 'Start' })).toHaveCount(0);
  });

  await test.step('renaming takes effect at once, and Enter on the name creates nothing', async () => {
    await renameTask(page, renamed);
    await expect(taskRow(page, renamed)).toBeVisible();
    await expect(listRowNames(page)).toHaveCount(1);
  });

  await test.step('the header star stars the task', async () => {
    await panel(page).getByRole('button', { name: 'Star', exact: true }).click();
    await expect(panel(page).getByRole('button', { name: 'Unstar' })).toBeVisible();
    await expect(taskRow(page, renamed).getByRole('button', { name: 'Unstar' })).toBeVisible();
  });

  await test.step('a due date brings in Remind and Time, Remind explains and keeps its lead time, and an undated task shows no time', async () => {
    await chooseProperty(page, 'Due', /^Tomorrow/);
    await expect(propertyRow(page, 'Due')).toContainText('Tomorrow');
    await expect(propertyRow(page, 'Remind me')).toContainText('1 week before');
    await expect(panel(page).getByRole('textbox', { name: 'Start' })).toBeVisible();
    await setDueDate(page, later);
    await expect(propertyRow(page, 'Due')).toContainText(shortDate(later));
    await setTaskTime(page, '08:15');
    await expect(taskRow(page, renamed)).toContainText('08:15');

    await test.step('a pull right after a time change does not undo it on screen', async () => {
      const taskId = await idOf(device, 'todotasks', renamed);
      await setTaskTime(page, '09:45');
      await device.send('RenameTask', { taskId, name: `${renamed} from elsewhere` });
      await expect(taskRow(page, `${renamed} from elsewhere`)).toBeVisible();
      await expect(panel(page).getByRole('textbox', { name: 'Start' })).toHaveValue('09:45');
      await device.send('RenameTask', { taskId, name: renamed });
      await expect(taskRow(page, renamed)).toContainText('09:45');
      await closeTask(page);
      await openTask(page, renamed);
      await expect(panel(page).getByRole('textbox', { name: 'Start' })).toHaveValue('09:45');
    });

    await menuOptions(page, 'Remind me');
    await expect(page.getByText(/^How early the task shows in Coming up/)).toBeVisible();
    await dismissMenu(page);
    await chooseProperty(page, 'Remind me', '3 days before');
    await expect(propertyRow(page, 'Remind me')).toContainText('3 days before');
    await setCustomLeadTime(page, 2, 'months');
    await expect(propertyRow(page, 'Remind me')).toContainText('2 months before');
    await clearProperty(page, 'Remind me');
    await expect(propertyRow(page, 'Remind me')).toContainText('1 week before');

    await clearProperty(page, 'Due');
    await expect(propertyRow(page, 'Due')).toContainText('No due date');
    await expect(taskRow(page, renamed)).not.toContainText('08:15');
    await expect(propertyRow(page, 'Remind me')).toHaveCount(0);
    await expect(panel(page).getByRole('textbox', { name: 'Start' })).toHaveCount(0);
    await closeTask(page);
    await openTask(page, renamed);
    await expect(propertyRow(page, 'Due')).toContainText('No due date');
    await expect(propertyRow(page, 'Remind me')).toHaveCount(0);
  });

  await test.step('priority and a notes marker show on the row, and the description saves rendered', async () => {
    await chooseProperty(page, 'Priority', 'High');
    await expect(propertyRow(page, 'Priority')).toContainText('High');
    await expect(taskRow(page, renamed).getByRole('img', { name: 'High priority' })).toBeVisible();
    await panel(page).getByLabel('Description').fill('**Whole** milk');
    await panel(page).getByLabel('Description').blur();
    await expect(panel(page).getByRole('strong')).toHaveText('Whole');
    await expect(taskRow(page, renamed).getByRole('img', { name: 'Description' })).toBeVisible();
    await closeTask(page);
    await openTask(page, renamed);
    await expect(panel(page).getByRole('strong')).toHaveText('Whole');
  });

  await test.step('the list row offers task lists only and moves the task to another area at once', async () => {
    const lists = await menuOptions(page, 'List');
    await expect(lists.filter({ hasText: desk })).toHaveCount(1);
    await expect(lists.filter({ hasText: recipes })).toHaveCount(0);
    await dismissMenu(page);
    await chooseProperty(page, 'List', desk);
    await expect(panel(page).getByText(`${office} › ${desk}`, { exact: true }).first()).toBeVisible();
    await expect(propertyRow(page, 'List')).toContainText(`${office} › ${desk}`);
    await closeTask(page);
    await expect(taskRow(page, renamed)).toHaveCount(0);
  });

  await test.step('deleting asks first: cancelling keeps the task, confirming removes it for good', async () => {
    await openArea(page, office);
    await openList(page, desk);
    await openTask(page, renamed);
    await panel(page).getByRole('button', { name: 'Delete task' }).click();
    await expect(page.getByRole('dialog')).toContainText(`Delete “${renamed}”?`);
    await confirm(page, 'Cancel');
    await expect(panel(page).getByPlaceholder('Task name')).toHaveValue(renamed);
    await panel(page).getByRole('button', { name: 'Delete task' }).click();
    await confirm(page, 'Delete');
    await awaitNoDrawer(page);
    await expect(taskRow(page, renamed)).toHaveCount(0);

    await page.goBack();
    await expect(page).toHaveURL(/task=/);
    await expect(panel(page).getByPlaceholder('Task name')).toHaveCount(0);
  });

  await device.dispose();
});
