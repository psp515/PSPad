import { expect, test } from '../fixtures/accounts';
import { anotherDevice, idOf } from '../helpers/api';
import { pageHeading, createArea } from '../helpers/areas';
import { createGoalOn, linkTasksOn } from '../helpers/goals';
import { addDays, monthButton, shortDate, utcToday } from '../helpers/dates';
import {
  calendarDay, closeFilterChip, showCalendarMonth, createList, filterButton, listRowNames, listRows, monthStrip, openList, pickListFilter,
  showListAs, sortListBy,
} from '../helpers/lists';
import { chooseProperty } from '../helpers/panels';
import { signIn } from '../helpers/sign-in';
import { addTask, closeTask, createTasksOn, openTask, taskRow } from '../helpers/tasks';
import { setDueDate } from '../helpers/today';

test('a list filters, sorts, remembers its choice and shows a month', async ({ page, account }) => {
  const area = `Home ${account.suffix}`;
  const list = `Moving ${account.suffix}`;
  const zebra = `Zebra crate ${account.suffix}`;
  const apple = `Apple boxes ${account.suffix}`;
  const plain = `Plain chore ${account.suffix}`;
  const goal = `Move out ${account.suffix}`;
  const tomorrow = addDays(utcToday(), 1);

  await signIn(page, account);
  await createArea(page, area);
  await createList(page, list);
  await openList(page, list);

  for (const name of [zebra, apple]) {
    await addTask(page, name);
    await openTask(page, name);
    await chooseProperty(page, 'Priority', 'High');
    await setDueDate(page, 'Tomorrow');
    await closeTask(page);
  }
  await test.step('a month with every task dated has no undated line', async () => {
    await showListAs(page, 'Calendar');
    await expect(page.getByText(/no date\.$/)).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Show them in List' })).toHaveCount(0);
    await showListAs(page, 'List');
  });
  await addTask(page, plain);

  await pickListFilter(page, 'High');
  await expect(taskRow(page, plain)).toHaveCount(0);
  await sortListBy(page, 'Name');
  await expect.poll(() => listRowNames(listRows(page))).toEqual([apple, zebra]);

  await page.reload();
  await expect(filterButton(page)).toHaveText(/^Filter\s*1$/);
  await expect.poll(() => listRowNames(listRows(page))).toEqual([apple, zebra]);

  await showListAs(page, 'Calendar');
  const day = calendarDay(page, tomorrow);
  if ((await day.count()) === 0) {
    await monthButton(monthStrip(page), tomorrow).click();
  }
  await expect(day).toHaveAccessibleName(/, 2 tasks$/);
  await expect(day).toContainText(new RegExp(`${apple}[\\s\\S]*${zebra}`));
  await day.click();

  const detail = page.getByRole('region', { name: shortDate(tomorrow), exact: true });
  await expect(detail.getByRole('article')).toHaveCount(2);
  await detail.getByRole('article', { name: apple }).getByRole('checkbox').first().click();
  await expect(detail.getByRole('article')).toHaveCount(1);

  const filter = page.getByRole('button', { name: /^Filter/ }).last();
  await test.step('the month counts the undated tasks, and Show them in List lists just those', async () => {
    await page.getByRole('button', { name: 'Clear', exact: true }).click();
    await expect(page.getByText('1 task has no date.')).toBeVisible();
    await page.getByRole('button', { name: 'Show them in List' }).click();
    await expect(page.getByText(plain)).toBeVisible();
    await expect(page.getByText(zebra)).toHaveCount(0);
    await expect(filter).toHaveText(/^Filter\s*1$/);
  });

  const device = await anotherDevice(page);
  await test.step('filter chips toggle, each active group is one closable chip, and Clear keeps the sort', async () => {
    const goalId = await createGoalOn(device, goal);
    await linkTasksOn(device, goalId, [await idOf(device, 'todotasks', plain)]);
    await filter.click();
    const chip = (name: string) => page.getByRole('button', { name, exact: true });
    await expect(chip(goal)).toBeVisible();
    await chip('Starred').click();
    await expect(filter).toHaveText(/^Filter\s*2$/);
    await chip('Starred').click();
    await expect(filter).toHaveText(/^Filter\s*1$/);
    await chip(goal).click();
    await chip('High').click();
    await expect(filter).toHaveText(/^Filter\s*3$/);
    await filter.click();
    await expect(chip('Starred')).toHaveCount(0);
    await expect(page.getByText(goal, { exact: true })).toBeVisible();
    await expect(page.getByText('No date', { exact: true })).toBeVisible();
    await expect(page.getByText('No tasks match.')).toBeVisible();
    await expect(pageHeading(page)).toHaveAccessibleDescription(`${area} · 2 open tasks`);

    await closeFilterChip(page, 'High');
    await expect(filter).toHaveText(/^Filter\s*2$/);
    await expect(page.getByText(plain)).toBeVisible();
    await page.getByRole('button', { name: 'Clear', exact: true }).first().click();
    await expect(filter).toHaveText('Filter');
    await expect(page.getByRole('button', { name: /^Name/ }).last()).toBeVisible();
    await expect.poll(() => listRowNames(listRows(page).filter({ visible: true }))).toEqual([plain, zebra]);
  });
  await device.dispose();

  await test.step('a calendar day shows two names in the list order and counts the rest, a repeat shows from its start, a row names its goal, and every day is labelled', async () => {
    const today = utcToday();
    const midNextMonth = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth() + 1, 15));
    const [alpha, bravo, charlie] = ['Crate A', 'Crate B', 'Crate C'].map(name => `${name} ${account.suffix}`);
    const herbs = `Water the herbs ${account.suffix}`;
    const planner = await anotherDevice(page);
    const listId = await idOf(planner, 'tasklists', list);
    const ids = await createTasksOn(planner, listId, [
      { name: charlie, until: midNextMonth }, { name: alpha, until: midNextMonth }, { name: bravo, until: midNextMonth },
      { name: herbs, repeatsFrom: midNextMonth },
    ]);
    const packing = await createGoalOn(planner, `Pack ${account.suffix}`);
    await linkTasksOn(planner, packing, [ids[alpha]]);
    await planner.dispose();

    await showListAs(page, 'Calendar');
    await expect(calendarDay(page, today)).toHaveAttribute('aria-pressed', 'true');
    const days = page.getByRole('button', { name: /^\d{1,2} \w+, \d+ tasks?$/ });
    await expect.poll(async () => (await days.count()) % 7).toBe(0);
    expect(await days.count()).toBeGreaterThanOrEqual(28);
    await showCalendarMonth(page, midNextMonth);
    const cell = calendarDay(page, midNextMonth);
    await expect(cell).toHaveAccessibleName(/, 4 tasks$/);
    await expect(cell).toContainText(new RegExp(`${alpha}[\\s\\S]*${bravo}`));
    await expect(cell).not.toContainText(charlie);
    await expect(cell).not.toContainText(herbs);
    await expect(cell).toContainText('+2 more');
    await expect(calendarDay(page, addDays(midNextMonth, -1))).not.toContainText(herbs);
    await expect(calendarDay(page, addDays(midNextMonth, 1))).toContainText(herbs);

    await cell.click();
    await expect(cell).toHaveAttribute('aria-pressed', 'true');
    await expect(taskRow(page, alpha)).toContainText(`Pack ${account.suffix}`);
    await expect(taskRow(page, charlie)).toBeVisible();
    await expect(taskRow(page, herbs)).toBeVisible();
  });
});
