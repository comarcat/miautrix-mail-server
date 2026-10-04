// Working hours are a display preference shared by two views: Settings & Rules owns
// the inputs, the calendar reads them. Keeping the storage keys and the parse here
// means the two cannot drift apart.
export const WORK_HOURS_START_KEY = 'miautrix_webmail_work_hours_start';
export const WORK_HOURS_END_KEY = 'miautrix_webmail_work_hours_end';
export const WORK_DAYS_KEY = 'miautrix_webmail_work_days';
export const WORK_HOURS_CHANGED_EVENT = 'miautrix:webmail:work-hours-changed';

export const DEFAULT_WORK_HOURS_START = '09:00';
export const DEFAULT_WORK_HOURS_END = '17:00';
export const DEFAULT_WORK_DAYS = [1, 2, 3, 4, 5];

/** Slots per hour in the calendar's time grid. */
const SLOTS_PER_HOUR = 2;
const SLOT_MINUTES = 30;

/** "09:30" → slot index (19), or null when the value is absent or malformed. */
export const hourToSlot = (value: string | null): number | null => {
  if (!value) return null;
  const [h, m] = value.split(':').map((part) => Number.parseInt(part, 10));
  if (!Number.isFinite(h) || h < 0 || h > 23) return null;
  const minutes = Number.isFinite(m) ? m : 0;
  return h * SLOTS_PER_HOUR + (minutes >= SLOT_MINUTES ? 1 : 0);
};

export interface WorkHours {
  /** Slot the working day starts at. */
  start: number;
  /** Slot the working day ends at (exclusive). */
  end: number;
  startLabel: string;
  endLabel: string;
  workDays: number[];
}

const parseWorkDays = (value: string | null): number[] => {
  const days = (value ?? '')
    .split(',')
    .map((part) => Number.parseInt(part, 10))
    .filter((day) => Number.isInteger(day) && day >= 0 && day <= 6);
  return days.length ? [...new Set(days)].sort() : DEFAULT_WORK_DAYS;
};

export const readWorkHours = (): WorkHours => {
  const rawStart = window.localStorage.getItem(WORK_HOURS_START_KEY) || DEFAULT_WORK_HOURS_START;
  const rawEnd = window.localStorage.getItem(WORK_HOURS_END_KEY) || DEFAULT_WORK_HOURS_END;
  return {
    start: hourToSlot(rawStart) ?? hourToSlot(DEFAULT_WORK_HOURS_START)!,
    end: hourToSlot(rawEnd) ?? hourToSlot(DEFAULT_WORK_HOURS_END)!,
    startLabel: rawStart,
    endLabel: rawEnd,
    workDays: parseWorkDays(window.localStorage.getItem(WORK_DAYS_KEY)),
  };
};

export const writeWorkHours = (start: string, end: string, workDays?: number[]) => {
  window.localStorage.setItem(WORK_HOURS_START_KEY, start);
  window.localStorage.setItem(WORK_HOURS_END_KEY, end);
  if (workDays) window.localStorage.setItem(WORK_DAYS_KEY, workDays.join(','));
  window.dispatchEvent(new Event(WORK_HOURS_CHANGED_EVENT));
};
