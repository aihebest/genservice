/**
 * Dip-to-litres for the calibrated AGO storage tanks (charts dated Feb 2026).
 *
 * Mirror of backend Services/TankCalibration.cs — the server is authoritative
 * and recalculates on save; this copy only gives a live preview in the form.
 * Keep the two in step if a tank is re-calibrated.
 */
export interface TankChart {
  tankName: string;
  location: string;
  tankNo: number;
  referenceHeightMm: number;
  maxDipMm: number;
  maxVolume: number;
  /** [fromMm, volumeAtFrom, litresPerMm] — as printed on the chart's "Page 1a". */
  segments: [number, number, number][];
}

export const TANK_CHARTS: TankChart[] = [
  { tankName: 'Tank No. 1 — PHC Office (calibrated)', location: 'Port Harcourt Office', tankNo: 1,
    referenceHeightMm: 2485, maxDipMm: 2445, maxVolume: 9088,
    segments: [[0, 0, 3.700], [10, 37, 3.711], [100, 371, 3.718], [1000, 3717, 3.717], [2000, 7434, 3.717]] },
  { tankName: 'Tank No. 2 — DGS House (calibrated)', location: 'DGS', tankNo: 2,
    referenceHeightMm: 1260, maxDipMm: 1220, maxVolume: 3686,
    segments: [[0, 0, 3.000], [10, 30, 3.022], [100, 302, 3.022], [1000, 3022, 3.022]] },
  { tankName: 'Tank No. 3 — Woji Spar yellow tank (calibrated)', location: 'Woji', tankNo: 3,
    referenceHeightMm: 1600, maxDipMm: 1511, maxVolume: 2391,
    segments: [[0, 0, 1.500], [10, 15, 1.589], [100, 158, 1.583], [1000, 1583, 1.583]] },
  { tankName: 'Tank No. 4 — DR (calibrated)', location: 'DR', tankNo: 4,
    referenceHeightMm: 1550, maxDipMm: 1511, maxVolume: 7308,
    segments: [[0, 0, 4.800], [10, 48, 4.833], [100, 483, 4.838], [1000, 4837, 4.837]] },
];

/** The calibrated tank serving a site (Daily Readings). Mirrors TankCalibration.ForLocation. */
export function chartForLocation(location?: string | null): TankChart | undefined {
  const l = (location ?? '').trim().toLowerCase();
  if (['port harcourt office', 'phc office', 'office', 'port harcourt (ph) office'].includes(l)) return TANK_CHARTS[0];
  if (['dgs', 'dgs house', 'dgs residence'].includes(l)) return TANK_CHARTS[1];
  if (['woji', 'woji spar'].includes(l)) return TANK_CHARTS[2];
  if (l === 'dr') return TANK_CHARTS[3];
  return undefined;
}

export const chartFor = (tankName?: string | null) =>
  TANK_CHARTS.find(c => c.tankName.toLowerCase() === (tankName ?? '').trim().toLowerCase());

export function litresFromDip(chart: TankChart, dipMm: number): number {
  if (dipMm <= 0) return 0;
  if (dipMm >= chart.maxDipMm) return chart.maxVolume;
  const seg = [...chart.segments].reverse().find(s => s[0] <= dipMm)!;
  return Math.floor(seg[1] + seg[2] * (dipMm - seg[0]) + 1e-9);
}
