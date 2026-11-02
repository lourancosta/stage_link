import { TemperatureSample } from '../../core/speaker.models';

// Pure math for the temperature chart (no Angular, easy to unit-test).

export const OVERTEMPERATURE_CELSIUS = 70;
const DEFAULT_MIN = 20;
const DEFAULT_MAX = 80;

export interface ChartSize {
  width: number;
  height: number;
  window: number; // seconds shown, ending at the newest sample
}

export interface Chart {
  points: string; // SVG polyline points: "x,y x,y ..."
  min: number;
  max: number;
  thresholdY: number;
}

export function buildChart(samples: TemperatureSample[], size: ChartSize): Chart {
  const empty = { points: '', min: DEFAULT_MIN, max: DEFAULT_MAX, thresholdY: yFor(OVERTEMPERATURE_CELSIUS, DEFAULT_MIN, DEFAULT_MAX, size.height) };
  if (samples.length === 0) {
    return empty;
  }

  // The right edge is the newest sample; anything older than the window is left out.
  const end = Date.parse(samples[samples.length - 1].time);
  const start = end - size.window * 1000;
  const visible = samples.filter((s) => Date.parse(s.time) >= start);

  // 20–80 °C keeps the 70 °C limit in view; widen only if a value falls outside.
  const temperatures = visible.map((s) => s.temperature);
  const min = Math.min(DEFAULT_MIN, ...temperatures);
  const max = Math.max(DEFAULT_MAX, ...temperatures);

  const points = visible
    .map((s) => {
      const x = ((Date.parse(s.time) - start) / (size.window * 1000)) * size.width;
      const y = yFor(s.temperature, min, max, size.height);
      return `${round(x)},${round(y)}`;
    })
    .join(' ');

  return { points, min, max, thresholdY: yFor(OVERTEMPERATURE_CELSIUS, min, max, size.height) };
}

// SVG y grows downward: the maximum is at the top (0), the minimum at the bottom.
function yFor(temperature: number, min: number, max: number, height: number): number {
  return height - ((temperature - min) / (max - min)) * height;
}

function round(value: number): number {
  return Math.round(value * 10) / 10;
}
