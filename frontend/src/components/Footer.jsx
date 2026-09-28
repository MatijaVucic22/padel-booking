import { Link } from "react-router-dom";
import { useSelector } from "react-redux";
import { getRoleUx } from "../utils/roleUx";

function Footer() {
  const user = useSelector((state) => state.auth.user);
  const roleUx = getRoleUx(user);

  return (
    <footer className="app-footer">
      <div className="footer-content">
        <div className="footer-main">
          <section className="footer-brand" aria-label="PadelBooking">
            <Link className="footer-logo" to="/">PADELBOOKING</Link>
            <p>Tvoj teren. Tvoj termin.<br />Padel bez komplikacija.</p>
            <span className="footer-location">Niš, Srbija</span>
          </section>
          <nav className="footer-section" aria-label="Brzi linkovi">
            <h2>Brzi linkovi</h2>
            <Link to="/">Početna</Link><Link to="/courts">Tereni</Link>
            {roleUx.showBookingCta && <Link to="/book">Rezerviši</Link>}
            {roleUx.showMyReservations && <Link to="/my-reservations">Moje rezervacije</Link>}
            {roleUx.showAdminPanel && <Link to="/admin">Admin panel</Link>}
          </nav>
          <section className="footer-section">
            <h2>Radno vreme</h2><p>Pon–Ned</p><strong>08:00–22:00</strong>
          </section>
        </div>
        <div className="footer-bottom">
          <span>© 2026 PadelBooking</span>
          <button type="button" onClick={() => window.scrollTo({ top: 0, behavior: "smooth" })}>Nazad na vrh ↑</button>
        </div>
      </div>
    </footer>
  );
}

export default Footer;
