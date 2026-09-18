import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useGetCheckoutStatusQuery } from "../services/padelApi";

function PaymentSuccess() {
  const [searchParams] = useSearchParams();
  const sessionId = searchParams.get("session_id");
  const [polling, setPolling] = useState(true);
  const [timedOut, setTimedOut] = useState(false);
  const { data, error, isLoading, refetch } = useGetCheckoutStatusQuery(sessionId, {
    skip: !sessionId,
    pollingInterval: sessionId && polling ? 2000 : 0,
  });

  useEffect(() => {
    setPolling(true);
    setTimedOut(false);
  }, [sessionId]);

  useEffect(() => {
    if (!sessionId || !polling) return undefined;
    const timer = window.setTimeout(() => {
      setTimedOut(true);
      setPolling(false);
    }, 30_000);
    return () => window.clearTimeout(timer);
  }, [sessionId, polling]);

  useEffect(() => {
    if (data?.status && data.status !== "Pending") setPolling(false);
  }, [data?.status]);

  const retry = () => {
    setTimedOut(false);
    setPolling(true);
    refetch();
  };

  const confirmed = data?.confirmed === true;
  const requiresResolution = data?.status === "Paid" &&
    data?.fulfillmentStatus === "RequiresResolution";
  const failed = data?.status === "Failed" || data?.status === "Cancelled";
  const noLongerActive = data?.status === "Paid" && !confirmed && !requiresResolution;

  return (
    <section className="page payment-result-page">
      <div className="payment-result-card" role="status">
        <span className="section-kicker">Stripe test plaćanje</span>
        {confirmed ? (
          <>
            <span className="booking-confirm-check" aria-hidden="true">✓</span>
            <h1>{data?.purpose === "RescheduleTopUp" ? "Termin je uspešno promenjen!" : "Rezervacija je potvrđena!"}</h1>
            <p>{data?.purpose === "RescheduleTopUp"
              ? "Doplata je potvrđena i novi termin je dodat u tvoje rezervacije."
              : "Uplata je potvrđena i termin je dodat u tvoje rezervacije."}</p>
            <Link className="primary-button" to="/my-reservations">Moje rezervacije</Link>
          </>
        ) : requiresResolution ? (
          <>
            <h1>Plaćanje zahteva proveru</h1>
            <p>Plaćanje je evidentirano, ali {data?.purpose === "RescheduleTopUp"
              ? "promena termina" : "rezervacija"} nije mogla automatski da bude završena. Potrebna je provera.</p>
            <p>Novac nije automatski refundiran. Obrati se podršci i sačuvaj broj rezervacije #{data.reservationId}.</p>
            <Link className="primary-button" to="/my-reservations">Moje rezervacije</Link>
          </>
        ) : noLongerActive ? (
          <>
            <h1>Rezervacija više nije aktivna</h1>
            <p>Plaćanje je potvrđeno, ali rezervacija više nije aktivna. Proveri svoje rezervacije.</p>
            <Link className="primary-button" to="/my-reservations">Moje rezervacije</Link>
          </>
        ) : failed ? (
          <>
            <h1>Plaćanje nije uspelo</h1>
            <p>Rezervacija nije potvrđena. Izaberi drugi termin ili pokušaj ponovo.</p>
            <Link className="primary-button" to="/book">Nazad na rezervaciju</Link>
          </>
        ) : !sessionId ? (
          <>
            <h1>Nedostaje Checkout sesija</h1>
            <p>Status plaćanja nije moguće proveriti bez Stripe sesije.</p>
            <Link className="primary-button" to="/book">Nazad na rezervaciju</Link>
          </>
        ) : timedOut ? (
          <>
            <h1>Potvrda plaćanja kasni</h1>
            <p>Rezervacija još nije potvrđena. Proveri status ponovo za trenutak.</p>
            <button type="button" onClick={retry}>Proveri ponovo</button>
          </>
        ) : (
          <>
            <span className="booking-confirm-spinner" aria-hidden="true" />
            <h1>Proveravamo plaćanje...</h1>
            <p>{error && !isLoading
              ? "Trenutno ne možemo da proverimo status. Pokušaj ponovo."
              : "Čekamo potvrdu od Stripe-a. Ne zatvaraj ovu stranicu."}</p>
            {error && <button type="button" onClick={retry}>Pokušaj ponovo</button>}
          </>
        )}
      </div>
    </section>
  );
}

export default PaymentSuccess;
