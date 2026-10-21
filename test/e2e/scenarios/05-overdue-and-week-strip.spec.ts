import { expect, test } from '../fixtures/accounts';
import { createArea } from '../helpers/areas';
import { addDays, shortDate, utcToday } from '../helpers/dates';
import { createList, openList } from '../helpers/lists';
import { signIn } from '../helpers/sign-in';
import { addTask, closeTask, openTask, taskRow } from '../helpers/tasks';
import { dayLabel, daySection, goToMyDay, pickCalendarDay, pickWeekDay, setDueDate } from '../helpers/today';

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

  await page.locator('.pspad-week-prev').click();
  await expect(dayLabel(page)).toHaveText(shortDate(addDays(today, -7)));
  await page.locator('.pspad-week-next').click();
  await expect(dayLabel(page)).toHaveText(`Today · ${shortDate(today)}`);

  const inTwoDays = addDays(today, 2);
  await page.locator('.pspad-week-pick').click();
  await pickCalendarDay(page.locator('.pspad-day-dialog'), inTwoDays);
  await expect(dayLabel(page)).toHaveText(shortDate(inTwoDays));

  await page.locator('.pspad-week-today').click();
  await expect(dayLabel(page)).toHaveText(`Today · ${shortDate(today)}`);

  if (today.getUTCDay() === 1) {
    await page.locator('.pspad-week-prev').click();
  }
  await pickWeekDay(page, yesterday);
  await expect(dayLabel(page)).toHaveText(`Yesterday · ${shortDate(yesterday)}`);
  await expect(daySection(page, 'completed')).toContainText(`Nothing completed on ${shortDate(yesterday)}.`);

  await page.locator('.pspad-week-today').click();
  await expect(dayLabel(page)).toHaveText(`Today · ${shortDate(today)}`);
  await expect(daySection(page, 'overdue')).toContainText(task);
});
