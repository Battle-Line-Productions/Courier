# E2E Test Report
Run: 2026-03-06T21:56:01.335Z
Duration: 25.4 minutes | Workers: 4

## Summary: 227 total | 158 passed | 32 failed | 37 flaky | 0 skipped

## Per-File Breakdown
| File | Pass | Fail | Flaky | Total | Status |
|------|------|------|-------|-------|--------|
| audit-log.spec.ts | 9 | 0 | 0 | 9 | ✅ PASS |
| auth-guard.spec.ts | 3 | 0 | 0 | 3 | ✅ PASS |
| auth.setup.ts | 1 | 0 | 0 | 1 | ✅ PASS |
| chains.spec.ts | 13 | 2 | 4 | 19 | ❌ FAIL |
| connections.spec.ts | 12 | 5 | 5 | 22 | ❌ FAIL |
| dashboard.spec.ts | 10 | 2 | 1 | 13 | ❌ FAIL |
| job-execution.spec.ts | 13 | 1 | 2 | 16 | ❌ FAIL |
| jobs.spec.ts | 26 | 1 | 1 | 28 | ❌ FAIL |
| keys.spec.ts | 20 | 4 | 8 | 32 | ❌ FAIL |
| login.spec.ts | 6 | 0 | 0 | 6 | ✅ PASS |
| monitors.spec.ts | 11 | 2 | 4 | 17 | ❌ FAIL |
| navigation.spec.ts | 1 | 5 | 0 | 6 | ❌ FAIL |
| notifications.spec.ts | 8 | 1 | 6 | 15 | ❌ FAIL |
| settings.spec.ts | 8 | 0 | 1 | 9 | ⚠️ FLAKY |
| setup.spec.ts | 0 | 4 | 0 | 4 | ❌ FAIL |
| tags.spec.ts | 5 | 4 | 5 | 14 | ❌ FAIL |
| users.spec.ts | 12 | 1 | 0 | 13 | ❌ FAIL |

## Failed Tests (32)
- **[chains.spec.ts]** creates a chain with name and description
  - `Error: expect(locator).toBeVisible() failed

Locator: locator('[data-sonner-toast]').filter({ hasText: 'Chain created' }`
- **[chains.spec.ts]** edits chain name
  - `Test timeout of 30000ms exceeded.`
- **[connections.spec.ts]** navigates to create connection page
  - `Error: expect(page).toHaveURL(expected) failed

Expected pattern: /\/connections\/new/
Received string:  "http://localho`
- **[connections.spec.ts]** creates an SFTP connection with password auth
  - `Error: expect(locator).toBeVisible() failed

Locator: getByRole('heading', { name: 'e2e-sftp-create-mmfew6sf' })
Expecte`
- **[connections.spec.ts]** created connection appears in list
  - `Error: expect(locator).toBeVisible() failed

Locator: getByText('list.example.com:22')
Expected: visible
Error: strict m`
- **[connections.spec.ts]** edits connection name and description
  - `Error: expect(locator).toBeVisible() failed

Locator: locator('[data-sonner-toast]').filter({ hasText: 'Connection updat`
- **[connections.spec.ts]** connection group field works
  - `Error: expect(locator).toBeVisible() failed

Locator: getByText('test-group-mmfeyyum')
Expected: visible
Timeout: 5000ms`
- **[dashboard.spec.ts]** sidebar navigation works
  - `Error: expect(page).toHaveURL(expected) failed

Expected pattern: /\/jobs/
Received string:  "http://localhost:57568/"
T`
- **[dashboard.spec.ts]** card click-through navigates to respective pages
  - `Error: expect(page).toHaveURL(expected) failed

Expected pattern: /\/jobs/
Received string:  "http://localhost:57568/"
T`
- **[job-execution.spec.ts]** execution pagination appears with multiple executions
  - `Test timeout of 30000ms exceeded.`
- **[jobs.spec.ts]** pagination controls appear with many jobs
  - `Error: expect(locator).toBeEnabled() failed

Locator: getByRole('button', { name: 'Next' })
Expected: enabled
Error: str`
- **[keys.spec.ts]** navigates to generate PGP key page
  - `Test timeout of 30000ms exceeded.`
- **[keys.spec.ts]** generates a new PGP key with ECC
  - `Error: expect(page).toHaveURL(expected) failed

Expected pattern: /\/keys\/pgp\/[0-9a-f-]+$/
Received string:  "http://l`
- **[keys.spec.ts]** generates PGP key with real name, email, and expiry
  - `Error: expect(page).toHaveURL(expected) failed

Expected pattern: /\/keys\/pgp\/[0-9a-f-]+$/
Received string:  "http://l`
- **[keys.spec.ts]** navigates to generate SSH key page
  - `Error: expect(page).toHaveURL(expected) failed

Expected pattern: /\/keys\/ssh\/new/
Received string:  "http://localhost`
- **[monitors.spec.ts]** navigates to create monitor page
  - `Error: expect(page).toHaveURL(expected) failed

Expected pattern: /\/monitors\/new/
Received string:  "http://localhost:`
- **[monitors.spec.ts]** creates a monitor with local watch target
  - `Test timeout of 30000ms exceeded.`
- **[navigation.spec.ts]** sidebar collapse and expand
  - `Error: expect(received).toBeGreaterThan(expected)

Expected: > 150
Received:   66.765625`
- **[navigation.spec.ts]** breadcrumb navigation on nested pages
  - `Test timeout of 30000ms exceeded.`
