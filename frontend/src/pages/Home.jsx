import { Link } from "react-router-dom";
import heroCourt from "../assets/brand/hero-court.jpg";
import featuredCourt from "../assets/brand/featured-court.jpg";
import padelDetail from "../assets/brand/padel-detail.jpg";
import footerCourt from "../assets/brand/footer-court.jpg";
import Reveal from "../components/Reveal";

const steps = [
  { number: "01", title: "Izaberi teren", text: "Pronađi mesto za svoj sledeći meč." },
  { number: "02", title: "Izaberi termin", text: "Odredi datum, vreme i trajanje igre." },
  { number: "03", title: "Igraj", text: "Potvrdi rezervaciju i vidimo se na terenu." },
];

function Home() {
  return (
    <div className="home-page">
      <section className="home-hero" aria-labelledby="home-hero-title">
        <img className="home-hero-image" src={heroCourt} alt="Padel teren pod večernjim nebom" />
        <div className="home-hero-shade" aria-hidden="true" />
        <div className="home-hero-inner">
          <span className="home-index">NIŠ / SRBIJA <span aria-hidden="true">—</span> PADELBOOKING</span>
          <h1 id="home-hero-title">PADEL.<br /><span>BEZ ČEKANJA.</span></h1>
          <p>Rezerviši teren za nekoliko sekundi. Više vremena za igru, manje za dogovaranje.</p>
          <Link to="/book" className="home-action">Rezerviši termin <span aria-hidden="true">↗</span></Link>
        </div>
        <div className="home-hero-foot" aria-hidden="true"><span>TEREN / TERMIN / MEČ</span><span>08:00 — 22:00</span></div>
      </section>

      <Reveal as="section" className="home-ball-stage" aria-labelledby="home-ball-stage-title">
        <span className="home-ball-stage-label" aria-hidden="true">PADELBOOKING / IGRA POČINJE OVDE</span>
        <span className="home-ball-stage-rule" aria-hidden="true" />
        <span className="home-ball-track" aria-hidden="true"><span className="home-ball-stage-ball" /></span>
        <h2 id="home-ball-stage-title">TVOJ TEREN. TVOJ TERMIN.</h2>
      </Reveal>

      <Reveal as="section" className="home-section home-featured" aria-labelledby="home-featured-title">
        <header className="home-ball-heading">
          <span className="home-section-index">01 / TEREN</span>
          <h2 id="home-featured-title">Teren za svaki meč.</h2>
        </header>
        <div className="home-featured-layout">
          <div className="home-featured-image"><img src={featuredCourt} alt="Padel teren okružen staklenim zidovima" loading="lazy" /></div>
          <div className="home-featured-copy">
            <span className="home-small-rule" aria-hidden="true" />
            <h3>Pravo mesto.<br />Pravi trenutak.</h3>
            <p>Pregledaj aktivne terene i pronađi onaj na kome ćeš odigrati sledeći meč.</p>
            <Link to="/courts" className="home-text-action">Pogledaj sve terene <span aria-hidden="true">↗</span></Link>
          </div>
        </div>
      </Reveal>

      <Reveal as="section" className="home-section home-editorial" aria-labelledby="home-editorial-title">
        <div className="home-editorial-copy">
          <span className="home-section-index">02 / IGRA</span>
          <h2 id="home-editorial-title">NOVA IGRA.<br /><span>POZNAT OSEĆAJ.</span></h2>
          <p>Padel spaja energiju tenisa i dinamiku skvoša. Igra se najčešće dva na dva, a staklo i zidovi ostaju deo poena nakon što lopta odskoči.</p>
          <span className="home-editorial-note">Jedan teren. Bezbroj dobrih poena.</span>
        </div>
        <div className="home-editorial-image"><img src={padelDetail} alt="Padel reket i loptica na liniji terena" loading="lazy" /></div>
      </Reveal>

      <Reveal as="section" className="home-section home-process" aria-labelledby="home-process-title">
        <div className="home-process-heading"><span className="home-section-index">03 / KAKO FUNKCIONIŠE</span><h2 id="home-process-title">OD IZBORA<br />DO PRVOG POENA.</h2></div>
        <ol className="home-process-list">
          {steps.map((step) => (
            <li key={step.number}>
              <span className="home-step-number">{step.number}</span>
              <h3>{step.title}</h3>
              <p>{step.text}</p>
            </li>
          ))}
        </ol>
      </Reveal>

      <Reveal as="section" className="home-section home-final" aria-labelledby="home-final-title">
        <img src={footerCourt} alt="Padel tereni spremni za igru" loading="lazy" />
        <div className="home-final-shade" aria-hidden="true" />
        <div className="home-final-content">
          <span className="home-section-index">04 / TVOJ SLEDEĆI MEČ</span>
          <h2 id="home-final-title">VIDIMO SE<br />NA TERENU.</h2>
          <Link to="/book" className="home-action">Rezerviši termin <span aria-hidden="true">↗</span></Link>
        </div>
      </Reveal>
    </div>
  );
}

export default Home;
