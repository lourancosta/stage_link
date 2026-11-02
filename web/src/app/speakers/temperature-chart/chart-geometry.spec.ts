import { TemperatureSample } from '../../core/speaker.models';
import { buildChart } from './chart-geometry';

function sample(secondsAfterStart: number, temperature: number): TemperatureSample {
  return { time: new Date(Date.UTC(2026, 9, 31, 20, 0, secondsAfterStart)).toISOString(), temperature, level: -20 };
}

describe('buildChart', () => {
  const size = { width: 600, height: 200, window: 600 }; // 600 s = 10 minutes

  it('has no line without samples', () => {
    expect(buildChart([], size).points).toBe('');
  });

  it('puts the newest sample at the right edge and older ones to the left', () => {
    const chart = buildChart([sample(0, 25), sample(300, 25), sample(600, 25)], size);

    const xs = chart.points.split(' ').map((p) => Number(p.split(',')[0]));
    expect(xs).toEqual([0, 300, 600]);
  });

  it('maps temperature to height: the default range is 20 to 80 °C', () => {
    const chart = buildChart([sample(0, 20), sample(1, 80)], size);

    const ys = chart.points.split(' ').map((p) => Number(p.split(',')[1]));
    expect(ys).toEqual([200, 0]); // SVG y grows downward
    expect(chart.min).toBe(20);
    expect(chart.max).toBe(80);
  });

  it('places the 70 °C overtemperature line', () => {
    const chart = buildChart([sample(0, 30)], size);

    expect(chart.thresholdY).toBeCloseTo(200 - ((70 - 20) / 60) * 200);
  });

  it('widens the range when a temperature falls outside it', () => {
    const chart = buildChart([sample(0, 15), sample(1, 90)], size);

    expect(chart.min).toBe(15);
    expect(chart.max).toBe(90);
  });

  it('leaves out samples older than the time window', () => {
    const chart = buildChart([sample(0, 30), sample(700, 30)], size);

    expect(chart.points.split(' ')).toHaveLength(1);
  });
});
