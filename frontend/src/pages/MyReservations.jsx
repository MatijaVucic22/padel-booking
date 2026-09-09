import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import api from "../api/api";
import {
  hasValidationErrors,
  parseValidationErrors,
} from "../utils/validationErrors";
import {
  longDateFormatter as dateFormatter,
  shortMonthFormatter as monthFormatter,
  timeFormatter,
} from "../utils/dateFormatters";

const priceFormatter = new Intl.NumberFormat("sr-Latn-RS", {
  style: "currency",
  currency: "RSD",
  maximumFractionDigits: 2,
});

const statusLabels = {
  Active: "Aktivna",
  Cancelled: "Otkazana",
  Completed: "Završena",
};

const reservationTabs = [
  {
    id: "upcoming",
    label: "Predstojeće",
    emptyTitle: "Nema predstojećih rezervacija",
    emptyText: "Pronađi teren i rezerviši sledeći termin.",
  },
  {
    id: "completed",
    label: "Završene",
    emptyTitle: "Nema završenih rezervacija",
    emptyText: "Ovde će se prikazati istorija odigranih termina.",
  },
  {
    id: "cancelled",
    label: "Otkazane",
    emptyTitle: "Nema otkazanih rezervacija",
    emptyText: "Ovde će se prikazati istorija otkazanih termina.",
  },
];

const belgradeClockFormatter = new Intl.DateTimeFormat("en-CA", {
  timeZone: "Europe/Belgrade",
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
  hour: "2-digit",
  minute: "2-digit",
  second: "2-digit",
  hourCycle: "h23",
});

function toWallClockValue(value) {
  const match = String(value).match(
    /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?/,
  );

  if (!match) return Number.NaN;

  return Date.UTC(
    Number(match[1]),
    Number(match[2]) - 1,
    Number(match[3]),
    Number(match[4]),
    Number(match[5]),
    Number(match[6] ?? 0),
  );
}

function getBelgradeWallClockValue() {
  const parts = Object.fromEntries(
    belgradeClockFormatter
      .formatToParts(new Date())
      .filter((part) => part.type !== "literal")
      .map((part) => [part.type, Number(part.value)]),
  );

  return Date.UTC(
    parts.year,
    parts.month - 1,
    parts.day,
    parts.hour,
    parts.minute,
    parts.second,
  );
}

function getLocalDate() {
  const today = new Date();
  const offset = today.getTimezoneOffset() * 60_000;

  return new Date(today.getTime() - offset).toISOString().split("T")[0];
}

