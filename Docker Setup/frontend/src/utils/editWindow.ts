import dayjs from 'dayjs';

/**
 * Mirror of the server's EditWindow rule (GenService.API/Services/EditWindow.cs).
 *
 * The Department Manager and System Admin can edit any record. Anyone else can
 * correct an entry they entered themselves, for EDIT_WINDOW_HOURS after entering
 * it. The server enforces this; the UI only uses it to decide whether to show
 * the Edit button, so keep the number in step with the backend.
 */
export const EDIT_WINDOW_HOURS = 48;

export const isManagerRole = (role?: string) =>
  role === 'DepartmentManager' || role === 'SystemAdmin';

export function canEditRecord(
  role: string | undefined,
  userEmail: string | undefined,
  record: { loggedByEmail?: string; createdAt?: string },
): boolean {
  if (isManagerRole(role)) return true;
  if (!userEmail || !record.loggedByEmail || !record.createdAt) return false;
  const own = record.loggedByEmail.trim().toLowerCase() === userEmail.trim().toLowerCase();
  const recent = dayjs().diff(dayjs(record.createdAt), 'hour', true) <= EDIT_WINDOW_HOURS;
  return own && recent;
}

/** Hours of the window left, for the Edit button tooltip. Null once it has passed. */
export function editHoursLeft(createdAt?: string): number | null {
  if (!createdAt) return null;
  const left = EDIT_WINDOW_HOURS - dayjs().diff(dayjs(createdAt), 'hour', true);
  return left > 0 ? Math.ceil(left) : null;
}
