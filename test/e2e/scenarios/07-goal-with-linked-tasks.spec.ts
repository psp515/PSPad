import { expect, test } from '../fixtures/accounts';
import { createArea, openArea } from '../helpers/areas';
import { createGoal, deleteGoal, goToGoals, goalCard, linkTaskToGoal, openGoal } from '../helpers/goals';
import { createList, openList } from '../helpers/lists';
import { chooseProperty, clearProperty, dismissMenu, menuOptions, propertyRow } from '../helpers/panels';
import { signIn } from '../helpers/sign-in';
import { addTask, closeTask, completeTaskRow, openTask, taskRow } from '../helpers/tasks';
import { daySection, goToMyDay } from '../helpers/today';

test('a goal tracks its linked tasks and a star never plans a task', async ({ page, account }) => {
  const goal = `Run a marathon ${account.suffix}`;
  const done = `Buy running shoes ${account.suffix}`;
  const open = `Sign up for the race ${account.suffix}`;
  const area = `Health ${account.suffix}`;
  const list = `Training ${account.suffix}`;
  const other = `Eat better ${account.suffix}`;

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
  await expect(taskRow(page, open).locator('.pspad-row-goal')).toHaveText(goal);
  await completeTaskRow(page, done);

  await openArea(page, area);
  await expect(taskRow(page, open).locator('.pspad-row-goal')).toHaveText(goal);

  await goToGoals(page);
  await openGoal(page, goal);
  await expect(taskRow(page, open)).toBeVisible();
  await expect(taskRow(page, open).locator('.pspad-row-goal')).toHaveCount(0);
  await expect(page.getByText('Completed · 1')).toBeVisible();

  await taskRow(page, open).getByRole('button', { name: 'Star' }).click();
  await goToMyDay(page);
  await expect(daySection(page, 'starred')).toContainText(open);
  await expect(daySection(page, 'anytime')).toContainText('Nothing planned for today.');
  await test.step('a My Day row names the goal holding it, and the goal itself is no row', async () => {
    await expect(taskRow(page, open)).toContainText(goal);
    await expect(taskRow(page, goal)).toHaveCount(0);
  });

  await goToGoals(page);
  await goalCard(page, goal).locator('.pspad-goal-menu').getByRole('button').click();
  await page.getByRole('menuitem', { name: 'Mark achieved' }).click();
  const achieved = page.locator('.pspad-goals-achieved');
  await expect(achieved).toContainText('1 of 2 tasks done');
  await achieved.getByRole('link', { name: goal }).click();
  await expect(page.getByRole('heading', { level: 1 })).toContainText(goal);
  await expect(page.locator('.pspad-goal-status')).toHaveText('Achieved');

  await openArea(page, area);
  await expect(page.getByRole('link', { name: list, exact: true })).toBeVisible();

  await test.step('the goal row still names an achieved goal but offers only goals in progress', async () => {
    await openTask(page, open);
    await expect(propertyRow(page, 'Goal')).toContainText(goal);
    const goals = await menuOptions(page, 'Goal');
    await expect(goals).toHaveText(['No goals in progress']);
    await dismissMenu(page);
    await closeTask(page);
  });

  await test.step('choosing another goal moves the task to it; clearing or deleting the goal unlinks it', async () => {
    await createGoal(page, other);
    await openArea(page, area);
    await openTask(page, open);
    await chooseProperty(page, 'Goal', other);
    await expect(propertyRow(page, 'Goal')).toContainText(other);
    await clearProperty(page, 'Goal');
    await expect(propertyRow(page, 'Goal')).toContainText('No goal');
    await chooseProperty(page, 'Goal', other);
    await closeTask(page);
    await expect(taskRow(page, open)).toContainText(other);
    await expect(taskRow(page, open)).not.toContainText(goal);

    await deleteGoal(page, other);
    await openArea(page, area);
    await expect(taskRow(page, open)).toBeVisible();
    await expect(taskRow(page, open)).not.toContainText(other);
  });
});
