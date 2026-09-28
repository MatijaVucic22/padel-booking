import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { QRCodeSVG } from "qrcode.react";
import { useLazyGetReservationVerificationTokenQuery } from "../services/padelApi";

const passDateFormatter = new Intl.DateTimeFormat("sr-Latn-RS", {
  day: "2-digit",
  month: "short",
  year: "numeric",
  timeZone: "UTC",
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

function ReservationQrPass({ reservation }) {
  const [getVerificationToken] = useLazyGetReservationVerificationTokenQuery();
  const [isOpen, setIsOpen] = useState(false);
  const [qrUrl, setQrUrl] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const triggerRef = useRef(null);
  const closeRef = useRef(null);
  const start = parseWallClock(reservation.startTime);
  const end = parseWallClock(reservation.endTime);

  const close = () => {
    setIsOpen(false);
    triggerRef.current?.focus();
  };

  useEffect(() => {
    if (!isOpen) return undefined;

    closeRef.current?.focus();
    const handleKeyDown = (event) => {
      if (event.key === "Escape") close();
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen]);

  if (!start || !end) return null;

  const loadPass = async () => {
    setIsOpen(true);
    setLoading(true);
    setError("");
    setQrUrl("");

    try {
      const result = await getVerificationToken(reservation.id).unwrap();
      setQrUrl(
        `${window.location.origin}/verify-booking/${encodeURIComponent(result.token)}`,
      );
    } catch {
      setError("QR propusnicu trenutno nije moguće učitati. Pokušaj ponovo.");
    } finally {
      setLoading(false);
    }
  };

  const location = reservation.courtLocation ?? reservation.location ?? "";

  return (
    <>
      <button
        ref={triggerRef}
        type="button"
        className="reservation-qr-trigger"
        aria-label="Prikaži QR propusnicu"
        title="Prikaži QR propusnicu"
        onClick={loadPass}
      >
        <svg aria-hidden="true" viewBox="0 0 24 24" fill="none">
          <path d="M4 4h6v6H4zM14 4h6v6h-6zM4 14h6v6H4z" />
          <path d="M14 14h2v2h-2zM18 14h2v4h-2zM14 18h4v2h-4zM20 20h.01" />
        </svg>
      </button>

      {isOpen && createPortal(
        <div
          className="reservation-qr-backdrop"
          role="presentation"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) close();
          }}
        >
          <section
            className="reservation-qr-pass"
            role="dialog"
            aria-modal="true"
            aria-labelledby="reservation-qr-title"
          >
            <header className="reservation-qr-heading">
              <div>
                <span>PADELBOOKING</span>
                <h2 id="reservation-qr-title">QR propusnica</h2>
              </div>
              <button
                ref={closeRef}
                type="button"
                aria-label="Zatvori QR propusnicu"
                onClick={close}
              >
                ×
              </button>
            </header>

            {loading && (
              <div className="reservation-qr-state" role="status">
                <span className="reservation-qr-spinner" aria-hidden="true" />
                <p>Pripremamo QR propusnicu...</p>
              </div>
            )}

            {!loading && error && (
              <div className="reservation-qr-state" role="alert">
                <p>{error}</p>
                <button type="button" onClick={loadPass}>Pokušaj ponovo</button>
              </div>
            )}

            {!loading && qrUrl && (
              <div className="reservation-qr-content">
                <div className="reservation-qr-code">
                  <QRCodeSVG
                    value={qrUrl}
                    size={224}
                    level="M"
                    bgColor="#fffdf7"
                    fgColor="#064e3b"
                    marginSize={2}
                    title={`QR propusnica za rezervaciju ${reservation.id}`}
                  />
                </div>
                <div className="reservation-qr-details">
                  <span>TEREN</span>
                  <h3>{reservation.courtName}</h3>
                  <strong>{passDateFormatter.format(start.date).replaceAll(".", "").toUpperCase()}</strong>
                  <p>{start.time}–{end.time}</p>
                  {location && <p>{location}</p>}
                  <small>Rezervacija #{reservation.id}</small>
                </div>
                <p className="reservation-qr-note">Prikažite QR kod pri dolasku.</p>
              </div>
            )}
          </section>
        </div>,
        document.body,
      )}
    </>
  );
}

export default ReservationQrPass;
