import assert from "node:assert/strict";
import test from "node:test";
import { getRoleUx } from "./roleUx.js";

test("normalan korisnik zadržava customer booking UX", () => {
  const ux = getRoleUx({ role: "User" });
  assert.equal(ux.showBookingCta, true);
  assert.equal(ux.showMyReservations, true);
  assert.equal(ux.showAdminPanel, false);
  assert.equal(ux.canUseCustomerBooking, true);
});

test("Admin dobija operativni UX bez customer booking akcija", () => {
  const ux = getRoleUx({ role: "Admin" });
  assert.equal(ux.showBookingCta, false);
  assert.equal(ux.showMyReservations, false);
  assert.equal(ux.showAdminPanel, true);
  assert.equal(ux.canUseCustomerBooking, false);
});

test("gost i dalje može da otvori booking pregled", () => {
  const ux = getRoleUx(null);
  assert.equal(ux.showBookingCta, true);
  assert.equal(ux.canUseCustomerBooking, true);
});

