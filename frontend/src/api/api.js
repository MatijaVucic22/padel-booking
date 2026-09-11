import axios from "axios";

export const apiBaseUrl = import.meta.env.VITE_API_URL || "http://localhost:5238/api";
const backendBaseUrl = apiBaseUrl.replace(/\/api\/?$/, "");

const api = axios.create({
  baseURL: apiBaseUrl,
});

export const courtAvailabilityHubUrl = `${backendBaseUrl}/hubs/court-availability`;

export function getBackendAssetUrl(relativeUrl) {
  if (!relativeUrl) return "";
  if (/^https?:\/\//i.test(relativeUrl)) return relativeUrl;

  return `${backendBaseUrl}${relativeUrl.startsWith("/") ? "" : "/"}${relativeUrl}`;
}

api.interceptors.request.use((config) => {
  const token = localStorage.getItem("token");

  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

api.interceptors.response.use(
  (response) => response,
  (error) => {
    const hadToken = Boolean(localStorage.getItem("token"));
    const requestUrl = error.config?.url?.split("?")[0].replace(/\/+$/, "") ?? "";
    const isLoginRequest = requestUrl.endsWith("/auth/login");

    if (error.response?.status === 401 && hadToken && !isLoginRequest) {
      localStorage.removeItem("token");
      localStorage.removeItem("user");
      window.dispatchEvent(new Event("auth:unauthorized"));
    }

    return Promise.reject(error);
  },
);

export default api;
