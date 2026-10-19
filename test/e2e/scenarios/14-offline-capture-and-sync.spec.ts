import { expect, test } from '../fixtures/accounts';
import { createArea, openArea } from '../helpers/areas';
import { createList, openList } from '../helpers/lists';
import { signIn } from '../helpers/sign-in';
import { goOffline, goOnline, syncButton, syncNow } from '../helpers/sync';
import { addTask, completeTaskRow, taskRow } from '../helpers/tasks';

// Chromium's offline emulation skips requests a service worker makes, so this context runs without one.
test.use({ serviceWorkers: 'block' });

test('changes made offline reach the server after the next sync', async ({ page, browser, account }) => {
  const area = `Field ${account.suffix}`;
  const list = `Site visit ${account.suffix}`;
  const before = `Check the fence ${account.suffix}`;
  const offline = `Photograph the gate ${account.suffix}`;

  await signIn(page, account);
  await createArea(page, area);
  await createList(page, list);
  await openList(page, list);
  await addTask(page, before);
  await syncNow(page);

  await goOffline(page);
  await expect(page.locator('.pspad-sync-stamp').first()).toContainText('Offline', { timeout: 30_000 });
  await expect(page.locator('.pspad-connection-status:visible')).toHaveCount(1);
  await addTask(page, offline);
  await completeTaskRow(page, before);
  await expect(page.getByText('Completed · 1')).toBeVisible();

  await goOnline(page);
  await expect(page.locator('.pspad-connection-status')).toHaveCount(0, { timeout: 30_000 });
  await expect(syncButton(page)).toBeEnabled();
  await syncNow(page);

  const otherContext = await browser.newContext();
  const other = await otherContext.newPage();
  await signIn(other, account);
  await openArea(other, area);
  await openList(other, list);
  await expect(taskRow(other, offline)).toBeVisible();
  await expect(other.getByText('Completed · 1')).toBeVisible();
  await expect(taskRow(other, before)).toBeHidden();
  await otherContext.close();
});
