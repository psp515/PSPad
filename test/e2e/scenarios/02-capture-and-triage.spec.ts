import { expect, test } from '../fixtures/accounts';
import { createArea, openArea } from '../helpers/areas';
import { createGoal } from '../helpers/goals';
import {
  capture, captureFromAnotherDevice, captureWithEnter, goToInbox, inboxItem, inboxItemRows, openInboxItem, renameInboxItem,
} from '../helpers/inbox';
import { createList, openList } from '../helpers/lists';
import { chooseProperty, closePanel, deleteFromPanel, dismissMenu, menuOptions, panel, propertyRow } from '../helpers/panels';
import { signIn } from '../helpers/sign-in';
import { pushedWithin } from '../helpers/sync';
import { addDays, dayMonth, isoDate, shiftTime, utcTime, utcToday } from '../helpers/dates';
import { goToSettings, setTimeZone } from '../helpers/settings';
import { addStep, closeTask, openTask, setStepDeadline, stepDeadlineOnAnotherDevice, stepRow, taskRow } from '../helpers/tasks';

test('captured items are organised into a task or discarded', async ({ page, account }) => {
  const area = `Home ${account.suffix}`;
  const list = `Errands ${account.suffix}`;
  const repairs = `Repairs ${account.suffix}`;
  const recipes = `Recipes ${account.suffix}`;
  const goal = `Eat better ${account.suffix}`;
  const captured = `Buy milk ${account.suffix}`;
  const keep = `Buy oat milk ${account.suffix}`;
  const drop = `Random thought ${account.suffix}`;
  const elsewhere = `Call the plumber ${account.suffix}`;
  const tomorrow = addDays(utcToday(), 1);

  await signIn(page, account);
  await goToInbox(page);

  await test.step('an empty inbox offers to capture, and a blank capture adds nothing', async () => {
    await page.getByRole('button', { name: 'Capture' }).filter({ hasText: 'Nothing captured.' }).click();
    await expect(panel(page).getByText('Capture', { exact: true })).toBeVisible();
    await expect(panel(page).getByRole('button', { name: 'Add', exact: true })).toBeDisabled();
    await expect(panel(page).getByRole('button', { name: 'Discard' })).toHaveCount(0);
    await expect(propertyRow(page, 'List')).toHaveCount(0);
    await panel(page).getByLabel('Name').fill('   ');
    await panel(page).getByLabel('Name').press('Enter');
    await expect(panel(page).getByRole('button', { name: 'Add', exact: true })).toBeDisabled();
    await closePanel(page);
    await expect(page.getByText('Nothing captured.')).toBeVisible();
  });

  await test.step('Enter or Add captures; the newest item comes first with its capture time', async () => {
    const before = utcTime();
    await captureWithEnter(page, captured);
    await capture(page, drop);
    const after = utcTime();
    await expect(page.getByText('2 to process').filter({ visible: true })).toBeVisible();
    await expect(page.getByText('Today', { exact: true })).toBeVisible();
    await expect(inboxItemRows(page)).toHaveText([new RegExp(drop), new RegExp(captured)]);
    await expect(inboxItem(page, captured)).toContainText(new RegExp(`${before}|${after}`));
    await expect(page.getByText('Nothing captured.')).toHaveCount(0);
  });

  await test.step('an item can be reworded without converting it, and with no list it cannot convert', async () => {
    await openInboxItem(page, captured);
    await expect(panel(page).getByText(/^Captured /)).toBeVisible();
    await expect(panel(page).getByRole('button', { name: 'Convert to task' })).toBeDisabled();
    await expect(panel(page).getByRole('button', { name: 'Discard' })).toBeEnabled();
    await renameInboxItem(page, keep);
    await closePanel(page);
    await expect(inboxItemRows(page)).toHaveCount(2);
    await openInboxItem(page, keep);
    await closePanel(page);
  });

  await createGoal(page, goal);
  await createArea(page, area);
  await createList(page, list);
  await createList(page, repairs);
  await createList(page, recipes, 'Reference');
  await goToInbox(page);

  await test.step('converting carries list, due date, priority, goal, star, steps and description', async () => {
    await openInboxItem(page, keep);
    await expect(propertyRow(page, 'List')).toContainText(`${area} › ${list}`);
    const lists = await menuOptions(page, 'List');
    await expect(lists.filter({ hasText: repairs })).toHaveCount(1);
    await expect(lists.filter({ hasText: recipes })).toHaveCount(0);
    await dismissMenu(page);
    await chooseProperty(page, 'List', repairs);
    await chooseProperty(page, 'Due', /^Tomorrow/);
    await chooseProperty(page, 'Priority', 'High');
    await chooseProperty(page, 'Goal', goal);
    await panel(page).getByRole('button', { name: 'Star', exact: true }).click();
    await expect(panel(page).getByRole('button', { name: 'Unstar' })).toBeVisible();
    await addStep(page, 'Check the fridge');
    await addStep(page, 'Go to the shop');
    await panel(page).getByLabel('Description').fill('**Oat**, 2 l');
    await panel(page).getByLabel('Description').blur();
    await panel(page).getByRole('button', { name: 'Convert to task' }).click();
    await expect(inboxItem(page, keep)).toBeHidden();
  });

  await test.step('the next item keeps the list picked last; discarding it empties the inbox', async () => {
    await openInboxItem(page, drop);
    await expect(propertyRow(page, 'List')).toContainText(`${area} › ${repairs}`);
    await deleteFromPanel(page, 'Discard');
    await expect(inboxItem(page, drop)).toBeHidden();
    await expect(page.getByText('Nothing captured.')).toBeVisible();
  });

  let capturedElsewhere: string[] = [];
  await test.step('an item captured on another device shows up without a reload', async () => {
    const before = utcTime();
    await captureFromAnotherDevice(page, elsewhere);
    capturedElsewhere = [before, utcTime()];
    await expect(inboxItem(page, elsewhere)).toBeVisible(pushedWithin);
  });

  await test.step('a deadline set on a draft step stays on the step the converted task keeps', async () => {
    const dated = `Call the vet ${account.suffix}`;
    await capture(page, dated);
    await openInboxItem(page, dated);
    await addStep(page, 'Find the number');
    await setStepDeadline(page, 'Find the number', 'Tomorrow');
    await panel(page).getByRole('button', { name: 'Convert to task' }).click();
    await expect(inboxItem(page, dated)).toBeHidden();
    await openArea(page, area);
    await openList(page, repairs);
    await openTask(page, dated);
    await expect(stepRow(page, 'Find the number')).toContainText('Tomorrow');
    await closeTask(page);
    await expect.poll(() => stepDeadlineOnAnotherDevice(page, dated, 'Find the number')).toBe(isoDate(tomorrow));
    await goToInbox(page);
  });

  await test.step('the task holds everything the inbox panel set', async () => {
    await openArea(page, area);
    await openList(page, repairs);
    const row = taskRow(page, keep);
    await expect(row).toContainText(dayMonth(tomorrow));
    await expect(row).toContainText('0/2');
    await expect(row).toContainText(goal);
    await expect(row.getByRole('img', { name: 'High priority' })).toBeVisible();
    await expect(row.getByRole('button', { name: 'Unstar' })).toBeVisible();
    await expect(row.getByRole('img', { name: 'Description' })).toBeVisible();
    await expect(taskRow(page, captured)).toHaveCount(0);

    await openTask(page, keep);
    await expect(propertyRow(page, 'Due')).toContainText('Tomorrow');
    await expect(propertyRow(page, 'Priority')).toContainText('High');
    await expect(propertyRow(page, 'Goal')).toContainText(goal);
    await expect(panel(page).getByRole('textbox', { name: 'Rename Check the fridge' })).toBeVisible();
    await expect(panel(page).getByRole('strong')).toHaveText('Oat');
    await closeTask(page);
  });

  await test.step('capture times read in the stored time zone', async () => {
    await goToSettings(page);
    await setTimeZone(page, 'Asia/Kolkata');
    await goToInbox(page);
    const [before, after] = capturedElsewhere.map(time => shiftTime(time, 330));
    await expect(inboxItem(page, elsewhere)).toContainText(new RegExp(`${before}|${after}`));
  });
});
