import { useParams } from "react-router-dom";
import { useVerifyReservationQuery } from "../services/padelApi";

const verificationDateFormatter = new Intl.DateTimeFormat("sr-Latn-RS", {
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

const statusContent = {
  Valid: { icon: "✓", label: "VALIDNA REZERVACIJA", className: "is-valid" },
  Cancelled: { icon: "×", label: "REZERVACIJA JE OTKAZANA", className: "is-invalid" },
  Expired: { icon: "—", label: "REZERVACIJA JE ZAVRŠENA", className: "is-expired" },
};

function VerifyBooking() {
  const { token = "" } = useParams();
  const { data, isLoading, isFetching, isError } = useVerifyReservationQuery(token, {
    skip: !token,
  });

  if (isLoading || isFetching) {
    return (
      <section className="verification-page">
        <div className="verification-card verification-loading" role="status">
          <span className="reservation-qr-spinner" aria-hidden="true" />
          <p>Proveravamo rezervaciju...</p>
        </div>
      </section>
    );
  }

  if (isError || !data || !statusContent[data.verificationStatus]) {
    return (
      <section className="verification-page">
        <article className="verification-card is-invalid">
          <span className="verification-icon" aria-hidden="true">×</span>
          <p className="verification-status">REZERVACIJA NIJE VALIDNA</p>
          <h1>Provera nije uspela</h1>
          <p>QR propusnica nije ispravna ili rezervacija ne postoji.</p>
        </article>
      </section>
    );
  }

  const start = parseWallClock(data.startTime);
  const end = parseWallClock(data.endTime);
  const status = statusContent[data.verificationStatus];

  return (
    <section className="verification-page">
      <article className={`verification-card ${status.className}`}>
        <span className="verification-brand">PADELBOOKING</span>
        <span className="verification-icon" aria-hidden="true">{status.icon}</span>
        <p className="verification-status">{status.label}</p>
        <h1>{data.courtName}</h1>
        {start && end && (
          <div className="verification-slot">
            <strong>{verificationDateFormatter.format(start.date)}</strong>
            <span>{start.time}–{end.time}</span>
          </div>
        )}
        <p>{data.location}</p>
        <small>Rezervacija #{data.reservationNumber}</small>
      </article>
    </section>
  );
}

export default VerifyBooking;
