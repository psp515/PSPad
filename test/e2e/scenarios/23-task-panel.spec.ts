import { expect, test } from '../fixtures/accounts';
import { createArea, openArea } from '../helpers/areas';
import { addDays, shortDate, utcToday } from '../helpers/dates';
import { createList, listRowNames, openList } from '../helpers/lists';
import {
  awaitNoDrawer, chooseProperty, clearProperty, confirm, dismissMenu, menuOptions, panel, propertyRow,
} from '../helpers/panels';
import { signIn } from '../helpers/sign-in';
import { pulledAfter } from '../helpers/sync';
import { anotherDevice, idOf } from '../helpers/api';
import { addTask, closeTask, openTask, renameTask, taskRow } from '../helpers/tasks';
import {
  leaveClockUntouched, pickClockHourAndLeave, setCustomLeadTime, setDueDate, setTaskTime, timeField, typeTaskTime,
} from '../helpers/today';

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

    await test.step('a typed time reads shorthand, says what is wrong, runs past midnight, and the clock changes only what is picked', async () => {
      const alert = panel(page).getByRole('alert');
      await expect(timeField(page, 'End')).toHaveCount(0);
      await expect(panel(page).getByRole('button', { name: 'Clear time' })).toHaveCount(0);
      await typeTaskTime(page, 'Start', '930');
      await expect(timeField(page, 'Start')).toHaveValue('09:30');
      await expect(timeField(page, 'End')).toBeVisible();
      await expect(taskRow(page, renamed)).toContainText('09:30');

      await typeTaskTime(page, 'Start', 'nine');
      await expect(alert).toHaveText('Enter a time like 14:30');
      await expect(timeField(page, 'Start')).toHaveValue('nine');
      await expect(taskRow(page, renamed)).toContainText('09:30');
      await typeTaskTime(page, 'Start', '2145');
      await expect(timeField(page, 'Start')).toHaveValue('21:45');
      await expect(alert).toHaveCount(0);

      await typeTaskTime(page, 'End', '21:45');
      await expect(alert).toHaveText('End must differ from start');
      await expect(taskRow(page, renamed)).not.toContainText('–');
      await typeTaskTime(page, 'End', '1');
      await expect(timeField(page, 'End')).toHaveValue('01:00');
      await expect(alert).toHaveCount(0);
      await expect(panel(page).getByText('Ends the next day (+1)')).toBeVisible();
      await expect(taskRow(page, renamed)).toContainText('21:45–01:00 (+1)');
      await typeTaskTime(page, 'End', '');
      await expect(timeField(page, 'End')).toHaveValue('');
      await expect(panel(page).getByText('Ends the next day (+1)')).toHaveCount(0);
      await expect(taskRow(page, renamed)).not.toContainText('01:00');
      await expect(taskRow(page, renamed)).toContainText('21:45');

      await leaveClockUntouched(page, 'Start');
      await expect(timeField(page, 'Start')).toHaveValue('21:45');
      await pickClockHourAndLeave(page, 'Start', 14);
      await expect(timeField(page, 'Start')).toHaveValue('14:45');
      await expect(taskRow(page, renamed)).toContainText('14:45');

      await typeTaskTime(page, 'End', '1530');
      await expect(taskRow(page, renamed)).toContainText('14:45–15:30');
      await panel(page).getByRole('button', { name: 'Clear time' }).click();
      await expect(timeField(page, 'Start')).toHaveValue('');
      await expect(timeField(page, 'End')).toHaveCount(0);
      await expect(taskRow(page, renamed)).not.toContainText('14:45');

      await typeTaskTime(page, 'Start', '8');
      await expect(timeField(page, 'Start')).toHaveValue('08:00');
      await typeTaskTime(page, 'Start', '');
      await expect(timeField(page, 'End')).toHaveCount(0);
      await expect(taskRow(page, renamed)).not.toContainText('08:00');
    });

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
    await expect(panel(page).getByPlaceholder('Add a description…')).toBeVisible();
    await expect(panel(page).getByRole('button', { name: 'Edit description' })).toHaveCount(0);
    const blank = panel(page).getByRole('textbox', { name: 'Description' });
    await blank.fill('Typed by mistake');
    await blank.press('Escape');
    await expect(blank).toHaveValue('');
    await blank.blur();
    await expect(taskRow(page, renamed).getByRole('img', { name: 'Description' })).toHaveCount(0);
    await panel(page).getByLabel('Description').fill('**Whole** milk');
    await panel(page).getByLabel('Description').blur();
    await expect(panel(page).getByRole('strong')).toHaveText('Whole');
    await expect(taskRow(page, renamed).getByRole('img', { name: 'Description' })).toBeVisible();
    await closeTask(page);
    await openTask(page, renamed);
    await expect(panel(page).getByRole('strong')).toHaveText('Whole');
  });

  await test.step('the description opens raw on a click or Edit, Escape or no change leaves it, and a link or a selection keeps the view', async () => {
    const description = panel(page).getByRole('textbox', { name: 'Description' });
    await panel(page).getByRole('strong').click();
    await expect(description).toHaveValue('**Whole** milk');
    await description.fill('**Skimmed** milk');
    await description.press('Escape');
    await expect(description).toHaveCount(0);
    await expect(panel(page).getByRole('strong')).toHaveText('Whole');

    await panel(page).getByRole('button', { name: 'Edit description' }).click();
    await expect(description).toHaveValue('**Whole** milk');
    await description.blur();
    await expect(description).toHaveCount(0);

    await panel(page).getByRole('button', { name: 'Edit description' }).click();
    await description.fill('**Whole** milk from [the shop](https://example.com/shop)');
    await description.blur();
    const link = panel(page).getByRole('link', { name: 'the shop' });
    await expect(link).toBeVisible();
    await page.context().route('https://example.com/**', route => route.fulfill({ body: 'shop' }));
    const shop = page.waitForEvent('popup');
    await link.click();
    await (await shop).close();
    await page.context().unroute('https://example.com/**');
    await expect(description).toHaveCount(0);

    const words = (await panel(page).getByRole('strong').boundingBox())!;
    await page.mouse.move(words.x + 1, words.y + words.height / 2);
    await page.mouse.down();
    await page.mouse.move(words.x + words.width - 1, words.y + words.height / 2, { steps: 5 });
    await page.mouse.up();
    await expect(description).toHaveCount(0);
  });

  await test.step('an open task panel shows the description and name another device changes, and keeps a description being edited', async () => {
    const description = panel(page).getByRole('textbox', { name: 'Description' });
    const taskId = await idOf(device, 'todotasks', renamed);
    await pulledAfter(page, () => device.send('SetTaskDescription', { taskId, description: '**Skimmed** milk' }));
    await expect(panel(page).getByRole('strong')).toHaveText('Skimmed');
    await panel(page).getByRole('button', { name: 'Edit description' }).click();
    await description.fill('**Oat** milk');
    await pulledAfter(page, () => device.send('SetTaskDescription', { taskId, description: '**Goat** milk' }));
    await expect(description).toHaveValue('**Oat** milk');
    await description.press('Escape');
    await expect(panel(page).getByRole('strong')).toHaveText('Goat');
    await pulledAfter(page, () => device.send('RenameTask', { taskId, name: `${renamed} from elsewhere` }));
    await expect(panel(page).getByPlaceholder('Task name')).toHaveValue(`${renamed} from elsewhere`);
    await pulledAfter(page, () => device.send('RenameTask', { taskId, name: renamed }));
    await expect(panel(page).getByPlaceholder('Task name')).toHaveValue(renamed);
  });

  await test.step('keeps a name being edited when another device renames the task, and saves it when done', async () => {
    const name = panel(page).getByPlaceholder('Task name');
    const typed = `${renamed} typed`;
    const taskId = await idOf(device, 'todotasks', renamed);
    await name.fill(typed);
    await pulledAfter(page, () => device.send('RenameTask', { taskId, name: `${renamed} from afar` }));
    await expect(taskRow(page, `${renamed} from afar`)).toBeVisible();
    await expect(name).toHaveValue(typed);
    await name.press('Enter');
    await expect(taskRow(page, typed)).toBeVisible();
    await renameTask(page, renamed);
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
