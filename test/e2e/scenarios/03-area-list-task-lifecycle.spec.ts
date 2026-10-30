import { expect, test } from '../fixtures/accounts';
import { pageHeading, deleteCurrentArea, listCard, openArea, openNewArea } from '../helpers/areas';
import { openList } from '../helpers/lists';
import { closePanel, confirm, openFabAction, panel } from '../helpers/panels';
import { shellMenu } from '../helpers/shell';
import { signIn } from '../helpers/sign-in';
import { addStep, addTask, checkStep, closeTask, completeTaskRow, openTask, taskRow } from '../helpers/tasks';

test('an area, a list and a task live and die', async ({ page, account }) => {
  const area = `Garden ${account.suffix}`;
  const renamed = `Allotment ${account.suffix}`;
  const list = `Spring ${account.suffix}`;
  const task = `Plant tomatoes ${account.suffix}`;

  await signIn(page, account);

  await test.step('a new area waits for a name, and Enter adds it and opens it', async () => {
    await openNewArea(page);
    await expect(panel(page).getByText('New area', { exact: true })).toBeVisible();
    await expect(panel(page).getByRole('button', { name: 'Add area' })).toBeDisabled();
    await expect(panel(page).getByRole('button', { name: 'Delete area' })).toHaveCount(0);
    const name = panel(page).getByPlaceholder('Area name');
    await name.fill('   ');
    await name.press('Enter');
    await expect(panel(page).getByText('New area', { exact: true })).toBeVisible();
    await name.fill(area);
    await name.press('Enter');
    await expect(page.getByRole('heading', { name: area, exact: true })).toBeVisible();
  });

  await test.step('an empty area offers a new list, which waits for a name and is a task list unless told otherwise', async () => {
    await expect(page.getByText('No lists yet.')).toBeVisible();
    await page.getByRole('button', { name: 'New list' }).focus();
    await page.keyboard.press('Enter');
    await expect(panel(page).getByText(`New list · ${area}`, { exact: true })).toBeVisible();
    await expect(panel(page).getByRole('button', { name: 'Add list' })).toBeDisabled();
    await expect(panel(page).getByRole('button', { name: 'Delete list' })).toHaveCount(0);
    const name = panel(page).getByPlaceholder('List name');
    await name.fill('  ');
    await name.press('Enter');
    await expect(panel(page).getByText(`New list · ${area}`, { exact: true })).toBeVisible();
    await name.fill(list);
    await name.press('Enter');
    await expect(listCard(page, list).getByRole('button', { name: 'Add task', exact: true })).toBeVisible();
  });

  await openList(page, list);
  await expect(page.getByText('No tasks yet.')).toBeVisible();
  await expect(pageHeading(page)).toHaveAccessibleDescription(`${area} · no open tasks`);
  await addTask(page, task);
  await expect(pageHeading(page)).toHaveAccessibleDescription(`${area} · 1 open task`);

  await test.step('the Add task row under the tasks opens a new task from the keyboard', async () => {
    await page.getByRole('button', { name: 'Add task', exact: true }).focus();
    await page.keyboard.press('Enter');
    await expect(panel(page).getByText(`New task · ${area} › ${list}`, { exact: true })).toBeVisible();
    await closePanel(page);
  });
  await openTask(page, task);
  await addStep(page, 'Buy seeds');
  await addStep(page, 'Prepare soil');
  await checkStep(page, 'Buy seeds');
  await closeTask(page);
  await expect(taskRow(page, task).getByText('1/2', { exact: true })).toBeVisible();

  await completeTaskRow(page, task);
  await expect(page.getByText('Completed · 1')).toBeVisible();
  await expect(taskRow(page, task)).toBeHidden();
  await page.getByText('Completed · 1').click();
  await expect(taskRow(page, task)).toBeVisible();
  await expect(taskRow(page, task).getByRole('checkbox').first()).toBeChecked();

  await page.getByRole('link', { name: 'Back to area' }).click();
  await expect(page.getByRole('heading', { name: area, exact: true })).toBeVisible();
  await expect(pageHeading(page)).toHaveAccessibleDescription('1 list · no open tasks');

  await test.step('deleting a list warns how many tasks go with it, lands on its area, and the list is gone', async () => {
    await openList(page, list);
    await openFabAction(page, 'Delete list');
    await expect(page.getByRole('dialog')).toContainText(`Delete “${list}” and its 1 task? This can’t be undone.`);
    await confirm(page, 'Delete');
    await expect(page.getByRole('heading', { name: area, exact: true })).toBeVisible();
    await expect(page.getByText('No lists yet.')).toBeVisible();
    await page.goBack();
    await expect(page.getByText('This list is not there anymore.')).toBeVisible();
    await openArea(page, area);
  });

  await test.step('the area panel renames the area as you type, says it has no lists, and deleting there asks first', async () => {
    await openFabAction(page, 'Edit area');
    await expect(panel(page).getByText('Area', { exact: true })).toBeVisible();
    await expect(panel(page).getByRole('button', { name: 'Add area' })).toHaveCount(0);
    await expect(panel(page).getByText('Lists · 0')).toBeVisible();
    await expect(panel(page).getByText('No lists yet.')).toBeVisible();
    await panel(page).getByPlaceholder('Area name').fill(renamed);
    await panel(page).getByPlaceholder('Area name').press('Tab');
    await expect(page.getByRole('heading', { name: renamed, exact: true })).toBeVisible();
    await panel(page).getByRole('button', { name: 'Delete area' }).click();
    await expect(page.getByRole('dialog')).toContainText(`Delete “${renamed}”? This can’t be undone.`);
    await confirm(page, 'Cancel');
    await expect(panel(page).getByPlaceholder('Area name')).toHaveValue(renamed);
    await closePanel(page);
  });

  await deleteCurrentArea(page);
  await expect(page.getByRole('heading', { name: 'My Day', exact: true })).toBeVisible();
  await expect(shellMenu(page).getByRole('link', { name: renamed, exact: true })).toBeHidden();
  await expect(shellMenu(page).getByRole('link', { name: 'Personal' })).toBeVisible();
});
