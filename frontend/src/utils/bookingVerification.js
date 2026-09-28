const signedTokenPattern = /^v1\.[1-9]\d*\.[A-Za-z0-9_-]{43}$/;

export function extractBookingVerificationToken(scannedValue) {
  const value = String(scannedValue ?? "").trim();
  if (!value) return null;

  if (signedTokenPattern.test(value)) return value;

  let url;
  try {
    url = new URL(value, "https://padelbooking.invalid");
  } catch {
    return null;
  }

  const pathMatch = url.pathname.match(/^\/verify-booking\/([^/]+)\/?$/);
  if (!pathMatch) return null;

  let token;
  try {
    token = decodeURIComponent(pathMatch[1]);
  } catch {
    return null;
  }

  return signedTokenPattern.test(token) ? token : null;
}

export const verificationStatusContent = {
  Valid: { icon: "✓", label: "VALIDNA REZERVACIJA", className: "is-valid" },
  Cancelled: { icon: "×", label: "REZERVACIJA JE OTKAZANA", className: "is-invalid" },
  Expired: { icon: "—", label: "REZERVACIJA JE ZAVRŠENA", className: "is-expired" },
};

export function getScannerVerificationStatusContent(status) {
  return verificationStatusContent[status] ?? {
    icon: "×",
    label: "QR KOD NIJE VALIDAN",
    className: "is-invalid",
  };
}

export function getScannerCheckInUiState(
  verificationStatus,
  checkedIn,
  isSubmitting = false,
) {
  const isValid = verificationStatus === "Valid";
  return {
    showConfirmation: isValid && !checkedIn,
    confirmationDisabled: isValid && !checkedIn && isSubmitting,
    showAlreadyCheckedIn: isValid && checkedIn,
    showScanNext: Boolean(verificationStatus),
  };
}

export function applyScannerCheckInSuccess(verification, result) {
  return {
    ...verification,
    checkedIn: true,
    checkedInAtUtc: result.checkedInAtUtc,
  };
}
