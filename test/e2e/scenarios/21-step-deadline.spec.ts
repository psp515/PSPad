import { expect, test } from '../fixtures/accounts';
import { createArea } from '../helpers/areas';
import { addDays, utcToday } from '../helpers/dates';
import { createList, openList } from '../helpers/lists';
import { signIn } from '../helpers/sign-in';
import { addStep, addTask, closeTask, openTask, setStepDeadline, taskRow } from '../helpers/tasks';
import { daySection, goToMyDay, showDay } from '../helpers/today';

test('a step deadline puts its task on that day, and an overdue task carries to later days', async ({ page, account }) => {
  const area = `Home ${account.suffix}`;
  const list = `Moving ${account.suffix}`;
  const task = `Move flat ${account.suffix}`;
  const carried = `Renew passport ${account.suffix}`;

  await signIn(page, account);
  await createArea(page, area);
  await createList(page, list);
  await openList(page, list);
  await addTask(page, task);
  await openTask(page, task);
  await addStep(page, 'Book the van');
  await addStep(page, 'Order boxes');
  await setStepDeadline(page, 'Order boxes', 'Tomorrow');
  await closeTask(page);

  await expect(taskRow(page, task).locator('.pspad-step-due-mark')).toContainText('Order boxes');

  await addTask(page, carried);
  await openTask(page, carried);
  await addStep(page, 'Get new photos taken');
  await addStep(page, 'Pay the fee');
  await setStepDeadline(page, 'Get new photos taken', 'Today');
  await setStepDeadline(page, 'Pay the fee', 'Tomorrow');
  await closeTask(page);

  await goToMyDay(page);
  await expect(page.locator('.pspad-row-name', { hasText: task })).toHaveCount(0);
  await expect(daySection(page, 'anytime')).toContainText(carried);
  await showDay(page, addDays(utcToday(), 1));
  await expect(daySection(page, 'anytime')).toContainText(task);
  await expect(daySection(page, 'overdue')).toContainText(carried);
  await expect(daySection(page, 'anytime')).not.toContainText(carried);
});
