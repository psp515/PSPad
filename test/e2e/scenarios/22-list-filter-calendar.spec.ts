import { expect, test } from '../fixtures/accounts';
import { createArea } from '../helpers/areas';
import { addDays, isoDate, utcToday } from '../helpers/dates';
import { createList, listRowNames, monthDay, openList, pickListFilter, showListAs, sortListBy } from '../helpers/lists';
import { pickFromPropertyRow } from '../helpers/panels';
import { signIn } from '../helpers/sign-in';
import { addTask, closeTask, openTask, taskRow } from '../helpers/tasks';
import { setDueDate } from '../helpers/today';

test('a list filters, sorts, remembers its choice and shows a month', async ({ page, account }) => {
  const area = `Home ${account.suffix}`;
  const list = `Moving ${account.suffix}`;
  const zebra = `Zebra crate ${account.suffix}`;
  const apple = `Apple boxes ${account.suffix}`;
  const plain = `Plain chore ${account.suffix}`;
  const tomorrow = addDays(utcToday(), 1);

  await signIn(page, account);
  await createArea(page, area);
  await createList(page, list);
  await openList(page, list);

  for (const name of [zebra, apple]) {
    await addTask(page, name);
    await openTask(page, name);
    await pickFromPropertyRow(page, 'pspad-task-priority', 'High');
    await setDueDate(page, 'Tomorrow');
    await closeTask(page);
  }
  await addTask(page, plain);

  await pickListFilter(page, 'High');
  await expect(taskRow(page, plain)).toHaveCount(0);
  await sortListBy(page, 'Name');
  await expect(listRowNames(page)).toHaveText([apple, zebra]);

  await page.reload();
  await expect(page.locator('.pspad-list-filter-badge')).toHaveText('1');
  await expect(listRowNames(page)).toHaveText([apple, zebra]);

  await showListAs(page, 'Calendar');
  const day = monthDay(page, isoDate(tomorrow));
  if ((await day.count()) === 0) {
    await page.locator('.pspad-month-strip').getByRole('button', {
      name: tomorrow.toLocaleString('en-US', { month: 'long', year: 'numeric', timeZone: 'UTC' }),
    }).click();
  }
  await expect(day.locator('.pspad-month-entry')).toHaveCount(2);
  await day.click();

  const detail = page.locator('.pspad-month-detail');
  await expect(detail.locator('.pspad-row')).toHaveCount(2);
  await detail.locator('.pspad-row', { hasText: apple }).getByRole('checkbox').first().click();
  await expect(detail.locator('.pspad-row')).toHaveCount(1);
});
