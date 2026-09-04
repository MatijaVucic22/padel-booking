import { Link } from "react-router-dom";

function Home() {
  return (
    <div className="home">
      <section className="hero">
        <h1>Rezerviši padel teren brzo i jednostavno</h1>

        <p>
          Pogledaj dostupne terene, izaberi termin i napravi rezervaciju
          za nekoliko sekundi.
        </p>

        <Link to="/courts" className="primary-button">
          Pogledaj terene
        </Link>
      </section>
    </div>
  );
}

export default Home;