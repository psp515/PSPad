import { expect, test } from '../fixtures/accounts';
import { createArea } from '../helpers/areas';
import { addDays, shortDate, utcToday } from '../helpers/dates';
import { createList, openList } from '../helpers/lists';
import { signIn } from '../helpers/sign-in';
import { addTask, closeTask, completeTaskRow, openTask, taskRow } from '../helpers/tasks';
import { daySection, goToMyDay, pickWeekDay, setRepeating } from '../helpers/today';

test('a daily task is never overdue and comes back tomorrow', async ({ page, account }) => {
  const today = utcToday();
  const task = `Water the plants ${account.suffix}`;

  await signIn(page, account);
  await createArea(page, `Home ${account.suffix}`);
  await createList(page, `Habits ${account.suffix}`);
  await openList(page, `Habits ${account.suffix}`);
  await addTask(page, task);
  await openTask(page, task);
  await setRepeating(page);
  await closeTask(page);
  await expect(taskRow(page, task).locator('.pspad-recurring')).toBeVisible();

  await goToMyDay(page);
  await expect(daySection(page, 'anytime')).toContainText(task);
  await expect(daySection(page, 'overdue')).toHaveCount(0);

  await completeTaskRow(page, task);
  await expect(daySection(page, 'completed')).toContainText('Completed (1)');
  await expect(daySection(page, 'anytime').locator('.pspad-row')).toHaveCount(0);

  const tomorrow = addDays(today, 1);
  if (tomorrow.getUTCDay() === 1) {
    await page.locator('.pspad-week-next').click();
  }
  await pickWeekDay(page, tomorrow);
  await expect(page.locator('.pspad-week-label')).toContainText(shortDate(tomorrow));
  await expect(daySection(page, 'anytime')).toContainText(task);
  await expect(daySection(page, 'overdue')).toHaveCount(0);
});
