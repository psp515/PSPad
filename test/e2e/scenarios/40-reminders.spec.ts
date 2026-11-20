import { expect, test } from '../fixtures/accounts';
import { createArea } from '../helpers/areas';
import { createList, openList } from '../helpers/lists';
import { clearProperty, panel } from '../helpers/panels';
import {
  chooseReminder, expectReminder, pickReminderTime, reminderChoice, reminderDays, typeReminder,
} from '../helpers/reminders';
import { goToSettings, notificationsCard } from '../helpers/settings';
import { signIn } from '../helpers/sign-in';
import { addTask, closeTask, openTask } from '../helpers/tasks';
import { setDueDate, setRepeating } from '../helpers/today';

test('a task reminder is set, customised, cleared and waits for a due date', async ({ page, account }) => {
  const task = `Call the bank ${account.suffix}`;
  const repeating = `Take vitamins ${account.suffix}`;
  const list = `Errands ${account.suffix}`;

  await signIn(page, account);
  await createArea(page, `Home ${account.suffix}`);
  await createList(page, list);
  await openList(page, list);
  await addTask(page, task);
  await openTask(page, task);

  await test.step('a task with a due date offers quick reminder choices and starts off', async () => {
    await setDueDate(page, 'Tomorrow');
    await expect(reminderChoice(page, '1 day')).not.toBeChecked();
    await expect(reminderDays(page)).toHaveCount(0);
  });

  await test.step('one tap on a quick choice turns the reminder on and survives a reload', async () => {
    await chooseReminder(page, '1 day');
    await expectReminder(page, '1', '09:00');
    await closeTask(page);
    await page.reload();
    await openTask(page, task);
    await expectReminder(page, '1', '09:00');
    await expect(reminderChoice(page, '1 day')).toBeChecked();
  });

  await test.step('typed days and time change the reminder in place', async () => {
    await typeReminder(page, '3', '18:30');
    await expect(reminderChoice(page, '1 day')).not.toBeChecked();
    await closeTask(page);
    await page.reload();
    await openTask(page, task);
    await expectReminder(page, '3', '18:30');
  });

  await test.step('a wrong number of days says what is allowed and keeps the reminder', async () => {
    await reminderDays(page).fill('120');
    await reminderDays(page).press('Enter');
    await expect(panel(page).getByRole('alert')).toHaveText('Enter 0 to 99 days');
    await reminderDays(page).fill('2');
    await reminderDays(page).press('Enter');
    await expect(panel(page).getByRole('alert')).toHaveCount(0);
    await expectReminder(page, '2', '18:30');
  });

  await test.step('the reminder time uses the task time clock and keeps the days', async () => {
    await pickReminderTime(page, 7, 15);
    await expectReminder(page, '2', '07:15');
  });

  await test.step('clearing the reminder turns it off', async () => {
    await clearProperty(page, 'Remind me');
    await expect(reminderDays(page)).toHaveCount(0);
    await expect(reminderChoice(page, '2 days')).not.toBeChecked();
  });

  await test.step('a reminder without a due date is shown as waiting for one', async () => {
    await chooseReminder(page, 'On the day');
    await expectReminder(page, '0', '09:00');
    await clearProperty(page, 'Due');
    await expectReminder(page, '0', '09:00');
    await expect(panel(page).getByText('Needs a due date', { exact: true })).toBeVisible();
    await closeTask(page);
  });

  await test.step('a repeating task reminds before each occurrence', async () => {
    await addTask(page, repeating);
    await openTask(page, repeating);
    await setRepeating(page);
    await expect(panel(page).getByText('Before each occurrence', { exact: true })).toBeVisible();
  });
});

test('the notifications card explains a blocked browser', async ({ page, account }) => {
  await page.addInitScript(() => {
    Object.defineProperty(Notification, 'permission', { get: () => 'denied' });
  });
  await signIn(page, account);
  await goToSettings(page);
  await expect(notificationsCard(page)).toContainText('Notifications are blocked for PSPad in your browser settings.');
});

test('the notifications card explains a browser without push', async ({ page, account }) => {
  await page.addInitScript(() => {
    delete (window as any).PushManager;
  });
  await signIn(page, account);
  await goToSettings(page);
  await expect(notificationsCard(page)).toContainText("This browser can't show notifications.");
});

test('the device caption is checked each time a task opens', async ({ page, account }) => {
  const task = `Water the plants ${account.suffix}`;
  await page.addInitScript(() => {
    let on = false;
    (window as any).__pushOn = () => { on = true; };
    Object.defineProperty(Notification, 'permission', { get: () => (on ? 'granted' : 'default') });
    const subscription = { endpoint: 'https://push.example/e2e', getKey: () => new Uint8Array(16).buffer };
    const realRegistration = ServiceWorkerContainer.prototype.getRegistration;
    ServiceWorkerContainer.prototype.getRegistration = async function (...args: any[]) {
      const registration = await realRegistration.apply(this, args as any);
      if (!on) { return registration; }
      return { pushManager: { getSubscription: async () => subscription } } as any;
    };
  });

  await signIn(page, account);
  await createArea(page, `Garden ${account.suffix}`);
  await createList(page, `Chores ${account.suffix}`);
  await openList(page, `Chores ${account.suffix}`);
  await addTask(page, task);
  await openTask(page, task);
  await setDueDate(page, 'Tomorrow');
  await chooseReminder(page, 'On the day');
  await expect(panel(page).getByRole('link', { name: 'Notifications are off on this device · Turn on' })).toBeVisible();
  await closeTask(page);

  await page.evaluate(() => (window as any).__pushOn());
  await openTask(page, task);
  await expectReminder(page, '0', '09:00');
  await expect(panel(page).getByRole('link', { name: 'Notifications are off on this device · Turn on' })).toHaveCount(0);
});
