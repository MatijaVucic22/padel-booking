import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";

function Navbar({ user, onLogout }) {
  const navigate = useNavigate();
  const [menuOpen, setMenuOpen] = useState(false);

  const closeMenu = () => setMenuOpen(false);

  const handleLogout = () => {
    setMenuOpen(false);
    onLogout();
    navigate("/");
  };

  return (
    <nav className="navbar">
      <Link to="/" className="logo" onClick={closeMenu}>
        PADEL<span>BOOKING</span>
      </Link>

      <button
        type="button"
        className="menu-toggle"
        aria-label={menuOpen ? "Zatvori meni" : "Otvori meni"}
        aria-expanded={menuOpen}
        aria-controls="main-navigation"
        onClick={() => setMenuOpen((isOpen) => !isOpen)}
      >
        <span />
        <span />
        <span />
      </button>

      <div
        id="main-navigation"
        className={`nav-links${menuOpen ? " open" : ""}`}
      >
        <div className="nav-primary">
          <Link to="/" onClick={closeMenu}>Početna</Link>
          <Link to="/courts" onClick={closeMenu}>Tereni</Link>

          {user && <Link to="/my-reservations" onClick={closeMenu}>Moje rezervacije</Link>}
          {user?.role === "Admin" && <Link to="/admin" onClick={closeMenu}>Admin</Link>}
        </div>

        <div className="nav-account">{user ? (
          <>
            <span className="user-name">{user.firstName}</span>

            <button className="logout-button" onClick={handleLogout}>
              Odjava
            </button>
          </>
        ) : (
          <>
            <Link to="/login" onClick={closeMenu}>Prijava</Link>
            <Link to="/register" onClick={closeMenu}>Registracija</Link>
          </>
        )}</div>
      </div>
    </nav>
  );
}

export default Navbar;
