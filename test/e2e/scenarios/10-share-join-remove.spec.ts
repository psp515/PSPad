import { expect, test } from '../fixtures/accounts';
import { apiFor, bearerToken, fullSync, me, sendCommand } from '../helpers/api';
import { createArea, openArea } from '../helpers/areas';
import { createList, listIdFromUrl, openList } from '../helpers/lists';
import { goTo } from '../helpers/nav';
import { closePanel, confirm, panel } from '../helpers/panels';
import { createInvite, joinWithCode, openListMembers, openSharedListMembersFromBoard } from '../helpers/sharing';
import { signIn } from '../helpers/sign-in';
import { syncNow } from '../helpers/sync';
import { addTask, completeTaskRow, taskRow } from '../helpers/tasks';

test('a shared list is joined, edited, and closed to a removed member', async ({ page, browser, account, otherAccount }) => {
  const list = `Trip ${account.suffix}`;
  const doneByMember = `Book the hotel ${account.suffix}`;
  const stillOpen = `Pack the bags ${account.suffix}`;

  await signIn(page, account);
  await createArea(page, `Travel ${account.suffix}`);
  await createList(page, list);
  await openList(page, list);
  await addTask(page, doneByMember);
  await addTask(page, stillOpen);
  const listId = listIdFromUrl(page);

  await openListMembers(page);
  const invite = await createInvite(page);
  await closePanel(page);

  const memberContext = await browser.newContext();
  const member = await memberContext.newPage();
  await signIn(member, otherAccount);
  await joinWithCode(member, invite.link, invite.code);
  await expect(member).toHaveURL(new RegExp(`/lists/${listId}`));
  await expect(member.getByRole('heading', { level: 1 })).toContainText(list);
  await expect(member.locator('.pspad-nav').getByRole('link', { name: 'Shared with me' })).toBeVisible();

  await completeTaskRow(member, doneByMember);
  await expect(member.getByText('Completed (1)')).toBeVisible();

  await openSharedListMembersFromBoard(member, list);
  await expect(panel(member).locator('.pspad-share-owner')).toHaveText(account.displayName);
  await panel(member).locator('.pspad-list-filed-area').click();
  await member.locator('.mud-popover-open').getByText('Personal', { exact: true }).click();
  await closePanel(member);
  await openArea(member, 'Personal');
  await expect(member.getByRole('link', { name: list, exact: true })).toBeVisible();

  await syncNow(page);
  await expect(page.getByText('Completed (1)')).toBeVisible();

  await openListMembers(page);
  await expect(panel(page).locator('.pspad-share-member')).toContainText(otherAccount.displayName);
  await panel(page).getByRole('button', { name: `Remove ${otherAccount.displayName}` }).click();
  await confirm(page);
  await expect(panel(page).locator('.pspad-share-member')).toHaveCount(0);
  await closePanel(page);

  const memberToken = await bearerToken(member);
  await expect(member.locator('.pspad-nav').getByRole('link', { name: 'Shared with me' })).toBeHidden();
  await goTo(member, 'My Day');
  await openArea(member, 'Personal');
  await expect(member.getByRole('link', { name: list, exact: true })).toBeHidden();

  const memberApi = await apiFor(memberToken);
  const memberSync = await fullSync(memberApi);
  expect(memberSync.memberListIds ?? []).not.toContain(listId);
  expect((memberSync.documents['tasklists'] ?? []).map(doc => doc['id'])).not.toContain(listId);
  expect((memberSync.documents['todotasks'] ?? []).map(doc => doc['listId'])).not.toContain(listId);

  const ownerApi = await apiFor(await bearerToken(page));
  const ownerSync = await fullSync(ownerApi);
  const task = (ownerSync.documents['todotasks'] ?? []).find(doc => (doc['name'] ?? doc['Name']) === stillOpen)!;
  const taskId = String(task['id'] ?? task['Id']);
  const memberIdentity = await me(memberApi);
  const rejected = await sendCommand(memberApi, 'CompleteTask', { userId: memberIdentity.userId, taskId });
  expect(rejected.accepted).toBe(false);

  await syncNow(page);
  await expect(taskRow(page, stillOpen)).toBeVisible();
  await memberContext.close();
});
