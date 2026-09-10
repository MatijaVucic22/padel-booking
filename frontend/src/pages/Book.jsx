import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import * as signalR from "@microsoft/signalr";
import api, { courtAvailabilityHubUrl, getBackendAssetUrl } from "../api/api";
import { getCourtImage } from "../utils/courtImages";
import { longDateFormatter } from "../utils/dateFormatters";
import DatePicker from "../components/DatePicker";

const timeGroups = [
  { label: "Jutro", hours: [8, 9, 10, 11] },
  { label: "Popodne", hours: [12, 13, 14, 15, 16] },
  { label: "Veče", hours: [17, 18, 19, 20, 21] },
];
const durations = [1, 2, 3];
const priceFormatter = new Intl.NumberFormat("sr-Latn-RS", {
  style: "currency", currency: "RSD", maximumFractionDigits: 2,
});

function getBelgradeDate() {
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone: "Europe/Belgrade", year: "numeric", month: "2-digit", day: "2-digit",
  }).formatToParts(new Date());
  const values = Object.fromEntries(parts.filter((part) => part.type !== "literal").map((part) => [part.type, part.value]));
  return `${values.year}-${values.month}-${values.day}`;
}

function Book() {
  const navigate = useNavigate();
  const requestId = useRef(0);
  const modalTimer = useRef(null);
  const signalRRefreshTimer = useRef(null);
  const courtChangeRefreshTimer = useRef(null);
  const componentMounted = useRef(true);
  const [date, setDate] = useState("");
  const [startHour, setStartHour] = useState(null);
  const [duration, setDuration] = useState(1);
  const [courts, setCourts] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [bookingCourtId, setBookingCourtId] = useState(null);
  const [reloadKey, setReloadKey] = useState(0);
  const [selectedCourt, setSelectedCourt] = useState(null);
  const [modalPhase, setModalPhase] = useState("confirm");
  const [modalError, setModalError] = useState("");
  const [activeCourtIds, setActiveCourtIds] = useState([]);
  const [activeCourtsReloadKey, setActiveCourtsReloadKey] = useState(0);

  useEffect(() => {
    componentMounted.current = true;

    return () => {
      componentMounted.current = false;
      if (modalTimer.current !== null) {
        window.clearTimeout(modalTimer.current);
        modalTimer.current = null;
      }
      if (signalRRefreshTimer.current !== null) {
        window.clearTimeout(signalRRefreshTimer.current);
        signalRRefreshTimer.current = null;
      }
      if (courtChangeRefreshTimer.current !== null) {
        window.clearTimeout(courtChangeRefreshTimer.current);
        courtChangeRefreshTimer.current = null;
      }
    };
  }, []);

  useEffect(() => {
    let disposed = false;

    api.get("/courts")
      .then((response) => {
        if (!disposed) setActiveCourtIds(response.data.map((court) => court.id));
      })
      .catch((requestError) => {
        if (!disposed) console.error("SignalR court subscriptions could not be prepared.", requestError);
      });

    return () => {
      disposed = true;
    };
  }, [activeCourtsReloadKey]);

  useEffect(() => {
    if (!date) return undefined;

    let disposed = false;
    const subscribedDate = date;
    const courtIds = [...activeCourtIds];
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(courtAvailabilityHubUrl)
      .withAutomaticReconnect()
      .build();

    const joinGroups = () => Promise.all(
      courtIds.map((courtId) => connection.invoke("JoinCourtDate", courtId, subscribedDate)),
    );
    const refreshAvailability = () => {
      if (disposed) return;
      if (signalRRefreshTimer.current !== null) window.clearTimeout(signalRRefreshTimer.current);
      signalRRefreshTimer.current = window.setTimeout(() => {
        signalRRefreshTimer.current = null;
        if (!disposed) setReloadKey((key) => key + 1);
      }, 120);
    };
    const refreshCourtData = () => {
      if (disposed) return;
      if (courtChangeRefreshTimer.current !== null) window.clearTimeout(courtChangeRefreshTimer.current);
      courtChangeRefreshTimer.current = window.setTimeout(() => {
        courtChangeRefreshTimer.current = null;
        if (!disposed) {
          setActiveCourtsReloadKey((key) => key + 1);
          setReloadKey((key) => key + 1);
        }
      }, 120);
    };

    connection.on("AvailabilityChanged", refreshAvailability);
    connection.on("CourtChanged", refreshCourtData);
    connection.onreconnected(() => {
      joinGroups().catch((connectionError) => {
        if (!disposed) console.error(connectionError);
      });
    });

    connection.start()
      .then(() => (disposed ? connection.stop() : joinGroups()))
      .catch((connectionError) => {
        if (!disposed) console.error(connectionError);
      });

    return () => {
      disposed = true;
      if (signalRRefreshTimer.current !== null) {
        window.clearTimeout(signalRRefreshTimer.current);
        signalRRefreshTimer.current = null;
      }
      connection.off("AvailabilityChanged", refreshAvailability);
      connection.off("CourtChanged", refreshCourtData);
      if (courtChangeRefreshTimer.current !== null) {
        window.clearTimeout(courtChangeRefreshTimer.current);
        courtChangeRefreshTimer.current = null;
      }
      if (connection.state !== signalR.HubConnectionState.Disconnected) {
        Promise.allSettled(
          courtIds.map((courtId) => connection.invoke("LeaveCourtDate", courtId, subscribedDate)),
        ).finally(() => connection.stop());
      }
    };
  }, [date, activeCourtIds]);

  useEffect(() => {
    if (!selectedCourt || modalPhase !== "confirm") return undefined;

    const handleEscape = (event) => {
      if (event.key === "Escape") setSelectedCourt(null);
    };
    window.addEventListener("keydown", handleEscape);
    return () => window.removeEventListener("keydown", handleEscape);
  }, [modalPhase, selectedCourt]);

  useEffect(() => {
    if (!date || startHour === null) {
      setCourts([]);
      setLoading(false);
      return undefined;
    }

    const controller = new AbortController();
    const currentRequestId = ++requestId.current;
    const startTime = `${date}T${String(startHour).padStart(2, "0")}:00:00`;

    setLoading(true);
    setError("");

    api.get("/courts/available", {
      params: { startTime, durationHours: duration },
      signal: controller.signal,
    })
      .then((response) => {
        if (currentRequestId === requestId.current) setCourts(response.data);
      })
      .catch((requestError) => {
        if (controller.signal.aborted || currentRequestId !== requestId.current) return;
        console.error(requestError);
        setError(requestError.response?.data?.message ?? "Dostupne terene trenutno nije moguće učitati.");
      })
      .finally(() => {
        if (currentRequestId === requestId.current) setLoading(false);
      });

    return () => controller.abort();
  }, [date, startHour, duration, reloadKey]);

  const selectDuration = (value) => {
    setDuration(value);
    if (startHour !== null && startHour + value > 22) setStartHour(null);
  };

  const openConfirmation = (court) => {
    if (!localStorage.getItem("token")) {
      navigate("/login", { state: { from: "/book" } });
      return;
    }

    setSelectedCourt(court);
    setModalPhase("confirm");
    setModalError("");
  };

  const closeConfirmation = () => {
    if (modalPhase === "confirm") setSelectedCourt(null);
  };

  const waitForMinimumLoading = (startedAt) => new Promise((resolve) => {
    const remaining = Math.max(0, 500 - (Date.now() - startedAt));
    modalTimer.current = window.setTimeout(() => {
      modalTimer.current = null;
      resolve();
    }, remaining);
  });

  const bookCourt = async () => {
    if (!selectedCourt || modalPhase !== "confirm" || bookingCourtId !== null) return;

    const startTime = `${date}T${String(startHour).padStart(2, "0")}:00:00`;
    const endTime = `${date}T${String(startHour + duration).padStart(2, "0")}:00:00`;
    const loadingStartedAt = Date.now();
    setBookingCourtId(selectedCourt.id);
    setModalPhase("loading");
    setModalError("");

    try {
      await api.post("/reservations", { courtId: selectedCourt.id, startTime, endTime });
      await waitForMinimumLoading(loadingStartedAt);
      if (!componentMounted.current) return;

      setModalPhase("success");
      modalTimer.current = window.setTimeout(() => {
        modalTimer.current = null;
        navigate("/my-reservations");
      }, 1200);
    } catch (requestError) {
      await waitForMinimumLoading(loadingStartedAt);
      if (!componentMounted.current) return;

      console.error(requestError);
      setModalError(requestError.response?.status === 409
        ? "Teren je u međuvremenu rezervisan. Rezultati su osveženi."
        : requestError.response?.data?.message ?? (typeof requestError.response?.data === "string" ? requestError.response.data : "Rezervacija nije uspela."));
      setModalPhase("confirm");
      setReloadKey((key) => key + 1);
    } finally {
      if (componentMounted.current) setBookingCourtId(null);
    }
  };

  const hasInterval = date && startHour !== null;
  const formattedDate = date ? longDateFormatter.format(new Date(`${date}T00:00:00`)) : "Izaberi datum";
  const formattedTime = startHour === null
    ? "Izaberi vreme početka"
    : `${String(startHour).padStart(2, "0")}:00 – ${String(startHour + duration).padStart(2, "0")}:00`;

  return (
    <section className="page quick-book-page">
      <header className="quick-book-header">
        <span className="section-kicker">Rezervacija</span>
        <h1>Pronađi slobodan teren</h1>
        <p>Izaberi datum, vreme i trajanje termina.</p>
      </header>

      <div className="quick-book-layout">
        <div className="quick-book-search">
          <label className="date-field">Datum
            <DatePicker min={getBelgradeDate()} value={date} ariaLabel="Izaberi datum rezervacije" onChange={setDate} />
          </label>

          <fieldset>
            <legend>Vreme početka</legend>
            <div className="quick-book-time-groups">
              {timeGroups.map((group) => (
                <div className="quick-book-time-group" key={group.label}>
                  <span>{group.label}</span>
                  <div className="quick-book-hours">
                    {group.hours.map((hour) => (
                      <button type="button" className={startHour === hour ? "selected" : ""} disabled={hour + duration > 22} key={hour} onClick={() => setStartHour(hour)}>
                        {String(hour).padStart(2, "0")}:00
                      </button>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          </fieldset>

          <fieldset>
            <legend>Trajanje</legend>
            <div className="quick-book-durations">
              {durations.map((value) => (
                <button type="button" className={duration === value ? "selected" : ""} key={value} onClick={() => selectDuration(value)}>
                  <strong>{value}</strong><span>{value === 1 ? "sat" : "sata"}</span>
                </button>
              ))}
            </div>
          </fieldset>
        </div>

        <aside className="quick-book-summary" aria-live="polite">
          <span className="section-kicker">Tvoj termin</span>
          <strong>{formattedDate}</strong>
          <strong>{formattedTime}</strong>
          <p>Trajanje: {duration} {duration === 1 ? "sat" : "sata"}</p>
        </aside>
      </div>

      {error && <p className="booking-error" role="alert">{error}</p>}

      {hasInterval && (
        <section className="quick-book-results-section">
          <header className="quick-book-results-header">
            <h2>Slobodni tereni</h2>
            {!loading && !error && <span>{courts.length} {courts.length === 1 ? "dostupan" : "dostupna"}</span>}
          </header>

          <div className={`quick-book-results-shell${loading ? " is-refreshing" : ""}`} aria-busy={loading}>
            {courts.length > 0 && (
              <div className="quick-book-results">
                {courts.map((court, index) => (
                  <article className="quick-book-court" key={court.id}>
                    <div className="quick-book-court-image"><img src={court.imageUrl ? getBackendAssetUrl(court.imageUrl) : getCourtImage(court.id, index)} alt={`${court.name}, padel teren`} /></div>
                    <div className="quick-book-court-content">
                      <div><span className="quick-book-location">{court.location}</span><h3>{court.name}</h3></div>
                      <div className="quick-book-prices"><span>{priceFormatter.format(court.pricePerHour)} / sat</span><strong>Ukupno: {priceFormatter.format(court.pricePerHour * duration)}</strong></div>
                      <button type="button" className="primary-button" disabled={bookingCourtId !== null} onClick={() => openConfirmation(court)}>
                        Rezerviši
                      </button>
                    </div>
                  </article>
                ))}
              </div>
            )}

            {!loading && !error && courts.length === 0 && <p className="courts-feedback">Nema slobodnih terena za ceo izabrani termin.</p>}
            {loading && <div className="quick-book-loading-overlay" role="status"><span className="quick-book-spinner" aria-hidden="true" /><strong>Proveravamo dostupnost...</strong></div>}
          </div>
        </section>
      )}

      {selectedCourt && (
        <div className="booking-confirm-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) closeConfirmation(); }}>
          <section className="booking-confirm-modal" role="dialog" aria-modal="true" aria-labelledby="booking-confirm-title">
            {modalPhase === "confirm" && (
              <>
                <header className="booking-confirm-heading">
                  <div><h2 id="booking-confirm-title">Potvrdi rezervaciju</h2><p>Proveri podatke pre potvrde termina.</p></div>
                  <button type="button" className="booking-confirm-close" aria-label="Zatvori potvrdu" onClick={closeConfirmation}>×</button>
                </header>

                <dl className="booking-confirm-details">
                  <div><dt>Teren</dt><dd>{selectedCourt.name}</dd></div>
                  <div><dt>Lokacija</dt><dd>{selectedCourt.location}</dd></div>
                  <div><dt>Datum</dt><dd>{formattedDate}</dd></div>
                  <div><dt>Vreme</dt><dd>{formattedTime}</dd></div>
                  <div><dt>Trajanje</dt><dd>{duration} {duration === 1 ? "sat" : "sata"}</dd></div>
                  <div><dt>Cena po satu</dt><dd>{priceFormatter.format(selectedCourt.pricePerHour)}</dd></div>
                  <div className="booking-confirm-total"><dt>Ukupna cena</dt><dd>{priceFormatter.format(selectedCourt.pricePerHour * duration)}</dd></div>
                </dl>

                {modalError && <p className="booking-confirm-error" role="alert">{modalError}</p>}
                <div className="booking-confirm-actions">
                  <button type="button" onClick={closeConfirmation}>Odustani</button>
                  <button type="button" className="primary-button" onClick={bookCourt}>Rezerviši</button>
                </div>
              </>
            )}

            {modalPhase === "loading" && (
              <div className="booking-confirm-state" role="status">
                <span className="booking-confirm-spinner" aria-hidden="true" />
                <h2 id="booking-confirm-title">Rezervacija u toku...</h2>
                <p>Potvrđujemo tvoj termin.</p>
              </div>
            )}

            {modalPhase === "success" && (
              <div className="booking-confirm-state" role="status">
                <span className="booking-confirm-check" aria-hidden="true">✓</span>
                <h2 id="booking-confirm-title">Rezervacija uspešna!</h2>
                <p>Termin je dodat u tvoje rezervacije.</p>
              </div>
            )}
          </section>
        </div>
      )}
    </section>
  );
}

export default Book;
