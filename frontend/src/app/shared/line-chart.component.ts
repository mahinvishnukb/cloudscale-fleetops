import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Input,
  OnChanges,
  OnDestroy,
  ViewChild,
} from '@angular/core';
import { Chart, ChartConfiguration, registerables } from 'chart.js';

Chart.register(...registerables);

export interface Series {
  label: string;
  colour: string;
  /** One value per entry in `labels`, in the same order. */
  values: number[];
  yAxis?: 'y' | 'y1';
}

/**
 * Thin Chart.js wrapper. Kept imperative on purpose: Chart.js owns the canvas, so
 * re-rendering through Angular templates would fight it. Updates mutate the existing
 * chart instance rather than destroying and rebuilding it.
 *
 * Data is supplied as `labels` plus parallel `values` arrays rather than {x,y} points:
 * a category axis indexes datasets positionally, and passing point objects to it is a
 * type error in Chart.js 4.
 */
@Component({
  selector: 'app-line-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="relative h-64"><canvas #canvas></canvas></div>`,
})
export class LineChartComponent implements AfterViewInit, OnChanges, OnDestroy {
  @ViewChild('canvas') private canvasRef?: ElementRef<HTMLCanvasElement>;

  @Input({ required: true }) series: Series[] = [];
  @Input({ required: true }) labels: string[] = [];
  @Input() secondaryAxisLabel?: string;

  private chart?: Chart<'line', number[], string>;

  ngAfterViewInit(): void {
    this.render();
  }

  ngOnChanges(): void {
    this.render();
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }

  private render(): void {
    const canvas = this.canvasRef?.nativeElement;
    if (!canvas) {
      return;
    }

    const datasets = this.series.map((s) => ({
      label: s.label,
      data: s.values,
      borderColor: s.colour,
      backgroundColor: s.colour,
      borderWidth: 2,
      pointRadius: 0,
      tension: 0.25,
      yAxisID: s.yAxis ?? 'y',
    }));

    if (this.chart) {
      this.chart.data.labels = this.labels;
      this.chart.data.datasets = datasets;
      // 'none' skips the animation: on a live feed, animating every tick looks broken.
      this.chart.update('none');
      return;
    }

    const config: ChartConfiguration<'line', number[], string> = {
      type: 'line',
      data: { labels: this.labels, datasets },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        scales: {
          x: {
            ticks: { color: '#64748b', maxTicksLimit: 8 },
            grid: { color: 'rgba(148,163,184,0.08)' },
          },
          y: {
            position: 'left',
            ticks: { color: '#64748b' },
            grid: { color: 'rgba(148,163,184,0.08)' },
          },
          y1: {
            position: 'right',
            ticks: { color: '#64748b' },
            grid: { drawOnChartArea: false },
            title: { display: !!this.secondaryAxisLabel, text: this.secondaryAxisLabel ?? '' },
          },
        },
        plugins: {
          legend: { labels: { color: '#94a3b8', boxWidth: 12 } },
          tooltip: { intersect: false },
        },
      },
    };

    this.chart = new Chart(canvas, config);
  }
}
