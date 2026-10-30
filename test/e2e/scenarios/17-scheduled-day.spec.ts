import { randomUUID } from 'node:crypto';
import { expect, test } from '../fixtures/accounts';
import { anotherDevice, idOf } from '../helpers/api';
import { createArea } from '../helpers/areas';
import { addDays, isoDate, shiftTime, shortDate, utcToday } from '../helpers/dates';
import { createList, openList } from '../helpers/lists';
import { closePanel, panel, propertyRow } from '../helpers/panels';
import { signIn } from '../helpers/sign-in';
import { addTask, closeTask, openTask, taskRow } from '../helpers/tasks';
import {
  addTaskAtFirstFreeSlot, backToToday, daySection, goToMyDay, pickTaskTimeFromClock, schedule, setDueDate, setTaskTime, showDay, timeField, timelineBlock,
} from '../helpers/today';

test('a timed task sits on the schedule and is ticked from the timeline', async ({ page, account }) => {
  const dentist = `Dentist ${account.suffix}`;
  const errand = `Post office ${account.suffix}`;

  await signIn(page, account);
  await expect(page.getByText('Schedule empty for today.')).toBeVisible();
  const now = schedule(page).getByRole('img', { name: /^Now, \d\d:\d\d$/ });
  const nowTime = (await now.getAttribute('aria-label'))!.slice(-5);
  const nowHour = nowTime.slice(0, 2);
  const hourLabels = schedule(page).getByText(`${nowHour}:00`, { exact: true }).filter({ visible: true });
  await expect(hourLabels).toHaveCount(nowTime.endsWith(':00') ? 2 : 1);
  await expect(schedule(page).getByRole('button', { name: /^Show \d\d:\d\d/ }).first()).toBeVisible();

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

  const slot = schedule(page).getByRole('button', { name: /^Add a task at \d\d:\d\d$/ }).first();
  const time = (await slot.getAttribute('aria-label'))!.slice(-5);
  await slot.click();
  await expect(timeField(page, 'Start')).toHaveValue(time);
  await expect(page).toHaveURL(new RegExp(`task=new&due=\\d{4}-\\d{2}-\\d{2}&time=${time}`));
  await closePanel(page);

  await test.step('overlapping tasks share lines that tick one at a time, a long block names its list, a late one runs past midnight, and a band unfolds its hours', async () => {
    const [standup, call, review] = ['Standup', 'Call', 'Review'].map(name => `${name} ${account.suffix}`);
    const workshop = `Workshop ${account.suffix}`;
    const night = `Night shift ${account.suffix}`;
    const gym = `Gym ${account.suffix}`;
    const device = await anotherDevice(page);
    const listId = await idOf(device, 'tasklists', `Town ${account.suffix}`);
    const timed: Array<[string, string, string | null]> = [
      [standup, '05:00', '05:15'], [call, '05:00', '05:30'], [review, '05:00', '06:00'],
      [workshop, '13:00', '15:00'], [night, '22:00', '01:00'], [gym, '18:00', null],
    ];
    for (const [name, start, end] of timed) {
      const taskId = randomUUID();
      await device.send('CreateTask', { taskId, listId, name });
      await device.send('SetTaskDueDate', { taskId, dueOn: isoDate(utcToday()) });
      await device.send('SetTaskTime', { taskId, time: { start: `${start}:00`, end: end && `${end}:00` } });
    }
    await device.dispose();

    const timeline = schedule(page);
    await expect(timeline.getByTitle(`${workshop} – 13:00–15:00`)).toContainText(`Town ${account.suffix}`);
    await expect(timeline.getByTitle(`${night} – 22:00–01:00 (+1)`)).toContainText('(+1)');
    await expect(timeline.getByTitle(`${gym} – 18:00`)).toContainText('18:00');
    await expect(timeline.getByTitle(`${gym} – 18:00`)).not.toContainText('–');
    await expect(timeline.getByTitle(`${gym} – 18:00`)).not.toContainText(`Town ${account.suffix}`);
    await expect(timeline.getByTitle(call, { exact: true })).toBeVisible();
    await expect(timeline.getByTitle(`${call} – 05:00–05:30`)).toHaveCount(0);

    await timeline.getByRole('checkbox', { name: call }).click();
    await expect(timeline.getByRole('checkbox', { name: call })).toHaveCount(0);
    await expect(timeline.getByRole('checkbox', { name: standup })).not.toBeChecked();
    await expect(timeline.getByRole('checkbox', { name: review })).not.toBeChecked();
    await expect(daySection(page, 'completed')).toContainText(call);

    const band = timeline.getByRole('button', { name: /^Show \d\d:00–\d\d:00$/ }).first();
    const range = (await band.getAttribute('aria-label'))!.slice('Show '.length);
    const firstHour = Number(range.slice(0, 2));
    await band.click();
    await expect(timeline.getByRole('button', { name: `Show ${range}`, exact: true })).toHaveCount(0);
    await expect(timeline.getByRole('button', { name: `Add a task at ${String(firstHour + 1).padStart(2, '0')}:30` })).toBeVisible();
  });
});
