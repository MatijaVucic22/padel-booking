import { useEffect, useState } from "react";
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

function App() {
  const navigate = useNavigate();
  const location = useLocation();
  const [user, setUser] = useState(null);
  const [sessionLoading, setSessionLoading] = useState(() =>
    Boolean(localStorage.getItem("token")),
  );

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
    </>
  );
}

export default App;
