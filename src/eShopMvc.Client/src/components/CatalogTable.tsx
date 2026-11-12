import {
  CellStyleModule,
  ClientSideRowModelModule,
  ModuleRegistry,
  RowAutoHeightModule,
  ValidationModule,
  themeQuartz,
} from 'ag-grid-community'
import type {
  CellPosition,
  ColDef,
  FocusGridInnerElementParams,
  GetRowIdParams,
  ICellRendererParams,
  SuppressKeyboardEventParams,
  TabToNextCellParams,
} from 'ag-grid-community'
import { AgGridReact } from 'ag-grid-react'
import { useCallback } from 'react'
import { Link } from 'react-router'
import type { CatalogItem } from '../api.ts'
import { formatPrice } from '../format.ts'

// Decision P3-2: only the modules the list uses, to keep the script small. The validation module
// (console messages about wrong options or missing modules) is for development and tests only.
ModuleRegistry.registerModules([ClientSideRowModelModule, RowAutoHeightModule, CellStyleModule])
if (import.meta.env.DEV) {
  ModuleRegistry.registerModules([ValidationModule])
}

// Decision P3-5: AG Grid's theme, set to the fonts, colours and lines of the Bootstrap table it replaces.
const theme = themeQuartz.withParams({
  fontFamily: 'inherit',
  fontSize: 14, // the page's size (P3-8)
  foregroundColor: '#212529',
  backgroundColor: '#FFFFFF',
  headerBackgroundColor: '#FFFFFF',
  headerFontWeight: 700,
  cellHorizontalPadding: 12,
  borderColor: '#EEEEEE',
  wrapperBorder: false,
  wrapperBorderRadius: 0,
  columnBorder: false,
  headerColumnBorder: false,
  headerRowBorder: false,
})

type CellParams = ICellRendererParams<CatalogItem>

function Thumbnail({ data }: CellParams) {
  return <img className="esh-thumbnail" src={data?.PictureUri ?? undefined} />
}

function Price({ data }: CellParams) {
  return data ? <span className="esh-table-price">{formatPrice(data.Price)}</span> : null
}

function Actions({ data }: CellParams) {
  if (!data) {
    return null
  }
  return (
    <>
      <Link className="esh-table-link" to={`/Catalog/Edit/${data.Id}`}>
        Edit
      </Link>
      {' | '}
      <Link className="esh-table-link" to={`/Catalog/Details/${data.Id}`}>
        Details
      </Link>
      {' | '}
      <Link className="esh-table-link" to={`/Catalog/Delete/${data.Id}`}>
        Delete
      </Link>
    </>
  )
}

/** The column with the Edit | Details | Delete links. */
const actionsColumn = 'actions'

// The columns, labels ([Display] names of CatalogItem) and order of the Razor CatalogTable.cshtml. The
// widths are close to the ones the HTML table had, so that all columns fit its 1440px; name and
// description share what is left.
const columns: ColDef<CatalogItem>[] = [
  { colId: 'picture', headerName: '', cellRenderer: Thumbnail, width: 108 },
  { field: 'Name', flex: 1, minWidth: 140 },
  { field: 'Description', flex: 1, minWidth: 140 },
  { colId: 'brand', headerName: 'Brand', valueGetter: (p) => p.data?.CatalogBrand?.Brand, width: 80 },
  { colId: 'type', headerName: 'Type', valueGetter: (p) => p.data?.CatalogType?.Type, width: 80 },
  { field: 'Price', cellRenderer: Price, width: 75 },
  { field: 'PictureFileName', headerName: 'Picture name', width: 140 },
  { field: 'AvailableStock', headerName: 'Stock', width: 75 },
  { field: 'RestockThreshold', headerName: 'Restock', width: 95 },
  { field: 'MaxStockThreshold', headerName: 'Max stock', width: 122 },
  { colId: actionsColumn, headerName: '', cellRenderer: Actions, width: 172, suppressKeyboardEvent: tabThroughLinks },
]

