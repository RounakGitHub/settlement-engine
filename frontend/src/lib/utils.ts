export function formatPaise(paise: number, currency = "INR"): string {
  const rupees = Math.abs(paise) / 100;
  const formatted = new Intl.NumberFormat("en-IN", {
    style: "currency",
    currency,
    minimumFractionDigits: 2,
  }).format(rupees);
  return paise < 0 ? `-${formatted}` : formatted;
}

export function cn(...classes: (string | false | null | undefined)[]): string {
  return classes.filter(Boolean).join(" ");
}

export function getInitials(name: string): string {
  return name
    .split(" ")
    .map((n) => n[0])
    .join("")
    .toUpperCase()
    .slice(0, 2);
}

export function timeAgo(date: string): string {
  const seconds = Math.floor(
    (Date.now() - new Date(date).getTime()) / 1000
  );
  if (seconds < 60) return "just now";
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

const MONTH_SHORT = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
const MONTH_LONG = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

export function getMonthDay(date: string): { month: string; day: number } {
  const d = new Date(date);
  return { month: MONTH_SHORT[d.getMonth()], day: d.getDate() };
}

export function getMonthYear(date: string): string {
  const d = new Date(date);
  return `${MONTH_LONG[d.getMonth()]} ${d.getFullYear()}`;
}

export function groupByMonth<T extends { createdAt: string }>(items: T[]): { label: string; items: T[] }[] {
  const groups: Map<string, T[]> = new Map();
  for (const item of items) {
    const key = getMonthYear(item.createdAt);
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key)!.push(item);
  }
  return Array.from(groups.entries()).map(([label, items]) => ({ label, items }));
}
