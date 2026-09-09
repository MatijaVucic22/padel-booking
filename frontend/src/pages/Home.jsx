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

const padelRules = [
  "Igra se uglavnom dva na dva",
  "Servis je ispod visine struka i dijagonalno",
  "Bodovanje je slično tenisu",
  "Lopta sme jednom da odskoči",
  "Staklo i zidovi mogu da se koriste nakon odskoka",
  "Meč se najčešće igra na dva dobijena seta",
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
            <Link to="/book" className="primary-button">Rezerviši teren <span aria-hidden="true">→</span></Link>
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
            <Link className="featured-court" to="/courts" key={court.title}>
              <div className="featured-image"><img src={court.image} alt={`${court.title} padel teren`} loading="lazy" /></div>
              <div><span>{court.meta}</span><h3>{court.title}</h3></div>
              <span className="featured-details-link">Detalji →</span>
            </Link>
          ))}
        </div>
      </section>

      <section className="home-section process-section" aria-labelledby="process-title">
        <header><span className="section-kicker">Kako funkcioniše</span><h2 id="process-title">Od ideje do terena u tri koraka.</h2></header>
        <ol className="process-list">
          <li><strong>01</strong><div><h3>Izaberi datum i vreme</h3><p>Odredi termin i trajanje koje odgovara tvojoj ekipi.</p></div></li>
          <li><strong>02</strong><div><h3>Pronađi slobodan teren</h3><p>Odmah vidi terene dostupne za ceo izabrani interval.</p></div></li>
          <li><strong>03</strong><div><h3>Potvrdi rezervaciju</h3><p>Rezerviši u nekoliko klikova i spremi se za meč.</p></div></li>
        </ol>
      </section>

      <section className="home-section padel-intro-section" aria-labelledby="padel-intro-title">
        <div className="padel-intro-image">
          <img src={courtIndoor} alt="Moderan zatvoreni padel teren" loading="lazy" />
        </div>
        <div className="padel-intro-copy">
          <span className="section-kicker">Upoznaj padel</span>
          <h2 id="padel-intro-title">Dinamična igra koja brzo osvaja teren.</h2>
          <p>Padel je nastao u Meksiku 1969. godine i najčešće se igra dva na dva. Spaja elemente tenisa i skvoša, uz jednu posebnost: staklo i zidovi ostaju deo igre nakon što lopta odskoči.</p>
          <Link to="/book" className="text-link">Pronađi slobodan termin <span aria-hidden="true">→</span></Link>
        </div>
      </section>

      <section className="home-section rules-section" aria-labelledby="rules-title">
        <header className="editorial-heading">
          <div><span className="section-kicker">Osnovna pravila</span><h2 id="rules-title">Dovoljno jednostavno za prvi meč.</h2></div>
          <p>Najvažnije smernice koje treba da znaš pre izlaska na teren.</p>
        </header>
        <div className="rules-grid">
          {padelRules.map((rule, index) => (
            <article className="rule-card" key={rule}>
              <span>{String(index + 1).padStart(2, "0")}</span>
              <p>{rule}</p>
            </article>
          ))}
        </div>
      </section>

      <section className="home-section home-cta">
        <img src={courtSunset} alt="Padel teren uz more u vreme zalaska sunca" loading="lazy" />
        <div className="home-cta-content"><span className="section-kicker">Vreme je za igru</span><h2>Spreman za sledeći meč?</h2><Link to="/book" className="primary-button">Rezerviši termin <span aria-hidden="true">→</span></Link></div>
      </section>

    </div>
  );
}

export default Home;