function MyReservations() {
  const [reservations, setReservations] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [reloadKey, setReloadKey] = useState(0);
  const [cancellingId, setCancellingId] = useState(null);
  const [actionMessage, setActionMessage] = useState("");
  const [actionError, setActionError] = useState("");
  const [rescheduling, setRescheduling] = useState(null);
  const [rescheduleDate, setRescheduleDate] = useState("");
  const [rescheduleSlots, setRescheduleSlots] = useState([]);
  const [selectedRescheduleSlot, setSelectedRescheduleSlot] = useState(null);
  const [rescheduleLoading, setRescheduleLoading] = useState(false);
  const [rescheduleSaving, setRescheduleSaving] = useState(false);
  const [rescheduleError, setRescheduleError] = useState("");
  const [rescheduleFieldErrors, setRescheduleFieldErrors] = useState({});
  const [activeTab, setActiveTab] = useState("upcoming");
  const [belgradeNow, setBelgradeNow] = useState(getBelgradeWallClockValue);

  useEffect(() => {
    const timer = window.setInterval(() => {
      setBelgradeNow(getBelgradeWallClockValue());
    }, 60_000);

    return () => window.clearInterval(timer);
  }, []);

  const reservationsByTab = useMemo(() => {
    const categorized = {
      upcoming: [],
      completed: [],
      cancelled: [],
    };

    reservations.forEach((reservation) => {
      if (reservation.status === "Cancelled") {
        categorized.cancelled.push(reservation);
      } else if (toWallClockValue(reservation.endTime) > belgradeNow) {
        categorized.upcoming.push(reservation);
      } else {
        categorized.completed.push(reservation);
      }
    });

    categorized.upcoming.sort(
      (first, second) =>
        toWallClockValue(first.startTime) - toWallClockValue(second.startTime),
    );
    categorized.completed.sort(
      (first, second) =>
        toWallClockValue(second.endTime) - toWallClockValue(first.endTime),
    );
    categorized.cancelled.sort(
      (first, second) =>
        toWallClockValue(second.startTime) - toWallClockValue(first.startTime),
    );

    return categorized;
  }, [reservations, belgradeNow]);

  const visibleReservations = reservationsByTab[activeTab];
  const activeTabDetails = reservationTabs.find((tab) => tab.id === activeTab);

  useEffect(() => {
    if (!rescheduling || !rescheduleDate) return undefined;

    let ignoreResponse = false;
    setRescheduleLoading(true);
    setRescheduleError("");

    api
      .get("/reservations/available", {
        params: {
          courtId: rescheduling.courtId,
          date: rescheduleDate,
        },
      })
      .then((response) => {
        if (!ignoreResponse) setRescheduleSlots(response.data.slots);
      })
      .catch((requestError) => {
        if (ignoreResponse) return;

        console.error(requestError);
        setRescheduleSlots([]);
        setRescheduleError("Slobodne termine trenutno nije moguće učitati.");
      })
      .finally(() => {
        if (!ignoreResponse) setRescheduleLoading(false);
      });

    return () => {
      ignoreResponse = true;
    };
  }, [rescheduling, rescheduleDate]);

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
            ? {
                ...currentReservation,
                status: "Cancelled",
                canCancel: false,
              }
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

  const openReschedule = (reservation) => {
    setRescheduling(reservation);
    setRescheduleDate(reservation.startTime.split("T")[0]);
    setRescheduleSlots([]);
    setSelectedRescheduleSlot(null);
    setRescheduleError("");
    setRescheduleFieldErrors({});
    setActionMessage("");
    setActionError("");
  };

  const closeReschedule = () => {
    if (rescheduleSaving) return;

    setRescheduling(null);
    setRescheduleDate("");
    setRescheduleSlots([]);
    setSelectedRescheduleSlot(null);
    setRescheduleError("");
    setRescheduleFieldErrors({});
  };

  const changeRescheduleDate = (event) => {
    setRescheduleDate(event.target.value);
    setRescheduleSlots([]);
    setSelectedRescheduleSlot(null);
    setRescheduleError("");
    setRescheduleFieldErrors({});
  };

  const submitReschedule = async () => {
    if (!rescheduling || !selectedRescheduleSlot || rescheduleSaving) return;

    setRescheduleSaving(true);
    setRescheduleError("");
    setRescheduleFieldErrors({});

    try {
      const response = await api.put(
        `/reservations/${rescheduling.id}/reschedule`,
        {
          startTime: selectedRescheduleSlot.startTime,
          endTime: selectedRescheduleSlot.endTime,
        },
      );

      setActionMessage(
        response.data?.message ?? "Termin rezervacije je uspešno promenjen.",
      );
      setRescheduling(null);
      setRescheduleDate("");
      setRescheduleSlots([]);
      setSelectedRescheduleSlot(null);
      setReloadKey((currentKey) => currentKey + 1);
    } catch (requestError) {
      console.error(requestError);
      const validationErrors = parseValidationErrors(requestError);

      if (hasValidationErrors(validationErrors)) {
        setRescheduleFieldErrors(validationErrors);
        return;
      }

      if (requestError.response?.status === 409) {
        setSelectedRescheduleSlot(null);
      }

      setRescheduleError(
        typeof requestError.response?.data === "string"
          ? requestError.response.data
          : requestError.response?.data?.message ??
            "Termin rezervacije trenutno nije moguće promeniti.",
      );
    } finally {
      setRescheduleSaving(false);
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

      <div className="reservation-tabs" role="tablist" aria-label="Vrste rezervacija">
        {reservationTabs.map((tab) => (
          <button
            type="button"
            role="tab"
            className={activeTab === tab.id ? "active" : ""}
            aria-selected={activeTab === tab.id}
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
          >
            {tab.label} <span>({reservationsByTab[tab.id].length})</span>
          </button>
        ))}
      </div>

      {visibleReservations.length === 0 ? (
        <div className="reservations-empty">
          <h2>{activeTabDetails.emptyTitle}</h2>
          <p>{activeTabDetails.emptyText}</p>
          {activeTab === "upcoming" && (
            <Link to="/courts" className="primary-button">
              Pogledaj terene
            </Link>
          )}
        </div>
      ) : (
        <div className="reservations-list">
          {visibleReservations.map((reservation) => {
            const startTime = new Date(reservation.startTime);
            const endTime = new Date(reservation.endTime);
            const normalizedStatus = reservation.status.toLowerCase();

            return (
              <article className="reservation-card" key={reservation.id}>
                <div className="reservation-ticket-date" aria-label={dateFormatter.format(startTime)}>
                  <strong>{String(startTime.getDate()).padStart(2, "0")}</strong>
                  <span>{monthFormatter.format(startTime).replace(".", "")}</span>
                </div>
                <div className="reservation-ticket-body">
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

                {activeTab === "upcoming" && reservation.canCancel && (
                  <div className="reservation-actions">
                    <button
                      type="button"
                      className="reschedule-reservation-button"
                      disabled={cancellingId !== null}
                      onClick={() => openReschedule(reservation)}
                    >
                      Promeni termin
                    </button>
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
                </div>
              </article>
            );
          })}
        </div>
      )}

      {rescheduling && (
        <div className="reschedule-modal-backdrop" role="presentation">
          <section
            className="reschedule-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="reschedule-title"
          >
            <div className="reschedule-modal-heading">
              <div>
                <span className="reservation-label">{rescheduling.courtName}</span>
                <h2 id="reschedule-title">Promeni termin</h2>
              </div>
              <button
                type="button"
                className="reschedule-close-button"
                aria-label="Zatvori"
                disabled={rescheduleSaving}
                onClick={closeReschedule}
              >
                ×
              </button>
            </div>

            <p className="reschedule-current-slot">
              Trenutni termin: <strong>
                {dateFormatter.format(new Date(rescheduling.startTime))}, {" "}
                {timeFormatter.format(new Date(rescheduling.startTime))}–
                {timeFormatter.format(new Date(rescheduling.endTime))}
              </strong>
            </p>

            <label className="date-field">
              Novi datum
              <input
                type="date"
                min={getLocalDate()}
                value={rescheduleDate}
                disabled={rescheduleSaving}
                onChange={changeRescheduleDate}
              />
            </label>

            {rescheduleLoading && <p className="slots-message">Učitavanje termina...</p>}
            {rescheduleError && <p className="booking-error" role="alert">{rescheduleError}</p>}
            {hasValidationErrors(rescheduleFieldErrors) && (
              <div className="booking-field-errors" role="alert">
                {rescheduleFieldErrors.startTime?.map((message) => (
                  <p className="field-error" key={message}><strong>Početak:</strong> {message}</p>
                ))}
                {rescheduleFieldErrors.endTime?.map((message) => (
                  <p className="field-error" key={message}><strong>Završetak:</strong> {message}</p>
                ))}
              </div>
            )}

            {!rescheduleLoading && !rescheduleError && rescheduleSlots.length === 0 && (
              <p className="slots-message">Nema slobodnih termina za izabrani datum.</p>
            )}

            {!rescheduleLoading && rescheduleSlots.length > 0 && (
              <div className="slots-grid" aria-label="Novi slobodni termini">
                {rescheduleSlots.map((slot) => {
                  const selected = selectedRescheduleSlot?.startTime === slot.startTime;

                  return (
                    <button
                      type="button"
                      className={`slot-button${selected ? " selected" : ""}`}
                      key={slot.startTime}
                      aria-pressed={selected}
                      disabled={rescheduleSaving}
                      onClick={() => {
                        setSelectedRescheduleSlot(slot);
                        setRescheduleError("");
                        setRescheduleFieldErrors({});
                      }}
                    >
                      {timeFormatter.format(new Date(slot.startTime))}–
                      {timeFormatter.format(new Date(slot.endTime))}
                    </button>
                  );
                })}
              </div>
            )}

            <div className="reschedule-modal-actions">
              <button type="button" disabled={rescheduleSaving} onClick={closeReschedule}>
                Odustani
              </button>
              <button
                type="button"
                className="primary-button"
                disabled={!selectedRescheduleSlot || rescheduleSaving}
                onClick={submitReschedule}
              >
                {rescheduleSaving ? "Čuvanje..." : "Potvrdi novi termin"}
              </button>
            </div>
          </section>
        </div>
      )}
    </section>
  );
}

export default MyReservations;
