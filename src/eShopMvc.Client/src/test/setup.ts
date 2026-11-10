import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'

// Vitest globals are off, so React Testing Library can't register its own clean-up.
afterEach(() => {
  cleanup()
  vi.unstubAllGlobals() // the fetch of test/fakeApi.ts
})
