import { useEffect } from "react";
import { Link, useParams } from "react-router-dom";
import * as signalR from "@microsoft/signalr";
import { courtAvailabilityHubUrl, getBackendAssetUrl } from "../api/api";
import { getCourtImage } from "../utils/courtImages";
import { useGetCourtByIdQuery } from "../services/padelApi";

const priceFormatter = new Intl.NumberFormat("sr-Latn-RS", {
  style: "currency", currency: "RSD", maximumFractionDigits: 2,
});

function CourtDetails() {
  const { id } = useParams();
  const {
    data: court,
    isLoading,
    isFetching,
    error,
    refetch,
  } = useGetCourtByIdQuery(id, { refetchOnMountOrArgChange: true });

  useEffect(() => {
    let disposed = false;
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(courtAvailabilityHubUrl)
      .withAutomaticReconnect()
      .build();
    const refreshCourt = (change) => {
      if (!disposed && Number(change?.courtId) === Number(id)) {
        refetch();
      }
    };

    connection.on("CourtChanged", refreshCourt);
    connection.start().catch((connectionError) => {
      if (!disposed) console.error(connectionError);
    });

    return () => {
      disposed = true;
      connection.off("CourtChanged", refreshCourt);
      connection.stop();
    };
  }, [id, refetch]);

  if (isLoading || isFetching) return <section className="page court-details-page"><div className="state-card">Učitavanje terena...</div></section>;

  if (error || !court) {
    return (
      <section className="page court-details-page">
        <div className="state-card error-message">{error?.data?.message || "Podaci o terenu trenutno nisu dostupni."}</div>
        <Link className="text-link court-details-back" to="/courts">← Svi tereni</Link>
      </section>
    );
  }

  const courtImage = getBackendAssetUrl(court.imageUrl) || getCourtImage(court);
  const isActive = court.isActive !== false;

  return (
    <section className="page court-details-page">
      <Link className="text-link court-details-back" to="/courts">← Svi tereni</Link>
      <div className="court-information-layout">
        <div className="court-information-image-wrap">
          <img className="court-information-image" src={courtImage} alt={court.name} />
        </div>
        <article className="court-information-card">
          <span className="eyebrow">Detalji terena</span>
          <div className={`court-status ${isActive ? "is-active" : "is-inactive"}`}>
            <span aria-hidden="true" />
            {isActive ? "Aktivan teren" : "Neaktivan teren"}
          </div>
          <h1>{court.name}</h1>
          <p className="court-information-location"><span aria-hidden="true">⌖</span>{court.location}</p>
          <div className="court-information-price">
            <span>Cena po satu</span>
            <strong>{priceFormatter.format(court.pricePerHour)}</strong>
          </div>
          {court.description && <p className="court-information-description">{court.description}</p>}
          <Link className="primary-button court-information-cta" to="/book">Rezerviši termin</Link>
        </article>
      </div>
    </section>
  );
}

export default CourtDetails;
