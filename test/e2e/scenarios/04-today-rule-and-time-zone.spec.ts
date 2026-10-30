import { expect, test } from '../fixtures/accounts';
import { createArea } from '../helpers/areas';
import { addDays, shiftedZone, utcToday } from '../helpers/dates';
import { createList, openList } from '../helpers/lists';
import { goToSettings, setTimeZone } from '../helpers/settings';
import { signIn } from '../helpers/sign-in';
import { addTask, closeTask, completeTaskRow, openTask, taskRow } from '../helpers/tasks';
import { backToToday, dayShown, daySection, goToMyDay, myDayLink, setDueDate, showDay, weekDay } from '../helpers/today';

test('My Day follows the Today rule in the stored time zone', async ({ page, account }) => {
  const dueToday = `Call the bank ${account.suffix}`;
  const dueTomorrow = `Send the parcel ${account.suffix}`;
  const undated = `Read the manual ${account.suffix}`;
  const list = `Chores ${account.suffix}`;
  const today = utcToday();

  await signIn(page, account);
  await createArea(page, `Admin ${account.suffix}`);
  await createList(page, list);
  await openList(page, list);

  await addTask(page, dueToday);
  await openTask(page, dueToday);
  await setDueDate(page, 'Today');
  await closeTask(page);

  await addTask(page, dueTomorrow);
  await openTask(page, dueTomorrow);
  await setDueDate(page, 'Tomorrow');
  await closeTask(page);

  await addTask(page, undated);

  await goToMyDay(page);
  await expect(daySection(page, 'anytime').getByRole('article')).toHaveCount(1);
  await expect(taskRow(page, dueToday)).toBeVisible();
  await expect(page.getByText(dueTomorrow)).toBeHidden();
  await expect(page.getByText(undated)).toBeHidden();

  await test.step('a row names its list, empty sections stay away, and busy days are marked', async () => {
    await expect(taskRow(page, dueToday)).toContainText(list);
    await expect(page.getByRole('heading', { name: 'Overdue' })).toHaveCount(0);
    await expect(page.getByRole('heading', { name: 'Starred' })).toHaveCount(0);
    await expect(myDayLink(page)).toHaveText(/My Day\s*1$/);
    await expect(weekDay(page, today)).toHaveAccessibleName(/, has tasks$/);
    if (today.getUTCDay() !== 0) {
      await expect(weekDay(page, addDays(today, 1))).toHaveAccessibleName(/, has tasks$/);
    }
  });

  await test.step('clicking a task on My Day opens its panel', async () => {
    await openTask(page, dueToday);
    await closeTask(page);
  });

  await test.step('another day lists what is due then, still carries what is overdue, and keeps the day open', async () => {
    await showDay(page, addDays(today, 1));
    await expect(page.getByRole('heading', { name: 'Tomorrow, any time' })).toBeVisible();
    await expect(daySection(page, 'anytime')).toContainText(dueTomorrow);
    await expect(daySection(page, 'anytime')).not.toContainText(dueToday);
    await expect(daySection(page, 'overdue')).toContainText(dueToday);
    await openTask(page, dueTomorrow);
    await expect(dayShown(page, addDays(today, 1))).toBeVisible();
    await closeTask(page);
    await expect(dayShown(page, addDays(today, 1))).toBeVisible();
    await backToToday(page);
  });

  await test.step('completing a task moves it to Completed and drops the My Day count', async () => {
    await completeTaskRow(page, dueToday);
    await expect(daySection(page, 'completed')).toContainText('Completed · 1');
    await expect(daySection(page, 'anytime')).not.toContainText(dueToday);
    await expect(myDayLink(page)).toHaveText(/My Day$/);
  });

  const zone = shiftedZone();
  await goToSettings(page);
  await setTimeZone(page, zone.id);

  await goToMyDay(page);
  await expect(dayShown(page, zone.localDate)).toBeVisible();
  if (zone.id === 'Pacific/Kiritimati') {
    await expect(daySection(page, 'anytime')).toContainText(dueTomorrow);
  } else {
    await expect(page.getByText(dueTomorrow)).toBeHidden();
    await showDay(page, addDays(utcToday(), 1));
    await expect(daySection(page, 'anytime')).toContainText(dueTomorrow);
  }
});
