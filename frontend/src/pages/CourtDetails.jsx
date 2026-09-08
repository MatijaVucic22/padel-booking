import { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { Link, useNavigate, useParams } from "react-router-dom";
import api, {
  courtAvailabilityHubUrl,
  getBackendAssetUrl,
} from "../api/api";
import { getCourtImage } from "../utils/courtImages";
import {
  hasValidationErrors,
  parseValidationErrors,
} from "../utils/validationErrors";
import {
  longDateWithWeekdayFormatter as dateFormatter,
  timeFormatter,
} from "../utils/dateFormatters";

const priceFormatter = new Intl.NumberFormat("sr-Latn-RS", {
  style: "currency",
  currency: "RSD",
  maximumFractionDigits: 2,
});

function getLocalDate() {
  const today = new Date();
  const offset = today.getTimezoneOffset() * 60_000;

  return new Date(today.getTime() - offset).toISOString().split("T")[0];
}

function CourtDetails() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [court, setCourt] = useState(null);
  const [courtLoading, setCourtLoading] = useState(true);
  const [courtError, setCourtError] = useState("");
  const [selectedDate, setSelectedDate] = useState("");
  const [selectedSlot, setSelectedSlot] = useState(null);
  const [slots, setSlots] = useState([]);
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [slotsError, setSlotsError] = useState("");
  const [slotsReloadKey, setSlotsReloadKey] = useState(0);
  const [booking, setBooking] = useState(false);
  const [bookingError, setBookingError] = useState("");
  const [bookingFieldErrors, setBookingFieldErrors] = useState({});
  const [successMessage, setSuccessMessage] = useState("");
  const bookingInProgress = useRef(false);

  useEffect(() => {
    let ignoreResponse = false;

    api
      .get(`/courts/${id}`)
      .then((response) => {
        if (!ignoreResponse) setCourt(response.data);
      })
      .catch((requestError) => {
        if (ignoreResponse) return;

        console.error(requestError);
        setCourtError(
          requestError.response?.status === 404
            ? "Teren nije pronađen."
            : "Detalje terena trenutno nije moguće učitati.",
        );
      })
      .finally(() => {
        if (!ignoreResponse) setCourtLoading(false);
      });

    return () => {
      ignoreResponse = true;
    };
  }, [id]);

  useEffect(() => {
    if (!selectedDate) return undefined;

    let ignoreResponse = false;

    api
      .get("/reservations/available", {
        params: { courtId: id, date: selectedDate },
      })
      .then((response) => {
        if (!ignoreResponse) setSlots(response.data.slots);
      })
      .catch((requestError) => {
        if (ignoreResponse) return;

        console.error(requestError);
        setSlots([]);
        setSlotsError("Slobodne termine trenutno nije moguće učitati.");
      })
      .finally(() => {
        if (!ignoreResponse) setSlotsLoading(false);
      });

    return () => {
      ignoreResponse = true;
    };
  }, [id, selectedDate, slotsReloadKey]);

  useEffect(() => {
    if (!selectedDate) return undefined;

    let disposed = false;
    const courtId = Number(id);
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(courtAvailabilityHubUrl)
      .withAutomaticReconnect()
      .build();

    const refreshAvailability = () => {
      if (disposed) return;

      setSlotsLoading(true);
      setSlotsError("");
      setSlotsReloadKey((currentKey) => currentKey + 1);
    };

    const joinGroup = () =>
      connection.invoke("JoinCourtDate", courtId, selectedDate);

    connection.on("AvailabilityChanged", refreshAvailability);
    connection.onreconnected(() => {
      joinGroup().catch((connectionError) => {
        if (!disposed) console.error(connectionError);
      });
    });

    const connect = async () => {
      try {
        await connection.start();

        if (disposed) {
          await connection.stop();
          return;
        }

        await joinGroup();
      } catch (connectionError) {
        if (!disposed) console.error(connectionError);
      }
    };

    connect();

    return () => {
      disposed = true;
      connection.off("AvailabilityChanged", refreshAvailability);

      if (connection.state === signalR.HubConnectionState.Connected) {
        connection
          .invoke("LeaveCourtDate", courtId, selectedDate)
          .catch(() => undefined)
          .finally(() => connection.stop());
      } else if (connection.state !== signalR.HubConnectionState.Disconnected) {
        connection.stop();
      }
    };
  }, [id, selectedDate]);

  const selectDate = (event) => {
    const date = event.target.value;

    setSelectedDate(date);
    setSelectedSlot(null);
    setSlots([]);
    setSlotsLoading(Boolean(date));
    setSlotsError("");
    setBookingError("");
    setBookingFieldErrors({});
    setSuccessMessage("");
  };

  const selectSlot = (slot) => {
    setSelectedSlot(slot);
    setBookingError("");
    setBookingFieldErrors((currentErrors) => {
      const nextErrors = { ...currentErrors };
      delete nextErrors.startTime;
      delete nextErrors.endTime;
      return nextErrors;
    });
    setSuccessMessage("");
  };

  const bookSelectedSlot = async () => {
    if (!selectedSlot || bookingInProgress.current) return;

    if (!localStorage.getItem("token")) {
      navigate("/login");
      return;
    }

    bookingInProgress.current = true;
    setBooking(true);
    setBookingError("");
    setBookingFieldErrors({});
    setSuccessMessage("");

    try {
      await api.post("/reservations", {
        courtId: Number(id),
        startTime: selectedSlot.startTime,
        endTime: selectedSlot.endTime,
      });

      setSlots((currentSlots) =>
        currentSlots.filter(
          (slot) => slot.startTime !== selectedSlot.startTime,
        ),
      );
      setSelectedSlot(null);
      setSuccessMessage("Termin je uspešno rezervisan.");
    } catch (requestError) {
      console.error(requestError);

      if (requestError.response?.status === 401) {
        navigate("/login");
        return;
      }

      const validationErrors = parseValidationErrors(requestError);

      if (hasValidationErrors(validationErrors)) {
        setBookingFieldErrors(validationErrors);
        setBookingError("");
        return;
      }

      if (requestError.response?.status === 409) {
        setSelectedSlot(null);
        setSlots([]);
        setSlotsLoading(true);
        setSlotsReloadKey((currentKey) => currentKey + 1);
      }

      setBookingError(
        requestError.response?.status === 409
          ? "Termin je u međuvremenu rezervisan. Izaberi drugi termin."
          : typeof requestError.response?.data === "string"
            ? requestError.response.data
            : "Rezervacija nije uspela. Pokušaj ponovo.",
      );
    } finally {
      bookingInProgress.current = false;
      setBooking(false);
    }
  };

  const selectedSlotPrice = selectedSlot
    ? court.pricePerHour *
      ((new Date(selectedSlot.endTime) - new Date(selectedSlot.startTime)) /
        3_600_000)
    : 0;

  if (courtLoading) {
    return <p className="page court-feedback" aria-live="polite">Učitavanje terena...</p>;
  }

  if (courtError) {
    return (
      <section className="page court-feedback" role="alert">
        <h1>{courtError}</h1>
        <Link to="/courts" className="primary-button">Nazad na terene</Link>
      </section>
    );
  }

  return (
    <section className="page court-details-page">
      <Link to="/courts" className="back-link">← Svi tereni</Link>

      <div className="court-details-layout">
        <div className="court-details-main">
          <div className="court-details-image">
            <img src={court.imageUrl ? getBackendAssetUrl(court.imageUrl) : getCourtImage(id)} alt={`${court.name}, padel teren`} />
          </div>
          <div className="court-details-card">
        <div className="court-details-copy">
          <span className="court-eyebrow">Padel teren</span>
          <h1>{court.name}</h1>
          <p className="court-location">{court.location}</p>
          {court.description && <p>{court.description}</p>}
        </div>
        <div className="court-price">
          <strong>{priceFormatter.format(court.pricePerHour)}</strong>
          <span>po satu</span>
        </div>
      </div>
        </div>

      <div className="booking-panel">
        <span className="section-kicker">Rezervacija</span>
        <div className="booking-panel-heading">
          <div>
            <h2>Izaberi termin</h2>
            <p>Odaberi datum, termin i potvrdi rezervaciju.</p>
          </div>
          <label className="date-field">
            Izabrani datum
            <input type="date" min={getLocalDate()} value={selectedDate} onChange={selectDate} disabled={booking} />
          </label>
        </div>

        {selectedDate && (
          <p className="selected-date-label">
            Termini za: <strong>{dateFormatter.format(new Date(`${selectedDate}T00:00:00`))}</strong>
          </p>
        )}

        {slotsLoading && <p className="slots-message" aria-live="polite">Učitavanje slobodnih termina...</p>}
        {slotsError && <p className="booking-error" role="alert">{slotsError}</p>}
        {bookingError && <p className="booking-error" role="alert">{bookingError}</p>}
        {hasValidationErrors(bookingFieldErrors) && (
          <div className="booking-field-errors" role="alert">
            {bookingFieldErrors.courtId?.map((message) => (
              <p className="field-error" key={message}><strong>Teren:</strong> {message}</p>
            ))}
            {bookingFieldErrors.startTime?.map((message) => (
              <p className="field-error" key={message}><strong>Početak:</strong> {message}</p>
            ))}
            {bookingFieldErrors.endTime?.map((message) => (
              <p className="field-error" key={message}><strong>Završetak:</strong> {message}</p>
            ))}
          </div>
        )}

        {successMessage && (
          <div className="booking-success" role="status">
            <span>{successMessage}</span>
            <Link to="/my-reservations">Pogledaj moje rezervacije</Link>
          </div>
        )}

        {!selectedDate && <p className="slots-message">Izaberi datum za prikaz termina.</p>}
        {selectedDate && !slotsLoading && !slotsError && slots.length === 0 && (
          <p className="slots-message">Nema slobodnih termina za izabrani datum.</p>
        )}

        {slots.length > 0 && !slotsLoading && (
          <div className="slots-grid" aria-label="Slobodni termini">
            {slots.map((slot) => {
              const isSelected = selectedSlot?.startTime === slot.startTime;
              return (
                <button
                  type="button"
                  className={`slot-button${isSelected ? " selected" : ""}`}
                  key={slot.startTime}
                  disabled={booking}
                  aria-pressed={isSelected}
                  onClick={() => selectSlot(slot)}
                >
                  {timeFormatter.format(new Date(slot.startTime))}–{timeFormatter.format(new Date(slot.endTime))}
                </button>
              );
            })}
          </div>
        )}

        {selectedDate && (
          <div className="booking-summary">
            <div>
              <span>Teren</span>
              <strong>{court.name}</strong>
            </div>
            <div>
              <span>Datum</span>
              <strong>{dateFormatter.format(new Date(`${selectedDate}T00:00:00`))}</strong>
            </div>
            <div>
              <span>Vreme</span>
              <strong>
                {selectedSlot
                  ? `${timeFormatter.format(new Date(selectedSlot.startTime))}–${timeFormatter.format(new Date(selectedSlot.endTime))}`
                  : "Izaberi termin"}
              </strong>
            </div>
            <div>
              <span>Cena</span>
              <strong>
                {selectedSlot ? priceFormatter.format(selectedSlotPrice) : "—"}
              </strong>
            </div>
            <button
              type="button"
              disabled={!selectedSlot || booking}
              onClick={bookSelectedSlot}
            >
              {booking ? "Rezervacija..." : "Rezerviši"}
            </button>
          </div>
        )}
      </div>
      </div>
    </section>
  );
}

export default CourtDetails;
