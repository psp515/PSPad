import { expect, test } from '../fixtures/accounts';
import { createArea, openArea } from '../helpers/areas';
import { createList, listIdFromUrl, openList } from '../helpers/lists';
import { awaitNoDrawer, panel, pickFromPropertyRow, propertyRow, savePanel } from '../helpers/panels';
import { signIn } from '../helpers/sign-in';
import { pushedWithin } from '../helpers/sync';
import { createTaskDueTodayFromAnotherDevice, stepNameField, taskRow } from '../helpers/tasks';
import { daySection, goToMyDay } from '../helpers/today';

test('a task added from My Day lands on the day and in the picked list, steps included', async ({ page, account }) => {
  const area = `Home ${account.suffix}`;
  const list = `Moving ${account.suffix}`;
  const task = `Move flat ${account.suffix}`;
  const elsewhere = `Book the movers ${account.suffix}`;

  await signIn(page, account);
  await createArea(page, area);
  await createList(page, list);

  await goToMyDay(page);
  await awaitNoDrawer(page);
  await page.locator('.pspad-fab').click();
  await expect(panel(page).locator('.pspad-panel-title')).toHaveText('New task');
  await expect(panel(page).locator('.pspad-task-due .pspad-property-value')).toContainText('Today');
  await expect(panel(page).locator('.pspad-panel-save')).toBeDisabled();

  await panel(page).getByPlaceholder('Task name').fill(task);
  for (const step of ['Order boxes', 'Pack the kitchen']) {
    const input = panel(page).locator('.pspad-step-add input');
    await input.fill(step);
    await input.press('Enter');
  }
  await expect(panel(page).locator('.pspad-step')).toHaveCount(3);
  await expect(panel(page).locator('.pspad-panel-save')).toBeDisabled();

  await test.step('without a list Enter adds nothing, and a draft step reverts on Escape', async () => {
    await expect(propertyRow(page, 'List')).toContainText('No list');
    await panel(page).getByPlaceholder('Task name').press('Enter');
    await expect(panel(page).getByText('New task', { exact: true })).toBeVisible();
    const draft = stepNameField(page, 'Order boxes');
    await draft.fill('Order crates');
    await draft.press('Escape');
    await expect(stepNameField(page, 'Order boxes')).toHaveValue('Order boxes');
  });
  await pickFromPropertyRow(page, 'pspad-task-list', list);
  await expect(panel(page).locator('.pspad-panel-title')).toContainText(list);
  await savePanel(page);

  const row = daySection(page, 'anytime').locator('.pspad-row').filter({ hasText: task });
  await expect(row).toBeVisible();
  await expect(row.locator('.pspad-steps-progress')).toContainText('0/2');

  await openArea(page, area);
  await openList(page, list);
  await expect(taskRow(page, task)).toBeVisible();

  await test.step('a task made due today on another device shows up on My Day without a reload', async () => {
    const listId = listIdFromUrl(page);
    await goToMyDay(page);
    await createTaskDueTodayFromAnotherDevice(page, listId, elsewhere);
    await expect(taskRow(page, elsewhere)).toBeVisible(pushedWithin);
  });
});
