import { useState } from "react";
import { useNavigate } from "react-router-dom";
import api from "../api/api";
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
    <div className="auth-page">
      <div className="auth-card">
        <h1>Registracija</h1>

        <form onSubmit={handleSubmit}>
          <label>Ime</label>

          <input
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

          <label>Prezime</label>

          <input
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

          {message && <p className="success-message">{message}</p>}

          <button type="submit" disabled={loading}>
            {loading ? "Registracija..." : "Registruj se"}
          </button>
        </form>
      </div>
    </div>
  );
}

export default Register;
