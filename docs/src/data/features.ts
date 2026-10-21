export interface Feature {
  id: string
  title: string
  does: string
  how: string
}

export interface Planned {
  title: string
  body: string
}

export const features: Feature[] = [
  { id: 'today', title: 'My Day',
    does: 'Everything due today or earlier, from every area, on one screen.',
    how: 'Timed tasks come first in order, then anything-time tasks, with overdue pinned on top, in your own time zone. A week strip opens any other day.' },
  { id: 'inbox', title: 'Inbox',
    does: 'Capture anything in one tap and sort it out later.',
    how: 'An item becomes a full task — list, date, priority, goal, steps — in one step when you are ready.' },
  { id: 'areas', title: 'Areas and lists',
    does: 'Work, home, side projects: each area holds its own lists in your order.',
    how: 'Lists pack into columns on a big screen and stack on a phone. Deleting an area tells you how much goes with it.' },
  { id: 'tasks', title: 'Tasks and steps',
    does: 'A due date, a priority, a goal and ordered steps on every task.',
    how: 'A step can carry its own date, and the next unchecked step is what puts the task on My Day.' },
  { id: 'descriptions', title: 'Descriptions',
    does: 'Markdown notes, links and checklists on every task and item.',
    how: 'Rendered the same online and offline.' },
  { id: 'repeats', title: 'Repeats',
    does: 'Daily, weekly, monthly, yearly or every third Monday, with an optional end.',
    how: 'Only today’s occurrence shows and a missed day stays missed, so a repeat is never overdue. A lead time shows it as far ahead as you choose.' },
  { id: 'goals', title: 'Goals',
    does: 'One goal, with tasks from any area pointed at it.',
    how: 'A due date and a status: in progress, achieved or not achieved. Goals are private to you, even on shared lists.' },
  { id: 'references', title: 'Reference lists',
    does: 'Lists of things you look up rather than do: spools, documents, links.',
    how: 'Each item has labelled fields shown as links, copyable paths or quantities. They never land on My Day.' },
  { id: 'budgets', title: 'Budgets',
    does: 'Expenses, incomes and monthly balances, in PLN and 28 other currencies.',
    how: 'Every amount keeps the rate to PLN it was entered at, so history never shifts. Categories grow as you type. Charts cover any range.' },
  { id: 'import', title: 'Spreadsheet import',
    does: 'Bring a budget’s history in from two CSV files.',
    how: 'Comma or semicolon, Polish characters included. A preview shows every row before anything is saved.' },
  { id: 'sharing', title: 'Share a list or budget',
    does: 'Keep one list or one budget with your household.',
    how: 'An invite is a link or QR code with a 6-character code, valid 30 minutes. Whoever joins works with you, offline included.' },
  { id: 'snapshots', title: 'Public snapshots',
    does: 'A read-only link to a list that anyone can open, no account needed.',
    how: 'Visitors tick things off and you see it on your copy. The link expires when you say: a day, a week, a month or a date.' },
  { id: 'statistics', title: 'Statistics',
    does: 'Charts and a heatmap of the days you finished things.',
    how: 'Built from what actually happened, so a deleted task still counts the day it was done.' },
  { id: 'offline', title: 'Works offline',
    does: 'Opens with your data and syncs when you are back.',
    how: 'Every screen says when it last updated. Pull down on a phone or press refresh on a computer. A device untouched for 7 days asks you to sign in again.' },
  { id: 'live', title: 'Live updates',
    does: 'Your other devices and list members see changes within a moment.',
    how: 'An open app receives a nudge and pulls the change; no refresh, no polling.' },
  { id: 'languages', title: 'English and Polish',
    does: 'One choice in Settings, followed on every device.',
    how: 'Dates, numbers and money follow the language, offline included.' },
  { id: 'phone', title: 'Made for a phone',
    does: 'A bottom bar puts Inbox, Areas, My Day, Goals and Budgets one tap away.',
    how: 'Install it from the browser for the offline app. Areas switch with a row of chips.' },
  { id: 'yours', title: 'Yours',
    does: 'Your server, your Keycloak, your data, as many users as you invite.',
    how: 'Everyone gets private areas, lists, goals and an Inbox. Time zone, theme, accent colour and account deletion sit in Settings.' }
]

export const planned: Planned[] = [
  { title: 'Habits', body: 'Streaks for daily habits.' },
  { title: 'Annual plans', body: 'Yearly goals, year-end summary.' },
  { title: 'Reminders', body: 'Push notifications, daily motivation.' },
  { title: 'GitHub issues', body: 'GitHub issues as tasks.' },
  { title: 'Cloud storage', body: 'OneDrive and Google Drive.' },
  { title: '3D printing', body: 'Materials, parts, Thingiverse links.' },
  { title: 'Checklists', body: 'Reusable checklists.' }
]

export function feature(id: string): Feature {
  const found = features.find((item) => item.id === id)
  if (!found) throw new Error(`No feature with id "${id}" in docs/src/data/features.ts`)
  return found
}
