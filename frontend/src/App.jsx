import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { Routes, Route, useLocation, useNavigate } from "react-router-dom";
import { flushSync } from "react-dom";
import { useDispatch, useSelector } from "react-redux";
import api from "./api/api";
import { clearAuth, setCredentials } from "./store/authSlice";
import Navbar from "./components/Navbar";
import Footer from "./components/Footer";
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
  const dispatch = useDispatch();
  const { token, user } = useSelector((state) => state.auth);
  const [sessionLoading, setSessionLoading] = useState(() =>
    Boolean(token),
  );
  const [routeLoaderMounted, setRouteLoaderMounted] = useState(false);
  const [routeLoaderActive, setRouteLoaderActive] = useState(false);
  const previousPathname = useRef(location.pathname);
  const routeLoaderVisible = useRef(false);
  const routeLoaderHideTimer = useRef(null);
  const routeLoaderUnmountTimer = useRef(null);
  const routeLoaderShowFrame = useRef(null);
  const routeNavigationTimer = useRef(null);
  const routeTransitionInProgress = useRef(false);
  const scrollLockBeforeLoader = useRef(null);

  const restoreBodyScroll = () => {
    if (scrollLockBeforeLoader.current === null) return;

    const { htmlWasLocked, bodyWasLocked } = scrollLockBeforeLoader.current;
    if (!htmlWasLocked) document.documentElement.classList.remove("route-transition-locked");
    if (!bodyWasLocked) document.body.classList.remove("route-transition-locked");
    scrollLockBeforeLoader.current = null;
  };

  const lockPageScroll = () => {
    if (scrollLockBeforeLoader.current !== null) return;

    scrollLockBeforeLoader.current = {
      htmlWasLocked: document.documentElement.classList.contains("route-transition-locked"),
      bodyWasLocked: document.body.classList.contains("route-transition-locked"),
    };
    document.documentElement.classList.add("route-transition-locked");
    document.body.classList.add("route-transition-locked");
  };

  const handleRouteNavigation = (
    destination,
    beforeNavigation = () => {},
    navigationOptions,
  ) => {
    if (destination === location.pathname) {
      beforeNavigation();
      return;
    }

    if (routeTransitionInProgress.current) return;
    routeTransitionInProgress.current = true;

    if (routeLoaderShowFrame.current !== null) {
      window.cancelAnimationFrame(routeLoaderShowFrame.current);
      routeLoaderShowFrame.current = null;
    }

    lockPageScroll();
    flushSync(() => {
      setRouteLoaderMounted(true);
      setRouteLoaderActive(false);
    });

    const loaderElement = document.querySelector(".route-transition-loader");
    if (loaderElement) loaderElement.getBoundingClientRect();

    flushSync(() => {
      setRouteLoaderActive(true);
    });

    routeNavigationTimer.current = window.setTimeout(() => {
      routeNavigationTimer.current = null;
      flushSync(() => beforeNavigation());
      navigate(destination, navigationOptions);
    }, 200);
  };

  useEffect(() => {
    const handleInternalLinkClick = (event) => {
      if (
        event.defaultPrevented ||
        event.button !== 0 ||
        event.metaKey ||
        event.ctrlKey ||
        event.shiftKey ||
        event.altKey ||
        !(event.target instanceof Element)
      ) return;

      const anchor = event.target.closest("a[href]");
      if (!anchor || anchor.target === "_blank" || anchor.hasAttribute("download")) return;

      const destinationUrl = new URL(anchor.href, window.location.href);
      if (destinationUrl.origin !== window.location.origin || destinationUrl.pathname === location.pathname) return;

      event.preventDefault();
      handleRouteNavigation(`${destinationUrl.pathname}${destinationUrl.search}${destinationUrl.hash}`);
    };

    document.addEventListener("click", handleInternalLinkClick, true);
    return () => document.removeEventListener("click", handleInternalLinkClick, true);
  }, [location.pathname]);

  useLayoutEffect(() => {
    if (previousPathname.current === location.pathname) return;
    previousPathname.current = location.pathname;
    window.scrollTo(0, 0);

    lockPageScroll();

    if (routeLoaderShowFrame.current !== null) {
      window.cancelAnimationFrame(routeLoaderShowFrame.current);
      routeLoaderShowFrame.current = null;
    }

    [
      routeLoaderHideTimer,
      routeLoaderUnmountTimer,
    ].forEach((timerRef) => {
      if (timerRef.current !== null) {
        window.clearTimeout(timerRef.current);
        timerRef.current = null;
      }
    });

    const loaderWasAlreadyMounted = routeLoaderMounted;
    setRouteLoaderMounted(true);
    routeLoaderVisible.current = true;

    if (loaderWasAlreadyMounted) {
      setRouteLoaderActive(true);
    } else {
      setRouteLoaderActive(false);
      routeLoaderShowFrame.current = window.requestAnimationFrame(() => {
        routeLoaderShowFrame.current = null;
        setRouteLoaderActive(true);
      });
    }

    routeLoaderHideTimer.current = window.setTimeout(() => {
      routeLoaderHideTimer.current = null;
      routeLoaderVisible.current = false;
      setRouteLoaderActive(false);

      routeLoaderUnmountTimer.current = window.setTimeout(() => {
        routeLoaderUnmountTimer.current = null;
        setRouteLoaderMounted(false);
        restoreBodyScroll();
        routeTransitionInProgress.current = false;
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
    restoreBodyScroll();
    if (routeLoaderShowFrame.current !== null) {
      window.cancelAnimationFrame(routeLoaderShowFrame.current);
      routeLoaderShowFrame.current = null;
    }
    if (routeNavigationTimer.current !== null) {
      window.clearTimeout(routeNavigationTimer.current);
      routeNavigationTimer.current = null;
    }
    routeTransitionInProgress.current = false;
  }, []);

  const handleLogin = (loggedInUser, loggedInToken, destination) => {
    handleRouteNavigation(
      destination,
      () => dispatch(setCredentials({ token: loggedInToken, user: loggedInUser })),
      { replace: true },
    );
  };

  const handleLogout = () => {
    localStorage.removeItem("token");
    localStorage.removeItem("user");

    dispatch(clearAuth());
  };

  useEffect(() => {
    const handleUnauthorized = () => {
      dispatch(clearAuth());
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
        if (!cancelled) dispatch(setCredentials({ token, user: response.data }));
      })
      .catch(() => {
        if (!cancelled) dispatch(clearAuth());
      })
      .finally(() => {
        if (!cancelled) setSessionLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [dispatch]);

  return (
    <>
      <Navbar onLogout={handleLogout} onNavigate={handleRouteNavigation} />

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
              <ProtectedRoute>
                <MyReservations />
              </ProtectedRoute>
            }
          />

          <Route
            path="/admin"
            element={
              <ProtectedRoute requiredRole="Admin">
                <AdminDashboard />
              </ProtectedRoute>
            }
          />
        </Routes>
        )}
      </main>

      <Footer />

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
