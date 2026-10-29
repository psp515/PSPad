import { expect, test } from '../fixtures/accounts';
import { anotherDevice, apiFor, bearerToken } from '../helpers/api';
import { createBudgetOn, openBudgetSettings } from '../helpers/budgets';
import { goTo } from '../helpers/nav';
import { closePanel, confirm, panel } from '../helpers/panels';
import { createInvite, joinButton, tryJoin, wrongCode } from '../helpers/sharing';
import { signIn } from '../helpers/sign-in';
import { goOffline, goOnline, pushedWithin, syncNow } from '../helpers/sync';

test.use({ serviceWorkers: 'block' });

test('a budget is shared, read by its member, removed, left, rejoined and stopped', async ({ page, browser, account, otherAccount }) => {
  const household = `Household ${account.suffix}`;
  const mine = `Pocket money ${account.suffix}`;
  const ownerName = account.displayName;
  const memberName = otherAccount.displayName;
  const card = (on: typeof page, name: string) => on.getByRole('group', { name, exact: true });
  const membersButton = (on: typeof page) => on.getByRole('button', { name: /^Members, \d+ (person|people)$/ });

  await signIn(page, account);
  const owner = await anotherDevice(page);
  const budgetId = await createBudgetOn(owner, household);
  await goTo(page, 'Budgets');
  await card(page, household).getByRole('link', { name: household }).click();
  await expect(page.getByRole('heading', { name: household, exact: true })).toBeVisible();

  let invite = { link: '', code: '', token: '' };
  await test.step('an unshared budget has no members button, and its settings offer sharing from Only you', async () => {
    await expect(membersButton(page)).toHaveCount(0);
    await openBudgetSettings(page);
    const sharing = page.getByRole('button', { name: /^Members\s+Only you$/ });
    await expect(sharing).toBeVisible();
    await expect(page.getByText('Members see every entry and balance and can add and edit them. Only you can rename, archive or invite.'))
      .toBeVisible();
    await sharing.click();
    await expect(page).toHaveURL(new RegExp(`\\?members=${budgetId}$`));
    await expect(panel(page).getByText('Only you use this budget')).toBeVisible();
    invite = await createInvite(page);
    expect(invite.link).toMatch(new RegExp(`/budgets/join/${invite.token}#code=${invite.code}$`));
    await closePanel(page);
  });

  const memberContext = await browser.newContext();
  const member = await memberContext.newPage();
  await signIn(member, otherAccount);
  const memberDevice = await anotherDevice(member);
  await createBudgetOn(memberDevice, mine);

  const joinPath = new URL(invite.link).pathname;
  const codeField = member.getByLabel('Code');
  const typed = (code: string) => `${code.slice(0, 3)}-${code.slice(3)}`.toLowerCase();

  await test.step("a wrong code in the link says so above the entry card, drops the code from the address and asks for the budget's code", async () => {
    await member.goto(`${joinPath}#code=${wrongCode(invite.code)}`);
    await expect(member.getByText("That link or code doesn't work.")).toBeVisible();
    await expect(member.getByText('Enter the invite code')).toBeVisible();
    await expect(member.getByText('The person who shared the budget has a 6-character code. Ask them for it.')).toBeVisible();
    await expect(joinButton(member)).toHaveText('Join budget');
    await expect(member).toHaveURL(new RegExp(`${joinPath}$`));
  });

  await test.step('the code field takes only a whole code, and a wrong one says so and keeps the input', async () => {
    await codeField.fill('abc');
    await expect(joinButton(member)).toBeDisabled();
    await codeField.press('Enter');
    await expect(member).toHaveURL(new RegExp(`${joinPath}$`));
    await codeField.fill(typed(wrongCode(invite.code)));
    await joinButton(member).click();
    await expect(member.getByText("That link or code doesn't work.")).toBeVisible();
    await expect(codeField).toHaveValue(typed(wrongCode(invite.code)));
  });

  await test.step('a lower-case code with a dash goes on Enter, a dropped connection offers Retry, and Retry joins and opens the budget', async () => {
    await codeField.fill(typed(invite.code));
    await memberContext.route('**/api/budgets/join', route => route.abort('internetdisconnected'));
    await codeField.press('Enter');
    await expect(member.getByText('Joining needs a connection.')).toBeVisible();
    await memberContext.unroute('**/api/budgets/join');
    await member.getByLabel('Retry', { exact: true }).click();
    await expect(member).toHaveURL(new RegExp(`/budgets/${budgetId}`));
    await expect(member.getByRole('heading', { name: household, exact: true })).toBeVisible();
  });

  await test.step('joining replaces the invite in history: Back leaves the budget for the page before the link', async () => {
    await member.goBack();
    await expect(member.getByRole('heading', { name: 'My Day', exact: true })).toBeVisible();
    await member.goForward();
    await expect(member.getByRole('heading', { name: household, exact: true })).toBeVisible();
  });

  await test.step('the member sees who shared it instead of Rename and Archive, and can still add categories', async () => {
    await openBudgetSettings(member);
    await expect(member.getByText(`Shared by ${ownerName}.`)).toBeVisible();
    await expect(member.getByText(`Only ${ownerName} can rename, archive or invite.`)).toBeVisible();
    await expect(member.getByRole('button', { name: 'Rename' })).toHaveCount(0);
    await expect(member.getByRole('button', { name: 'Archive budget' })).toHaveCount(0);
    await expect(member.getByRole('region', { name: 'Expense categories' }).getByRole('button', { name: 'Add' })).toBeVisible();
  });

  await test.step('the member opens the members panel from the head count and sees the owner and no invite', async () => {
    await membersButton(member).click();
    await expect(membersButton(member)).toHaveAccessibleName('Members, 2 people');
    await expect(panel(member).getByText('Shared by', { exact: true })).toBeVisible();
    await expect(panel(member).getByText(ownerName, { exact: true }).first()).toBeVisible();
    await expect(panel(member).getByText(`Only ${ownerName} can invite people.`)).toBeVisible();
    await expect(panel(member).getByRole('img', { name: 'QR code for the invite link' })).toHaveCount(0);
    await expect(panel(member).getByRole('button', { name: /^Remove / })).toHaveCount(0);
    await closePanel(member);
  });

  await test.step("Budgets lists the member's own budgets first, then the shared one saying who shared it", async () => {
    await goTo(member, 'Budgets');
    const cards = member.getByRole('group', { name: /^(Pocket money|Household) / });
    await expect(cards).toHaveCount(2);
    await expect(cards.first()).toHaveAccessibleName(mine);
    await expect(cards.last()).toHaveAccessibleName(household);
    await expect(card(member, household)).toContainText(`Shared by ${ownerName}`);
    await expect(card(member, mine)).not.toContainText('Shared by');
  });

  await test.step("the owner's budget shows a head count that opens its members, and its card says it is shared", async () => {
    await syncNow(page);
    await expect(membersButton(page)).toHaveAccessibleName('Members, 2 people');
    await membersButton(page).click();
    await expect(page).toHaveURL(new RegExp(`\\?members=${budgetId}$`));
    await expect(panel(page).getByRole('button', { name: `Remove ${memberName}` })).toBeVisible();
    await closePanel(page);
    await goTo(page, 'Budgets');
    await expect(card(page, household).getByLabel('Shared · 2 people')).toBeVisible();
    await card(page, household).getByRole('link', { name: household }).click();
  });

  await test.step('offline the owner cannot change who is in', async () => {
    await membersButton(page).click();
    await goOffline(page);
    await expect(panel(page).getByText('Sharing changes need a connection.')).toBeVisible();
    await expect(panel(page).getByRole('button', { name: `Remove ${memberName}` })).toBeDisabled();
    await expect(panel(page).getByRole('button', { name: 'Stop sharing · remove everyone' })).toBeDisabled();
    await expect(panel(page).getByRole('button', { name: 'New link' })).toBeDisabled();
    await goOnline(page);
    await expect(panel(page).getByRole('button', { name: 'New link' })).toBeEnabled();
  });

  await test.step('removing the member takes the budget from them, and a code in the link brings them back at once', async () => {
    await panel(page).getByRole('button', { name: `Remove ${memberName}` }).click();
    await confirm(page, 'Remove');
    await expect(panel(page).getByRole('button', { name: `Remove ${memberName}` })).toHaveCount(0);
    await syncNow(member);
    await goTo(member, 'My Day');
    await goTo(member, 'Budgets');
    await expect(card(member, household)).toHaveCount(0);
    await expect(card(member, mine)).toBeVisible();

    await member.goto(invite.link);
    await expect(member).toHaveURL(new RegExp(`/budgets/${budgetId}`));
    await expect(member.getByRole('heading', { name: household, exact: true })).toBeVisible();
  });

  await closePanel(page);
  await syncNow(page);
  await membersButton(page).click();

  await test.step('an archived budget cannot invite, and its member reads who archived it with no way to restore', async () => {
    await expect(panel(page).getByRole('button', { name: `Remove ${memberName}` })).toBeVisible();
    await panel(page).getByRole('button', { name: 'End link now' }).click();
    await expect(panel(page).getByText('Nobody can join with the old link and code. 1 person already in the budget stays.')).toBeVisible();
    await closePanel(page);
    await owner.send('ArchiveBudget', { budgetId });
    await expect(page.getByText('This budget is archived. It is read-only.')).toBeVisible(pushedWithin);
    await membersButton(page).click();
    await expect(panel(page).getByText('Restore the budget to invite people.')).toBeVisible();
    await expect(panel(page).getByRole('button', { name: 'Create link · 30 min' })).toBeDisabled();
    await expect(panel(page).getByRole('button', { name: `Remove ${memberName}` })).toBeEnabled();

    await goTo(member, 'Budgets');
    await syncNow(member);
    await expect(card(member, household)).toHaveCount(0);
    await member.getByRole('switch', { name: 'Show archived' }).click();
    await card(member, household).getByRole('link', { name: household }).click();
    await expect(member.getByText(`${ownerName} archived this budget. It is read-only.`)).toBeVisible();
    await openBudgetSettings(member);
    await expect(member.getByRole('button', { name: 'Restore budget' })).toHaveCount(0);
  });

  await closePanel(page);
  await owner.send('RestoreBudget', { budgetId });
  await expect(page.getByText('This budget is archived. It is read-only.')).toBeHidden(pushedWithin);
  await membersButton(page).click();
  const again = await createInvite(page, 'Create link · 30 min');

  await test.step('leaving takes the member back to Budgets without the budget, and a code in the link joins at once', async () => {
    await membersButton(member).click();
    await panel(member).getByRole('button', { name: 'Leave budget' }).click();
    await confirm(member, 'Leave');
    await expect(member).toHaveURL(/\/budgets$/);
    await expect(card(member, household)).toHaveCount(0);
    await expect(card(member, mine)).toBeVisible();

    await member.goto(again.link);
    await expect(member).toHaveURL(new RegExp(`/budgets/${budgetId}`));
    await expect(member.getByRole('heading', { name: household, exact: true })).toBeVisible();
  });

  await test.step('after too many join attempts the join page says to wait', async () => {
    const memberApi = await apiFor(await bearerToken(member));
    let status = 0;
    for (let attempt = 0; attempt < 12 && status !== 429; attempt++) {
      status = await tryJoin(memberApi, 'budgets', again.token, wrongCode(again.code));
    }
    expect(status).toBe(429);
    await member.goto(again.link);
    await expect(member.getByText('Too many tries. Wait a few minutes and try again.')).toBeVisible();
    await expect(member).toHaveURL(new RegExp(`/budgets/join/${again.token}$`));
  });

  await test.step('stopping sharing with the member still in ends the link and takes the budget from them', async () => {
    await expect(panel(page).getByRole('button', { name: `Remove ${memberName}` })).toBeVisible();
    await panel(page).getByRole('button', { name: 'Stop sharing · remove everyone' }).click();
    await confirm(page, 'Stop sharing');
    await expect(panel(page).getByText('Only you use this budget')).toBeVisible();
    await expect(panel(page).getByRole('button', { name: `Remove ${memberName}` })).toHaveCount(0);
    await closePanel(page);
    await expect(membersButton(page)).toHaveCount(0);

    await goTo(member, 'My Day');
    await syncNow(member);
    await goTo(member, 'Budgets');
    await expect(card(member, household)).toHaveCount(0);
    await expect(card(member, mine)).toBeVisible();
  });
  await memberContext.close();
});
