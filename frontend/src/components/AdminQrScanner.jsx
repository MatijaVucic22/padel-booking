import { useCallback, useEffect, useRef, useState } from "react";
import { BrowserQRCodeReader } from "@zxing/browser";
import {
  useCheckInReservationMutation,
  useLazyVerifyReservationQuery,
} from "../services/padelApi";
import {
  extractBookingVerificationToken,
  applyScannerCheckInSuccess,
  getScannerCheckInUiState,
  getScannerVerificationStatusContent,
} from "../utils/bookingVerification";

const scannerDateFormatter = new Intl.DateTimeFormat("sr-Latn-RS", {
  day: "2-digit",
  month: "long",
  year: "numeric",
  timeZone: "UTC",
});

const checkInTimeFormatter = new Intl.DateTimeFormat("sr-Latn-RS", {
  hour: "2-digit",
  minute: "2-digit",
  timeZone: "Europe/Belgrade",
});

function parseWallClock(value) {
  const match = String(value ?? "").match(
    /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/,
  );
  if (!match) return null;

  return {
    date: new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3]))),
    time: `${match[4]}:${match[5]}`,
  };
}

function getCameraErrorMessage(error) {
  if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) {
    return "Kamera nije dostupna. Otvorite aplikaciju preko bezbedne HTTPS veze.";
  }

  if (error?.name === "NotAllowedError" || error?.name === "SecurityError") {
    return "Dozvolite pristup kameri da biste skenirali QR kod.";
  }

  if (error?.name === "NotFoundError" || error?.name === "OverconstrainedError") {
    return "Kamera nije dostupna na ovom uređaju.";
  }

  return "Kameru trenutno nije moguće pokrenuti. Pokušajte ponovo.";
}

