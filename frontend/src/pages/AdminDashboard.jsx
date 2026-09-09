import { useEffect, useState } from "react";
import * as signalR from "@microsoft/signalr";
import api, { courtAvailabilityHubUrl } from "../api/api";
import {
  hasValidationErrors,
  parseValidationErrors,
} from "../utils/validationErrors";
import {
  runAdminMutation,
  settleAdminRequests,
} from "../utils/adminAsync";
import {
  numericDateFormatter as dateFormatter,
  timeFormatter,
} from "../utils/dateFormatters";

const priceFormatter = new Intl.NumberFormat("sr-Latn-RS", {
  style: "currency",
  currency: "RSD",
  maximumFractionDigits: 2,
});

const emptyCourtForm = {
  name: "",
  location: "",
  description: "",
  pricePerHour: "",
};

const statusLabels = {
  Active: "Aktivna",
  Cancelled: "Otkazana",
  Completed: "Završena",
};

const tabs = [
  { id: "dashboard", label: "Dashboard" },
  { id: "calendar", label: "Kalendar" },
  { id: "courts", label: "Tereni" },
  { id: "reservations", label: "Rezervacije" },
  { id: "users", label: "Korisnici" },
];

const calendarHours = Array.from({ length: 14 }, (_, index) => index + 8);
const calendarBoundaryHours = Array.from({ length: 15 }, (_, index) => index + 8);

function getTodayDate() {
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone: "Europe/Belgrade",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(new Date());
  const values = Object.fromEntries(parts.map((part) => [part.type, part.value]));

  return `${values.year}-${values.month}-${values.day}`;
}

function shiftCalendarDate(date, days) {
  const value = new Date(`${date}T00:00:00Z`);
  value.setUTCDate(value.getUTCDate() + days);
  return value.toISOString().slice(0, 10);
}

function getWallClockMinutes(value) {
  const match = value?.match(/T(\d{2}):(\d{2})/);
  return match ? Number(match[1]) * 60 + Number(match[2]) : 0;
}

const initialSectionState = {
  stats: { loading: true, error: "" },
  courts: { loading: true, error: "" },
  users: { loading: true, error: "" },
  reservations: { loading: true, error: "" },
};

const sectionErrorMessages = {
  stats: "Statistiku trenutno nije moguće učitati.",
  courts: "Terene trenutno nije moguće učitati.",
  users: "Korisnike trenutno nije moguće učitati.",
  reservations: "Rezervacije trenutno nije moguće učitati.",
};

function SectionFeedback({ state, onRetry }) {
  if (state.loading) {
    return <p className="admin-feedback" aria-live="polite">Učitavanje podataka...</p>;
  }

  if (!state.error) return null;

  return (
    <div className="admin-feedback" role="alert">
      <p>{state.error}</p>
      <button type="button" onClick={onRetry}>Pokušaj ponovo</button>
    </div>
  );
}

