# Phase 3: scrollable grid for the catalog list

> **Complete (2026-11-11);** the current phase is named in `AGENTS.md`. Read this file before starting any task. The project-wide rules in `AGENTS.md`
> still apply, and so do the decisions of phase 1 (`phase-01-port.md`) and phase 2
> (`phase-02-react.md`) unless a P3 decision below changes one of them.

## Goal
The catalog list uses the visible space of the window: the table of the list page becomes a grid
that scrolls on its own, with a fixed header row, so the page itself does not have to be scrolled
(requested 2026-11-11). The grid component is **AG Grid Community**. Paging, URLs, the API and the
other pages stay as they are.

Added on 2026-11-11 after a look at the grid (steps 5 to 7, approved the same day): no footer and no
session (P3-7), smaller sizes on all pages (P3-8), and a list that never shows a page beyond the
last one (P3-9). These lift the non-goals below where they say so.

**Reference behavior** is the React UI at the end of phase 2 (commit `c132c54`), plus the deliberate
differences recorded below and in `docs/parity.md`.

## Non-goals (ask before doing any of these)
- `ag-grid-enterprise` or any feature that needs it (row grouping, pivoting, Excel export, the
  server-side row model). It is a separate, paid package and must never be installed.
- Sorting and filtering. With server paging they would only act on the rows of the current page;
  doing them properly needs new API parameters (a route change).
- The grid's "infinite" row model (rows loaded while scrolling, no pager). Possible later step; it
  replaces the pager and the paging URLs (P2-9, R-24).
- Changes to the API, the Create, Edit, Details and Delete pages, the error page or the container
  setup, other than those of P3-7 (footer, session endpoint, `TZ`) and P3-8 (sizes).
- Visitor tracking (a session or correlation id in the log lines). Discussed on 2026-11-11 when the
  session endpoint went; it would be a new feature with its own step.
- Any other new npm package.

## Phase 3 rules (in addition to the ground rules in AGENTS.md and the phase 2 rules)
- One step at a time, in the order below; plan first for multi-file steps.
- After every step: `dotnet build`, the client's `npm run build`, `npm test` and `npm run lint`, the
  smoke tests and the browser tests must pass.
- Every deliberate difference from the phase 2 UI gets a P3 decision here and a test.

## Decisions
- **P3-1 Grid component** (approved 2026-11-11): AG Grid Community, packages `ag-grid-community` and
  `ag-grid-react`, exact version **36.3.0** (`save-exact`, as P2-5). This lifts the phase 2 non-goal
  "a component library" for this one component.
  - **Licence:** MIT. Checked on 2026-11-11 by reading `LICENSE.txt` in both package files (standard
    MIT text, no eligibility conditions, no licence key). The dependencies they bring (`ag-stack`
    36.3.0, `ag-charts-types` 14.3.0, `prop-types` 15.8.1 with `loose-envify`, `js-tokens`,
    `object-assign`, `react-is`) are MIT according to the npm metadata. Re-check the licence at every
    version update: a vendor can change it between versions.
  - **Rejected:** PrimeReact (11.x, the current major, is under the commercial "PrimeUI License" with
    a licence key; only 10.9.9, the previous major, is MIT); KendoReact Free (commercial EULA; paging
    and row virtualization are listed as premium); MUI X Data Grid (needs Material UI and Emotion);
    TanStack Table (headless: the scrolling and the header would be our own code).
- **P3-2 Modules** (step 1): the grid registers only the modules it uses (`ModuleRegistry`), not
  `AllCommunityModule`. Measured in step 1 with a throwaway import: the client script grows from
  274 kB (86 kB gzip) to about 1,008 kB (294 kB gzip) with `ClientSideRowModelModule` alone, and to
  1,479 kB (423 kB gzip) with `AllCommunityModule`.
  - **Steps 2 and 3:** registered are `ClientSideRowModelModule`, `RowAutoHeightModule` and
    `CellStyleModule`; `ValidationModule` (console messages about wrong options) only in development
    and tests (`import.meta.env.DEV`). Final script: 1,014 kB (296 kB gzip), 3.7 times phase 2's.
- **P3-3 Paging stays on the server:** the grid shows the rows of the current page (client-side row
  model); the phase 2 pager below it, its text (R-24), the `?pageSize=…&pageIndex=…` URLs (P2-9) and
  the messages of P2-14 are unchanged. AG Grid's own pagination panel is not used.