function AdminQrScanner() {
  const [verifyReservation] = useLazyVerifyReservationQuery();
  const [checkInReservation, { isLoading: isCheckingIn }] =
    useCheckInReservationMutation();
  const [phase, setPhase] = useState("idle");
  const [verification, setVerification] = useState(null);
  const [error, setError] = useState("");
  const [checkInError, setCheckInError] = useState("");
  const [checkInOutcome, setCheckInOutcome] = useState(null);
  const videoRef = useRef(null);
  const controlsRef = useRef(null);
  const processingRef = useRef(false);
  const mountedRef = useRef(true);

  const stopCamera = useCallback(() => {
    controlsRef.current?.stop();
    controlsRef.current = null;

    const stream = videoRef.current?.srcObject;
    if (stream?.getTracks) {
      stream.getTracks().forEach((track) => track.stop());
    }
    if (videoRef.current) videoRef.current.srcObject = null;
  }, []);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      processingRef.current = true;
      stopCamera();
    };
  }, [stopCamera]);

  const processScannedValue = useCallback(async (value, scannerControls) => {
    if (processingRef.current) return;
    processingRef.current = true;
    scannerControls?.stop();
    stopCamera();

    const token = extractBookingVerificationToken(value);
    if (!token) {
      if (!mountedRef.current) return;
      setVerification({ verificationStatus: "Invalid" });
      setPhase("result");
      return;
    }

    setPhase("verifying");
    setError("");

    try {
      const result = await verifyReservation(token).unwrap();
      if (!mountedRef.current) return;

      if (result.verificationStatus === "Valid" && result.checkedIn) {
        try {
          const existing = await checkInReservation({
            reservationId: result.reservationNumber,
            verificationToken: token,
          }).unwrap();
          if (!mountedRef.current) return;
          setVerification({
            ...result,
            verificationToken: token,
            checkedInAtUtc: existing.checkedInAtUtc,
          });
          setCheckInOutcome("existing");
        } catch {
          if (!mountedRef.current) return;
          setVerification({ ...result, verificationToken: token });
          setCheckInOutcome("existing");
        }
      } else {
        setVerification({ ...result, verificationToken: token });
      }
      setPhase("result");
    } catch (requestError) {
      if (!mountedRef.current) return;

      if (requestError?.status === 400 || requestError?.status === 404) {
        setVerification({ verificationStatus: "Invalid" });
        setPhase("result");
      } else {
        setError("Provera rezervacije trenutno nije dostupna.");
        setPhase("error");
      }
    }
  }, [checkInReservation, stopCamera, verifyReservation]);

  const startCamera = useCallback(async () => {
    stopCamera();
    processingRef.current = false;
    setVerification(null);
    setError("");
    setCheckInError("");
    setCheckInOutcome(null);

    if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) {
      setError(getCameraErrorMessage());
      setPhase("error");
      return;
    }

    setPhase("requesting");

    try {
      const reader = new BrowserQRCodeReader(undefined, {
        delayBetweenScanAttempts: 250,
      });
      const controls = await reader.decodeFromConstraints(
        {
          audio: false,
          video: { facingMode: { ideal: "environment" } },
        },
        videoRef.current,
        (result, _decodeError, scannerControls) => {
          if (result) void processScannedValue(result.getText(), scannerControls);
        },
      );

      if (!mountedRef.current || processingRef.current) {
        controls.stop();
        return;
      }

      controlsRef.current = controls;
      setPhase("scanning");
    } catch (cameraError) {
      stopCamera();
      if (!mountedRef.current) return;
      setError(getCameraErrorMessage(cameraError));
      setPhase("error");
    }
  }, [processScannedValue, stopCamera]);

  const confirmCheckIn = async () => {
    if (!verification || verification.verificationStatus !== "Valid" ||
        verification.checkedIn || isCheckingIn) return;

    setCheckInError("");
    try {
      const result = await checkInReservation({
        reservationId: verification.reservationNumber,
        verificationToken: verification.verificationToken,
      }).unwrap();
      if (!mountedRef.current) return;
      setVerification((current) => applyScannerCheckInSuccess(current, result));
      setCheckInOutcome("confirmed");
    } catch (requestError) {
      if (!mountedRef.current) return;
      const code = requestError?.data?.code;
      setCheckInError(code === "CHECK_IN_TOO_EARLY"
        ? "Dolazak još nije moguće potvrditi."
        : code === "CHECK_IN_WINDOW_CLOSED"
          ? "Vreme za potvrdu dolaska je isteklo."
          : requestError?.status === 409
            ? "Dolazak nije moguće potvrditi za ovu rezervaciju."
            : "Potvrda dolaska trenutno nije dostupna. Pokušajte ponovo.");
    }
  };

  const isCameraVisible = phase === "requesting" || phase === "scanning";
  const isInvalid = verification?.verificationStatus === "Invalid";
  const status = verification
    ? getScannerVerificationStatusContent(verification.verificationStatus)
    : null;
  const start = parseWallClock(verification?.startTime);
  const end = parseWallClock(verification?.endTime);
  const checkInUi = getScannerCheckInUiState(
    verification?.verificationStatus,
    verification?.checkedIn,
    isCheckingIn,
    verification?.startTime,
    verification?.endTime,
  );
  const checkedInTime = verification?.checkedInAtUtc
    ? checkInTimeFormatter.format(new Date(verification.checkedInAtUtc))
    : null;
  const resultLabel = checkInOutcome === "confirmed"
    ? "DOLAZAK POTVRĐEN"
    : checkInOutcome === "existing"
      ? "DOLAZAK VEĆ POTVRĐEN"
      : status?.label;

  return (
    <section className="admin-section admin-qr-scanner" aria-labelledby="admin-qr-title">
      <header className="admin-qr-heading">
        <span className="admin-eyebrow">Recepcija</span>
        <h2 id="admin-qr-title">QR skener</h2>
        <p>Skenirajte QR kod rezervacije.</p>
      </header>

      <div className={`admin-qr-device ${isCameraVisible ? "is-camera-active" : ""}`}>
        <video
          ref={videoRef}
          className="admin-qr-video"
          muted
          playsInline
          aria-label="Prikaz kamere za skeniranje QR koda"
        />

        {phase === "idle" && (
          <div className="admin-qr-idle">
            <span className="admin-qr-mark" aria-hidden="true">⌗</span>
            <h3>Spremno za skeniranje</h3>
            <p>Kamera se uključuje tek kada pokrenete skener.</p>
            <button type="button" className="primary-button" onClick={startCamera}>
              Pokreni kameru
            </button>
          </div>
        )}

        {phase === "requesting" && (
          <div className="admin-qr-overlay" role="status">
            <span className="reservation-qr-spinner" aria-hidden="true" />
            <p>Pokrećemo kameru...</p>
          </div>
        )}

        {phase === "scanning" && (
          <div className="admin-qr-frame" aria-hidden="true">
            <span />
          </div>
        )}

        {phase === "verifying" && (
          <div className="admin-qr-idle" role="status">
            <span className="reservation-qr-spinner" aria-hidden="true" />
            <h3>Proveravamo rezervaciju...</h3>
          </div>
        )}

        {phase === "error" && (
          <div className="admin-qr-idle" role="alert">
            <span className="admin-qr-mark is-error" aria-hidden="true">!</span>
            <h3>Kamera ili provera nisu dostupne</h3>
            <p>{error}</p>
            <button type="button" className="secondary-button" onClick={startCamera}>
              Pokušaj ponovo
            </button>
          </div>
        )}

        {phase === "result" && status && (
          <article className={`admin-qr-result ${status.className}`} aria-live="polite">
            <span className="admin-qr-result-icon" aria-hidden="true">{status.icon}</span>
            <p className="admin-qr-result-status">{resultLabel}</p>

            {!isInvalid && (
              <>
                <h3>{verification.courtName}</h3>
                <p>{verification.location}</p>
                {start && end && (
                  <div className="admin-qr-result-slot">
                    <strong>{scannerDateFormatter.format(start.date)}</strong>
                    <span>{start.time}–{end.time}</span>
                  </div>
                )}
                <small>Rezervacija #{verification.reservationNumber}</small>
              </>
            )}

            {isInvalid && <p>Ovaj QR kod nije PadelBooking rezervacija.</p>}

            {checkInUi.showAlreadyCheckedIn && checkedInTime && (
              <p className="admin-qr-check-in-time">Potvrđeno u {checkedInTime}</p>
            )}

            {checkInUi.showTooEarly && (
              <div className="admin-qr-check-in-pending" role="status">
                <p>Dolazak još nije moguće potvrditi.</p>
                {checkInUi.checkInAvailableFrom && (
                  <small>Dostupno od {checkInUi.checkInAvailableFrom}.</small>
                )}
              </div>
            )}

            {checkInError && (
              <p className="admin-qr-check-in-error" role="alert">{checkInError}</p>
            )}

            {checkInUi.showConfirmation && (
              <button
                type="button"
                className="primary-button admin-qr-check-in-button"
                disabled={checkInUi.confirmationDisabled}
                onClick={confirmCheckIn}
              >
                {isCheckingIn ? "Potvrđujemo..." : "Potvrdi dolazak"}
              </button>
            )}

            {checkInUi.showScanNext && (
              <button type="button" className="secondary-button" onClick={startCamera}>
                Skeniraj sledeći
              </button>
            )}
          </article>
        )}
      </div>

      {phase === "scanning" && (
        <p className="admin-qr-hint" role="status">Usmerite kameru ka QR kodu.</p>
      )}
    </section>
  );
}

export default AdminQrScanner;
