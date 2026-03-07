import { test, expect } from "./fixtures";

test.describe("Navigation & Layout", () => {
  test("sidebar collapse and expand", async ({ authenticatedPage }) => {
    const page = authenticatedPage;
    const sidebar = page.locator("aside");

    // Sidebar starts expanded (w-56 = 224px)
    await expect(sidebar).toBeVisible({ timeout: 10_000 });

    // Verify a nav label is visible when expanded
    await expect(
      sidebar.getByRole("link", { name: "Jobs" }).locator("span")
    ).toBeVisible({ timeout: 10_000 });

    // The collapse button is the only button with data-slot="button" inside the sidebar
    const collapseButton = sidebar.locator('[data-slot="button"]');
    await expect(collapseButton).toBeVisible({ timeout: 5_000 });

    // Verify it contains "Collapse" text when expanded
    await expect(collapseButton.getByText("Collapse")).toBeVisible();

    // Click the collapse button
    await collapseButton.click();

    // After collapsing, nav labels should be hidden
    // The sidebar links still exist but their text spans are not rendered
    await expect(
      sidebar.getByText("Collapse")
    ).not.toBeVisible({ timeout: 5_000 });

    // Wait for CSS transition to complete (sidebar uses transition-all)
    await page.waitForTimeout(500);

    // The sidebar should now be narrow (w-16 = 64px)
    const collapsedBox = await sidebar.boundingBox();
    expect(collapsedBox).toBeTruthy();
    expect(collapsedBox!.width).toBeLessThan(100);

    // Click the expand button (same button, now shows just a chevron icon)
    await sidebar.locator('[data-slot="button"]').click();

    // After expanding, labels should be visible again
    await expect(
      sidebar.getByText("Collapse")
    ).toBeVisible({ timeout: 5_000 });

    // Wait for CSS transition to complete (sidebar uses transition-all duration-200)
    await page.waitForTimeout(500);

    const expandedBox = await sidebar.boundingBox();
    expect(expandedBox).toBeTruthy();
    expect(expandedBox!.width).toBeGreaterThan(150);
  });

  test("user dropdown menu shows options", async ({ authenticatedPage }) => {
    const page = authenticatedPage;
    const header = page.locator("header");

    // Find and click the user menu trigger button in the header
    // It contains the user display name
    const userMenuButton = header.getByRole("button");
    await expect(userMenuButton).toBeVisible({ timeout: 10_000 });
    await userMenuButton.click();

    // Verify dropdown menu items appear
    await expect(
      page.getByRole("menuitem", { name: "Profile" })
    ).toBeVisible({ timeout: 5_000 });
    await expect(
      page.getByRole("menuitem", { name: "Change Password" })
    ).toBeVisible();
    await expect(
      page.getByRole("menuitem", { name: "Sign Out" })
    ).toBeVisible();
  });

  test("breadcrumb navigation on nested pages", async ({
    authenticatedPage,
  }) => {
    const page = authenticatedPage;

    // Navigate to a nested page (Jobs > Create)
    await page.goto("/jobs/new");
    await page.waitForURL(/\/jobs\/new/, { timeout: 10_000 });
    await page.waitForLoadState("networkidle");

    const header = page.locator("header");

    // Verify breadcrumbs render: "Jobs" as a link and "Create" as text
    await expect(
      header.getByRole("link", { name: "Jobs" })
    ).toBeVisible({ timeout: 10_000 });
    await expect(
      header.getByText("Create", { exact: true })
    ).toBeVisible();

    // Click the "Jobs" breadcrumb link to navigate back
    await header.getByRole("link", { name: "Jobs" }).click();
    await expect(page).toHaveURL(/\/jobs/, { timeout: 10_000 });
  });

  test("profile link navigates to users page", async ({
    authenticatedPage,
  }) => {
    const page = authenticatedPage;
    const header = page.locator("header");

    // Open user dropdown
    const userMenuButton = header.getByRole("button");
    await expect(userMenuButton).toBeVisible({ timeout: 10_000 });
    await userMenuButton.click();

    // Click Profile
    const profileItem = page.getByRole("menuitem", { name: "Profile" });
    await expect(profileItem).toBeVisible({ timeout: 5_000 });
    await profileItem.click();

    // Verify navigation to /settings/users
    await expect(page).toHaveURL(/\/settings\/users/, { timeout: 10_000 });
  });

  test("change password link navigates to settings page", async ({
    authenticatedPage,
  }) => {
    const page = authenticatedPage;
    const header = page.locator("header");

    // Open user dropdown
    const userMenuButton = header.getByRole("button");
    await expect(userMenuButton).toBeVisible({ timeout: 10_000 });
    await userMenuButton.click();

    // Click Change Password
    const changePasswordItem = page.getByRole("menuitem", { name: "Change Password" });
    await expect(changePasswordItem).toBeVisible({ timeout: 5_000 });
    await changePasswordItem.click();

    // Verify navigation to /settings
    await expect(page).toHaveURL(/\/settings$/, { timeout: 10_000 });
  });

  test("sign out redirects to login page", async ({ authenticatedPage }) => {
    const page = authenticatedPage;
    const header = page.locator("header");

    // Open user dropdown
    const userMenuButton = header.getByRole("button");
    await expect(userMenuButton).toBeVisible({ timeout: 10_000 });
    await userMenuButton.click();

    // Click Sign Out
    const signOutItem = page.getByRole("menuitem", { name: "Sign Out" });
    await expect(signOutItem).toBeVisible({ timeout: 5_000 });
    await signOutItem.click();

    // Verify redirect to login page
    await expect(page).toHaveURL(/\/login/, { timeout: 10_000 });
  });
});
