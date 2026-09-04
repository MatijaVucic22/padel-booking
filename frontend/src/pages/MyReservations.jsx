import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import api from "../api/api";

const dateFormatter = new Intl.DateTimeFormat("sr-RS", {
  day: "2-digit",
  month: "long",
  year: "numeric",
});

const timeFormatter = new Intl.DateTimeFormat("sr-RS", {
  hour: "2-digit",
  minute: "2-digit",
});

const priceFormatter = new Intl.NumberFormat("sr-RS", {
  style: "currency",
  currency: "RSD",
  maximumFractionDigits: 2,
});

const statusLabels = {
  Active: "Aktivna",
  Cancelled: "Otkazana",
  Completed: "Završena",
};

function MyReservations() {
  const [reservations, setReservations] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [reloadKey, setReloadKey] = useState(0);
  const [cancellingId, setCancellingId] = useState(null);
  const [actionMessage, setActionMessage] = useState("");
  const [actionError, setActionError] = useState("");

  useEffect(() => {
    let ignoreResponse = false;

    api
      .get("/reservations/my")
      .then((response) => {
        if (!ignoreResponse) {
          setReservations(response.data);
          setError("");
        }
      })
      .catch((requestError) => {
        if (ignoreResponse) return;

        console.error(requestError);
        setError(
          requestError.response?.status === 401
            ? "Morate biti prijavljeni da biste videli rezervacije."
            : "Rezervacije trenutno nije moguće učitati.",
        );
      })
      .finally(() => {
        if (!ignoreResponse) {
          setLoading(false);
        }
      });

    return () => {
      ignoreResponse = true;
    };
  }, [reloadKey]);

  const retryLoading = () => {
    setLoading(true);
    setError("");
    setReloadKey((currentKey) => currentKey + 1);
  };

  const cancelReservation = async (reservation) => {
    const confirmed = window.confirm(
      `Da li sigurno želiš da otkažeš rezervaciju za teren „${reservation.courtName}”?`,
    );

    if (!confirmed) return;

    setCancellingId(reservation.id);
    setActionMessage("");
    setActionError("");

    try {
      const response = await api.delete(`/reservations/${reservation.id}`);

      setReservations((currentReservations) =>
        currentReservations.map((currentReservation) =>
          currentReservation.id === reservation.id
            ? { ...currentReservation, status: "Cancelled" }
            : currentReservation,
        ),
      );
      setActionMessage(
        response.data?.message ?? "Rezervacija je uspešno otkazana.",
      );
    } catch (requestError) {
      console.error(requestError);
      setActionError(
        typeof requestError.response?.data === "string"
          ? requestError.response.data
          : "Rezervaciju trenutno nije moguće otkazati.",
      );
    } finally {
      setCancellingId(null);
    }
  };

  if (loading) {
    return (
      <section className="page reservations-page" aria-live="polite">
        <h1>Moje rezervacije</h1>
        <p className="reservations-feedback">Učitavanje rezervacija...</p>
      </section>
    );
  }

  if (error) {
    return (
      <section className="page reservations-page">
        <h1>Moje rezervacije</h1>
        <div className="reservations-feedback reservations-error" role="alert">
          <p>{error}</p>
          <button type="button" onClick={retryLoading}>
            Pokušaj ponovo
          </button>
        </div>
      </section>
    );
  }

  return (
    <section className="page reservations-page">
      <header className="reservations-header">
        <div>
          <h1>Moje rezervacije</h1>
          <p>Pregled svih tvojih termina na jednom mestu.</p>
        </div>

        <Link to="/courts" className="primary-button">
          Rezerviši teren
        </Link>
      </header>

      {actionMessage && (
        <p className="reservation-action-message success-message" role="status">
          {actionMessage}
        </p>
      )}

      {actionError && (
        <p className="reservation-action-message error-message" role="alert">
          {actionError}
        </p>
      )}

      {reservations.length === 0 ? (
        <div className="reservations-empty">
          <h2>Još nemaš rezervacije</h2>
          <p>Pronađi teren i izaberi termin koji ti odgovara.</p>
          <Link to="/courts" className="primary-button">
            Pogledaj terene
          </Link>
        </div>
      ) : (
        <div className="reservations-list">
          {reservations.map((reservation) => {
            const startTime = new Date(reservation.startTime);
            const endTime = new Date(reservation.endTime);
            const normalizedStatus = reservation.status.toLowerCase();

            return (
              <article className="reservation-card" key={reservation.id}>
                <div className="reservation-card-heading">
                  <div>
                    <span className="reservation-label">Teren</span>
                    <h2>{reservation.courtName}</h2>
                  </div>

                  <span
                    className={`reservation-status reservation-status-${normalizedStatus}`}
                  >
                    {statusLabels[reservation.status] ?? reservation.status}
                  </span>
                </div>

                <dl className="reservation-details">
                  <div>
                    <dt>Datum</dt>
                    <dd>{dateFormatter.format(startTime)}</dd>
                  </div>
                  <div>
                    <dt>Termin</dt>
                    <dd>
                      {timeFormatter.format(startTime)}–{timeFormatter.format(endTime)}
                    </dd>
                  </div>
                  <div>
                    <dt>Cena</dt>
                    <dd>{priceFormatter.format(reservation.totalPrice)}</dd>
                  </div>
                </dl>

                {reservation.status === "Active" && (
                  <div className="reservation-actions">
                    <button
                      type="button"
                      className="cancel-reservation-button"
                      disabled={cancellingId !== null}
                      onClick={() => cancelReservation(reservation)}
                    >
                      {cancellingId === reservation.id
                        ? "Otkazivanje..."
                        : "Otkaži rezervaciju"}
                    </button>
                  </div>
                )}
              </article>
            );
          })}
        </div>
      )}
    </section>
  );
}

export default MyReservations;
