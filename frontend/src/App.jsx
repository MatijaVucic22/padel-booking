import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { Routes, Route, useLocation, useNavigate } from "react-router-dom";
import api from "./api/api";
import Navbar from "./components/Navbar";
import ProtectedRoute from "./components/ProtectedRoute";
import Home from "./pages/Home";
import Courts from "./pages/Courts";
import CourtDetails from "./pages/CourtDetails";
import Login from "./pages/Login";
import Register from "./pages/Register";
import "./App.css";
import MyReservations from "./pages/MyReservations";
import AdminDashboard from "./pages/AdminDashboard";
import Book from "./pages/Book";

function App() {
  const navigate = useNavigate();
  const location = useLocation();
  const [user, setUser] = useState(null);
  const [sessionLoading, setSessionLoading] = useState(() =>
    Boolean(localStorage.getItem("token")),
  );
  const [routeLoaderMounted, setRouteLoaderMounted] = useState(false);
  const [routeLoaderActive, setRouteLoaderActive] = useState(false);
  const previousPathname = useRef(location.pathname);
  const routeLoaderVisible = useRef(false);
  const routeLoaderHideTimer = useRef(null);
  const routeLoaderUnmountTimer = useRef(null);

  useLayoutEffect(() => {
    if (previousPathname.current === location.pathname) return;
    previousPathname.current = location.pathname;

    [
      routeLoaderHideTimer,
      routeLoaderUnmountTimer,
    ].forEach((timerRef) => {
      if (timerRef.current !== null) {
        window.clearTimeout(timerRef.current);
        timerRef.current = null;
      }
    });

    setRouteLoaderMounted(true);
    routeLoaderVisible.current = true;
    setRouteLoaderActive(true);

    routeLoaderHideTimer.current = window.setTimeout(() => {
      routeLoaderHideTimer.current = null;
      routeLoaderVisible.current = false;
      setRouteLoaderActive(false);

      routeLoaderUnmountTimer.current = window.setTimeout(() => {
        routeLoaderUnmountTimer.current = null;
        setRouteLoaderMounted(false);
      }, 200);
    }, 700);
  }, [location.pathname]);

  useEffect(() => () => {
    [
      routeLoaderHideTimer,
      routeLoaderUnmountTimer,
    ].forEach((timerRef) => {
      if (timerRef.current !== null) window.clearTimeout(timerRef.current);
    });
  }, []);

  const handleLogin = (loggedInUser) => {
    setUser(loggedInUser);
  };

  const handleLogout = () => {
    localStorage.removeItem("token");
    localStorage.removeItem("user");

    setUser(null);
  };

  useEffect(() => {
    const handleUnauthorized = () => {
      setUser(null);
      setSessionLoading(false);
      navigate("/login", {
        replace: true,
        state: { from: `${location.pathname}${location.search}${location.hash}` },
      });
    };

    window.addEventListener("auth:unauthorized", handleUnauthorized);

    return () => {
      window.removeEventListener("auth:unauthorized", handleUnauthorized);
    };
  }, [location.hash, location.pathname, location.search, navigate]);

  useEffect(() => {
    const token = localStorage.getItem("token");
    localStorage.removeItem("user");

    if (!token) return undefined;

    let cancelled = false;

    api.get("/auth/me")
      .then((response) => {
        if (!cancelled) setUser(response.data);
      })
      .catch(() => {
        if (!cancelled) setUser(null);
      })
      .finally(() => {
        if (!cancelled) setSessionLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <>
      <Navbar user={user} onLogout={handleLogout} />

      <main>
        {sessionLoading ? (
          <div className="session-loading" role="status">
            Provera sesije...
          </div>
        ) : (
        <Routes>
          <Route path="/" element={<Home />} />

          <Route path="/courts" element={<Courts />} />

          <Route path="/book" element={<Book />} />

          <Route path="/courts/:id" element={<CourtDetails />} />

          <Route path="/login" element={<Login onLogin={handleLogin} />} />

          <Route path="/register" element={<Register />} />
          
          <Route
            path="/my-reservations"
            element={
              <ProtectedRoute user={user}>
                <MyReservations />
              </ProtectedRoute>
            }
          />

          <Route
            path="/admin"
            element={
              <ProtectedRoute user={user} requiredRole="Admin">
                <AdminDashboard />
              </ProtectedRoute>
            }
          />
        </Routes>
        )}
      </main>

      {routeLoaderMounted && (
        <div
          className={`route-transition-loader${routeLoaderActive ? " is-visible" : ""}`}
          role="status"
          aria-live="polite"
          aria-label="Učitavanje stranice"
        >
          <div className="route-transition-content">
            <span className="route-transition-brand">PadelBooking</span>
            <span className="route-transition-spinner" aria-hidden="true">
              <span />
            </span>
            <span className="route-transition-copy">Učitavanje...</span>
          </div>
        </div>
      )}
    </>
  );
}

export default App;
