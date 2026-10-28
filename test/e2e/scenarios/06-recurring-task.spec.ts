import { expect, test } from '../fixtures/accounts';
import { createArea, openArea } from '../helpers/areas';
import { addDays, dayMonth, shortDate, utcToday, weekdayShort, weekdaysInWeekOrder } from '../helpers/dates';
import { createList, openList } from '../helpers/lists';
import { chooseProperty, panel, propertyRow } from '../helpers/panels';
import { signIn } from '../helpers/sign-in';
import { addTask, closeTask, completeTaskRow, openTask, taskRow } from '../helpers/tasks';
import { daySection, goToMyDay, pickWeekDay, setCustomRepeat, setOneTime, setRepeating } from '../helpers/today';

test('a daily task is never overdue and comes back tomorrow', async ({ page, account }) => {
  const today = utcToday();
  const task = `Water the plants ${account.suffix}`;
  const list = `Habits ${account.suffix}`;
  const area = `Home ${account.suffix}`;

  await signIn(page, account);
  await createArea(page, area);
  await createList(page, list);
  await openList(page, list);
  await addTask(page, task);
  await openTask(page, task);
  await setRepeating(page);
  await closeTask(page);
  await expect(taskRow(page, task).locator('.pspad-recurring')).toBeVisible();

  await goToMyDay(page);
  await expect(daySection(page, 'anytime')).toContainText(task);
  await expect(daySection(page, 'overdue')).toHaveCount(0);

  await completeTaskRow(page, task);
  await expect(daySection(page, 'completed')).toContainText('Completed · 1');
  await expect(daySection(page, 'anytime').locator('.pspad-row')).toHaveCount(0);

  const tomorrow = addDays(today, 1);
  if (tomorrow.getUTCDay() === 1) {
    await page.locator('.pspad-week-next').click();
  }
  await pickWeekDay(page, tomorrow);
  await expect(page.locator('.pspad-week-label')).toContainText(shortDate(tomorrow));
  await expect(daySection(page, 'anytime')).toContainText(task);
  await expect(daySection(page, 'overdue')).toHaveCount(0);

  await test.step('the panel shows a repeat as Repeat, Until and Remind rows, and ticks only today', async () => {
    await openArea(page, area);
    await openList(page, list);
    await openTask(page, task);
    await expect(propertyRow(page, 'Repeat')).toContainText('Daily');
    await expect(propertyRow(page, 'Until')).toContainText('No end date');
    await expect(propertyRow(page, 'Remind me')).toContainText('1 week before');
    await expect(panel(page).getByText('Done 1 time · 1 in a row')).toBeVisible();
    await expect(panel(page).getByRole('checkbox', { name: 'Done' })).toBeChecked();
    await closeTask(page);
  });

  const rent = `Pay rent ${account.suffix}`;
  const yesterday = addDays(today, -1);
  const inTwoDays = addDays(today, 2);
  const inThreeDays = addDays(today, 3);

  await test.step('a repeat that started yesterday shows the missed day as skipped, never overdue', async () => {
    await addTask(page, rent);
    await openTask(page, rent);
    await expect(propertyRow(page, 'Remind me')).toHaveCount(0);
    await setRepeating(page);
    await setCustomRepeat(page, { start: yesterday });
    await expect(propertyRow(page, 'Repeat')).toContainText('Daily');
    await expect(panel(page).getByText(`${weekdayShort(yesterday)} · Skipped`)).toBeVisible();
    await expect(panel(page).getByText('Overdue')).toHaveCount(0);
  });

  await test.step('custom repeats follow a moved start and save interval, unit and weekdays', async () => {
    await setCustomRepeat(page, { unit: 'week', start: inTwoDays });
    await expect(propertyRow(page, 'Repeat')).toContainText(`Weekly on ${weekdayShort(inTwoDays)}`);
    await setCustomRepeat(page, { unit: 'month', start: inThreeDays });
    await expect(propertyRow(page, 'Repeat')).toContainText(`Monthly on day ${inThreeDays.getUTCDate()}`);
    const extra = inThreeDays.getUTCDay() === 1 ? 'Tue' : 'Mon';
    await setCustomRepeat(page, { every: 3, unit: 'weeks', days: [extra] });
    await expect(propertyRow(page, 'Repeat')).toContainText(
      `Every 3 weeks on ${weekdaysInWeekOrder([extra, weekdayShort(inThreeDays)]).join(', ')}`);
    await setCustomRepeat(page, { every: 1, unit: 'year' });
    await expect(propertyRow(page, 'Repeat')).toContainText('Yearly on');
    await chooseProperty(page, 'Repeat', /^Weekly on/);
    await expect(propertyRow(page, 'Repeat')).toContainText('Weekly on');
  });

  await test.step('Until sets the end date, and going back to one-time drops the repeat and the end', async () => {
    await chooseProperty(page, 'Until', /^Tomorrow/);
    await expect(propertyRow(page, 'Until')).toContainText('Tomorrow');
    await expect(taskRow(page, rent)).toContainText(`Until ${dayMonth(tomorrow)}`);
    await setOneTime(page);
    await expect(propertyRow(page, 'Repeat')).toHaveCount(0);
    await expect(propertyRow(page, 'Due')).toContainText('No due date');
    await expect(taskRow(page, rent)).not.toContainText('Until');
    await closeTask(page);
  });
});
