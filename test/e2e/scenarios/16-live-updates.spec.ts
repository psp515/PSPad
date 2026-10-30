import { expect, test } from '../fixtures/accounts';
import { createArea } from '../helpers/areas';
import { createList, openList } from '../helpers/lists';
import { closePanel, confirm, panel } from '../helpers/panels';
import { createInvite, joinWithCode, openListMembers } from '../helpers/sharing';
import { shellMenu } from '../helpers/shell';
import { signIn } from '../helpers/sign-in';
import { liveStreamOpened, pushedWithin } from '../helpers/sync';
import { addTask, completeTaskRow, taskRow } from '../helpers/tasks';

test('open clients pick up changes made elsewhere without a manual sync', async ({ page, browser, account, otherAccount }) => {
  const list = `Garden ${account.suffix}`;
  const byMember = `Water the roses ${account.suffix}`;
  const byOwner = `Mow the lawn ${account.suffix}`;
  const bySecondDevice = `Trim the hedge ${account.suffix}`;

  const ownerStream = liveStreamOpened(page);
  await signIn(page, account);
  await ownerStream;
  await createArea(page, `Outside ${account.suffix}`);
  await createList(page, list);
  await openList(page, list);
  await openListMembers(page);
  const invite = await createInvite(page);
  await closePanel(page);

  const memberContext = await browser.newContext();
  const member = await memberContext.newPage();
  const memberStream = liveStreamOpened(member);
  await signIn(member, otherAccount);
  await memberStream;
  await joinWithCode(member, invite.link, invite.code);
  await expect(member.getByRole('heading', { level: 1 })).toContainText(list);

  await addTask(member, byMember);
  await expect(taskRow(page, byMember)).toBeVisible(pushedWithin);

  await addTask(page, byOwner);
  await expect(taskRow(member, byOwner)).toBeVisible(pushedWithin);

  await completeTaskRow(member, byOwner);
  await expect(page.getByText('Completed · 1')).toBeVisible(pushedWithin);

  const deviceContext = await browser.newContext();
  const device = await deviceContext.newPage();
  const deviceStream = liveStreamOpened(device);
  await signIn(device, account);
  await deviceStream;
  await device.goto(new URL(page.url()).pathname);
  await expect(device.getByRole('heading', { level: 1 })).toContainText(list);
  await addTask(device, bySecondDevice);
  await expect(taskRow(page, bySecondDevice)).toBeVisible(pushedWithin);
  await expect(taskRow(member, bySecondDevice)).toBeVisible(pushedWithin);
  await deviceContext.close();

  await openListMembers(page);
  await panel(page).getByRole('button', { name: `Remove ${otherAccount.displayName}` }).click();
  await confirm(page);
  await closePanel(page);
  await expect(shellMenu(member).getByRole('link', { name: 'Shared with me' })).toBeHidden(pushedWithin);
  await memberContext.close();
});
