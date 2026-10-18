import { expect, test } from '../fixtures/accounts';
import { createArea } from '../helpers/areas';
import { createList, openList } from '../helpers/lists';
import { closePanel, confirm, panel } from '../helpers/panels';
import { openListSnapshots } from '../helpers/sharing';
import { signIn } from '../helpers/sign-in';
import { syncNow } from '../helpers/sync';
import { addTask, closeTask, openTask, taskRow } from '../helpers/tasks';

test('a public snapshot collects ticks until it is revoked', async ({ page, browser, account }) => {
  const list = `Shopping ${account.suffix}`;
  const task = `Olive oil ${account.suffix}`;

  await signIn(page, account);
  await createArea(page, `Kitchen ${account.suffix}`);
  await createList(page, list);
  await openList(page, list);
  await addTask(page, task);

  await openListSnapshots(page);
  await panel(page).locator('.pspad-snapshot-preset').filter({ hasText: '1 day' }).click();
  await panel(page).locator('.pspad-snapshot-publish').click();
  const snapshot = panel(page).locator('.pspad-snapshot').first();
  await expect(snapshot).toBeVisible();
  const open = snapshot.locator('.pspad-snapshot-open');
  await expect(async () => {
    await snapshot.locator('.pspad-snapshot-qr-toggle').click();
    await expect(open).toBeVisible({ timeout: 2_000 });
  }).toPass({ timeout: 20_000 });
  const link = await open.getAttribute('href');
  expect(link).toBeTruthy();
  await closePanel(page);

  const visitorContext = await browser.newContext();
  const visitor = await visitorContext.newPage();
  await visitor.goto(link!);
  await expect(visitor.locator('.pspad-snapshot-name')).toHaveText(list);
  const entry = visitor.locator('.pspad-snapshot-task').filter({ hasText: task });
  const marked = visitor.waitForResponse(response => response.url().includes('/marks') && response.ok());
  await entry.getByRole('checkbox').check();
  await marked;
  await expect(entry.getByText('ticked')).toBeVisible();

  await syncNow(page);
  await expect(taskRow(page, task).locator('.pspad-snapshot-mark')).toBeVisible();
  await openTask(page, task);
  await panel(page).getByRole('button', { name: 'Dismiss' }).click();
  await expect(panel(page).locator('.pspad-snapshot-mark')).toHaveCount(0);
  await closeTask(page);
  await expect(taskRow(page, task).locator('.pspad-snapshot-mark')).toHaveCount(0);

  await openListSnapshots(page);
  await panel(page).locator('.pspad-snapshot-revoke').first().click();
  await confirm(page);
  await expect(panel(page).getByText('No live links.')).toBeVisible();

  await visitor.reload();
  await expect(visitor.getByText('This snapshot has expired or never existed.')).toBeVisible();
  await visitorContext.close();
});
