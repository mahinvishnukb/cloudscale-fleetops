import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

@Component({
  selector: 'app-stat-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="card">
      <p class="text-xs font-medium uppercase tracking-wide text-slate-400">{{ label }}</p>
      <p class="mt-1 text-2xl font-semibold" [class]="valueClass">{{ value }}</p>
      @if (hint) {
        <p class="mt-1 text-xs text-slate-500">{{ hint }}</p>
      }
    </div>
  `,
})
export class StatCardComponent {
  @Input({ required: true }) label = '';
  @Input({ required: true }) value: string | number = '';
  @Input() hint?: string;
  @Input() valueClass = 'text-white';
}
