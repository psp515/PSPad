import { expect, test } from '../fixtures/accounts';
import { createArea } from '../helpers/areas';
import { addDays, isoWeek, monthTitle, shortDate, utcToday } from '../helpers/dates';
import { createList, openList } from '../helpers/lists';
import { signIn } from '../helpers/sign-in';
import { addTask, closeTask, openTask, taskRow } from '../helpers/tasks';
import { closePanel, propertyRow } from '../helpers/panels';
import {
  dayLabel, daySection, goToMyDay, openNewTaskFromFab, pickCalendarDay, pickWeekDay, setDueDate, setRepeating, swipeWeek, weekDay,
} from '../helpers/today';

test('an overdue task is marked overdue and the week strip moves the day', async ({ page, account }) => {
  const today = utcToday();
  const yesterday = addDays(today, -1);
  const task = `Pay the invoice ${account.suffix}`;

  await signIn(page, account);
  await createArea(page, `Office ${account.suffix}`);
  await createList(page, `Bills ${account.suffix}`);
  await openList(page, `Bills ${account.suffix}`);
  await addTask(page, task);
  await openTask(page, task);
  await setDueDate(page, yesterday);
  await closeTask(page);

  await goToMyDay(page);
  await expect(daySection(page, 'overdue')).toContainText(task);
  await expect(taskRow(page, task).getByText('Overdue', { exact: true })).toBeAttached();
  await expect(dayLabel(page, `Today · ${shortDate(today)}`)).toBeVisible();
  await expect(page.getByText('Nothing planned for today.')).toBeHidden();

  await test.step('today is current and picked, there is no Today button on it, the week is named, and a swipe turns the week', async () => {
    await expect(weekDay(page, today)).toHaveAttribute('aria-current', 'date');
    await expect(weekDay(page, today)).toHaveAttribute('aria-pressed', 'true');
    await expect(page.getByRole('button', { name: 'Today', exact: true })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Pick a date' })).toBeVisible();
    await expect(page.getByText(`Week ${isoWeek(today)} · ${monthTitle(today)}`, { exact: true })).toBeVisible();
    await swipeWeek(page, 'left');
    await expect(dayLabel(page, shortDate(addDays(today, 7)))).toBeVisible();
    await expect(weekDay(page, today)).toHaveCount(0);
    await swipeWeek(page, 'right');
    await expect(dayLabel(page, `Today · ${shortDate(today)}`)).toBeVisible();
  });

  await page.getByRole('button', { name: 'Previous week', exact: true }).click();
  await expect(dayLabel(page, shortDate(addDays(today, -7)))).toBeVisible();
  await page.getByRole('button', { name: 'Next week', exact: true }).click();
  await expect(dayLabel(page, `Today · ${shortDate(today)}`)).toBeVisible();

  const inTwoDays = addDays(today, 2);
  await page.getByRole('button', { name: 'Pick a date', exact: true }).click();
  await pickCalendarDay(page, inTwoDays);
  await expect(dayLabel(page, shortDate(inTwoDays))).toBeVisible();
  await expect(weekDay(page, inTwoDays)).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByRole('button', { name: 'Today', exact: true })).toBeVisible();

  await test.step('a later day still carries the overdue task, and the FAB adds a task due that day', async () => {
    await expect(daySection(page, 'overdue')).toContainText(task);
    await openNewTaskFromFab(page);
    await expect(propertyRow(page, 'Due')).toContainText(shortDate(inTwoDays));
    await closePanel(page);
  });

  await page.getByRole('button', { name: 'Today', exact: true }).click();
  await expect(dayLabel(page, `Today · ${shortDate(today)}`)).toBeVisible();

  if (today.getUTCDay() === 1) {
    await page.getByRole('button', { name: 'Previous week', exact: true }).click();
  }
  await pickWeekDay(page, yesterday);
  await expect(dayLabel(page, `Yesterday · ${shortDate(yesterday)}`)).toBeVisible();
  await expect(daySection(page, 'completed')).toContainText(`Nothing completed on ${shortDate(yesterday)}.`);

  await test.step('a past day shows only what was completed, and its FAB adds a task due today', async () => {
    await expect(page.getByText(task)).toBeHidden();
    await openNewTaskFromFab(page);
    await expect(propertyRow(page, 'Due')).toContainText('Today');
    await closePanel(page);
  });

  await page.getByRole('button', { name: 'Today', exact: true }).click();
  await expect(dayLabel(page, `Today · ${shortDate(today)}`)).toBeVisible();
  await expect(daySection(page, 'overdue')).toContainText(task);

  await test.step('making the overdue task repeat drops its due date, so it is no longer overdue', async () => {
    await openTask(page, task);
    await setRepeating(page);
    await expect(propertyRow(page, 'Until')).toContainText('No end date');
    await closeTask(page);
    await expect(page.getByRole('heading', { name: 'Overdue' })).toHaveCount(0);
    await expect(daySection(page, 'anytime')).toContainText(task);
  });
});
