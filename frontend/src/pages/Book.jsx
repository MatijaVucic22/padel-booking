import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import api, { getBackendAssetUrl } from "../api/api";
import { getCourtImage } from "../utils/courtImages";
import { longDateFormatter } from "../utils/dateFormatters";
import DatePicker from "../components/DatePicker";

const hours = Array.from({ length: 14 }, (_, index) => index + 8);
const durations = [1, 2, 3];
const priceFormatter = new Intl.NumberFormat("sr-Latn-RS", {
  style: "currency",
  currency: "RSD",
  maximumFractionDigits: 2,
});

function getBelgradeDate() {
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone: "Europe/Belgrade",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(new Date());
  const values = Object.fromEntries(
    parts.filter((part) => part.type !== "literal")
      .map((part) => [part.type, part.value]),
  );

  return `${values.year}-${values.month}-${values.day}`;
}

function Book() {
  const navigate = useNavigate();
  const [date, setDate] = useState("");
  const [startHour, setStartHour] = useState(null);
  const [duration, setDuration] = useState(1);
  const [courts, setCourts] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [bookingCourtId, setBookingCourtId] = useState(null);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    if (!date || startHour === null) {
      setCourts([]);
      return undefined;
    }

    let ignoreResponse = false;
    const startTime = `${date}T${String(startHour).padStart(2, "0")}:00:00`;

    setLoading(true);
    setError("");

    api.get("/courts/available", {
      params: { startTime, durationHours: duration },
    })
      .then((response) => {
        if (!ignoreResponse) setCourts(response.data);
      })
      .catch((requestError) => {
        if (ignoreResponse) return;
        console.error(requestError);
        setCourts([]);
        setError(
          requestError.response?.data?.message ??
          "Dostupne terene trenutno nije moguće učitati.",
        );
      })
      .finally(() => {
        if (!ignoreResponse) setLoading(false);
      });

    return () => {
      ignoreResponse = true;
    };
  }, [date, startHour, duration, reloadKey]);

  const selectDuration = (value) => {
    setDuration(value);
    setSuccess("");

    if (startHour !== null && startHour + value > 22) {
      setStartHour(null);
    }
  };

  const bookCourt = async (court) => {
    if (!localStorage.getItem("token")) {
      navigate("/login", { state: { from: "/book" } });
      return;
    }

    const startTime = `${date}T${String(startHour).padStart(2, "0")}:00:00`;
    const endTime = `${date}T${String(startHour + duration).padStart(2, "0")}:00:00`;

    setBookingCourtId(court.id);
    setError("");
    setSuccess("");

    try {
      await api.post("/reservations", {
        courtId: court.id,
        startTime,
        endTime,
      });
      setSuccess(`Teren „${court.name}” je uspešno rezervisan.`);
      setReloadKey((currentKey) => currentKey + 1);
    } catch (requestError) {
      console.error(requestError);
      setError(
        requestError.response?.status === 409
          ? "Teren je u međuvremenu rezervisan. Rezultati su osveženi."
          : requestError.response?.data?.message ??
            (typeof requestError.response?.data === "string"
              ? requestError.response.data
              : "Rezervacija nije uspela."),
      );
      setReloadKey((currentKey) => currentKey + 1);
    } finally {
      setBookingCourtId(null);
    }
  };

  const hasInterval = date && startHour !== null;

  return (
    <section className="page quick-book-page">
      <header className="quick-book-header">
        <span className="section-kicker">Brza rezervacija</span>
        <h1>Pronađi slobodan teren</h1>
        <p>Izaberi datum, početak i trajanje termina.</p>
      </header>

      <div className="quick-book-search">
        <label className="date-field">
          Datum
          <DatePicker
            min={getBelgradeDate()}
            value={date}
            ariaLabel="Izaberi datum rezervacije"
            onChange={(nextDate) => {
              setDate(nextDate);
              setSuccess("");
            }}
          />
        </label>

        <fieldset>
          <legend>Početak</legend>
          <div className="quick-book-hours">
            {hours.map((hour) => (
              <button
                type="button"
                className={startHour === hour ? "selected" : ""}
                disabled={hour + duration > 22}
                key={hour}
                onClick={() => {
                  setStartHour(hour);
                  setSuccess("");
                }}
              >
                {String(hour).padStart(2, "0")}:00
              </button>
            ))}
          </div>
        </fieldset>

        <fieldset>
          <legend>Trajanje</legend>
          <div className="quick-book-durations">
            {durations.map((value) => (
              <button
                type="button"
                className={duration === value ? "selected" : ""}
                key={value}
                onClick={() => selectDuration(value)}
              >
                {value} {value === 1 ? "sat" : "sata"}
              </button>
            ))}
          </div>
        </fieldset>

        <p className="quick-book-interval">
          Izabrani termin: <strong>{hasInterval
            ? `${longDateFormatter.format(new Date(`${date}T00:00:00`))}, ${String(startHour).padStart(2, "0")}:00–${String(startHour + duration).padStart(2, "0")}:00`
            : "izaberi datum i vreme"}</strong>
        </p>
      </div>

      {success && <p className="booking-success" role="status">{success}</p>}
      {error && <p className="booking-error" role="alert">{error}</p>}
      {loading && <p className="courts-feedback">Pretraga slobodnih terena...</p>}

      {hasInterval && !loading && !error && courts.length === 0 && (
        <p className="courts-feedback">Nema slobodnih terena za ceo izabrani termin.</p>
      )}

      {!loading && courts.length > 0 && (
        <div className="courts-grid quick-book-results">
          {courts.map((court, index) => (
            <article className="court-card" key={court.id}>
              <div className="court-card-image">
                <img
                  src={court.imageUrl ? getBackendAssetUrl(court.imageUrl) : getCourtImage(court.id, index)}
                  alt={`${court.name}, padel teren`}
                />
              </div>
              <div className="court-card-content">
                <div className="court-card-heading">
                  <div><span>{court.location}</span><h2>{court.name}</h2></div>
                </div>
                <div className="quick-book-prices">
                  <span>{priceFormatter.format(court.pricePerHour)} / sat</span>
                  <strong>Ukupno: {priceFormatter.format(court.pricePerHour * duration)}</strong>
                </div>
                <button
                  type="button"
                  className="primary-button"
                  disabled={bookingCourtId !== null}
                  onClick={() => bookCourt(court)}
                >
                  {bookingCourtId === court.id ? "Rezervacija..." : "Rezerviši"}
                </button>
              </div>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

export default Book;
