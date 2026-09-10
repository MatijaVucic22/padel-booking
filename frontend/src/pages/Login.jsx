import { useState } from "react";
import { useLocation } from "react-router-dom";
import api from "../api/api";
import courtNight from "../assets/images/court-night.jpg";
import {
  hasValidationErrors,
  parseValidationErrors,
} from "../utils/validationErrors";

function Login({ onLogin }) {
  const location = useLocation();

  const [formData, setFormData] = useState({
    email: "",
    password: "",
  });

  const [error, setError] = useState("");
  const [fieldErrors, setFieldErrors] = useState({});
  const [loading, setLoading] = useState(false);

  const handleChange = (event) => {
    const { name, value } = event.target;

    setFormData((previous) => ({
      ...previous,
      [name]: value,
    }));
    setFieldErrors((currentErrors) => {
      if (!currentErrors[name]) return currentErrors;

      const nextErrors = { ...currentErrors };
      delete nextErrors[name];
      return nextErrors;
    });
  };

  const handleSubmit = async (event) => {
    event.preventDefault();

    setError("");
    setFieldErrors({});
    setLoading(true);

    try {
      const response = await api.post("/auth/login", formData);

      localStorage.setItem("token", response.data.token);
      localStorage.removeItem("user");

      const requestedPath = location.state?.from;
      const destination =
        typeof requestedPath === "string" &&
        requestedPath.startsWith("/") &&
        !requestedPath.startsWith("//") &&
        requestedPath !== "/login"
          ? requestedPath
          : "/";

      onLogin(response.data.user, destination);
    } catch (error) {
      const validationErrors = parseValidationErrors(error);

      if (hasValidationErrors(validationErrors)) {
        setFieldErrors(validationErrors);
        setError("");
        return;
      }

      if (error.response?.status === 401) {
        setError("Pogrešan email ili lozinka.");
      } else if (error.response?.status === 429) {
        setError(
          error.response.data?.message ??
            "Previše pokušaja prijave. Pokušajte ponovo za minut.",
        );
      } else {
        setError("Prijava nije uspela.");
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-page auth-split">
      <aside className="auth-visual">
        <img src={courtNight} alt="Osvetljen padel teren tokom večernjeg meča" />
        <div className="auth-visual-copy"><span>PADELBOOKING</span><h2>Rezerviši.<br />Igraj.<br />Ponovi.</h2></div>
      </aside>
      <div className="auth-card">
        <div className="auth-heading"><span className="section-kicker">Dobrodošao nazad</span><h1>Prijavi se za sledeći meč.</h1><p>Tvoji tereni i rezervacije čekaju te na jednom mestu.</p></div>

        <form onSubmit={handleSubmit}>
          <label htmlFor="login-email">Email</label>

          <input
            id="login-email"
            type="email"
            name="email"
            value={formData.email}
            onChange={handleChange}
            aria-invalid={Boolean(fieldErrors.email)}
            required
          />
          {fieldErrors.email?.map((message) => (
            <span className="field-error" key={message}>{message}</span>
          ))}

          <label htmlFor="login-password">Lozinka</label>

          <input
            id="login-password"
            type="password"
            name="password"
            value={formData.password}
            onChange={handleChange}
            aria-invalid={Boolean(fieldErrors.password)}
            required
          />
          {fieldErrors.password?.map((message) => (
            <span className="field-error" key={message}>{message}</span>
          ))}

          {error && <p className="error-message">{error}</p>}

          <button type="submit" disabled={loading}>
            {loading ? "Prijavljivanje..." : "Prijavi se"}
          </button>
        </form>
      </div>
    </div>
  );
}

export default Login;
