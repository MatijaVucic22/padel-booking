import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import * as signalR from "@microsoft/signalr";
import api, { courtAvailabilityHubUrl, getBackendAssetUrl } from "../api/api";
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

  useEffect(() => {
    let disposed = false;
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(courtAvailabilityHubUrl)
      .withAutomaticReconnect()
      .build();
    const refreshCourts = () => {
      if (!disposed) setReloadKey((currentKey) => currentKey + 1);
    };

    connection.on("CourtChanged", refreshCourts);
    connection.start().catch((connectionError) => {
      if (!disposed) console.error(connectionError);
    });

    return () => {
      disposed = true;
      connection.off("CourtChanged", refreshCourts);
      connection.stop();
    };
  }, []);

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
        <div><span className="section-kicker">Naša ponuda</span><h1>Padel tereni</h1></div>
        <p>Upoznaj svaki teren, ambijent i lokaciju, pa pronađi termin koji ti odgovara.</p>
      </header>

      {courts.length === 0 ? (
        <p className="courts-feedback">Trenutno nema dostupnih terena.</p>
      ) : (
        <div className="courts-grid">
          {courts.map((court, index) => (
            <Link
              to={`/courts/${court.id}`}
              className="court-card"
              key={court.id}
              aria-label={`Otvori detalje terena ${court.name}`}
            >
              <div className="court-card-image">
                <img src={court.imageUrl ? getBackendAssetUrl(court.imageUrl) : getCourtImage(court.id, index)} alt={`${court.name}, padel teren`} loading="lazy" />
              </div>
              <div className="court-card-content">
                <div className="court-card-status"><span aria-hidden="true" /> Dostupan za rezervacije</div>
                <div className="court-card-heading"><div><span>{court.location}</span><h2>{court.name}</h2></div></div>
                <p className="court-description">{court.description || "Detalji o terenu dostupni su na stranici terena."}</p>
                <div className="court-card-price"><span>Cena po satu</span><strong>{priceFormatter.format(court.pricePerHour)}</strong></div>
                <span className="court-card-details">Detalji →</span>
              </div>
            </Link>
          ))}
        </div>
      )}
    </section>
  );
}

export default Courts;
