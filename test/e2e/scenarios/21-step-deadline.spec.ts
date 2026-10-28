import { expect, test } from '../fixtures/accounts';
import { createArea } from '../helpers/areas';
import { addDays, dayMonth, utcToday } from '../helpers/dates';
import { createList, openList } from '../helpers/lists';
import { signIn } from '../helpers/sign-in';
import { addStep, addTask, closeTask, completeTaskRow, openTask, setStepDeadline, taskRow } from '../helpers/tasks';
import { daySection, goToMyDay, showDay } from '../helpers/today';

test('a step deadline puts its task on that day, and an overdue task carries to later days', async ({ page, account }) => {
  const area = `Home ${account.suffix}`;
  const list = `Moving ${account.suffix}`;
  const task = `Move flat ${account.suffix}`;
  const carried = `Renew passport ${account.suffix}`;
  const today = utcToday();
  const tomorrow = addDays(today, 1);

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
  await addStep(page, 'Book an appointment');
  await addStep(page, 'Pay the fee');
  await setStepDeadline(page, 'Get new photos taken', 'Today');
  await setStepDeadline(page, 'Book an appointment', 'Today');
  await setStepDeadline(page, 'Pay the fee', 'Tomorrow');
  await closeTask(page);

  await goToMyDay(page);
  await expect(page.locator('.pspad-row-name', { hasText: task })).toHaveCount(0);
  await expect(daySection(page, 'anytime')).toContainText(carried);
  await showDay(page, addDays(utcToday(), 1));
  await expect(daySection(page, 'anytime')).toContainText(task);
  await expect(daySection(page, 'overdue')).toContainText(carried);
  await expect(daySection(page, 'anytime')).not.toContainText(carried);

  await test.step('a step row under its task shows its date, not a relative word', async () => {
    await expect(daySection(page, 'anytime')).toContainText(new RegExp(`Order boxes\\s*${dayMonth(tomorrow)}`));
    await expect(daySection(page, 'overdue')).toContainText(new RegExp(`Get new photos taken\\s*${dayMonth(today)}`));
  });

  await page.locator('.pspad-week-today').click();

  await test.step('ticking one of two steps due today checks only that step and keeps the task on the day', async () => {
    const photos = page.getByRole('checkbox', { name: 'Get new photos taken' });
    await photos.click();
    await expect(photos).toBeChecked();
    await expect(page.getByRole('checkbox', { name: 'Book an appointment' })).not.toBeChecked();
    await expect(taskRow(page, carried).getByRole('checkbox')).not.toBeChecked();
    await expect(daySection(page, 'anytime')).toContainText(carried);
  });

  const appointment = daySection(page, 'anytime').locator('.pspad-day-step', { hasText: 'Book an appointment' });
  await appointment.getByRole('checkbox').click();
  await expect(page.locator('.mud-snackbar')).toContainText(`"${carried}" moved to Tomorrow`);
  await expect(page.locator('.pspad-day-anytime .pspad-row-name', { hasText: carried })).toHaveCount(0);

  await showDay(page, addDays(utcToday(), 1));
  await expect(daySection(page, 'anytime')).toContainText(carried);
  await expect(page.locator('.pspad-day-overdue', { hasText: carried })).toHaveCount(0);

  await test.step('ticking the task itself still completes it', async () => {
    await completeTaskRow(page, task);
    await expect(daySection(page, 'anytime')).not.toContainText(task);
  });
});
