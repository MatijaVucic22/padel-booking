import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import api from "../api/api";
import courtIndoor from "../assets/images/court-indoor.jpg";
import {
  hasValidationErrors,
  parseValidationErrors,
} from "../utils/validationErrors";

function Register() {
  const navigate = useNavigate();

  const [formData, setFormData] = useState({
    firstName: "",
    lastName: "",
    email: "",
    password: "",
  });

  const [message, setMessage] = useState("");
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

    setMessage("");
    setError("");
    setFieldErrors({});
    setLoading(true);

    try {
      const response = await api.post("/auth/register", formData);

      setMessage(response.data.message);

      setTimeout(() => {
        navigate("/login");
      }, 1000);
    } catch (error) {
      console.error(error);

      const validationErrors = parseValidationErrors(error);

      if (hasValidationErrors(validationErrors)) {
        setFieldErrors(validationErrors);
        setError("");
        return;
      }

      if (error.response?.data) {
        setError(
          typeof error.response.data === "string"
            ? error.response.data
            : "Registracija nije uspela."
        );
      } else {
        setError("Nije moguće povezati se sa serverom.");
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-page auth-split">
      <aside className="auth-visual">
        <img src={courtIndoor} alt="Premium zatvoreni padel teren" />
        <div className="auth-visual-copy"><span>PADELBOOKING</span><h2>Tvoj teren.<br />Tvoj termin.<br />Tvoja igra.</h2></div>
      </aside>
      <div className="auth-card">
        <div className="auth-heading"><span className="section-kicker">Novi igrač</span><h1>Napravi nalog.</h1><p>Do sledećeg termina deli te manje od minut.</p></div>

        <form onSubmit={handleSubmit}>
          <label htmlFor="register-first-name">Ime</label>

          <input
            id="register-first-name"
            type="text"
            name="firstName"
            value={formData.firstName}
            onChange={handleChange}
            aria-invalid={Boolean(fieldErrors.firstName)}
            required
          />
          {fieldErrors.firstName?.map((message) => (
            <span className="field-error" key={message}>{message}</span>
          ))}

          <label htmlFor="register-last-name">Prezime</label>

          <input
            id="register-last-name"
            type="text"
            name="lastName"
            value={formData.lastName}
            onChange={handleChange}
            aria-invalid={Boolean(fieldErrors.lastName)}
            required
          />
          {fieldErrors.lastName?.map((message) => (
            <span className="field-error" key={message}>{message}</span>
          ))}

          <label htmlFor="register-email">Email</label>

          <input
            id="register-email"
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

          <label htmlFor="register-password">Lozinka</label>

          <input
            id="register-password"
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

          {message && <p className="success-message">{message}</p>}

          <p className="auth-form-helper">
            Već imaš nalog? <Link to="/login">Prijavi se ovde.</Link>
          </p>

          <button type="submit" disabled={loading}>
            {loading ? "Registracija..." : "Registruj se"}
          </button>
        </form>
      </div>
    </div>
  );
}

export default Register;
