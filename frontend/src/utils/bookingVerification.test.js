import assert from "node:assert/strict";
import test from "node:test";
import {
  extractBookingVerificationToken,
  applyScannerCheckInSuccess,
  getScannerVerificationStatusContent,
  getScannerCheckInUiState,
  verificationStatusContent,
} from "./bookingVerification.js";

const token = `v1.42.${"a".repeat(43)}`;

test("prihvata PadelBooking putanju i očekivane tipove origin-a", () => {
  assert.equal(extractBookingVerificationToken(`/verify-booking/${token}`), token);
  assert.equal(extractBookingVerificationToken(`http://localhost:5173/verify-booking/${token}`), token);
  assert.equal(extractBookingVerificationToken(`https://random.trycloudflare.com/verify-booking/${token}`), token);
  assert.equal(extractBookingVerificationToken(`https://padel.example/verify-booking/${token}`), token);
});

test("prikazuje potvrdu dolaska samo za validnu nepotvrđenu rezervaciju", () => {
  assert.deepEqual(getScannerCheckInUiState("Valid", false), {
    showConfirmation: true,
    confirmationDisabled: false,
    showAlreadyCheckedIn: false,
    showScanNext: true,
  });
  assert.equal(getScannerCheckInUiState("Valid", true).showAlreadyCheckedIn, true);
  assert.equal(getScannerCheckInUiState("Cancelled", false).showConfirmation, false);
  assert.equal(getScannerCheckInUiState("Expired", false).showConfirmation, false);
  assert.equal(getScannerCheckInUiState("Invalid", false).showConfirmation, false);
});

test("uspešna potvrda ažurira prikaz i zadržava sledeće skeniranje", () => {
  const updated = applyScannerCheckInSuccess(
    { verificationStatus: "Valid", checkedIn: false, reservationNumber: 42 },
    { checkedInAtUtc: "2026-09-28T15:54:00Z" },
  );

  assert.equal(updated.checkedIn, true);
  assert.equal(updated.checkedInAtUtc, "2026-09-28T15:54:00Z");
  assert.equal(
    getScannerCheckInUiState(updated.verificationStatus, updated.checkedIn)
      .showScanNext,
    true,
  );
});

test("potvrda dolaska je onemogućena dok zahtev traje", () => {
  assert.equal(
    getScannerCheckInUiState("Valid", false, true).confirmationDisabled,
    true,
  );
});

test("odbija nepovezane URL-ove i neispravne tokene", () => {
  assert.equal(extractBookingVerificationToken("https://evil.example.com"), null);
  assert.equal(extractBookingVerificationToken("https://padel.example/courts/42"), null);
  assert.equal(extractBookingVerificationToken("/verify-booking/v1.42.short"), null);
  assert.equal(extractBookingVerificationToken("javascript:alert(1)"), null);
});

test("prihvata sirov potpisani token", () => {
  assert.equal(extractBookingVerificationToken(token), token);
});

test("definiše prikaz za sve rezultate verifikacije", () => {
  assert.equal(getScannerVerificationStatusContent("Valid").label, "VALIDNA REZERVACIJA");
  assert.equal(getScannerVerificationStatusContent("Cancelled").label, "REZERVACIJA JE OTKAZANA");
  assert.equal(getScannerVerificationStatusContent("Expired").label, "REZERVACIJA JE ZAVRŠENA");
  assert.equal(getScannerVerificationStatusContent("Invalid").label, "QR KOD NIJE VALIDAN");
  assert.equal(verificationStatusContent.Invalid, undefined);
});
