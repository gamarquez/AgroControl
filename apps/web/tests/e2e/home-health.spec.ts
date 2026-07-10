import { expect, test } from "@playwright/test";

test("shows the API health status on the home screen", async ({ page }) => {
  await page.route("http://127.0.0.1:4010/health", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        status: "Healthy",
        service: "AgroControl API",
        timestamp: "2026-07-07T21:00:00.000Z",
        checks: {
          database: "healthy",
        },
      }),
    });
  });

  await page.goto("/");

  await expect(
    page.getByRole("heading", {
      name: /AgroControl arranca con una base web lista para crecer/i,
    }),
  ).toBeVisible();
  await expect(page.getByText("Disponible")).toBeVisible();
  await expect(page.getByText("AgroControl API")).toBeVisible();
  await expect(page.getByText("database")).toBeVisible();
});
