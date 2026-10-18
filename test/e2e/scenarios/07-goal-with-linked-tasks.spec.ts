import { expect, test } from '../fixtures/accounts';
import { createArea, openArea } from '../helpers/areas';
import { createGoal, goToGoals, goalCard, linkTaskToGoal, openGoal } from '../helpers/goals';
import { createList, openList } from '../helpers/lists';
import { signIn } from '../helpers/sign-in';
import { addTask, closeTask, completeTaskRow, openTask, taskRow } from '../helpers/tasks';
import { daySection, goToMyDay } from '../helpers/today';

test('a goal tracks its linked tasks and a star never plans a task', async ({ page, account }) => {
  const goal = `Run a marathon ${account.suffix}`;
  const done = `Buy running shoes ${account.suffix}`;
  const open = `Sign up for the race ${account.suffix}`;
  const area = `Health ${account.suffix}`;
  const list = `Training ${account.suffix}`;

  await signIn(page, account);
  await createGoal(page, goal);
  await createArea(page, area);
  await createList(page, list);
  await openList(page, list);

  for (const name of [done, open]) {
    await addTask(page, name);
    await openTask(page, name);
    await linkTaskToGoal(page, goal);
    await closeTask(page);
  }
  await completeTaskRow(page, done);

  await goToGoals(page);
  await openGoal(page, goal);
  await expect(taskRow(page, open)).toBeVisible();
  await expect(page.getByText('Completed (1)')).toBeVisible();

  await taskRow(page, open).getByRole('button', { name: 'Star' }).click();
  await goToMyDay(page);
  await expect(daySection(page, 'starred')).toContainText(open);
  await expect(daySection(page, 'anytime')).toContainText('Nothing planned for today.');

  await goToGoals(page);
  await goalCard(page, goal).locator('.pspad-goal-menu').getByRole('button').click();
  await page.getByRole('menuitem', { name: 'Mark achieved' }).click();
  const achieved = page.locator('.pspad-goals-achieved');
  await expect(achieved).toContainText('1 of 2 tasks done');
  await achieved.getByRole('button', { name: goal }).click();
  await expect(page.getByRole('heading', { level: 1 })).toContainText(goal);
  await expect(page.locator('.pspad-goal-status')).toHaveText('Achieved');

  await openArea(page, area);
  await expect(page.getByRole('link', { name: list, exact: true })).toBeVisible();
});