function AdminDashboard() {
  const [activeTab, setActiveTab] = useState("dashboard");
  const [stats, setStats] = useState(null);
  const [courts, setCourts] = useState([]);
  const [users, setUsers] = useState([]);
  const [reservations, setReservations] = useState([]);
  const [sectionState, setSectionState] = useState(initialSectionState);
  const [courtForm, setCourtForm] = useState(emptyCourtForm);
  const [editingCourtId, setEditingCourtId] = useState(null);
  const [savingCourt, setSavingCourt] = useState(false);
  const [deactivatingCourtId, setDeactivatingCourtId] = useState(null);
  const [courtMessage, setCourtMessage] = useState("");
  const [courtError, setCourtError] = useState("");
  const [courtWarning, setCourtWarning] = useState("");
  const [courtFieldErrors, setCourtFieldErrors] = useState({});
  const [courtImage, setCourtImage] = useState(null);
  const [courtImagePreview, setCourtImagePreview] = useState("");
  const [calendarDate, setCalendarDate] = useState(getTodayDate);
  const [calendarData, setCalendarData] = useState({ courts: [], reservations: [], blockedPeriods: [] });
  const [calendarLoading, setCalendarLoading] = useState(false);
  const [calendarError, setCalendarError] = useState("");
  const [calendarReloadKey, setCalendarReloadKey] = useState(0);
  const [selectedCalendarReservation, setSelectedCalendarReservation] = useState(null);
  const [selectedBlockedPeriod, setSelectedBlockedPeriod] = useState(null);
  const [showBlockForm, setShowBlockForm] = useState(false);
  const [blockForm, setBlockForm] = useState({ courtId: "", date: calendarDate, startHour: "08", endHour: "09", reason: "" });
  const [blockFieldErrors, setBlockFieldErrors] = useState({});
  const [blockError, setBlockError] = useState("");
  const [savingBlock, setSavingBlock] = useState(false);
  const [deletingBlock, setDeletingBlock] = useState(false);

  const calendarCourtIds = calendarData.courts
    .map((court) => court.id)
    .join(",");

  const openBlockForm = () => {
    setBlockForm({
      courtId: String(calendarData.courts[0]?.id ?? ""),
      date: calendarDate,
      startHour: "08",
      endHour: "09",
      reason: "",
    });
    setBlockFieldErrors({});
    setBlockError("");
    setShowBlockForm(true);
  };

  const handleBlockInput = (event) => {
    const { name, value } = event.target;
    setBlockForm((current) => ({ ...current, [name]: value }));
    setBlockFieldErrors((current) => {
      const errorField = name === "startHour"
        ? "startTime"
        : name === "endHour"
          ? "endTime"
          : name;
      if (!current[errorField]) return current;
      const next = { ...current };
      delete next[errorField];
      return next;
    });
  };

  const saveBlockedPeriod = async (event) => {
    event.preventDefault();
    setSavingBlock(true);
    setBlockError("");
    setBlockFieldErrors({});

    try {
      await api.post("/admin/blocked-periods", {
        courtId: Number(blockForm.courtId),
        startTime: `${blockForm.date}T${blockForm.startHour}:00:00`,
        endTime: `${blockForm.date}T${blockForm.endHour}:00:00`,
        reason: blockForm.reason,
      });
      setShowBlockForm(false);
      setCalendarDate(blockForm.date);
      setCalendarReloadKey((current) => current + 1);
    } catch (requestError) {
      const validationErrors = parseValidationErrors(requestError);
      if (hasValidationErrors(validationErrors)) {
        setBlockFieldErrors(validationErrors);
      } else {
        setBlockError(
          requestError.response?.data?.message ??
          requestError.response?.data ??
          "Blokiranje termina nije uspelo.",
        );
      }
    } finally {
      setSavingBlock(false);
    }
  };

  const deleteBlockedPeriod = async () => {
    if (!window.confirm("Da li sigurno želiš da odblokiraš ovaj termin?")) return;

    setDeletingBlock(true);
    setBlockError("");
    try {
      await api.delete(`/admin/blocked-periods/${selectedBlockedPeriod.id}`);
      setSelectedBlockedPeriod(null);
      setCalendarReloadKey((current) => current + 1);
    } catch (requestError) {
      setBlockError(
        requestError.response?.data?.message ??
        requestError.response?.data ??
        "Odblokiranje termina nije uspelo.",
      );
    } finally {
      setDeletingBlock(false);
    }
  };

  useEffect(() => {
    if (activeTab !== "calendar") return undefined;

    let ignoreResponse = false;
    setCalendarLoading(true);
    setCalendarError("");

    api.get("/admin/calendar", { params: { date: calendarDate } })
      .then((response) => {
        if (!ignoreResponse) setCalendarData(response.data);
      })
      .catch((requestError) => {
        if (ignoreResponse) return;
        console.error(requestError);
        setCalendarError("Kalendar trenutno nije moguće učitati.");
      })
      .finally(() => {
        if (!ignoreResponse) setCalendarLoading(false);
      });

    return () => {
      ignoreResponse = true;
    };
  }, [activeTab, calendarDate, calendarReloadKey]);

  useEffect(() => {
    if (activeTab !== "calendar" || !calendarCourtIds) return undefined;

    let disposed = false;
    const courtIds = calendarCourtIds.split(",").map(Number);
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(courtAvailabilityHubUrl)
      .withAutomaticReconnect()
      .build();

    const joinGroups = () => Promise.all(
      courtIds.map((courtId) =>
        connection.invoke("JoinCourtDate", courtId, calendarDate)),
    );
    const refreshCalendar = () => {
      if (!disposed) setCalendarReloadKey((current) => current + 1);
    };

    connection.on("AvailabilityChanged", refreshCalendar);
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
      connection.off("AvailabilityChanged", refreshCalendar);
      if (connection.state !== signalR.HubConnectionState.Disconnected) {
        Promise.allSettled(
          courtIds.map((courtId) =>
            connection.invoke("LeaveCourtDate", courtId, calendarDate)),
        ).finally(() => connection.stop());
      }
    };
  }, [activeTab, calendarDate, calendarCourtIds]);

  useEffect(() => () => {
    if (courtImagePreview) URL.revokeObjectURL(courtImagePreview);
  }, [courtImagePreview]);

  useEffect(() => {
    let ignoreResponse = false;

    const requests = [
      api.get("/admin/stats"),
      api.get("/courts"),
      api.get("/admin/users"),
      api.get("/admin/reservations"),
    ];
    const sections = ["stats", "courts", "users", "reservations"];
    const setters = [setStats, setCourts, setUsers, setReservations];

    settleAdminRequests(requests)
      .then((results) => {
        if (ignoreResponse) return;

        results.forEach((result, index) => {
          const section = sections[index];

          if (result.status === "fulfilled") {
            setters[index](result.value.data);
            setSectionState((current) => ({
              ...current,
              [section]: { loading: false, error: "" },
            }));
          } else {
            setSectionState((current) => ({
              ...current,
              [section]: {
                loading: false,
                error:
                  result.reason.response?.status === 403
                    ? "Nemaš dozvolu za ovu admin sekciju."
                    : sectionErrorMessages[section],
              },
            }));
          }
        });
      });

    return () => {
      ignoreResponse = true;
    };
  }, []);

  const retrySection = async (section) => {
    const requests = {
      stats: () => api.get("/admin/stats"),
      courts: () => api.get("/courts"),
      users: () => api.get("/admin/users"),
      reservations: () => api.get("/admin/reservations"),
    };
    const setters = {
      stats: setStats,
      courts: setCourts,
      users: setUsers,
      reservations: setReservations,
    };

    setSectionState((current) => ({
      ...current,
      [section]: { loading: true, error: "" },
    }));

    try {
      const response = await requests[section]();
      setters[section](response.data);
      setSectionState((current) => ({
        ...current,
        [section]: { loading: false, error: "" },
      }));
    } catch (requestError) {
      setSectionState((current) => ({
        ...current,
        [section]: {
          loading: false,
          error:
            requestError.response?.status === 403
              ? "Nemaš dozvolu za ovu admin sekciju."
              : sectionErrorMessages[section],
        },
      }));
    }
  };

  const refreshCourtsAndStats = async () => {
    const results = await Promise.allSettled([
      api.get("/courts"),
      api.get("/admin/stats"),
    ]);

    const sections = ["courts", "stats"];
    const setters = [setCourts, setStats];

    results.forEach((result, index) => {
      const section = sections[index];

      if (result.status === "fulfilled") {
        setters[index](result.value.data);
        setSectionState((current) => ({
          ...current,
          [section]: { loading: false, error: "" },
        }));
      }
    });

    return results.every((result) => result.status === "fulfilled");
  };

  const handleCourtInput = (event) => {
    const { name, value } = event.target;
    setCourtForm((currentForm) => ({ ...currentForm, [name]: value }));
    setCourtFieldErrors((currentErrors) => {
      if (!currentErrors[name]) return currentErrors;

      const nextErrors = { ...currentErrors };
      delete nextErrors[name];
      return nextErrors;
    });
  };

  const handleCourtImage = (event) => {
    const image = event.target.files?.[0] ?? null;

    setCourtImage(image);
    setCourtImagePreview(image ? URL.createObjectURL(image) : "");
    setCourtFieldErrors((currentErrors) => {
      if (!currentErrors.image) return currentErrors;

      const nextErrors = { ...currentErrors };
      delete nextErrors.image;
      return nextErrors;
    });
  };

  const startEditingCourt = (court) => {
    setEditingCourtId(court.id);
    setCourtForm({
      name: court.name,
      location: court.location,
      description: court.description ?? "",
      pricePerHour: String(court.pricePerHour),
    });
    setCourtMessage("");
    setCourtError("");
    setCourtWarning("");
    setCourtFieldErrors({});
    setCourtImage(null);
    setCourtImagePreview("");
  };

  const resetCourtForm = () => {
    setEditingCourtId(null);
    setCourtForm(emptyCourtForm);
    setCourtFieldErrors({});
    setCourtImage(null);
    setCourtImagePreview("");
  };

  const saveCourt = async (event) => {
    event.preventDefault();
    setSavingCourt(true);
    setCourtMessage("");
    setCourtError("");
    setCourtWarning("");
    setCourtFieldErrors({});

    const courtPayload = {
      ...courtForm,
      pricePerHour: Number(courtForm.pricePerHour),
    };

    try {
      const { refreshSucceeded } = await runAdminMutation({
        mutate: async () => {
          let response;

          if (editingCourtId) {
            response = await api.put(
              `/courts/${editingCourtId}`,
              courtPayload,
            );
          } else {
            const formData = new FormData();
            formData.append("name", courtForm.name);
            formData.append("location", courtForm.location);
            formData.append("description", courtForm.description);
            formData.append("pricePerHour", courtForm.pricePerHour);

            if (courtImage) formData.append("image", courtImage);

            response = await api.post("/courts", formData);
          }

          return response.data;
        },
        onSaved: (savedCourt) => {
          if (editingCourtId) {
            setCourts((currentCourts) =>
              currentCourts.map((court) =>
                court.id === editingCourtId ? savedCourt : court,
              ),
            );
          } else {
            setCourts((currentCourts) => [...currentCourts, savedCourt]);
            setStats((currentStats) =>
              currentStats
                ? { ...currentStats, activeCourts: currentStats.activeCourts + 1 }
                : currentStats,
            );
          }

          setCourtMessage(
            editingCourtId
              ? "Teren je uspešno izmenjen."
              : "Teren je uspešno dodat.",
          );
          resetCourtForm();
        },
        refresh: refreshCourtsAndStats,
      });

      if (!refreshSucceeded) {
        setCourtWarning(
          "Izmena je sačuvana, ali osvežavanje podataka nije uspelo.",
        );
      }
    } catch (requestError) {
      console.error(requestError);
      const validationErrors = parseValidationErrors(requestError);

      if (hasValidationErrors(validationErrors)) {
        setCourtFieldErrors(validationErrors);
        setCourtError("");
        return;
      }

      setCourtError(
        typeof requestError.response?.data === "string"
          ? requestError.response.data
          : "Čuvanje terena nije uspelo.",
      );
    } finally {
      setSavingCourt(false);
    }
  };

  const deactivateCourt = async (court) => {
    const confirmed = window.confirm(
      `Da li sigurno želiš da deaktiviraš teren „${court.name}”?`,
    );

    if (!confirmed) return;

    setDeactivatingCourtId(court.id);
    setCourtMessage("");
    setCourtError("");
    setCourtWarning("");

    try {
      const { refreshSucceeded } = await runAdminMutation({
        mutate: () => api.delete(`/courts/${court.id}`),
        onSaved: () => {
          setCourts((currentCourts) =>
            currentCourts.filter((currentCourt) => currentCourt.id !== court.id),
          );
          setStats((currentStats) =>
            currentStats
              ? {
                  ...currentStats,
                  activeCourts: Math.max(0, currentStats.activeCourts - 1),
                }
              : currentStats,
          );
          setCourtMessage("Teren je uspešno deaktiviran.");

          if (editingCourtId === court.id) resetCourtForm();
        },
        refresh: refreshCourtsAndStats,
      });

      if (!refreshSucceeded) {
        setCourtWarning(
          "Izmena je sačuvana, ali osvežavanje podataka nije uspelo.",
        );
      }
    } catch (requestError) {
      console.error(requestError);
      setCourtError(
        typeof requestError.response?.data === "string"
          ? requestError.response.data
          : "Deaktiviranje terena nije uspelo.",
      );
    } finally {
      setDeactivatingCourtId(null);
    }
  };

  const statCards = stats
    ? [
        { label: "Korisnici", value: stats.totalUsers },
        { label: "Aktivni tereni", value: stats.activeCourts },
        { label: "Sve rezervacije", value: stats.totalReservations },
        { label: "Predstojeće", value: stats.upcomingReservations },
        { label: "U toku", value: stats.ongoingReservations },
        { label: "Završene", value: stats.completedReservations },
        { label: "Otkazane", value: stats.cancelledReservations },
        { label: "Realizovan prihod", value: priceFormatter.format(stats.realizedRevenue) },
        { label: "Budući prihod", value: priceFormatter.format(stats.upcomingRevenue) },
      ]
    : [];

  return (
    <section className="page admin-page">
      <header className="admin-header">
        <span className="admin-eyebrow">Administracija</span>
        <h1>Admin panel</h1>
        <p>Upravljaj terenima i pregledaj poslovne podatke.</p>
      </header>

      <nav className="admin-tabs" aria-label="Sekcije admin panela">
        {tabs.map((tab) => (
          <button
            type="button"
            className={activeTab === tab.id ? "active" : ""}
            aria-current={activeTab === tab.id ? "page" : undefined}
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
          >
            {tab.label}
          </button>
        ))}
      </nav>

      {activeTab === "dashboard" && (
        <>
          <SectionFeedback
            state={sectionState.stats}
            onRetry={() => retrySection("stats")}
          />
          {!sectionState.stats.loading && !sectionState.stats.error && (
            <div className="admin-stats-grid">
              {statCards.map((stat) => (
                <article className="admin-stat-card" key={stat.label}>
                  <span>{stat.label}</span>
                  <strong>{stat.value}</strong>
                </article>
              ))}
            </div>
          )}
        </>
      )}

      {activeTab === "calendar" && (
        <section className="admin-section admin-calendar-section">
          <div className="admin-calendar-toolbar">
            <div>
              <span className="admin-eyebrow">Raspored terena</span>
              <h2>{dateFormatter.format(new Date(`${calendarDate}T12:00:00`))}</h2>
            </div>
            <div className="admin-calendar-date-controls">
              <button
                type="button"
                aria-label="Prethodni dan"
                onClick={() => setCalendarDate((current) => shiftCalendarDate(current, -1))}
              >
                ←
              </button>
              <input
                type="date"
                value={calendarDate}
                aria-label="Datum kalendara"
                onChange={(event) => setCalendarDate(event.target.value)}
              />
              <button
                type="button"
                aria-label="Sledeći dan"
                onClick={() => setCalendarDate((current) => shiftCalendarDate(current, 1))}
              >
                →
              </button>
              <button
                type="button"
                className="admin-calendar-block-action"
                disabled={calendarData.courts.length === 0}
                onClick={openBlockForm}
              >
                + Blokiraj termin
              </button>
            </div>
          </div>

          {calendarLoading && <p className="admin-feedback" aria-live="polite">Učitavanje kalendara...</p>}
          {calendarError && (
            <div className="admin-feedback" role="alert">
              <p>{calendarError}</p>
              <button type="button" onClick={() => setCalendarReloadKey((current) => current + 1)}>
                Pokušaj ponovo
              </button>
            </div>
          )}

          {!calendarLoading && !calendarError && calendarData.courts.length === 0 && (
            <p className="admin-empty">Nema aktivnih terena.</p>
          )}

          {!calendarLoading && !calendarError && calendarData.courts.length > 0 && (
            <>
              <div className="admin-calendar-desktop">
                <div
                  className="admin-calendar-grid"
                  style={{
                    gridTemplateColumns: `76px repeat(${calendarData.courts.length}, minmax(180px, 1fr))`,
                    minWidth: `${76 + calendarData.courts.length * 180}px`,
                  }}
                >
                  <div className="admin-calendar-corner">Vreme</div>
                  {calendarData.courts.map((court, courtIndex) => (
                    <div
                      className="admin-calendar-court-heading"
                      style={{ gridColumn: courtIndex + 2 }}
                      key={court.id}
                    >
                      {court.name}
                    </div>
                  ))}
                  {calendarHours.map((hour, hourIndex) => (
                    <div className="admin-calendar-hour-label" style={{ gridRow: hourIndex + 2 }} key={hour}>
                      {String(hour).padStart(2, "0")}:00
                    </div>
                  ))}
                  {calendarData.courts.flatMap((court, courtIndex) =>
                    calendarHours.map((hour, hourIndex) => (
                      <div
                        className="admin-calendar-cell"
                        style={{ gridColumn: courtIndex + 2, gridRow: hourIndex + 2 }}
                        key={`${court.id}-${hour}`}
                      />
                    )),
                  )}
                  {calendarData.reservations.map((reservation) => {
                    const courtIndex = calendarData.courts.findIndex(
                      (court) => court.id === reservation.courtId,
                    );
                    const startMinutes = Math.max(8 * 60, getWallClockMinutes(reservation.startTime));
                    const endMinutes = Math.min(22 * 60, getWallClockMinutes(reservation.endTime));
                    const rowStart = 2 + Math.floor((startMinutes - 8 * 60) / 60);
                    const rowSpan = Math.max(1, Math.ceil((endMinutes - startMinutes) / 60));

                    return (
                      <button
                        type="button"
                        className="admin-calendar-reservation"
                        style={{
                          gridColumn: courtIndex + 2,
                          gridRow: `${rowStart} / span ${rowSpan}`,
                        }}
                        key={reservation.id}
                        onClick={() => setSelectedCalendarReservation(reservation)}
                      >
                        <strong>{timeFormatter.format(new Date(reservation.startTime))}–{timeFormatter.format(new Date(reservation.endTime))}</strong>
                        <span>{reservation.userName}</span>
                      </button>
                    );
                  })}
                  {(calendarData.blockedPeriods ?? []).map((period) => {
                    const courtIndex = calendarData.courts.findIndex(
                      (court) => court.id === period.courtId,
                    );
                    const startMinutes = Math.max(8 * 60, getWallClockMinutes(period.startTime));
                    const endMinutes = Math.min(22 * 60, getWallClockMinutes(period.endTime));
                    const rowStart = 2 + Math.floor((startMinutes - 8 * 60) / 60);
                    const rowSpan = Math.max(1, Math.ceil((endMinutes - startMinutes) / 60));

                    return (
                      <button
                        type="button"
                        className="admin-calendar-reservation admin-calendar-blocked"
                        style={{
                          gridColumn: courtIndex + 2,
                          gridRow: `${rowStart} / span ${rowSpan}`,
                        }}
                        key={`blocked-${period.id}`}
                        onClick={() => setSelectedBlockedPeriod(period)}
                      >
                        <strong>{timeFormatter.format(new Date(period.startTime))}–{timeFormatter.format(new Date(period.endTime))}</strong>
                        <span>Održavanje · {period.reason}</span>
                      </button>
                    );
                  })}
                </div>
              </div>

              <div className="admin-calendar-mobile">
                {calendarData.courts.map((court) => {
                  const courtReservations = calendarData.reservations.filter(
                    (reservation) => reservation.courtId === court.id,
                  );
                  const courtBlockedPeriods = (calendarData.blockedPeriods ?? []).filter(
                    (period) => period.courtId === court.id,
                  );
                  return (
                    <article className="admin-calendar-court-card" key={court.id}>
                      <h3>{court.name}</h3>
                      {courtReservations.length === 0 && courtBlockedPeriods.length === 0 ? (
                        <p>Slobodan ceo dan</p>
                      ) : null}
                      {courtReservations.map((reservation) => (
                        <button
                          type="button"
                          key={reservation.id}
                          onClick={() => setSelectedCalendarReservation(reservation)}
                        >
                          <strong>{timeFormatter.format(new Date(reservation.startTime))}–{timeFormatter.format(new Date(reservation.endTime))}</strong>
                          <span>{reservation.userName}</span>
                        </button>
                      ))}
                      {courtBlockedPeriods.map((period) => (
                        <button
                          type="button"
                          className="admin-calendar-blocked"
                          key={`blocked-${period.id}`}
                          onClick={() => setSelectedBlockedPeriod(period)}
                        >
                          <strong>{timeFormatter.format(new Date(period.startTime))}–{timeFormatter.format(new Date(period.endTime))}</strong>
                          <span>Održavanje · {period.reason}</span>
                        </button>
                      ))}
                    </article>
                  );
                })}
              </div>
            </>
          )}
        </section>
      )}

      {activeTab === "courts" && (
        <section className="admin-courts-layout">
          <form className="admin-court-form" onSubmit={saveCourt}>
            <div className="admin-section-heading">
              <h2>{editingCourtId ? "Izmeni teren" : "Dodaj teren"}</h2>
            </div>

            <label>
              Naziv
              <input
                name="name"
                value={courtForm.name}
                onChange={handleCourtInput}
                aria-invalid={Boolean(courtFieldErrors.name)}
                required
              />
              {courtFieldErrors.name?.map((message) => (
                <span className="field-error" key={message}>{message}</span>
              ))}
            </label>
            <label>
              Lokacija
              <input
                name="location"
                value={courtForm.location}
                onChange={handleCourtInput}
                aria-invalid={Boolean(courtFieldErrors.location)}
                required
              />
              {courtFieldErrors.location?.map((message) => (
                <span className="field-error" key={message}>{message}</span>
              ))}
            </label>
            <label>
              Opis
              <textarea
                name="description"
                value={courtForm.description}
                onChange={handleCourtInput}
                aria-invalid={Boolean(courtFieldErrors.description)}
                rows="4"
              />
              {courtFieldErrors.description?.map((message) => (
                <span className="field-error" key={message}>{message}</span>
              ))}
            </label>
            <label>
              Cena po satu (RSD)
              <input
                type="number"
                name="pricePerHour"
                min="0.01"
                step="0.01"
                value={courtForm.pricePerHour}
                onChange={handleCourtInput}
                aria-invalid={Boolean(courtFieldErrors.pricePerHour)}
                required
              />
              {courtFieldErrors.pricePerHour?.map((message) => (
                <span className="field-error" key={message}>{message}</span>
              ))}
            </label>

            {!editingCourtId && (
              <label>
                Slika terena
                <input
                  type="file"
                  name="image"
                  accept="image/jpeg,image/png,image/webp,.jpg,.jpeg,.png,.webp"
                  onChange={handleCourtImage}
                  aria-invalid={Boolean(courtFieldErrors.image)}
                />
                <small>JPG, PNG ili WebP, maksimalno 5 MB.</small>
                {courtFieldErrors.image?.map((message) => (
                  <span className="field-error" key={message}>{message}</span>
                ))}
              </label>
            )}

            {courtImagePreview && (
              <div className="court-image-preview">
                <img src={courtImagePreview} alt="Pregled izabrane slike terena" />
              </div>
            )}

            <div className="admin-form-actions">
              <button type="submit" disabled={savingCourt}>
                {savingCourt ? "Čuvanje..." : editingCourtId ? "Sačuvaj izmene" : "Dodaj teren"}
              </button>
              {editingCourtId && (
                <button type="button" className="secondary-button" onClick={resetCourtForm}>
                  Otkaži izmenu
                </button>
              )}
            </div>

            {courtMessage && <p className="admin-action-message success-message" role="status">{courtMessage}</p>}
            {courtWarning && <p className="admin-action-message admin-warning" role="alert">{courtWarning}</p>}
            {courtError && <p className="admin-action-message error-message" role="alert">{courtError}</p>}
          </form>

          <div className="admin-section admin-courts-section">
            <div className="admin-section-heading">
              <h2>Aktivni tereni</h2>
              <span>{courts.length} ukupno</span>
            </div>

            <SectionFeedback
              state={sectionState.courts}
              onRetry={() => retrySection("courts")}
            />
            {!sectionState.courts.loading && !sectionState.courts.error && (
              <div className="admin-courts-list">
                {courts.map((court) => (
                <article className="admin-court-item" key={court.id}>
                  <div>
                    <h3>{court.name}</h3>
                    <p>{court.location}</p>
                    <strong>{priceFormatter.format(court.pricePerHour)} / sat</strong>
                  </div>
                  <div className="admin-court-actions">
                    <button type="button" onClick={() => startEditingCourt(court)}>Izmeni</button>
                    <button
                      type="button"
                      className="danger-button"
                      disabled={deactivatingCourtId !== null}
                      onClick={() => deactivateCourt(court)}
                    >
                      {deactivatingCourtId === court.id ? "Deaktiviranje..." : "Deaktiviraj"}
                    </button>
                  </div>
                </article>
                ))}
              </div>
            )}
            {!sectionState.courts.loading && !sectionState.courts.error && courts.length === 0 && (
              <p className="admin-empty">Nema aktivnih terena.</p>
            )}
          </div>
        </section>
      )}

      {activeTab === "users" && (
        <section className="admin-section">
          <div className="admin-section-heading"><h2>Korisnici</h2><span>{users.length} ukupno</span></div>
          <SectionFeedback
            state={sectionState.users}
            onRetry={() => retrySection("users")}
          />
          {!sectionState.users.loading && !sectionState.users.error && <div className="admin-table-wrapper">
            <table className="admin-table">
              <thead><tr><th>Ime i prezime</th><th>Email</th><th>Uloga</th><th>Datum registracije</th></tr></thead>
              <tbody>
                {users.map((user) => (
                  <tr key={user.id}>
                    <td>{user.firstName} {user.lastName}</td><td>{user.email}</td>
                    <td><span className={`admin-role admin-role-${user.role.toLowerCase()}`}>{user.role}</span></td>
                    <td>{dateFormatter.format(new Date(user.createdAt))}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>}
          {!sectionState.users.loading && !sectionState.users.error && users.length === 0 && <p className="admin-empty">Nema korisnika.</p>}
        </section>
      )}

      {activeTab === "reservations" && (
        <section className="admin-section">
          <div className="admin-section-heading"><h2>Rezervacije</h2><span>{reservations.length} ukupno</span></div>
          <SectionFeedback
            state={sectionState.reservations}
            onRetry={() => retrySection("reservations")}
          />
          {!sectionState.reservations.loading && !sectionState.reservations.error && <div className="admin-table-wrapper">
            <table className="admin-table">
              <thead><tr><th>Korisnik</th><th>Teren</th><th>Datum</th><th>Termin</th><th>Cena</th><th>Status</th></tr></thead>
              <tbody>
                {reservations.map((reservation) => {
                  const startTime = new Date(reservation.startTime);
                  const endTime = new Date(reservation.endTime);
                  return (
                    <tr key={reservation.id}>
                      <td><strong>{reservation.userName}</strong><small>{reservation.userEmail}</small></td>
                      <td>{reservation.courtName}</td><td>{dateFormatter.format(startTime)}</td>
                      <td>{timeFormatter.format(startTime)}–{timeFormatter.format(endTime)}</td>
                      <td>{priceFormatter.format(reservation.totalPrice)}</td>
                      <td><span className={`reservation-status reservation-status-${reservation.status.toLowerCase()}`}>{statusLabels[reservation.status] ?? reservation.status}</span></td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>}
          {!sectionState.reservations.loading && !sectionState.reservations.error && reservations.length === 0 && <p className="admin-empty">Nema rezervacija.</p>}
        </section>
      )}

      {showBlockForm && (
        <div className="reschedule-modal-backdrop" role="presentation">
          <form className="reschedule-modal admin-block-form" onSubmit={saveBlockedPeriod}>
            <div className="reschedule-modal-heading">
              <div>
                <span className="admin-eyebrow">Održavanje</span>
                <h2>Blokiraj termin</h2>
              </div>
              <button type="button" className="reschedule-close-button" aria-label="Zatvori" onClick={() => setShowBlockForm(false)}>×</button>
            </div>

            <label>
              Teren
              <select name="courtId" value={blockForm.courtId} onChange={handleBlockInput} required>
                {calendarData.courts.map((court) => (
                  <option value={court.id} key={court.id}>{court.name}</option>
                ))}
              </select>
              {blockFieldErrors.courtId?.map((message) => <span className="field-error" key={message}>{message}</span>)}
            </label>
            <label>
              Datum
              <input type="date" name="date" value={blockForm.date} onChange={handleBlockInput} required />
            </label>
            <div className="admin-block-time-fields">
              <label>
                Početak
                <select name="startHour" value={blockForm.startHour} onChange={handleBlockInput}>
                  {calendarHours.map((hour) => (
                    <option value={String(hour).padStart(2, "0")} key={hour}>{String(hour).padStart(2, "0")}:00</option>
                  ))}
                </select>
                {blockFieldErrors.startTime?.map((message) => <span className="field-error" key={message}>{message}</span>)}
              </label>
              <label>
                Kraj
                <select name="endHour" value={blockForm.endHour} onChange={handleBlockInput}>
                  {calendarBoundaryHours.slice(1).map((hour) => (
                    <option value={String(hour).padStart(2, "0")} key={hour}>{String(hour).padStart(2, "0")}:00</option>
                  ))}
                </select>
                {blockFieldErrors.endTime?.map((message) => <span className="field-error" key={message}>{message}</span>)}
              </label>
            </div>
            <label>
              Razlog
              <textarea name="reason" value={blockForm.reason} onChange={handleBlockInput} maxLength="300" rows="4" required />
              {blockFieldErrors.reason?.map((message) => <span className="field-error" key={message}>{message}</span>)}
            </label>

            {blockError && <p className="error-message" role="alert">{blockError}</p>}
            <div className="reschedule-modal-actions">
              <button type="button" onClick={() => setShowBlockForm(false)}>Otkaži</button>
              <button type="submit" className="primary-button" disabled={savingBlock}>
                {savingBlock ? "Blokiranje..." : "Blokiraj termin"}
              </button>
            </div>
          </form>
        </div>
      )}

      {selectedBlockedPeriod && (
        <div className="reschedule-modal-backdrop" role="presentation">
          <section className="reschedule-modal" role="dialog" aria-modal="true" aria-labelledby="blocked-period-title">
            <div className="reschedule-modal-heading">
              <div>
                <span className="admin-eyebrow">Održavanje</span>
                <h2 id="blocked-period-title">Blokirani termin</h2>
              </div>
              <button type="button" className="reschedule-close-button" aria-label="Zatvori" onClick={() => { setSelectedBlockedPeriod(null); setBlockError(""); }}>×</button>
            </div>
            <dl className="admin-calendar-details">
              <div><dt>Teren</dt><dd>{selectedBlockedPeriod.courtName}</dd></div>
              <div><dt>Datum</dt><dd>{dateFormatter.format(new Date(selectedBlockedPeriod.startTime))}</dd></div>
              <div><dt>Vreme</dt><dd>{timeFormatter.format(new Date(selectedBlockedPeriod.startTime))}–{timeFormatter.format(new Date(selectedBlockedPeriod.endTime))}</dd></div>
              <div><dt>Razlog</dt><dd>{selectedBlockedPeriod.reason}</dd></div>
            </dl>
            {blockError && <p className="error-message" role="alert">{blockError}</p>}
            <div className="reschedule-modal-actions">
              <button type="button" onClick={() => setSelectedBlockedPeriod(null)}>Zatvori</button>
              <button type="button" className="danger-button" disabled={deletingBlock} onClick={deleteBlockedPeriod}>
                {deletingBlock ? "Odblokiranje..." : "Odblokiraj termin"}
              </button>
            </div>
          </section>
        </div>
      )}

      {selectedCalendarReservation && (
        <div
          className="reschedule-modal-backdrop"
          role="presentation"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) setSelectedCalendarReservation(null);
          }}
        >
          <section className="reschedule-modal admin-calendar-modal" role="dialog" aria-modal="true" aria-labelledby="calendar-reservation-title">
            <div className="reschedule-modal-heading">
              <div>
                <span className="admin-eyebrow">Rezervacija #{selectedCalendarReservation.id}</span>
                <h2 id="calendar-reservation-title">Detalji termina</h2>
              </div>
              <button type="button" className="reschedule-close-button" aria-label="Zatvori" onClick={() => setSelectedCalendarReservation(null)}>×</button>
            </div>
            <dl className="admin-calendar-details">
              <div><dt>Korisnik</dt><dd>{selectedCalendarReservation.userName}</dd></div>
              <div><dt>Email</dt><dd>{selectedCalendarReservation.userEmail}</dd></div>
              <div><dt>Teren</dt><dd>{selectedCalendarReservation.courtName}</dd></div>
              <div><dt>Datum</dt><dd>{dateFormatter.format(new Date(selectedCalendarReservation.startTime))}</dd></div>
              <div><dt>Vreme</dt><dd>{timeFormatter.format(new Date(selectedCalendarReservation.startTime))}–{timeFormatter.format(new Date(selectedCalendarReservation.endTime))}</dd></div>
              <div><dt>Trajanje</dt><dd>{(getWallClockMinutes(selectedCalendarReservation.endTime) - getWallClockMinutes(selectedCalendarReservation.startTime)) / 60} h</dd></div>
              <div><dt>Cena</dt><dd>{priceFormatter.format(selectedCalendarReservation.totalPrice)}</dd></div>
              <div><dt>Status</dt><dd>{statusLabels[selectedCalendarReservation.status] ?? selectedCalendarReservation.status}</dd></div>
            </dl>
          </section>
        </div>
      )}
    </section>
  );
}

export default AdminDashboard;
