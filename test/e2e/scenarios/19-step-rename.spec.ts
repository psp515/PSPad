import { expect, test } from '../fixtures/accounts';
import { createArea } from '../helpers/areas';
import { createList, openList } from '../helpers/lists';
import { panel } from '../helpers/panels';
import { signIn } from '../helpers/sign-in';
import { addStep, addTask, closeTask, openTask, renameStep, stepNameField } from '../helpers/tasks';

test('a step can be renamed after it is added', async ({ page, account }) => {
  const area = `Kitchen ${account.suffix}`;
  const list = `Groceries ${account.suffix}`;
  const task = `Weekly shop ${account.suffix}`;

  await signIn(page, account);
  await createArea(page, area);
  await createList(page, list);
  await openList(page, list);
  await addTask(page, task);
  await openTask(page, task);
  await addStep(page, 'Check the fridge');

  await renameStep(page, 'Check the fridge', 'Check the freezer');
  await closeTask(page);
  await openTask(page, task);
  await expect(stepNameField(page, 'Check the freezer')).toHaveValue('Check the freezer');

  await page.reload();
  await expect(panel(page).getByPlaceholder('Task name')).toHaveValue(task);
  const field = stepNameField(page, 'Check the freezer');
  await expect(field).toHaveValue('Check the freezer');

  await field.fill('');
  await field.blur();
  await expect(stepNameField(page, 'Check the freezer')).toHaveValue('Check the freezer');
});