- **[navigation.spec.ts]** profile link navigates to users page
  - `Error: expect(page).toHaveURL(expected) failed

Expected pattern: /\/settings\/users/
Received string:  "http://localhos`
- **[navigation.spec.ts]** change password link navigates to settings page
  - `Error: expect(page).toHaveURL(expected) failed

Expected pattern: /\/settings$/
Received string:  "http://localhost:5756`
- **[navigation.spec.ts]** sign out redirects to login page
  - `Test timeout of 30000ms exceeded.`
- **[notifications.spec.ts]** creates an email notification rule
  - `Error: expect(page).toHaveURL(expected) failed

Expected pattern: /\/notifications\/[a-f0-9-]+$/
Received string:  "http`
- **[tags.spec.ts]** navigates to create tag page via button
  - `Error: expect(page).toHaveURL(expected) failed

Expected pattern: /\/tags\/new/
Received string:  "http://localhost:5756`
- **[tags.spec.ts]** creates a new tag with name, color, and category
  - `Error: expect(page).toHaveURL(expected) failed

Expected: "http://localhost:57568/tags"
Received: "http://localhost:5756`
- **[tags.spec.ts]** navigates to tag detail page
  - `Error: expect(page).toHaveURL(expected) failed

Expected pattern: /\/tags\/019cc521-fab4-799a-9eb2-4f305eccbcc6/
Receive`
- **[tags.spec.ts]** pagination controls appear with many tags
  - `Test timeout of 30000ms exceeded.`
- **[users.spec.ts]** navigates to create user page
  - `Error: expect(page).toHaveURL(expected) failed

Expected pattern: /\/settings\/users\/new/
Received string:  "http://loc`
- **[setup.spec.ts]** redirects to setup when not initialized
  - `Error: expect(page).toHaveURL(expected) failed

Expected pattern: /\/setup/
Received string:  "http://localhost:57568/lo`
- **[setup.spec.ts]** displays setup form
  - `Error: expect(locator).toBeVisible() failed

Locator: getByLabel('Display Name')
Expected: visible
Timeout: 5000ms
Error`
- **[setup.spec.ts]** validates password match
  - `Test timeout of 30000ms exceeded.`
- **[setup.spec.ts]** successful setup creates admin and redirects
  - `Test timeout of 30000ms exceeded.`

## Flaky Tests (37) — passed on retry
- **[chains.spec.ts]** navigates to create chain page (2 attempts)
- **[chains.spec.ts]** chain detail page shows chain info (2 attempts)
- **[chains.spec.ts]** chain detail shows empty members message (2 attempts)
- **[chains.spec.ts]** search/filter chains on list page (2 attempts)
- **[connections.spec.ts]** displays empty state when no connections exist (2 attempts)
- **[connections.spec.ts]** connection detail page shows connection info (2 attempts)
- **[connections.spec.ts]** filters connections by protocol (2 attempts)
- **[connections.spec.ts]** creates an FTP connection (2 attempts)
- **[connections.spec.ts]** SFTP form shows host key policy setting (2 attempts)
- **[dashboard.spec.ts]** execution click-through navigates to job detail (2 attempts)
- **[job-execution.spec.ts]** trigger button opens confirmation dialog (2 attempts)
- **[job-execution.spec.ts]** triggering a job creates an execution (2 attempts)
- **[jobs.spec.ts]** edits job name and description (2 attempts)
- **[keys.spec.ts]** displays empty state for PGP keys (2 attempts)
- **[keys.spec.ts]** generated PGP key appears in list (2 attempts)
- **[keys.spec.ts]** PGP key detail page shows key info (2 attempts)
- **[keys.spec.ts]** retires a PGP key (2 attempts)
- **[keys.spec.ts]** navigates to import PGP key page (2 attempts)
- **[keys.spec.ts]** generates a new SSH key with Ed25519 (2 attempts)
- **[keys.spec.ts]** SSH key detail page shows fingerprint (2 attempts)
- **[keys.spec.ts]** navigates to import SSH key page (2 attempts)
- **[monitors.spec.ts]** displays empty state when no monitors exist (2 attempts)
- **[monitors.spec.ts]** monitor detail page shows monitor info (2 attempts)
- **[monitors.spec.ts]** deletes a monitor (2 attempts)
- **[monitors.spec.ts]** polling config on create form (2 attempts)
- **[notifications.spec.ts]** displays empty state when no rules exist (2 attempts)
- **[notifications.spec.ts]** navigates to create rule page (2 attempts)
- **[notifications.spec.ts]** rule appears in list (2 attempts)
- **[notifications.spec.ts]** edits a notification rule (2 attempts)
- **[notifications.spec.ts]** deletes a notification rule (2 attempts)
- **[notifications.spec.ts]** toggles rule enabled state via edit page (2 attempts)
- **[settings.spec.ts]** all auth fields — modify refresh token days and restore (2 attempts)
- **[tags.spec.ts]** displays empty state when no tags exist (2 attempts)
- **[tags.spec.ts]** edits an existing tag (2 attempts)
- **[tags.spec.ts]** tag picker on job detail assigns and shows tag (2 attempts)
- **[tags.spec.ts]** tagged entities section shows assigned job (2 attempts)
- **[tags.spec.ts]** tag badge renders with correct color (2 attempts)

## Clean Pass Files (no failures or flakiness)
- ✅ audit-log.spec.ts (9/9)
- ✅ auth-guard.spec.ts (3/3)
- ✅ auth.setup.ts (1/1)
- ✅ login.spec.ts (6/6)