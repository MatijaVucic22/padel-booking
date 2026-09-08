import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";

function Navbar({ user, onLogout }) {
  const navigate = useNavigate();
  const [menuOpen, setMenuOpen] = useState(false);
  const [theme, setTheme] = useState(
    () => document.documentElement.dataset.theme || "light",
  );

  useEffect(() => {
    const mediaQuery = window.matchMedia("(prefers-color-scheme: dark)");

    const handleSystemThemeChange = (event) => {
      if (!localStorage.getItem("theme")) {
        const systemTheme = event.matches ? "dark" : "light";
        document.documentElement.dataset.theme = systemTheme;
        setTheme(systemTheme);
      }
    };

    mediaQuery.addEventListener("change", handleSystemThemeChange);
    return () => mediaQuery.removeEventListener("change", handleSystemThemeChange);
  }, []);

  const closeMenu = () => setMenuOpen(false);

  const handleLogout = () => {
    setMenuOpen(false);
    onLogout();
    navigate("/");
  };

  const toggleTheme = () => {
    const nextTheme = theme === "dark" ? "light" : "dark";
    document.documentElement.dataset.theme = nextTheme;
    localStorage.setItem("theme", nextTheme);
    setTheme(nextTheme);
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
        )}

          <button
            type="button"
            className="theme-toggle"
            aria-label={theme === "dark" ? "Uključi svetlu temu" : "Uključi tamnu temu"}
            title={theme === "dark" ? "Svetla tema" : "Tamna tema"}
            onClick={toggleTheme}
          >
            <span aria-hidden="true">{theme === "dark" ? "☀" : "☾"}</span>
          </button>
        </div>
      </div>
    </nav>
  );
}

export default Navbar;
