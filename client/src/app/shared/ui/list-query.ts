import { HttpParams } from '@angular/common/http';
import { computed, signal } from '@angular/core';
import { toParams } from '../../core/http-params';

type Filters = Record<string, string | number | boolean | null>;

/**
 * Signal-based state for server-side lists (page, size, sort, debounced search and filters).
 * `params()` is meant to feed an `httpResource`, which re-fetches whenever it changes.
 */
export class ListQuery<F extends Filters = Filters> {
  readonly page = signal(1);
  readonly pageSize = signal(15);
  readonly sort = signal<string | null>(null);
  readonly search = signal('');
  readonly filters = signal<F>({} as F);
  private readonly debouncedSearch = signal('');
  private timer?: ReturnType<typeof setTimeout>;

  readonly params = computed<HttpParams>(() =>
    toParams({
      page: this.page(),
      pageSize: this.pageSize(),
      sort: this.sort(),
      search: this.debouncedSearch(),
      ...this.filters(),
    }),
  );

  constructor(defaults: { sort?: string; pageSize?: number; filters?: F } = {}) {
    this.sort.set(defaults.sort ?? null);
    this.pageSize.set(defaults.pageSize ?? 15);
    this.filters.set(defaults.filters ?? ({} as F));
  }

  setSearch(term: string, delay = 300): void {
    this.search.set(term);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => {
      this.debouncedSearch.set(term.trim());
      this.page.set(1);
    }, delay);
  }

  setFilter<K extends keyof F>(key: K, value: F[K]): void {
    this.filters.update((f) => ({ ...f, [key]: value }));
    this.page.set(1);
  }

  /** Toggles asc → desc on the same column; a new column starts ascending. */
  toggleSort(key: string): void {
    const current = this.sort();
    this.sort.set(current === key ? `-${key}` : key);
    this.page.set(1);
  }
}
