import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import api, { getBackendAssetUrl } from "../api/api";
import { getCourtImage } from "../utils/courtImages";

const priceFormatter = new Intl.NumberFormat("sr-Latn-RS", {
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
          {courts.map((court, index) => (
            <article className="court-card" key={court.id}>
              <Link to={`/courts/${court.id}`} className="court-card-image" aria-label={`Otvori teren ${court.name}`}>
                <img src={court.imageUrl ? getBackendAssetUrl(court.imageUrl) : getCourtImage(court.id, index)} alt={`${court.name}, padel teren`} loading="lazy" />
              </Link>
              <div className="court-card-content">
                <div className="court-card-heading"><div><span>{court.location}</span><h2>{court.name}</h2></div><span className="court-card-arrow" aria-hidden="true">↗</span></div>
                {court.description && <p className="court-description">{court.description}</p>}
                <div className="court-card-footer"><p className="price"><strong>{priceFormatter.format(court.pricePerHour)}</strong><span>/ sat</span></p><Link to={`/courts/${court.id}`} className="court-booking-link">Izaberi termin</Link></div>
              </div>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

export default Courts;
