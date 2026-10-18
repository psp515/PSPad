import { expect, test } from '../fixtures/accounts';
import { createArea } from '../helpers/areas';
import { shiftedZone, shortDate } from '../helpers/dates';
import { createList, openList } from '../helpers/lists';
import { goToSettings, setTimeZone } from '../helpers/settings';
import { signIn } from '../helpers/sign-in';
import { addTask, closeTask, completeTaskRow, openTask, taskRow } from '../helpers/tasks';
import { dayLabel, daySection, goToMyDay, setDueDate } from '../helpers/today';

test('My Day follows the Today rule in the stored time zone', async ({ page, account }) => {
  const dueToday = `Call the bank ${account.suffix}`;
  const dueTomorrow = `Send the parcel ${account.suffix}`;
  const undated = `Read the manual ${account.suffix}`;

  await signIn(page, account);
  await createArea(page, `Admin ${account.suffix}`);
  await createList(page, `Chores ${account.suffix}`);
  await openList(page, `Chores ${account.suffix}`);

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
  await expect(daySection(page, 'anytime').locator('.pspad-row')).toHaveCount(1);
  await expect(taskRow(page, dueToday)).toBeVisible();
  await expect(daySection(page, 'comingup')).toContainText(dueTomorrow);
  await expect(page.getByText(undated)).toBeHidden();

  await completeTaskRow(page, dueToday);
  await expect(daySection(page, 'completed')).toContainText('Completed (1)');

  const zone = shiftedZone();
  await goToSettings(page);
  await setTimeZone(page, zone.id);

  await goToMyDay(page);
  await expect(dayLabel(page)).toContainText(shortDate(zone.localDate));
  if (zone.id === 'Pacific/Kiritimati') {
    await expect(daySection(page, 'anytime')).toContainText(dueTomorrow);
  } else {
    await expect(daySection(page, 'comingup')).toContainText(dueTomorrow);
  }
});
