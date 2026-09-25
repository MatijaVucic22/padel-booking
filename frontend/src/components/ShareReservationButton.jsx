import { useEffect, useRef, useState } from "react";

const shareDateFormatter = new Intl.DateTimeFormat("sr-Latn-RS", {
  day: "2-digit",
  month: "long",
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

function ShareReservationButton({ reservation }) {
  const [feedback, setFeedback] = useState(null);
  const feedbackTimerRef = useRef(null);
  const start = parseWallClock(reservation.startTime);
  const end = parseWallClock(reservation.endTime);

  useEffect(
    () => () => window.clearTimeout(feedbackTimerRef.current),
    [],
  );

  if (!start || !end) return null;

  const location =
    reservation.courtLocation ??
    reservation.location ??
    reservation.court?.location ??
    "";
  const title = `PadelBooking - ${reservation.courtName}`;
  const text = [
    "PadelBooking",
    reservation.courtName,
    `${shareDateFormatter.format(start.date)} · ${start.time}–${end.time}`,
    location || null,
    reservation.id ? `Rezervacija #${reservation.id}` : null,
  ]
    .filter(Boolean)
    .join("\n");

  const showFeedback = (message, type) => {
    window.clearTimeout(feedbackTimerRef.current);
    setFeedback({ message, type });
    feedbackTimerRef.current = window.setTimeout(() => setFeedback(null), 2800);
  };

  const copyDetails = async () => {
    await navigator.clipboard.writeText(text);
    showFeedback("Detalji rezervacije su kopirani.", "success");
  };

  const handleShare = async () => {
    if (typeof navigator.share === "function") {
      try {
        await navigator.share({ title, text });
        return;
      } catch (error) {
        if (error?.name === "AbortError") return;
      }
    }

    try {
      await copyDetails();
    } catch {
      showFeedback("Deljenje trenutno nije dostupno.", "error");
    }
  };

  return (
    <div className="reservation-share-action">
      <button
        type="button"
        className="reservation-share-trigger"
        aria-label="Podeli rezervaciju"
        title="Podeli rezervaciju"
        onClick={handleShare}
      >
        <svg aria-hidden="true" viewBox="0 0 24 24" fill="none">
          <circle cx="18" cy="5" r="2.5" />
          <circle cx="6" cy="12" r="2.5" />
          <circle cx="18" cy="19" r="2.5" />
          <path d="m8.2 10.8 7.6-4.5M8.2 13.2l7.6 4.5" />
        </svg>
      </button>

      <span
        className={`reservation-share-feedback${feedback ? ` ${feedback.type}` : ""}`}
        aria-live="polite"
      >
        {feedback?.message ?? ""}
      </span>
    </div>
  );
}

export default ShareReservationButton;
