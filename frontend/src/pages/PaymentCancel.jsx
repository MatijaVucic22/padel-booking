import { Link } from "react-router-dom";

function PaymentCancel() {
  return (
    <section className="page payment-result-page">
      <div className="payment-result-card">
        <span className="section-kicker">Plaćanje karticom</span>
        <h1>Plaćanje je prekinuto</h1>
        <p>Uplata nije potvrđena. Privremeno zadržan termin biće oslobođen kada sesija plaćanja istekne; postojeća rezervacija ostaje nepromenjena.</p>
        <Link className="primary-button" to="/book">Nazad na rezervaciju</Link>
      </div>
    </section>
  );
}

export default PaymentCancel;
