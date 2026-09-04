import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import api from "../api/api";

const priceFormatter = new Intl.NumberFormat("sr-RS", {
  style: "currency",
  currency: "RSD",
  maximumFractionDigits: 2,
});

function Courts() {
  const [courts, setCourts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let ignoreResponse = false;

    api
      .get("/courts")
      .then((response) => {
        if (!ignoreResponse) {
          setCourts(response.data);
          setError("");
        }
      })
      .catch((requestError) => {
        if (ignoreResponse) return;

        console.error(requestError);
        setError("Terene trenutno nije moguće učitati.");
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

  if (loading) {
    return (
      <section className="page courts-page" aria-live="polite">
        <h1>Padel tereni</h1>
        <p className="courts-feedback">Učitavanje terena...</p>
      </section>
    );
  }

  if (error) {
    return (
      <section className="page courts-page">
        <h1>Padel tereni</h1>
        <div className="courts-feedback" role="alert">
          <p>{error}</p>
          <button type="button" onClick={retryLoading}>Pokušaj ponovo</button>
        </div>
      </section>
    );
  }

  return (
    <section className="page courts-page">
      <header className="courts-header">
        <h1>Padel tereni</h1>
        <p>Izaberi teren, datum i slobodan termin.</p>
      </header>

      {courts.length === 0 ? (
        <p className="courts-feedback">Trenutno nema dostupnih terena.</p>
      ) : (
        <div className="courts-grid">
          {courts.map((court) => (
            <article className="court-card" key={court.id}>
              <h2>{court.name}</h2>
              <p><strong>Lokacija:</strong> {court.location}</p>
              {court.description && <p>{court.description}</p>}
              <p className="price">{priceFormatter.format(court.pricePerHour)} / sat</p>
              <Link to={`/courts/${court.id}`} className="court-booking-link">
                Izaberi termin
              </Link>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

export default Courts;