- **P3-4 Layout** (step 3): on the list page the header, banner, "Create New", pager and footer stay
  in place and the grid takes the remaining window height and scrolls inside it. Below a minimum
  grid height the page scrolls as before. The other pages keep their layout.
  - **Step 3:** `CatalogIndex` sets the class `esh-fill` on `<body>` while it is shown; the rules are
    in `wwwroot/css/site.css` (a flex column of the window's height; the grid's container `.esh-grid`
    takes the rest, minimum 300px). No markup of the shell changed. Measured: at 1920×1080 the grid is
    441px high (about four rows of 104px); at 1280×800 the minimum applies and the page scrolls by
    39px.
  - **Answered 2026-11-11:** the footer goes (P3-7) and the banner shrinks (P3-8). Measured after
    step 6: the grid is 818px at 1920×1080 (eleven full rows of 68px), 538px at 1280×800 (seven),
    458px at 1280×720 (five); the page scrolls at none of them. The 300px minimum now applies only
    below a window height of about 562px.
- **P3-5 Look** (step 2): AG Grid renders `div` rows with ARIA roles (`grid`, `row`, `columnheader`,
  `gridcell`), not a `<table>`, and brings its own theme, set in code and matched to the current
  fonts and colours as far as the theme allows. Columns, labels, price format, thumbnail and the
  Edit | Details | Delete links stay.
  - **Step 2** (`components/CatalogTable.tsx`): theme `themeQuartz.withParams` (inherited font, 16px,
    the table's colours, no wrapper, column or header borders); header text in capitals and the
    cells' weight, padding and vertical centring in `site.css` (`.esh-grid`). Cells wrap their text
    and rows are as tall as their tallest cell (`wrapText`, `autoHeight`); the table never wrapped
    (`white-space: nowrap`) and was wider than the page instead. Column widths are close to the
    table's and fit 1440px; name and description share the rest (`flex`). Columns can be resized;
    not sorted (non-goal) or moved. No row animation.
  - **All rows of the page are rendered**, in reading order (`suppressRowVirtualisation`,
    `suppressColumnVirtualisation`, `ensureDomOrder`): the server pages the list, so there are only
    as many rows as the page size, as in the table, and Tab, screen readers and jsdom reach them all.
  - **Keyboard:** AG Grid's default stops Tab at every header and cell (121 stops for ten rows) and
    never reaches a link in a cell. Implemented as its documentation recommends for cells with
    links: Tab enters the grid at the links' cell of the first row (`focusGridInnerElement`), walks
    through the three links (`suppressKeyboardEvent` on that column), goes to the links' cell of the
    next row (`tabToNextCell`) and leaves the grid after the last row; Shift+Tab backwards. One stop
    more per row than the table had. Two simpler attempts failed: `suppressCellFocus` kept Tab on
    the first link for good, and leaving Tab to the browser everywhere skipped the rows or stopped
    on them. Browser-tested.
  - AG Grid adds its theme as `<style>` elements; `shell.spec.ts` now checks the linked sheets only.
- **P3-6 Tests:**
  - **Vitest:** AG Grid renders in jsdom (found in step 1), with two things to know: custom cell
    components appear one tick after the row (use `findBy…`), and jsdom has no layout, so the grid
    needs `suppressColumnVirtualisation` to render every column and renders only the first rows of a
    long list (11 of 30 in the spike). The list tests keep their checks with role-based selectors.
    Since step 2 the grid renders all rows and columns anyway (P3-5).
  - **Browser tests:** `list.spec.ts` gets the new selectors and, in step 3, the layout checks (the
    page does not scroll, the grid does, header row and pager stay in view).
  - **Steps 2 and 3:** rows are `.ag-row`, cells `[role="gridcell"]`, headers
    `[role="columnheader"]` in all spec files. New in `list.spec.ts`: the Tab key, the layout at
    1920×1080 (Playwright's default 1280×720 is below the minimum, so the other tests run with a
    scrolling page), the low window, and that the other pages are not filled.
  - **Tooling note:** the Vite dev server stopped answering after the first page load during step 2
    (`ECONNRESET` from its proxy for `/lib/bootstrap/…`; the app itself was healthy). Not
    investigated; the checks ran against the built client on port 5070.
- **P3-7 No footer, no session** (step 5, approved 2026-11-11: "the footer has no value"): the
  footer is removed from the React layout and from the Razor error page's `_Layout.cshtml`. The
  session only held the footer's machine name and start time, so it went too:
  `SessionApiController` (`GET /api/v1/session`, now 404), `SessionStartFilter`, `SessionInfo`,
  `AddSession`/`UseSession`, the client's `getSession`, and in the container setup `TZ` and
  `hostname`. **Supersedes R-11, P2-8 and D-E.** It was not tracking: nothing logged or stored it.
  The logging scopes (`X-Client-Page`, P2-19; user agent, L2) stay. The two footer images are still
  in `wwwroot/images`, unused.
- **P3-8 Smaller sizes** (step 6, approved 2026-11-11: "everything is too large"), on all pages:
  - base font 14px (`base.css`, was 16px), so everything in `rem` follows; the grid's theme too;
  - list thumbnails 84px wide (120px, −30%), the picture column 108px; the other columns narrower
    in proportion;
  - grid cell padding 6px top and bottom (12px): rows are 68px (104px);
  - buttons: padding `0.5rem 1rem` (`1rem 1.5rem`), `margin-top: 0.5rem` (`1rem`);
  - banner 72px high (`12.5vw`, and `20vw` up to 1024px wide), title `2rem` = 28px (`4vw`);
  - **one scale for text** (added the same day, after "huge Edit | Back to List" on the Details
    page: the first pass had only shrunk these by the base font, to 28px links and a 42px page
    title above a 28px banner title): banner title 28px, page title `.esh-body-title` `1.5rem` =
    21px (`3rem`) with half the padding, the Delete page's `h3` `1.25rem` = 17.5px, and the links
    under the item pages (`.esh-link-item`, `.esh-link-list`) `1rem` = 14px (`2rem`), as the links
    in the list's rows. Everything else is 14px.
  The Edit page's picture and the forms were not otherwise touched.
- **P3-9 Paging corrections** (step 7, approved 2026-11-11):
  - **Beyond the last page:** when the API's answer is for a page index beyond the last page,
    `CatalogIndex` navigates (replace) to the last page's URL, which loads it; an empty catalog
    goes to its first page. Rule in `paging.ts` (`lastPageIfBeyond`), unit-tested. The API is
    unchanged (it answers such an index with an empty page), so the case costs a second call.
    Invalid values still get the message of P2-14.
  - **Pager text:** "Showing" prints the number of rows on the page, not the page size
    (**supersedes this part of R-24**). The rest of the text and the URLs (P2-9) are unchanged.

## Steps
1. **Phase document and packages:** this file, `AGENTS.md`, the two packages; nothing imports them yet.
2. **Table becomes the grid:** `CatalogTable.tsx` renders AG Grid (P3-2, P3-3, P3-5); list tests rewritten.
3. **Fill the window:** layout of the list page (P3-4), with browser tests.
4. **Parity and docs:** differences in `docs/parity.md` ("Phase 3"), `AGENTS.md`.
5. **Footer and session removed** (P3-7).
6. **Smaller sizes** (P3-8).
7. **Paging corrections** (P3-9).
8. **Close:** docs for steps 5 to 7, a run against the rebuilt container, close the phase.

## Progress
- [x] Phase document and packages (2026-11-11; packages installed, not imported: the built script is
  byte-identical to phase 2's. `dotnet build`; client build, 81/81 Vitest, lint clean; smoke vs new
  37/0/29 of 66 and browser 28/28 on port 5070)
- [x] Table becomes the grid (2026-11-11; client build, 81/81 Vitest, lint clean; smoke vs new 37/0/29
  of 66 and browser 29/29 on port 5070)
- [x] Fill the window (2026-11-11; `dotnet build`; client build, 82/82 Vitest, lint clean; smoke vs
  new 37/0/29 of 66 and browser 32/32 on port 5070)
- [x] Parity and docs (2026-11-11; `docs/parity.md` "Phase 3")
- [x] Footer and session removed (2026-11-11; `dotnet build`; client build, 82/82 Vitest, lint clean;
  unit 163/163, integration 99/99; smoke vs new 36/0/29 of 65 and browser 32/32 on port 5070)
- [x] Smaller sizes (2026-11-11; `dotnet build`; client build, 82/82 Vitest, lint clean; smoke vs new
  36/0/29 of 65 and browser 33/33 on port 5070)
- [x] Paging corrections (2026-11-11; `dotnet build`; client build, 94/94 Vitest, lint clean; unit
  163/163, integration 99/99; smoke vs new 36/0/29 of 65 and browser 34/34 on port 5070)
- [x] Close (2026-11-11; container rebuilt: smoke vs container 36/0/29 of 65 and browser 34/34 on
  port 5080, both services healthy, `/api/v1/session` 404; again after the text scale of P3-8:
  browser 35/35 on ports 5070 and 5080, smoke vs container 36/0/29)
  - **Tooling note:** the image build first failed twice in `npm ci` with
    `UNABLE_TO_VERIFY_LEAF_SIGNATURE`. Cause: Avast's HTTPS scanning (Web Shield) re-signs the
    traffic to registry.npmjs.org with its own root ("Avast Web/Mail Shield Root"), which Windows
    trusts and the build container does not. The build passed with Avast off. Fix it in Avast, not
    in the Dockerfile; never by switching off npm's certificate check.

Update this checklist as steps complete.
