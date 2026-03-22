import { Component, Input } from '@angular/core';

export interface MoveRow {
  number: number;
  white: string;
  black: string;
}

@Component({
  selector: 'app-move-list',
  standalone: true,
  templateUrl: './move-list.component.html',
  styleUrl: './move-list.component.scss'
})
export class MoveListComponent {
  @Input() moveRows: MoveRow[] = [];
}
