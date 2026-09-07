import { Link } from "react-router-dom";
import heroPadel from "../assets/images/hero-padel.jpg";
import courtIndoor from "../assets/images/court-indoor.jpg";
import courtNight from "../assets/images/court-night.jpg";
import courtSunset from "../assets/images/court-sunset.jpg";

const featuredCourts = [
  { title: "Indoor arena", meta: "Celogodišnja igra", image: courtIndoor },
  { title: "Sunset court", meta: "Meč na otvorenom", image: courtSunset },
  { title: "Night court", meta: "Večernji termini", image: courtNight },
];

function Home() {
  return (
    <div className="home-page">
      <section className="home-hero">
        <div className="home-hero-copy">
          <span className="section-kicker">Premium urban padel</span>
          <h1>Rezerviši teren.<br />Igraj bez čekanja.</h1>
          <p>Izaberi teren, pronađi slobodan termin i rezerviši za manje od minut.</p>
          <div className="hero-actions">
            <Link to="/courts" className="primary-button">Rezerviši teren <span aria-hidden="true">→</span></Link>
            <Link to="/courts" className="text-link">Pogledaj terene</Link>
          </div>
        </div>
        <div className="home-hero-media">
          <img src={heroPadel} alt="Igrači na padel terenima u večernjem svetlu" />
          <div className="hero-note">
            <span>Rezervacija u tri koraka</span>
            <strong>Teren. Termin. Meč.</strong>
          </div>
        </div>
      </section>

      <section className="home-section featured-section">
        <header className="editorial-heading">
          <div><span className="section-kicker">Naši tereni</span><h2>Pronađi teren za sledeći meč</h2></div>
          <Link to="/courts" className="text-link">Svi tereni <span aria-hidden="true">→</span></Link>
        </header>
        <div className="featured-grid">
          {featuredCourts.map((court) => (
            <Link to="/courts" className="featured-court" key={court.title}>
              <div className="featured-image"><img src={court.image} alt={`${court.title} padel teren`} loading="lazy" /></div>
              <div><span>{court.meta}</span><h3>{court.title}</h3></div>
              <span className="featured-arrow" aria-hidden="true">↗</span>
            </Link>
          ))}
        </div>
      </section>

      <section className="home-section process-section">
        <header><span className="section-kicker">Od terena do meča za manje od minut</span><h2>Jednostavno. Kako igra i treba da bude.</h2></header>
        <ol className="process-list">
          <li><strong>01</strong><div><h3>Izaberi teren</h3><p>Pronađi ambijent koji odgovara tvom meču.</p></div></li>
          <li><strong>02</strong><div><h3>Odaberi termin</h3><p>Pregledaj slobodne slotove u realnom vremenu.</p></div></li>
          <li><strong>03</strong><div><h3>Rezerviši i igraj</h3><p>Potvrdi rezervaciju i pojavi se spreman za teren.</p></div></li>
        </ol>
      </section>

      <section className="home-section home-cta">
        <img src={courtSunset} alt="Padel teren uz more u vreme zalaska sunca" loading="lazy" />
        <div className="home-cta-content"><span className="section-kicker">Vreme je za igru</span><h2>Tvoj sledeći meč počinje ovde.</h2><Link to="/courts" className="primary-button">Pronađi termin <span aria-hidden="true">→</span></Link></div>
      </section>
    </div>
  );
}

export default Home;
