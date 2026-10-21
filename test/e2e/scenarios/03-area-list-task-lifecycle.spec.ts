import { expect, test } from '../fixtures/accounts';
import { areaSubtitle, createArea, deleteCurrentArea, openArea } from '../helpers/areas';
import { createList, deleteCurrentList, openList } from '../helpers/lists';
import { signIn } from '../helpers/sign-in';
import { addStep, addTask, checkStep, closeTask, completeTaskRow, openTask, taskRow } from '../helpers/tasks';

test('an area, a list and a task live and die', async ({ page, account }) => {
  const area = `Garden ${account.suffix}`;
  const list = `Spring ${account.suffix}`;
  const task = `Plant tomatoes ${account.suffix}`;

  await signIn(page, account);
  await createArea(page, area);
  await createList(page, list);
  await openList(page, list);

  await addTask(page, task);
  await openTask(page, task);
  await addStep(page, 'Buy seeds');
  await addStep(page, 'Prepare soil');
  await checkStep(page, 'Buy seeds');
  await closeTask(page);
  await expect(taskRow(page, task).locator('.pspad-steps-progress')).toContainText('1/2');

  await completeTaskRow(page, task);
  await expect(page.getByText('Completed · 1')).toBeVisible();
  await expect(taskRow(page, task)).toBeHidden();
  await page.locator('.pspad-completed').getByText('Completed · 1').click();
  await expect(taskRow(page, task)).toBeVisible();
  await expect(taskRow(page, task).getByRole('checkbox').first()).toBeChecked();

  await openArea(page, area);
  await expect(areaSubtitle(page)).toHaveText('1 list · no open tasks');

  await openList(page, list);
  await deleteCurrentList(page);
  await expect(page.getByRole('heading', { name: area, exact: true })).toBeVisible();
  await expect(page.getByText('No lists yet.')).toBeVisible();

  await deleteCurrentArea(page);
  await expect(page.locator('.pspad-nav').getByRole('link', { name: area, exact: true })).toBeHidden();
  await expect(page.locator('.pspad-nav').getByRole('link', { name: 'Personal' })).toBeVisible();
});
