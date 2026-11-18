import { expect, test } from '../fixtures/accounts';
import { createArea } from '../helpers/areas';
import { createList, openList } from '../helpers/lists';
import { chooseProperty, clearProperty, panel, propertyRow } from '../helpers/panels';
import { setCustomReminder } from '../helpers/reminders';
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

  await test.step('a task with a due date offers a reminder that starts off', async () => {
    await setDueDate(page, 'Tomorrow');
    await expect(propertyRow(page, 'Remind me')).toContainText('Off');
    await expect(propertyRow(page, 'Show ahead')).toContainText('1 week before');
  });

  await test.step('a preset reminder survives a reload', async () => {
    await chooseProperty(page, 'Remind me', '1 day before at 09:00');
    await expect(propertyRow(page, 'Remind me')).toContainText('1 day before at 09:00');
    await closeTask(page);
    await page.reload();
    await openTask(page, task);
    await expect(propertyRow(page, 'Remind me')).toContainText('1 day before at 09:00');
  });

  await test.step('a custom reminder reads its days and time', async () => {
    await setCustomReminder(page, 3, 18, 30);
    await expect(propertyRow(page, 'Remind me')).toContainText('3 days before at 18:30');
    await closeTask(page);
    await page.reload();
    await openTask(page, task);
    await expect(propertyRow(page, 'Remind me')).toContainText('3 days before at 18:30');
  });

  await test.step('clearing the reminder turns it off', async () => {
    await clearProperty(page, 'Remind me');
    await expect(propertyRow(page, 'Remind me')).toContainText('Off');
  });

  await test.step('a reminder without a due date is shown as waiting for one', async () => {
    await chooseProperty(page, 'Remind me', 'On the day at 09:00');
    await expect(propertyRow(page, 'Remind me')).toContainText('On the day at 09:00');
    await clearProperty(page, 'Due');
    await expect(propertyRow(page, 'Remind me')).toContainText('On the day at 09:00');
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
