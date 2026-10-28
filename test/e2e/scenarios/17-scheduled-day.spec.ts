import { expect, test } from '../fixtures/accounts';
import { createArea } from '../helpers/areas';
import { addDays, shiftTime, shortDate, utcToday } from '../helpers/dates';
import { createList, openList } from '../helpers/lists';
import { closePanel, panel, propertyRow } from '../helpers/panels';
import { signIn } from '../helpers/sign-in';
import { addTask, closeTask, openTask, taskRow } from '../helpers/tasks';
import { addTaskAtFirstFreeSlot, backToToday, daySection, goToMyDay, pickTaskTimeFromClock, setDueDate, setTaskTime, showDay, timelineBlock } from '../helpers/today';

test('a timed task sits on the schedule and is ticked from the timeline', async ({ page, account }) => {
  const dentist = `Dentist ${account.suffix}`;
  const errand = `Post office ${account.suffix}`;

  await signIn(page, account);
  await expect(page.getByText('Schedule empty for today.')).toBeVisible();
  const nowHour = (await page.locator('.pspad-timeline-now-label').innerText()).slice(0, 2);
  await expect(page.locator('.pspad-timeline-hour-label', { hasText: `${nowHour}:00` })).toBeVisible();
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
  await expect(daySection(page, 'scheduled')).toContainText('09:00–10:30');

  await test.step('on a phone the Schedule tab counts what is pending and swaps the task list for the timeline', async () => {
    await page.setViewportSize({ width: 390, height: 844 });
    const tasksTab = page.getByRole('tab', { name: 'Tasks' });
    const scheduleTab = page.getByRole('tab', { name: 'Schedule · 1' });
    await expect(tasksTab).toHaveAttribute('aria-selected', 'true');
    await expect(page.getByRole('heading', { name: 'Today, any time' })).toBeVisible();
    await scheduleTab.click();
    await expect(scheduleTab).toHaveAttribute('aria-selected', 'true');
    await expect(page.getByRole('heading', { name: 'Today, any time' })).toBeHidden();
    await expect(timelineBlock(page, dentist)).toBeVisible();
    await tasksTab.click();
    await page.setViewportSize({ width: 1280, height: 800 });
  });

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

  await test.step('another day names itself when its schedule is empty, and a slot there adds an hour-long task on it', async () => {
    const tomorrow = addDays(utcToday(), 1);
    await showDay(page, tomorrow);
    await expect(page.getByText(`Schedule empty for ${shortDate(tomorrow)}.`)).toBeVisible();
    const start = await addTaskAtFirstFreeSlot(page);
    await expect(panel(page).getByText('New task', { exact: true })).toBeVisible();
    await expect(propertyRow(page, 'Due')).toContainText('Tomorrow');
    await expect(panel(page).getByRole('textbox', { name: 'Start' })).toHaveValue(start);
    await expect(panel(page).getByRole('textbox', { name: 'End' })).toHaveValue(shiftTime(start, 60));
    await closePanel(page);
    await backToToday(page);
  });

  const slot = page.locator('.pspad-timeline-slot').first();
  const time = await slot.getAttribute('data-time');
  await slot.click();
  await expect(page.locator('.pspad-task-time .pspad-time-start input')).toHaveValue(time!);
  await expect(page).toHaveURL(new RegExp(`task=new&due=\\d{4}-\\d{2}-\\d{2}&time=${time}`));
});
