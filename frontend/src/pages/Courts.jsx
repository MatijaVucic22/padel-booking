import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import * as signalR from "@microsoft/signalr";
import { courtAvailabilityHubUrl, getBackendAssetUrl } from "../api/api";
import { getCourtImage } from "../utils/courtImages";
import { useGetCourtsQuery } from "../services/padelApi";
import Reveal from "../components/Reveal";

const priceFormatter = new Intl.NumberFormat("sr-Latn-RS", {
  style: "currency",
  currency: "RSD",
  maximumFractionDigits: 2,
});

function Courts() {
  const [location, setLocation] = useState("");
  const [debouncedLocation, setDebouncedLocation] = useState("");
  const {
    data: courts = [],
    isLoading,
    isFetching,
    isError,
    refetch,
  } = useGetCourtsQuery(
    { location: debouncedLocation },
    { refetchOnMountOrArgChange: true },
  );

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedLocation(location.trim());
    }, 300);

    return () => window.clearTimeout(timer);
  }, [location]);

  useEffect(() => {
    let disposed = false;
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(courtAvailabilityHubUrl)
      .withAutomaticReconnect()
      .build();
    const refreshCourts = () => {
      if (!disposed) refetch();
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
  }, [refetch]);

  const retryLoading = () => refetch();

  if (isLoading && courts.length === 0) {
    return (
      <section className="page courts-page" aria-live="polite">
        <h1>Padel tereni</h1>
        <p className="courts-feedback">Učitavanje terena...</p>
      </section>
    );
  }

  if (isError) {
    return (
      <section className="page courts-page">
        <h1>Padel tereni</h1>
        <div className="courts-feedback" role="alert">
          <p>Terene trenutno nije moguće učitati.</p>
          <button type="button" onClick={retryLoading}>Pokušaj ponovo</button>
        </div>
      </section>
    );
  }

  return (
    <section className="page courts-page">
      <Reveal as="header" className="courts-header">
        <div><span className="section-kicker">Naša ponuda</span><h1>Padel tereni</h1></div>
        <div className="courts-intro">
          <p>Upoznaj svaki teren, ambijent i lokaciju, pa pronađi termin koji ti odgovara.</p>
          <Link to="/book" className="courts-book-link">Rezerviši termin <span aria-hidden="true">↗</span></Link>
        </div>
      </Reveal>

      <div className="courts-location-filter">
        <label htmlFor="court-location-filter">Lokacija</label>
        <div className="courts-location-control">
          <input
            id="court-location-filter"
            type="search"
            value={location}
            placeholder="Pretraži grad ili lokaciju"
            onChange={(event) => setLocation(event.target.value)}
            aria-controls="courts-results"
          />
          {location && (
            <button type="button" onClick={() => setLocation("")}>
              Obriši
            </button>
          )}
        </div>
        {isFetching && <span className="courts-filter-status" role="status">Osvežavanje terena...</span>}
      </div>

      {courts.length === 0 ? (
        debouncedLocation ? (
          <div className="courts-feedback courts-filter-empty">
            <p>Nema terena koji odgovaraju unetoj lokaciji.</p>
            <button type="button" onClick={() => setLocation("")}>Prikaži sve terene</button>
          </div>
        ) : (
          <p className="courts-feedback">Trenutno nema dostupnih terena.</p>
        )
      ) : (
        <div className="courts-grid" id="courts-results" aria-busy={isFetching}>
          {courts.map((court, index) => (
            <Reveal
              as={Link}
              to={`/courts/${court.id}`}
              className="court-card"
              key={court.id}
              delay={(index % 3) * 80}
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
            </Reveal>
          ))}
        </div>
      )}
    </section>
  );
}

export default Courts;
