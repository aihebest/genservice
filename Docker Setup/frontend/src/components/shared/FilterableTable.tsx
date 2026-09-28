import { Table } from 'antd';
import type { TableProps } from 'antd';
import type { ColumnType, ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';

/**
 * Drop-in replacement for antd's <Table> that gives every column header sorting,
 * and a filter dropdown wherever the column holds a manageable set of values
 * (location, status, guest house, cost code, supplier…).
 *
 * The General Service team works from Excel, where every header has a filter.
 * Adding filters to 30-odd tables by hand would drift out of step the moment a
 * column is added, so this reads the loaded rows and works out, per column,
 * whether the values are text, numbers or dates.
 *
 * Rules:
 *  • A column that already defines `filters` or `sorter` is left exactly as is.
 *  • Columns with no data field (actions, buttons) are left alone.
 *  • Filters appear when a column has between 2 and MAX_FILTER_VALUES distinct
 *    values; free-text columns (descriptions, notes) get sorting only.
 *  • On server-paginated tables the filters act on the rows currently loaded.
 */
const MAX_FILTER_VALUES = 60;
const SEARCHABLE_OVER   = 8;
const ISO_DATE = /^\d{4}-\d{2}-\d{2}([T ][\d:.]+)?(Z|[+-]\d{2}:?\d{2})?$/;

type AnyCol<T> = ColumnsType<T>[number];

function getValue(row: unknown, dataIndex: unknown): unknown {
  if (row == null) return undefined;
  if (Array.isArray(dataIndex)) {
    return dataIndex.reduce<unknown>(
      (acc, k) => (acc == null ? undefined : (acc as Record<string, unknown>)[k as string]), row);
  }
  if (typeof dataIndex === 'string' || typeof dataIndex === 'number') {
    return (row as Record<string, unknown>)[dataIndex as string];
  }
  return undefined;
}

/** "InWorkshop" → "In Workshop", "port_harcourt" → "Port harcourt" for filter labels. */
function humanise(v: string): string {
  if (/\s/.test(v) || v.length > 40) return v;
  const spaced = v.replace(/_/g, ' ').replace(/([a-z])([A-Z])/g, '$1 $2');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

type Kind = 'number' | 'date' | 'text' | 'bool' | 'none';

function kindOf(values: unknown[]): Kind {
  const present = values.filter(v => v !== null && v !== undefined && v !== '');
  if (present.length === 0) return 'none';
  if (present.every(v => typeof v === 'number')) return 'number';
  if (present.every(v => typeof v === 'boolean')) return 'bool';
  if (present.every(v => typeof v === 'string' && ISO_DATE.test(v))) return 'date';
  if (present.every(v => typeof v === 'string' || typeof v === 'number')) return 'text';
  return 'none';
}

function enhance<T>(col: AnyCol<T>, rows: readonly T[]): AnyCol<T> {
  // Column groups: enhance the children.
  if ('children' in col && Array.isArray(col.children)) {
    return { ...col, children: col.children.map(c => enhance(c as AnyCol<T>, rows)) } as AnyCol<T>;
  }

  const c = col as ColumnType<T>;
  if (c.filters || c.sorter || c.dataIndex === undefined || c.dataIndex === null) return col;

  const values = rows.map(r => getValue(r, c.dataIndex));
  const kind = kindOf(values);
  if (kind === 'none') return col;

  const out: ColumnType<T> = { ...c };

  if (kind === 'number') {
    out.sorter = (a, b) => (Number(getValue(a, c.dataIndex)) || 0) - (Number(getValue(b, c.dataIndex)) || 0);
  } else if (kind === 'date') {
    out.sorter = (a, b) => {
      const x = getValue(a, c.dataIndex) as string | undefined;
      const y = getValue(b, c.dataIndex) as string | undefined;
      return (x ? dayjs(x).valueOf() : 0) - (y ? dayjs(y).valueOf() : 0);
    };
  } else {
    out.sorter = (a, b) =>
      String(getValue(a, c.dataIndex) ?? '').localeCompare(String(getValue(b, c.dataIndex) ?? ''),
                                                          undefined, { numeric: true });
  }

  // Filters for categorical columns — dates filter by month, which is how the
  // team slices their spreadsheets.
  if (kind === 'date') {
    const months = Array.from(new Set(
      values.filter(Boolean).map(v => dayjs(v as string).format('MMM YYYY'))));
    if (months.length >= 2 && months.length <= MAX_FILTER_VALUES) {
      out.filters = months
        .sort((a, b) => dayjs(a, 'MMM YYYY').valueOf() - dayjs(b, 'MMM YYYY').valueOf())
        .map(m => ({ text: m, value: m }));
      out.onFilter = (v, r) => {
        const x = getValue(r, c.dataIndex) as string | undefined;
        return !!x && dayjs(x).format('MMM YYYY') === v;
      };
    }
  } else if (kind === 'text' || kind === 'bool') {
    const distinct = Array.from(new Set(
      values.filter(v => v !== null && v !== undefined && v !== '').map(v => String(v))));
    const avgLen = distinct.reduce((s, v) => s + v.length, 0) / Math.max(1, distinct.length);
    // Long free text (descriptions, notes) is not worth a checkbox list.
    if (distinct.length >= 2 && distinct.length <= MAX_FILTER_VALUES && avgLen <= 45) {
      out.filters = distinct
        .sort((a, b) => a.localeCompare(b, undefined, { numeric: true }))
        .map(v => ({ text: kind === 'bool' ? (v === 'true' ? 'Yes' : 'No') : humanise(v), value: v }));
      out.filterSearch = distinct.length > SEARCHABLE_OVER;
      out.onFilter = (v, r) => String(getValue(r, c.dataIndex) ?? '') === String(v);
    }
  }

  return out as AnyCol<T>;
}

export default function FilterableTable<T extends object>(props: TableProps<T>) {
  const rows = (props.dataSource ?? []) as readonly T[];
  const columns = props.columns?.map(col => enhance(col as AnyCol<T>, rows));
  return <Table<T> {...props} columns={columns} />;
}
