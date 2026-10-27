const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const weekdays = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

export function utcToday(): Date {
  const now = new Date();
  return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()));
}

export function addDays(day: Date, days: number): Date {
  return new Date(day.getTime() + days * 86_400_000);
}

export function shortDate(day: Date): string {
  return `${weekdays[day.getUTCDay()]}, ${day.getUTCDate()} ${months[day.getUTCMonth()]}`;
}

export function isoDate(day: Date): string {
  return day.toISOString().slice(0, 10);
}

export function isoMonth(day: Date): string {
  return isoDate(day).slice(0, 7);
}

export function shiftedZone(): { id: string; localDate: Date } {
  const today = utcToday();
  return new Date().getUTCHours() >= 10
    ? { id: 'Pacific/Kiritimati', localDate: addDays(today, 1) }
    : { id: 'Pacific/Pago_Pago', localDate: addDays(today, -1) };
}
