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
  startTime,
  endTime,
  now = getBelgradeWallClockNow(),
) {
  const isValid = verificationStatus === "Valid";
  const windowStatus = getCheckInWindowStatus(startTime, endTime, now);
  return {
    showConfirmation: isValid && !checkedIn && windowStatus === "Open",
    confirmationDisabled:
      isValid && !checkedIn && windowStatus === "Open" && isSubmitting,
    showAlreadyCheckedIn: isValid && checkedIn,
    showTooEarly: isValid && !checkedIn && windowStatus === "TooEarly",
    checkInAvailableFrom: windowStatus === "TooEarly"
      ? formatCheckInAvailableFrom(startTime)
      : null,
    showScanNext: Boolean(verificationStatus),
  };
}

export function getCheckInWindowStatus(startTime, endTime, now) {
  const start = parseWallClockMilliseconds(startTime);
  const end = parseWallClockMilliseconds(endTime);
  const current = parseWallClockMilliseconds(now);
  if (start === null || end === null || current === null) return "Closed";

  if (current < start - 60 * 60 * 1000) return "TooEarly";
  return current < end ? "Open" : "Closed";
}

function getBelgradeWallClockNow() {
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone: "Europe/Belgrade",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hourCycle: "h23",
  }).formatToParts(new Date());
  const values = Object.fromEntries(parts.map(({ type, value }) => [type, value]));
  return `${values.year}-${values.month}-${values.day}T${values.hour}:${values.minute}:${values.second}`;
}

function parseWallClockMilliseconds(value) {
  const match = String(value ?? "").match(
    /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?/,
  );
  if (!match) return null;

  return Date.UTC(
    Number(match[1]),
    Number(match[2]) - 1,
    Number(match[3]),
    Number(match[4]),
    Number(match[5]),
    Number(match[6] ?? 0),
  );
}

function formatCheckInAvailableFrom(startTime) {
  const start = parseWallClockMilliseconds(startTime);
  if (start === null) return null;
  const available = new Date(start - 60 * 60 * 1000);
  return `${String(available.getUTCHours()).padStart(2, "0")}:${String(available.getUTCMinutes()).padStart(2, "0")}`;
}

export function applyScannerCheckInSuccess(verification, result) {
  return {
    ...verification,
    checkedIn: true,
    checkedInAtUtc: result.checkedInAtUtc,
  };
}
