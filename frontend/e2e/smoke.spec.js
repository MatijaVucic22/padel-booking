import { randomUUID } from "node:crypto";
import { expect, test } from "@playwright/test";

async function registerAndLogin(page) {
  const email = `padel-e2e-${randomUUID()}@example.test`;
  const password = `E2eStrong${randomUUID().slice(0, 8)}1`;

  await page.goto("/register");
  await page.getByLabel("Ime", { exact: true }).fill("E2e");
  await page.getByLabel("Prezime", { exact: true }).fill("Igrac");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Lozinka").fill(password);
  await page.getByRole("button", { name: "Registruj se" }).click();
  await expect(page).toHaveURL(/\/login(?:[?#]|$)/);

  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Lozinka").fill(password);
  await page.getByRole("button", { name: "Prijavi se", exact: true }).click();
  await expect(page).toHaveURL(/\/$/);
}

test("Unauthenticated_User_Is_Redirected_From_Protected_Page", async ({ page }) => {
  await page.goto("/my-reservations");
  await expect(page).toHaveURL(/\/login(?:[?#]|$)/);
  await expect(page.getByRole("heading", { name: "Prijavi se za sledeći meč." })).toBeVisible();
});

test.describe("isolated backend flows", () => {
  test("User_Can_Register_Login_And_See_Authenticated_UI", async ({ page }) => {
    await registerAndLogin(page);
    await page.goto("/my-reservations");
    await expect(page.getByRole("heading", { name: "Moje rezervacije" })).toBeVisible();
    await expect(page.getByRole("tab", { name: /Predstojeće/ })).toBeVisible();
  });

  test("Authenticated_User_Can_Open_Courts_And_Booking_UI", async ({ page }) => {
    await registerAndLogin(page);
    await page.goto("/courts");
    await page.getByRole("link", { name: /Otvori detalje terena/ }).first().click();
    await expect(page).toHaveURL(/\/courts\/\d+$/);
    await page.getByRole("link", { name: "Rezerviši termin" }).click();
    await expect(page).toHaveURL(/\/book$/);
    await expect(page.getByRole("heading", { name: "Pronađi slobodan teren" })).toBeVisible();
    await expect(page.getByRole("button", { name: "1 sat" })).toBeVisible();
  });
});
