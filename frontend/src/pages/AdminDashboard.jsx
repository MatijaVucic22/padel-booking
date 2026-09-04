import { useEffect, useState } from "react";
import api from "../api/api";
import {
  hasValidationErrors,
  parseValidationErrors,
} from "../utils/validationErrors";

const dateFormatter = new Intl.DateTimeFormat("sr-RS", {
  day: "2-digit",
  month: "2-digit",
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
  { id: "courts", label: "Tereni" },
  { id: "reservations", label: "Rezervacije" },
  { id: "users", label: "Korisnici" },
];

function AdminDashboard() {
  const [activeTab, setActiveTab] = useState("dashboard");
  const [stats, setStats] = useState(null);
  const [courts, setCourts] = useState([]);
  const [users, setUsers] = useState([]);
  const [reservations, setReservations] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [reloadKey, setReloadKey] = useState(0);
  const [courtForm, setCourtForm] = useState(emptyCourtForm);
  const [editingCourtId, setEditingCourtId] = useState(null);
  const [savingCourt, setSavingCourt] = useState(false);
  const [deactivatingCourtId, setDeactivatingCourtId] = useState(null);
  const [courtMessage, setCourtMessage] = useState("");
  const [courtError, setCourtError] = useState("");
  const [courtFieldErrors, setCourtFieldErrors] = useState({});

  useEffect(() => {
    let ignoreResponse = false;

    Promise.all([
      api.get("/admin/stats"),
      api.get("/courts"),
      api.get("/admin/users"),
      api.get("/admin/reservations"),
    ])
      .then(([statsResponse, courtsResponse, usersResponse, reservationsResponse]) => {
        if (ignoreResponse) return;

        setStats(statsResponse.data);
        setCourts(courtsResponse.data);
        setUsers(usersResponse.data);
        setReservations(reservationsResponse.data);
        setError("");
      })
      .catch((requestError) => {
        if (ignoreResponse) return;

        console.error(requestError);
        setError(
          requestError.response?.status === 403
            ? "Nemaš dozvolu za pristup admin panelu."
            : "Podatke admin panela trenutno nije moguće učitati.",
        );
      })
      .finally(() => {
        if (!ignoreResponse) setLoading(false);
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

  const refreshCourtsAndStats = async () => {
    const [courtsResponse, statsResponse] = await Promise.all([
      api.get("/courts"),
      api.get("/admin/stats"),
    ]);

    setCourts(courtsResponse.data);
    setStats(statsResponse.data);
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
    setCourtFieldErrors({});
  };

  const resetCourtForm = () => {
    setEditingCourtId(null);
    setCourtForm(emptyCourtForm);
    setCourtFieldErrors({});
  };

  const saveCourt = async (event) => {
    event.preventDefault();
    setSavingCourt(true);
    setCourtMessage("");
    setCourtError("");
    setCourtFieldErrors({});

    const payload = {
      ...courtForm,
      pricePerHour: Number(courtForm.pricePerHour),
    };

    try {
      if (editingCourtId) {
        await api.put(`/courts/${editingCourtId}`, payload);
      } else {
        await api.post("/courts", payload);
      }

      await refreshCourtsAndStats();
      setCourtMessage(
        editingCourtId
          ? "Teren je uspešno izmenjen."
          : "Teren je uspešno dodat.",
      );
      resetCourtForm();
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

    try {
      await api.delete(`/courts/${court.id}`);
      await refreshCourtsAndStats();
      setCourtMessage("Teren je uspešno deaktiviran.");

      if (editingCourtId === court.id) resetCourtForm();
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

  if (loading) {
    return (
      <section className="page admin-page" aria-live="polite">
        <h1>Admin panel</h1>
        <p className="admin-feedback">Učitavanje podataka...</p>
      </section>
    );
  }

  if (error) {
    return (
      <section className="page admin-page">
        <h1>Admin panel</h1>
        <div className="admin-feedback" role="alert">
          <p>{error}</p>
          <button type="button" onClick={retryLoading}>Pokušaj ponovo</button>
        </div>
      </section>
    );
  }

  const statCards = [
    { label: "Korisnici", value: stats.totalUsers },
    { label: "Aktivni tereni", value: stats.activeCourts },
    { label: "Sve rezervacije", value: stats.totalReservations },
    { label: "Aktivne rezervacije", value: stats.activeReservations },
    { label: "Prihod", value: priceFormatter.format(stats.totalRevenue) },
  ];

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
        <div className="admin-stats-grid">
          {statCards.map((stat) => (
            <article className="admin-stat-card" key={stat.label}>
              <span>{stat.label}</span>
              <strong>{stat.value}</strong>
            </article>
          ))}
        </div>
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
            {courtError && <p className="admin-action-message error-message" role="alert">{courtError}</p>}
          </form>

          <div className="admin-section admin-courts-section">
            <div className="admin-section-heading">
              <h2>Aktivni tereni</h2>
              <span>{courts.length} ukupno</span>
            </div>

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
            {courts.length === 0 && <p className="admin-empty">Nema aktivnih terena.</p>}
          </div>
        </section>
      )}

      {activeTab === "users" && (
        <section className="admin-section">
          <div className="admin-section-heading"><h2>Korisnici</h2><span>{users.length} ukupno</span></div>
          <div className="admin-table-wrapper">
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
          </div>
          {users.length === 0 && <p className="admin-empty">Nema korisnika.</p>}
        </section>
      )}

      {activeTab === "reservations" && (
        <section className="admin-section">
          <div className="admin-section-heading"><h2>Rezervacije</h2><span>{reservations.length} ukupno</span></div>
          <div className="admin-table-wrapper">
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
          </div>
          {reservations.length === 0 && <p className="admin-empty">Nema rezervacija.</p>}
        </section>
      )}
    </section>
  );
}

export default AdminDashboard;
