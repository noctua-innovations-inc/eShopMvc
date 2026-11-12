import { describe, expect, it } from 'vitest'
import { lastPageIfBeyond, pageUrl } from './paging.ts'

describe('pageUrl', () => {
  it('is the list URL with both paging values', () => {
    expect(pageUrl(5, 2)).toBe('/?pageSize=5&pageIndex=2')
  })
})

// Decision P3-9.
describe('lastPageIfBeyond', () => {
  it.each([
    [0, 2],
    [1, 2],
    [0, 1],
    [0, 0], // empty catalog, first page
  ])('is null for page index %i of %i pages', (actualPage, totalPages) => {
    expect(lastPageIfBeyond({ ActualPage: actualPage, TotalPages: totalPages })).toBeNull()
  })

  it.each([
    [2, 2, 1], // the first index beyond the last page
    [3, 2, 1],
    [1000, 1, 0],
    [3, 0, 0], // empty catalog: back to the first page
  ])('for page index %i of %i pages it is %i', (actualPage, totalPages, expected) => {
    expect(lastPageIfBeyond({ ActualPage: actualPage, TotalPages: totalPages })).toBe(expected)
  })
})
