import { expect, test } from '../fixtures/accounts';
import { createArea } from '../helpers/areas';
import { addDays, shortDate, utcToday } from '../helpers/dates';
import { createList, openList } from '../helpers/lists';
import { signIn } from '../helpers/sign-in';
import { addTask, closeTask, openTask, taskRow } from '../helpers/tasks';
import { closePanel, propertyRow } from '../helpers/panels';
import { dayLabel, daySection, goToMyDay, openNewTaskFromFab, pickCalendarDay, pickWeekDay, setDueDate, setRepeating } from '../helpers/today';

test('an overdue task shows in red and the week strip moves the day', async ({ page, account }) => {
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
  await expect(taskRow(page, task)).toHaveClass(/pspad-overdue/);
  await expect(dayLabel(page)).toHaveText(`Today · ${shortDate(today)}`);
  await expect(page.getByText('Nothing planned for today.')).toBeHidden();

  await page.locator('.pspad-week-prev').click();
  await expect(dayLabel(page)).toHaveText(shortDate(addDays(today, -7)));
  await page.locator('.pspad-week-next').click();
  await expect(dayLabel(page)).toHaveText(`Today · ${shortDate(today)}`);

  const inTwoDays = addDays(today, 2);
  await page.locator('.pspad-week-pick').click();
  await pickCalendarDay(page.locator('.pspad-day-dialog'), inTwoDays);
  await expect(dayLabel(page)).toHaveText(shortDate(inTwoDays));

  await test.step('a later day still carries the overdue task, and the FAB adds a task due that day', async () => {
    await expect(daySection(page, 'overdue')).toContainText(task);
    await openNewTaskFromFab(page);
    await expect(propertyRow(page, 'Due')).toContainText(shortDate(inTwoDays));
    await closePanel(page);
  });

  await page.locator('.pspad-week-today').click();
  await expect(dayLabel(page)).toHaveText(`Today · ${shortDate(today)}`);

  if (today.getUTCDay() === 1) {
    await page.locator('.pspad-week-prev').click();
  }
  await pickWeekDay(page, yesterday);
  await expect(dayLabel(page)).toHaveText(`Yesterday · ${shortDate(yesterday)}`);
  await expect(daySection(page, 'completed')).toContainText(`Nothing completed on ${shortDate(yesterday)}.`);

  await test.step('a past day shows only what was completed, and its FAB adds a task due today', async () => {
    await expect(page.getByText(task)).toBeHidden();
    await openNewTaskFromFab(page);
    await expect(propertyRow(page, 'Due')).toContainText('Today');
    await closePanel(page);
  });

  await page.locator('.pspad-week-today').click();
  await expect(dayLabel(page)).toHaveText(`Today · ${shortDate(today)}`);
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
