import { Component, Input, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface Database {
  id: string;
  name: string;
  owner: string;
  creationDate: Date;
  gamesCount: number;
}

@Component({
  selector: 'app-databases-panel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './databases-panel.component.html',
  styleUrl: './databases-panel.component.scss'
})
export class DatabasesPanelComponent {
  @Input() currentUser = '';
  @Input() set databases(value: Database[]) {
    this.databasesSignal.set(value ?? []);
  }

  searchQuery = signal('');
  sortByCreationDesc = signal(true);
  myDatabasesOnly = signal(false);

  private readonly databasesSignal = signal<Database[]>([]);

  filteredAndSortedDatabases = computed(() => {
    let result = this.databasesSignal();
    const query = this.searchQuery().toLowerCase().trim();

    if (this.myDatabasesOnly()) {
      result = result.filter(db => db.owner === this.currentUser);
    }

    if (query) {
      result = result.filter(db =>
        db.name.toLowerCase().includes(query) ||
        db.owner.toLowerCase().includes(query)
      );
    }

    result = [...result].sort((a, b) => {
      const timeA = a.creationDate.getTime();
      const timeB = b.creationDate.getTime();
      return this.sortByCreationDesc() ? timeB - timeA : timeA - timeB;
    });

    return result;
  });

  toggleSort() {
    this.sortByCreationDesc.update(val => !val);
  }

  toggleMyDatabases(): void {
    this.myDatabasesOnly.update(value => !value);
  }

  createNewDatabase(): void {
    const name = window.prompt('Database name');
    if (!name || !name.trim()) {
      return;
    }

    const newDatabase: Database = {
      id: this.generateId(),
      name: name.trim(),
      owner: this.currentUser || 'current-user',
      creationDate: new Date(),
      gamesCount: 0
    };

    this.databasesSignal.update(existing => [newDatabase, ...existing]);
  }

  private generateId(): string {
    if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
      return crypto.randomUUID();
    }

    return `${Date.now()}-${Math.floor(Math.random() * 100000)}`;
  }
}