// Cells behave like the table cells they replace: text wraps and the row is as tall as its tallest
// cell. No sorting: it would only reorder the rows of the current page (P3-3; phase 3 non-goal).
const defaultColumn: ColDef<CatalogItem> = {
  sortable: false,
  resizable: true,
  suppressMovable: true,
  wrapText: true,
  autoHeight: true,
  cellClass: 'esh-grid-cell',
}

function rowId({ data }: GetRowIdParams<CatalogItem>): string {
  return String(data.Id)
}

// Keyboard (P3-5). Left to itself the grid stops Tab at every header and cell (121 stops for ten
// rows) and never reaches a link inside a cell. Instead, as AG Grid's documentation recommends for
// cells with links: Tab enters the grid at the links' cell of the first row, walks through its
// links and goes on to the links' cell of the next row; after the last row it leaves the grid.
// Shift+Tab does the same backwards. The arrow keys move between all cells.

function tabThroughLinks({ event }: SuppressKeyboardEventParams<CatalogItem>): boolean {
  if (event.key !== 'Tab' || !(event.target instanceof Element)) {
    return false
  }
  const links = [...(event.target.closest('.ag-cell')?.querySelectorAll<HTMLAnchorElement>('a[href]') ?? [])]
  const current = links.findIndex((link) => link === document.activeElement)
  const next = event.shiftKey ? (current === -1 ? links.length : current) - 1 : current + 1
  if (next < 0 || next >= links.length) {
    return false // first or last link: the grid moves on (tabToNextCell)
  }
  event.preventDefault()
  links[next].focus()
  return true
}

// Decision P3-1: the list's table is an AG Grid (Community). It renders rows as `div`s with the ARIA
// roles of a grid, not a `<table>`. The grid is as tall as its container (`.esh-grid`, P3-4) and
// scrolls its rows inside it, under a header row that stays in place.
export function CatalogTable({ items }: { items: CatalogItem[] }) {
  const rowCount = items.length

  const tabToNextRow = useCallback(
    ({ backwards, previousCellPosition, nextCellPosition }: TabToNextCellParams<CatalogItem>): CellPosition | boolean => {
      if (previousCellPosition.column.getColId() !== actionsColumn) {
        return nextCellPosition ?? false // a cell reached with the mouse or the arrow keys: the grid's order
      }
      const rowIndex = previousCellPosition.rowIndex + (backwards ? -1 : 1)
      // Before the first and after the last row the browser takes Tab out of the grid.
      return rowIndex < 0 || rowIndex >= rowCount ? false : { ...previousCellPosition, rowIndex }
    },
    [rowCount],
  )

  // Tabbing into the grid: the links' cell of the first row, or of the last row when coming from
  // below (Shift+Tab). The grid's default is its first header cell.
  const focusLinksCell = useCallback(
    ({ api, fromBottom }: FocusGridInnerElementParams<CatalogItem>): boolean => {
      if (rowCount === 0) {
        return false
      }
      api.setFocusedCell(fromBottom ? rowCount - 1 : 0, actionsColumn)
      return true
    },
    [rowCount],
  )

  return (
    <div className="esh-grid">
      <AgGridReact<CatalogItem>
        theme={theme}
        rowData={items}
        columnDefs={columns}
        defaultColDef={defaultColumn}
        getRowId={rowId}
        animateRows={false}
        tabToNextCell={tabToNextRow}
        focusGridInnerElement={focusLinksCell}
        // Every row and column of the page is in the document, in reading order, also the ones
        // scrolled out of view: the server pages the list (P3-3), so there are only as many rows as
        // the page size, as in the table; Tab and screen readers reach all of them; and a test
        // environment without layout would otherwise render only some.
        suppressRowVirtualisation
        suppressColumnVirtualisation
        ensureDomOrder
      />
    </div>
  )
}
