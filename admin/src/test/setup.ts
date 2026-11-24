import { expect } from 'vitest';
import * as matchers from '@testing-library/jest-dom/matchers';

expect.extend(matchers);

// Node 25+ ships a global Web Storage that is undefined unless started with
// --localstorage-file, and it shadows jsdom's localStorage. Provide an
// in-memory Storage for tests when the global one is unusable.
function createMemoryStorage(): Storage {
  const data = new Map<string, string>();
  return {
    get length() { return data.size; },
    clear: () => data.clear(),
    getItem: (key: string) => (data.has(key) ? data.get(key)! : null),
    key: (index: number) => Array.from(data.keys())[index] ?? null,
    removeItem: (key: string) => { data.delete(key); },
    setItem: (key: string, value: string) => { data.set(key, String(value)); },
  };
}

for (const name of ['localStorage', 'sessionStorage'] as const) {
  let usable = false;
  try { usable = typeof globalThis[name]?.getItem === 'function'; } catch { usable = false; }
  if (!usable) {
    Object.defineProperty(globalThis, name, { value: createMemoryStorage(), configurable: true, writable: true });
  }
}
