import { randomUUID } from 'node:crypto';
import { expect, test } from '../fixtures/accounts';
import { anotherDevice, apiFor, bearerToken, idOf } from '../helpers/api';
import { createArea } from '../helpers/areas';
import { addDays, utcToday } from '../helpers/dates';
import { createList, openList } from '../helpers/lists';
import { openInApp } from '../helpers/nav';
import { closePanel, confirm, panel } from '../helpers/panels';
import { addFieldsOn, createItemsOn, openReferenceItem, referenceRow } from '../helpers/references';
import { openListSnapshots } from '../helpers/sharing';
import { markOnSnapshot, publishSnapshotOn, recordedVisit, showNewestSnapshot, snapshotDate, snapshotRows } from '../helpers/snapshots';
import { signIn } from '../helpers/sign-in';
import { goOffline, goOnline, syncNow } from '../helpers/sync';
import { addTask, closeTask, createTasksOn, openTask, taskRow } from '../helpers/tasks';

test.use({ serviceWorkers: 'block' });

test('a public snapshot collects ticks until it is revoked', async ({ page, browser, account, otherAccount }) => {
  const kitchen = `Kitchen ${account.suffix}`;
  const list = `Shopping ${account.suffix}`;
  const oil = `Olive oil ${account.suffix}`;
  const invoice = `Send the invoice ${account.suffix}`;
  const stamps = `Buy stamps ${account.suffix}`;
  const printed = `Print it ${account.suffix}`;
  const domain = `Renew the domain ${account.suffix}`;
  const bread = `Bread ${account.suffix}`;
  const supplies = `Print supplies ${account.suffix}`;
  const filament = `PLA filament ${account.suffix}`;
  const marked = 'Marked on a snapshot';

  await signIn(page, account);
  await createArea(page, kitchen);
  await createList(page, list);
  const owner = await anotherDevice(page);
  const listId = await idOf(owner, 'tasklists', list);
  const tasks = await createTasksOn(owner, listId, [{ name: oil }, { name: invoice }, { name: domain, completed: true }]);
  const stampsId = randomUUID();
  const printedId = randomUUID();
  await owner.send('AddStep', { taskId: tasks[invoice], stepId: stampsId, name: stamps });
  await owner.send('AddStep', { taskId: tasks[invoice], stepId: printedId, name: printed });
  await owner.send('CheckStep', { taskId: tasks[invoice], stepId: printedId, checked: true });
  await owner.send('SetTaskDescription', { taskId: tasks[oil], description: 'Extra virgin only' });
  const suppliesId = randomUUID();
  await owner.send('CreateTaskList', { listId: suppliesId, areaId: await idOf(owner, 'areas', kitchen), name: supplies, kind: 1 });
  const items = await createItemsOn(owner, suppliesId, [filament], 0);
  await addFieldsOn(owner, items[filament], [['Brand', 'Prusament']]);
  await openList(page, list);

  let link = '';
  await test.step('a task added just before publishing goes into the 1 day link, whose QR code opens it', async () => {
    await addTask(page, bread);
    await openListSnapshots(page);
    await panel(page).getByRole('checkbox', { name: '1 day', exact: true }).click();
    await panel(page).getByRole('button', { name: /^Publish until / }).click();
    await expect(panel(page).getByText('Live links · 1')).toBeVisible();
    const open = await showNewestSnapshot(page);
    await expect(panel(page).getByRole('img', { name: 'Snapshot link QR code' })).toBeVisible();
    link = (await open.getAttribute('href'))!;
    expect(link).toMatch(/\/public\/snapshot\/[^/]+$/);
    await closePanel(page);
  });

  const ownerApi = await apiFor(await bearerToken(page));
  const tomorrow = addDays(utcToday(), 1);
  const suppliesSnapshot = await publishSnapshotOn(ownerApi, suppliesId, tomorrow);
  const secondSnapshot = await publishSnapshotOn(ownerApi, listId, tomorrow);

  const visitorContext = await browser.newContext({ locale: 'pl-PL' });
  const visitor = await visitorContext.newPage();

  await test.step('a visitor without an account reads the frozen copy in their language: who shared it, what is left and what is done', async () => {
    await visitor.goto(link);
    await expect(visitor.getByText(`Publiczna migawka · udostępnia ${account.displayName}`)).toBeVisible();
    await expect(visitor.getByRole('heading', { name: list })).toBeVisible();
    await expect(visitor.getByText('Odhaczone przez odwiedzających: 0 z 3')).toBeVisible();
    await expect(visitor.getByText('Do zrobienia · 3')).toBeVisible();
    await expect(visitor.getByText(bread)).toBeVisible();
    await expect(visitor.getByText('Extra virgin only')).toBeVisible();
    await expect(visitor.getByText('Brak opisu')).toHaveCount(0);
    await expect(visitor.getByText(`Już zrobione przez właściciela (${account.displayName}) · 1`)).toBeVisible();
    await expect(visitor.getByText(domain)).toBeVisible();
    await expect(visitor.getByRole('checkbox', { name: domain })).toHaveCount(0);
    await expect(visitor.getByRole('checkbox', { name: printed })).toHaveCount(0);
    await expect(visitor.getByRole('img', { name: 'Zrobione przez właściciela' })).toBeVisible();
    await expect(visitor.getByText('Kopia tylko do odczytu, zrobiona w PSPad.')).toBeVisible();
    await expect(visitor.getByRole('link', { name: 'Zaloguj się' })).toBeVisible();
  });

  await test.step('ticking a task and a step marks them for everyone with the link', async () => {
    for (const entry of [oil, stamps]) {
      const saved = visitor.waitForResponse(response => response.url().includes('/marks') && response.ok());
      await visitor.getByRole('checkbox', { name: entry }).check();
      await saved;
    }
    await expect(visitor.getByText('odhaczone', { exact: true })).toHaveCount(2);
    await expect(visitor.getByText('Odhaczone przez odwiedzających: 1 z 3')).toBeVisible();
  });

  await test.step('a reference list snapshot shows its items with their fields, and an item ticks', async () => {
    await visitor.goto(`/public/snapshot/${suppliesSnapshot.token}`);
    await expect(visitor.getByRole('heading', { name: supplies })).toBeVisible();
    await expect(visitor.getByText('Brand', { exact: true })).toBeVisible();
    await expect(visitor.getByText('Prusament')).toBeVisible();
    const saved = visitor.waitForResponse(response => response.url().includes('/marks') && response.ok());
    await visitor.getByRole('checkbox', { name: filament }).check();
    await saved;
    await expect(visitor.getByText('Odhaczone przez odwiedzających: 1 z 1')).toBeVisible();
  });
  await visitorContext.close();

  await markOnSnapshot(secondSnapshot.token, tasks[oil]);
  await ownerApi.delete(`/api/snapshots/${secondSnapshot.id}`);

  await test.step('the owner sees one mark on a task ticked on two snapshots and on a task with only a step ticked', async () => {
    await syncNow(page);
    await expect(taskRow(page, oil).getByRole('img', { name: marked })).toHaveCount(1);
    await expect(taskRow(page, invoice).getByRole('img', { name: marked })).toBeVisible();
    await expect(taskRow(page, bread).getByRole('img', { name: marked })).toHaveCount(0);
  });

  await openTask(page, oil);
  await panel(page).getByRole('button', { name: 'Dismiss' }).click();
  await expect(panel(page).getByText(marked)).toHaveCount(0);
  await closeTask(page);
  await expect(taskRow(page, oil).getByRole('img', { name: marked })).toHaveCount(0);

  await test.step('a reference item ticked on a snapshot shows the mark until the panel dismisses it', async () => {
    await page.getByRole('link', { name: 'Back to area' }).click();
    await openList(page, supplies);
    await expect(referenceRow(page, filament).getByRole('img', { name: marked })).toBeVisible();
    await openReferenceItem(page, filament);
    await expect(panel(page).getByText(marked)).toBeVisible();
    await panel(page).getByRole('button', { name: 'Dismiss' }).click();
    await expect(panel(page).getByText(marked)).toHaveCount(0);
    await closePanel(page);
    await expect(referenceRow(page, filament).getByRole('img', { name: marked })).toHaveCount(0);
  });

  const readerContext = await browser.newContext({ serviceWorkers: 'block' });
  const reader = await readerContext.newPage();
  await signIn(reader, otherAccount);
  const openedSnapshots = reader.getByRole('navigation').getByRole('link', { name: 'List snapshots' });

  await test.step('List snapshots starts empty, and a signed-in visitor gets Open PSPad instead of Log in', async () => {
    await openedSnapshots.click();
    await expect(reader.getByRole('heading', { name: 'List snapshots', exact: true })).toBeVisible();
    await expect(reader.getByText('Snapshots you open while signed in show up here.')).toBeVisible();
    const visited = recordedVisit(reader);
    await reader.goto(link);
    await visited;
    await expect(reader.getByRole('heading', { name: list })).toBeVisible();
    await expect(reader.getByRole('link', { name: 'Open PSPad' })).toBeVisible();
    await expect(reader.getByRole('link', { name: 'Log in' })).toHaveCount(0);
    const visitedSupplies = recordedVisit(reader);
    await reader.goto(`/public/snapshot/${suppliesSnapshot.token}`);
    await visitedSupplies;
    await expect(reader.getByRole('heading', { name: supplies })).toBeVisible();
  });

  const rows = snapshotRows(reader);

  await test.step('List snapshots lists what was opened, newest first, with when it expires and was opened', async () => {
    await reader.getByRole('link', { name: 'Open PSPad' }).click();
    await openedSnapshots.click();
    await expect(rows).toHaveCount(2);
    await expect(rows.first()).toContainText(supplies);
    await expect(rows.last()).toContainText(list);
    await expect(rows.first()).toContainText(`· Opened ${snapshotDate(utcToday())}`);
    await expect(rows.first()).toContainText(/Expires \d{1,2} \w{3} \d{4}/);
  });

  await test.step('offline a row opens the saved copy of its snapshot with ticks paused, and one never opened asks to connect', async () => {
    await goOffline(reader);
    await rows.last().click();
    await expect(reader).toHaveURL(link);
    await expect(reader.getByText(`Offline — showing the copy from ${snapshotDate(utcToday())}. Ticks are paused.`)).toBeVisible();
    await expect(reader.getByRole('checkbox', { name: bread })).toBeDisabled();
    await reader.goBack();
    await expect(rows).toHaveCount(2);
    await openInApp(reader, '/public/snapshot/never-published');
    await expect(reader.getByText('Connect to the internet to open this snapshot.')).toBeVisible();
    await goOnline(reader);
  });

  await test.step('when the server cannot be reached List snapshots falls back to the saved copies', async () => {
    await readerContext.route('**/api/me/snapshot-visits', route => route.abort('connectionrefused'));
    await reader.goBack();
    await expect(reader.getByRole('heading', { name: 'List snapshots', exact: true })).toBeVisible();
    await expect(rows).toHaveCount(2);
    await readerContext.unroute('**/api/me/snapshot-visits');
  });

  await reader.goto(link);
  await expect(reader.getByRole('checkbox', { name: bread })).toBeEnabled();

  await page.getByRole('link', { name: 'Back to area' }).click();
  await openList(page, list);
  await openListSnapshots(page);
  await showNewestSnapshot(page);
  await panel(page).getByRole('button', { name: 'Revoke' }).click();
  await confirm(page);
  await expect(panel(page).getByText('No live links.')).toBeVisible();

  await test.step('a tick on a revoked snapshot is not saved: it says so and unticks', async () => {
    await reader.getByRole('checkbox', { name: bread }).click();
    await expect(reader.getByText("Couldn't save that tick. Try again.")).toBeVisible();
    await expect(reader.getByRole('checkbox', { name: bread })).not.toBeChecked();
  });

  await reader.reload();
  await expect(reader.getByText('This snapshot has expired or never existed.')).toBeVisible();
  await expect(reader.getByRole('link', { name: 'What is PSPad?' })).toBeVisible();
  await readerContext.close();
});
