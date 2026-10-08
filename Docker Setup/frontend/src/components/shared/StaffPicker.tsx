import { AutoComplete } from 'antd';
import { useQuery } from '@tanstack/react-query';
import usersApi from '../../api/users.api';
import type { StaffOption } from '../../api/users.api';

/**
 * Real General Service staff, fetched once and cached. Replaces the hard-coded
 * demo names the scheduler and activity logger shipped with.
 */
export function useGsStaff(): StaffOption[] {
  const { data } = useQuery({
    queryKey: ['gs-staff'],
    queryFn:  usersApi.staff,
    staleTime: 10 * 60_000,
  });
  return data ?? [];
}

/** Email for a typed or picked name, if it belongs to a known staff member. */
export function emailForName(staff: StaffOption[], name?: string): string | undefined {
  const n = name?.trim().toLowerCase();
  return n ? staff.find(s => s.name.toLowerCase() === n)?.email : undefined;
}

/**
 * "Assign to" field: suggests GS staff as you type, but accepts any name —
 * a supervisor or contractor who has no platform login can still be assigned.
 * Value is the person's name.
 */
export default function StaffPicker(props: {
  value?: string;
  onChange?: (name: string) => void;
  placeholder?: string;
}) {
  const staff = useGsStaff();
  return (
    <AutoComplete
      value={props.value}
      onChange={props.onChange}
      allowClear
      placeholder={props.placeholder ?? 'Type or pick a GS staff member'}
      options={staff.map(s => ({ value: s.name, label: `${s.name} · ${s.role}` }))}
      filterOption={(input, option) =>
        String(option?.value ?? '').toLowerCase().includes(input.toLowerCase())}
    />
  );
}
