import { expect, test } from '../fixtures/accounts';
import { createArea } from '../helpers/areas';
import { createList, openList } from '../helpers/lists';
import { panel } from '../helpers/panels';
import { signIn } from '../helpers/sign-in';
import { addStep, addTask, closeTask, openTask, removeStep, renameStep, stepNameField } from '../helpers/tasks';

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

  await test.step('Escape while renaming a step puts the old name back', async () => {
    const step = stepNameField(page, 'Check the freezer');
    await step.fill('Check the cellar');
    await step.press('Escape');
    await expect(stepNameField(page, 'Check the freezer')).toHaveValue('Check the freezer');
    await closeTask(page);
    await openTask(page, task);
    await expect(stepNameField(page, 'Check the freezer')).toHaveValue('Check the freezer');
  });

  await test.step('a rename made right after another edit survives the sync that follows', async () => {
    const field = stepNameField(page, 'Check the freezer');
    await field.fill('Check the pantry');
    await field.press('Enter');
    await closeTask(page);
    await openTask(page, task);
    await expect(stepNameField(page, 'Check the pantry')).toHaveValue('Check the pantry');
  });

  await test.step('a blank step is not added, and a removed step stays gone', async () => {
    await addStep(page, 'Check the bins');
    const next = panel(page).getByRole('textbox', { name: 'Next step', exact: true });
    await next.fill('   ');
    await next.press('Enter');
    await expect(panel(page).getByRole('textbox', { name: /^Rename / })).toHaveCount(2);
    await removeStep(page, 'Check the bins');
    await closeTask(page);
    await openTask(page, task);
    await expect(stepNameField(page, 'Check the pantry')).toHaveValue('Check the pantry');
    await expect(stepNameField(page, 'Check the bins')).toHaveCount(0);
  });
});
