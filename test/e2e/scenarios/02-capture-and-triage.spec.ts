import { expect, test } from '../fixtures/accounts';
import { createArea, openArea } from '../helpers/areas';
import { capture, goToInbox, inboxItem, openInboxItem } from '../helpers/inbox';
import { createList, openList } from '../helpers/lists';
import { deleteFromPanel, pickFromPropertyRow, savePanel } from '../helpers/panels';
import { signIn } from '../helpers/sign-in';
import { taskRow } from '../helpers/tasks';

test('captured items are organised into a task or discarded', async ({ page, account }) => {
  const area = `Home ${account.suffix}`;
  const list = `Errands ${account.suffix}`;
  const keep = `Buy stamps ${account.suffix}`;
  const drop = `Random thought ${account.suffix}`;

  await signIn(page, account);
  await createArea(page, area);
  await createList(page, list);

  await goToInbox(page);
  await capture(page, keep);
  await capture(page, drop);
  await expect(page.locator('.pspad-row-subhead')).toHaveText('Today');
  await expect(page.locator('.pspad-row-group .pspad-inbox-item')).toHaveCount(2);

  await openInboxItem(page, keep);
  await pickFromPropertyRow(page, 'pspad-task-list', list);
  await savePanel(page);
  await expect(inboxItem(page, keep)).toBeHidden();

  await openInboxItem(page, drop);
  await deleteFromPanel(page);
  await expect(inboxItem(page, drop)).toBeHidden();
  await expect(page.getByText('Nothing captured.')).toBeVisible();

  await openArea(page, area);
  await openList(page, list);
  await expect(taskRow(page, keep)).toBeVisible();
});
