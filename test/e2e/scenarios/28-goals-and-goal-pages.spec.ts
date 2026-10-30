import { expect, test } from '../fixtures/accounts';
import { anotherDevice, idOf } from '../helpers/api';
import { createArea } from '../helpers/areas';
import { addDays, dayMonth, shortDate, utcToday } from '../helpers/dates';
import { createGoalOn, editGoal, goalRow, goToGoals, linkTasksOn, openGoalMenu } from '../helpers/goals';
import { createList } from '../helpers/lists';
import { awaitNoDrawer, chooseProperty, clearProperty, closePanel, confirm, openFabAction, panel, propertyRow } from '../helpers/panels';
import { signIn } from '../helpers/sign-in';
import { createTasksOn, taskRow } from '../helpers/tasks';

test('goals are listed by status and due date, the goal panel edits a goal, and a goal page tracks its tasks and pace', async ({ page, account }) => {
  const area = `Self ${account.suffix}`;
  const list = `Kitchen ${account.suffix}`;
  const bake = `Bake bread ${account.suffix}`;
  const late = `Fix the roof ${account.suffix}`;
  const later = `Learn Polish ${account.suffix}`;
  const undated = `Read more ${account.suffix}`;
  const learn = `Learn Polish well ${account.suffix}`;
  const today = utcToday();

  await signIn(page, account);
  await createArea(page, area);
  await createList(page, list);
  await goToGoals(page);

  await test.step('with no goals the empty state opens a new goal that waits for a name and keeps a due date picked first', async () => {
    await expect(page.getByText('No goals yet.')).toBeVisible();
    await page.getByText('No goals yet.').click();
    await expect(panel(page).getByText('New goal', { exact: true })).toBeVisible();
    await expect(panel(page).getByRole('button', { name: 'Add goal' })).toBeDisabled();
    await expect(panel(page).getByRole('button', { name: 'Delete goal' })).toHaveCount(0);
    await expect(panel(page).getByLabel('Status')).toHaveCount(0);
    const name = panel(page).getByPlaceholder('Goal name');
    await name.fill('   ');
    await name.press('Enter');
    await expect(panel(page).getByText('New goal', { exact: true })).toBeVisible();
    await chooseProperty(page, 'Due', /^In 2 days/);
    await name.fill(bake);
    await name.press('Enter');
    await awaitNoDrawer(page);
    await expect(page).toHaveURL(/\/goals$/);
    await expect(goalRow(page, bake)).toContainText(`Due ${shortDate(addDays(today, 2))}`);
  });

  const device = await anotherDevice(page);
  const ids: Record<string, string> = {};
  await test.step('goals from another device appear without a reload, in progress ordered by due date, undated last', async () => {
    ids[undated] = await createGoalOn(device, undated);
    ids[later] = await createGoalOn(device, later, addDays(today, 30));
    ids[late] = await createGoalOn(device, late, addDays(today, -3));
    ids[bake] = await idOf(device, 'goals', bake);
    const names = page.getByRole('link', { name: new RegExp(`^(${[late, bake, later, undated].join('|')})$`) });
    await expect(names).toHaveText([late, bake, later, undated].map(goal => new RegExp(`^\\s*${goal}`)));
    await expect(goalRow(page, late)).toContainText(`Overdue · ${dayMonth(addDays(today, -3))}`);
  });

  await test.step('a row counts done tasks, an ended repeat among them, instead of previewing them', async () => {
    const tasks = await createTasksOn(device, await idOf(device, 'tasklists', list), [
      { name: 'Buy flour', completed: true },
      { name: 'Knead the dough' },
      { name: 'Feed the starter', repeatsFrom: addDays(today, -7), until: addDays(today, -1) },
      { name: 'Climb the ladder' },
    ]);
    await linkTasksOn(device, ids[bake], [tasks['Buy flour'], tasks['Knead the dough'], tasks['Feed the starter']]);
    await linkTasksOn(device, ids[late], [tasks['Climb the ladder']]);
    await expect(goalRow(page, bake)).toContainText('2/3');
    await expect(goalRow(page, bake)).not.toContainText('Knead the dough');
  });

  await test.step('a row menu offers the two closing statuses without opening the goal, and Not achieved gets its own section', async () => {
    await openGoalMenu(page, undated);
    await expect(page.getByRole('menuitem', { name: /^Mark/ })).toHaveText(['Mark achieved', 'Mark not achieved']);
    await expect(page).toHaveURL(/\/goals$/);
    await page.getByRole('menuitem', { name: 'Mark not achieved' }).click();
    await expect(page.getByRole('heading', { name: 'Not achieved 1', exact: true })).toBeVisible();
    await expect(goalRow(page, undated)).toContainText('No tasks linked');
  });

  await test.step('the goal panel renames as you type, shows and saves the status, and sets or clears the due date', async () => {
    await editGoal(page, later);
    await expect(panel(page).getByText('Goal', { exact: true })).toBeVisible();
    await expect(panel(page).getByRole('button', { name: 'Add goal' })).toHaveCount(0);
    await expect(panel(page).getByLabel('Status')).toHaveValue('In progress');
    await panel(page).getByPlaceholder('Goal name').fill(learn);
    await panel(page).getByPlaceholder('Goal name').press('Tab');
    await expect(goalRow(page, learn)).toBeVisible();

    await editGoal(page, learn);
    await chooseProperty(page, 'Due', 'Tomorrow');
    await expect(goalRow(page, learn)).toContainText('Due Tomorrow');
    await editGoal(page, learn);
    await expect(propertyRow(page, 'Due')).toContainText('Tomorrow');
    await clearProperty(page, 'Due');
    await expect(goalRow(page, learn)).not.toContainText('Due');
    await panel(page).getByLabel('Status').click();
    await page.getByRole('option', { name: 'Achieved', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Achieved 1', exact: true })).toBeVisible();

    await panel(page).getByRole('button', { name: 'Delete goal' }).click();
    await expect(page.getByRole('dialog')).toContainText(`Delete “${learn}”? Its tasks stay, without a goal.`);
    await confirm(page, 'Cancel');
    await editGoal(page, learn);
    await expect(panel(page).getByLabel('Status')).toHaveValue('Achieved');
    await expect(propertyRow(page, 'Due')).toContainText('No due date');
    await closePanel(page);
    await expect(goalRow(page, learn)).toContainText('No tasks linked');
    await expect(goalRow(page, later)).toHaveCount(0);
  });

  await test.step('a goal page sums up its status, progress and pace, and lists its own tasks by list', async () => {
    await goalRow(page, bake).click();
    await expect(page).toHaveURL(new RegExp(`/goals/${ids[bake]}$`));
    await expect(page.getByRole('heading', { level: 1 })).toContainText(bake);
    await expect(page.getByText('In progress', { exact: true }).filter({ visible: true }).first()).toBeVisible();
    await expect(page.getByText('2 of 3 done')).toBeVisible();
    await expect(page.getByText(`Due ${shortDate(addDays(today, 2))} · 2 days left`)).toBeVisible();
    await expect(page.getByText('Progress', { exact: true })).toBeVisible();
    await expect(page.getByText('On the goal', { exact: true })).toBeVisible();
    await expect(page.getByText('Done', { exact: true })).toBeVisible();
    await expect(page.getByText('Recurring tasks not counted')).toBeVisible();
    await expect(page.getByText('Pace', { exact: true })).toBeVisible();
    await expect(page.getByText('2 days left', { exact: true })).toBeVisible();
    await expect(page.getByText('50%', { exact: true })).toBeVisible();

    await expect(taskRow(page, 'Knead the dough')).toContainText(list);
    await expect(taskRow(page, 'Knead the dough')).not.toContainText(bake);
    await expect(page.getByText('Climb the ladder')).toHaveCount(0);
    await expect(page.getByText('Completed · 2')).toBeVisible();

    await taskRow(page, 'Knead the dough').getByText('Knead the dough').click();
    await expect(page).toHaveURL(new RegExp(`/goals/${ids[bake]}\\?task=`));
    await expect(panel(page).getByPlaceholder('Task name')).toHaveValue('Knead the dough');
    await closePanel(page);

    await openFabAction(page, 'Edit goal');
    await expect(panel(page).getByPlaceholder('Goal name')).toHaveValue(bake);
    await closePanel(page);
  });

  await test.step('an overdue goal says how late it is, an undated or empty one has no pace and says so', async () => {
    await page.goto(`/goals/${ids[late]}`);
    await expect(page.getByText('3 days overdue')).toBeVisible();
    await page.goto(`/goals/${ids[undated]}`);
    await expect(page.getByText('No tasks linked')).toBeVisible();
    await expect(page.getByText('No open tasks.')).toBeVisible();
    await expect(page.getByText('Pace', { exact: true })).toHaveCount(0);
    await expect(page.getByText('Progress', { exact: true })).toHaveCount(0);
    await page.getByRole('link', { name: 'Back to goals' }).click();
    await expect(page).toHaveURL(/\/goals$/);
  });

  await test.step('deleting a goal from its page asks first and goes back to Goals, and the gone goal says so', async () => {
    await goalRow(page, late).click();
    await openFabAction(page, 'Delete goal');
    await expect(page.getByRole('dialog')).toContainText(`Delete “${late}”? Its tasks stay, without a goal.`);
    await confirm(page, 'Delete');
    await expect(page).toHaveURL(/\/goals$/);
    await expect(goalRow(page, late)).toHaveCount(0);
    await page.goto(`/goals/${ids[late]}`);
    await expect(page.getByText('This goal is not there anymore.')).toBeVisible();
  });

  await test.step('deleting a list takes its tasks off the goal but keeps the goal', async () => {
    await goToGoals(page);
    await device.send('DeleteTaskList', { listId: await idOf(device, 'tasklists', list) });
    await expect(goalRow(page, bake)).toContainText('No tasks linked');
    await page.getByRole('button', { name: 'Add goal' }).click();
    await expect(panel(page).getByText('New goal', { exact: true })).toBeVisible();
    await closePanel(page);
  });

  await device.dispose();
});
