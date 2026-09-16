import { Link } from "react-router-dom";

function PaymentCancel() {
  return (
    <section className="page payment-result-page">
      <div className="payment-result-card">
        <span className="section-kicker">Stripe test plaćanje</span>
        <h1>Plaćanje je prekinuto</h1>
        <p>Rezervacija nije potvrđena. Privremeno zadržan termin biće oslobođen kada Checkout sesija istekne.</p>
        <Link className="primary-button" to="/book">Nazad na rezervaciju</Link>
      </div>
    </section>
  );
}

export default PaymentCancel;
