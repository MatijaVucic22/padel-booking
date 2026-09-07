import { useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import api from "../api/api";
import {
  hasValidationErrors,
  parseValidationErrors,
} from "../utils/validationErrors";

function Login({ onLogin }) {
  const navigate = useNavigate();
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

      onLogin(response.data.user);

      const requestedPath = location.state?.from;
      const destination =
        typeof requestedPath === "string" &&
        requestedPath.startsWith("/") &&
        !requestedPath.startsWith("//") &&
        requestedPath !== "/login"
          ? requestedPath
          : "/";

      navigate(destination, { replace: true });
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
    <div className="auth-page">
      <div className="auth-card">
        <h1>Prijava</h1>

        <form onSubmit={handleSubmit}>
          <label>Email</label>

          <input
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

          <label>Lozinka</label>

          <input
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
