import { test, expect } from "@playwright/test";

// Browser smoke: home renders, a category list loads products from PostgreSQL, product detail opens,
// demo login works and add-to-cart round-trips through DynamoDB (header cart counter).
test("browse, log in, add to cart", async ({ page }) => {
  const errors = [];
  page.on("pageerror", (e) => errors.push(e.message));

  await page.goto("/");
  await expect(page.locator("header.header")).toBeVisible();

  await page.goto("/list/homeappliances");
  const firstProduct = page.locator('a[href^="/product/detail/"]').first();
  await expect(firstProduct).toBeVisible({ timeout: 20_000 });

  // demo login (any email/password)
  await page.locator(".secondary-nav__login").click();
  await page.locator("#email").fill("e2e@contoso.com");
  await page.locator("#password").fill("demo");
  await page.locator('.modal button[type="submit"]').click();
  await expect(page.locator(".secondary-nav__cart")).toBeVisible();

  await page.goto("/list/homeappliances");
  await page.locator('a[href^="/product/detail/"]').first().click();
  await expect(page.locator(".detail__title")).toBeVisible();
  const cartCount = page.locator(".secondary-nav__cart-number");
  const before = Number((await cartCount.textContent()) || 0);
  await page.locator(".btn--cart").click();
  await expect(cartCount).toHaveText(String(before + 1));

  await page.goto("/shopping-cart");
  await expect(page.locator("body")).toContainText(/\$/);
  expect(errors).toEqual([]);
});
