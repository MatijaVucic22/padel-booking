import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useSelector } from "react-redux";

function Navbar({ onLogout, onNavigate }) {
  const user = useSelector((state) => state.auth.user);
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

  const handleNavigation = () => closeMenu();

  const handleLogout = () => {
    onNavigate("/", () => {
      onLogout();
      closeMenu();
    });
  };

  const toggleTheme = () => {
    const nextTheme = theme === "dark" ? "light" : "dark";
    document.documentElement.dataset.theme = nextTheme;
    localStorage.setItem("theme", nextTheme);
    setTheme(nextTheme);
  };

  return (
    <nav className="navbar">
      <Link to="/" className="logo" onClick={(event) => handleNavigation(event, "/")}>
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
      </button>

      <div
        id="main-navigation"
        className={`nav-links${menuOpen ? " open" : ""}`}
      >
        <div className="nav-primary">
          <Link to="/" onClick={(event) => handleNavigation(event, "/")}>Početna</Link>
          <Link to="/courts" onClick={(event) => handleNavigation(event, "/courts")}>Tereni</Link>
          <Link to="/book" onClick={(event) => handleNavigation(event, "/book")}>Rezerviši</Link>

          {user && <Link to="/my-reservations" onClick={(event) => handleNavigation(event, "/my-reservations")}>Moje rezervacije</Link>}
          {user?.role === "Admin" && <Link to="/admin" onClick={(event) => handleNavigation(event, "/admin")}>Admin</Link>}
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
            <Link to="/login" onClick={(event) => handleNavigation(event, "/login")}>Prijava</Link>
            <Link to="/register" onClick={(event) => handleNavigation(event, "/register")}>Registracija</Link>
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
