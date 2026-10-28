import { expect, test } from '../fixtures/accounts';
import { createArea } from '../helpers/areas';
import { addDays, dayMonth, shortDate, utcToday } from '../helpers/dates';
import { createList, openList } from '../helpers/lists';
import { chooseProperty, closePanel, panel, propertyRow, savePanel } from '../helpers/panels';
import { signIn } from '../helpers/sign-in';
import { syncNow } from '../helpers/sync';
import {
  addStep, closeTask, openNewTask, openTask, stepNameField, taskRow,
} from '../helpers/tasks';
import { setCustomRepeat, setDueDate, setOneTime, setRepeating, setTaskTime } from '../helpers/today';

test('a new task keeps everything its draft panel set', async ({ page, account }) => {
  const list = `Moving ${account.suffix}`;
  const task = `Move flat ${account.suffix}`;
  const repeating = `Water the ferns ${account.suffix}`;
  const today = utcToday();
  const inTwoDays = addDays(today, 2);
  const inFiveDays = addDays(today, 5);
  const inTenDays = addDays(today, 10);

  await signIn(page, account);
  await createArea(page, `Home ${account.suffix}`);
  await createList(page, list);
  await openList(page, list);

  await test.step('a new task in a list waits for a name and offers no list, delete, remind or description', async () => {
    await openNewTask(page);
    await expect(panel(page).getByText(`New task · Home ${account.suffix} › ${list}`, { exact: true })).toBeVisible();
    await expect(panel(page).getByRole('button', { name: 'Add task', exact: true })).toBeDisabled();
    await expect(propertyRow(page, 'List')).toHaveCount(0);
    await expect(panel(page).getByRole('button', { name: 'Delete task' })).toHaveCount(0);
    await expect(panel(page).getByLabel('Description')).toHaveCount(0);
    await expect(propertyRow(page, 'Remind me')).toHaveCount(0);
  });

  await test.step('closing the draft forgets its steps', async () => {
    await addStep(page, 'Stale step');
    await closePanel(page);
    await openNewTask(page);
    await expect(stepNameField(page, 'Stale step')).toHaveCount(0);
  });

  await test.step('steps, star, due date, reminder, priority and time land on the task Enter adds', async () => {
    await panel(page).getByPlaceholder('Task name').fill(task);
    await addStep(page, 'Order boxes');
    await addStep(page, 'Pack the kitchen');
    await panel(page).getByRole('button', { name: 'Star', exact: true }).click();
    await chooseProperty(page, 'Due', /^In 2 days/);
    await chooseProperty(page, 'Remind me', '3 days before');
    await chooseProperty(page, 'Priority', 'Medium');
    await setTaskTime(page, '09:30');
    await panel(page).getByPlaceholder('Task name').press('Enter');
    await expect(panel(page).getByPlaceholder('Task name')).toBeHidden();

    const row = taskRow(page, task);
    await expect(row).toContainText(dayMonth(inTwoDays));
    await expect(row).toContainText('09:30');
    await expect(row).toContainText('0/2');
    await expect(row.getByRole('img', { name: 'Medium priority' })).toBeVisible();
    await expect(row.getByRole('button', { name: 'Unstar' })).toBeVisible();

    await openTask(page, task);
    await expect(propertyRow(page, 'Due')).toContainText(shortDate(inTwoDays));
    await expect(propertyRow(page, 'Remind me')).toContainText('3 days before');
    await expect(propertyRow(page, 'Priority')).toContainText('Medium');
    await expect(panel(page).getByRole('textbox', { name: 'Start' })).toHaveValue('09:30');
    const steps = panel(page).getByRole('textbox', { name: /^Rename / });
    await expect(steps).toHaveCount(2);
    await expect(steps.nth(0)).toHaveValue('Order boxes');
    await expect(steps.nth(1)).toHaveValue('Pack the kitchen');
    await closeTask(page);
  });

  await test.step('a repeating draft drops a due date before its start and keeps its rule, end and reminder', async () => {
    await openNewTask(page);
    await panel(page).getByPlaceholder('Task name').fill(repeating);
    await setRepeating(page);
    await expect(propertyRow(page, 'Repeat')).toContainText('Daily');
    await expect(propertyRow(page, 'Until')).toContainText('No end date');
    await setOneTime(page);
    await expect(propertyRow(page, 'Repeat')).toHaveCount(0);
    await expect(propertyRow(page, 'Due')).toContainText('No due date');
    await setRepeating(page);

    await chooseProperty(page, 'Until', /^Tomorrow/);
    await expect(propertyRow(page, 'Until')).toContainText('Tomorrow');
    await setCustomRepeat(page, { every: 2, start: inFiveDays });
    await expect(propertyRow(page, 'Repeat')).toContainText('Every 2 days');
    await expect(propertyRow(page, 'Until')).toContainText('No end date');
    await setDueDate(page, inTenDays);
    await expect(propertyRow(page, 'Until')).toContainText(shortDate(inTenDays));
    await chooseProperty(page, 'Remind me', '1 month before');
    await savePanel(page);

    await syncNow(page);
    await expect(taskRow(page, repeating)).toContainText(`Until ${dayMonth(inTenDays)}`);
    await openTask(page, repeating);
    await expect(propertyRow(page, 'Repeat')).toContainText('Every 2 days');
    await expect(propertyRow(page, 'Until')).toContainText(shortDate(inTenDays));
    await expect(propertyRow(page, 'Remind me')).toContainText('1 month before');
    await closeTask(page);
  });
});
