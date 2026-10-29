import { expect, test } from '../fixtures/accounts';
import { createArea } from '../helpers/areas';
import { createList, openList } from '../helpers/lists';
import { signIn } from '../helpers/sign-in';
import { addTask, closeTask, openTask, taskRow } from '../helpers/tasks';
import { daySection, goToMyDay, pickTaskTimeFromClock, setDueDate, setTaskTime, timelineBlock } from '../helpers/today';

test('a timed task sits on the schedule and is ticked from the timeline', async ({ page, account }) => {
  const dentist = `Dentist ${account.suffix}`;
  const errand = `Post office ${account.suffix}`;

  await signIn(page, account);
  await expect(page.getByText('Schedule empty for today.')).toBeVisible();
  await expect(page.locator('.pspad-timeline-hour')).toHaveCount(1);
  await expect(page.locator('.pspad-timeline-fold').first()).toBeVisible();

  await createArea(page, `Errands ${account.suffix}`);
  await createList(page, `Town ${account.suffix}`);
  await openList(page, `Town ${account.suffix}`);

  await addTask(page, dentist);
  await openTask(page, dentist);
  await setDueDate(page, 'Today');
  await setTaskTime(page, '08:00', '10:30');
  await pickTaskTimeFromClock(page, 'start', 9, 0);
  await closeTask(page);
  await expect(taskRow(page, dentist)).toContainText('09:00');

  await addTask(page, errand);
  await openTask(page, errand);
  await setDueDate(page, 'Today');
  await closeTask(page);

  await goToMyDay(page);
  await expect(daySection(page, 'scheduled').getByRole('heading', { level: 2 })).toHaveText(/^Scheduled\s*1$/);
  await expect(daySection(page, 'scheduled')).toContainText(dentist);
  await expect(daySection(page, 'anytime')).toContainText(errand);
  await expect(daySection(page, 'anytime')).not.toContainText(dentist);
  await expect(page.getByText('Schedule empty for today.')).toBeHidden();

  const block = timelineBlock(page, dentist);
  await expect(block).toBeVisible();
  await expect(block).toContainText('09:00');
  await expect(block).toContainText('10:30');

  await block.getByRole('checkbox').click();
  await expect(block).toHaveCount(0);
  await expect(page.getByText('Schedule empty for today.')).toBeVisible();
  await expect(daySection(page, 'scheduled')).toHaveCount(0);
  await expect(daySection(page, 'completed')).toContainText('Completed · 1');
  await expect(daySection(page, 'anytime')).toContainText(errand);

  const slot = page.locator('.pspad-timeline-slot').first();
  const time = await slot.getAttribute('data-time');
  await slot.click();
  await expect(page.locator('.pspad-task-time .pspad-time-start input')).toHaveValue(time!);
  await expect(page).toHaveURL(new RegExp(`task=new&due=\\d{4}-\\d{2}-\\d{2}&time=${time}`));
});
