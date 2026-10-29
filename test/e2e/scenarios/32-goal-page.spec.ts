import { expect, test } from '../fixtures/accounts';
import { anotherDevice, idOf } from '../helpers/api';
import { createArea } from '../helpers/areas';
import { addDays, shortDate, utcToday } from '../helpers/dates';
import { createGoalOn, goalRow, goToGoals, linkTasksOn } from '../helpers/goals';
import { createList } from '../helpers/lists';
import { closePanel, confirm, openFabAction, panel } from '../helpers/panels';
import { signIn } from '../helpers/sign-in';
import { createTasksOn, taskRow } from '../helpers/tasks';

test('a goal page tracks its own tasks, progress and pace', async ({ page, account }) => {
  const area = `Self ${account.suffix}`;
  const list = `Kitchen ${account.suffix}`;
  const bake = `Bake bread ${account.suffix}`;
  const late = `Fix the roof ${account.suffix}`;
  const undated = `Read more ${account.suffix}`;
  const today = utcToday();

  await signIn(page, account);
  await createArea(page, area);
  await createList(page, list);
  await goToGoals(page);
  const device = await anotherDevice(page);
  const ids: Record<string, string> = {
    [bake]: await createGoalOn(device, bake, addDays(today, 2)),
    [late]: await createGoalOn(device, late, addDays(today, -3)),
    [undated]: await createGoalOn(device, undated),
  };
  const tasks = await createTasksOn(device, await idOf(device, 'tasklists', list), [
    { name: 'Buy flour', completed: true },
    { name: 'Knead the dough' },
    { name: 'Feed the starter', repeatsFrom: addDays(today, -7), until: addDays(today, -1) },
    { name: 'Climb the ladder' },
  ]);
  await linkTasksOn(device, ids[bake], [tasks['Buy flour'], tasks['Knead the dough'], tasks['Feed the starter']]);
  await linkTasksOn(device, ids[late], [tasks['Climb the ladder']]);
  await expect(goalRow(page, bake)).toContainText('2/3');

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

  await device.dispose();
});
