import axios from "axios";
import { normalizeApiErrorData } from "../utils/validationErrors";

export const apiBaseUrl = import.meta.env.VITE_API_URL || "http://localhost:5238/api";
const backendBaseUrl = apiBaseUrl.replace(/\/api\/?$/, "");

const api = axios.create({
  baseURL: apiBaseUrl,
  withCredentials: true,
});

export const courtAvailabilityHubUrl = `${backendBaseUrl}/hubs/court-availability`;

export function getBackendAssetUrl(relativeUrl) {
  if (!relativeUrl) return "";
  if (/^https?:\/\//i.test(relativeUrl)) return relativeUrl;

  return `${backendBaseUrl}${relativeUrl.startsWith("/") ? "" : "/"}${relativeUrl}`;
}

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response) {
      error.response.data = normalizeApiErrorData(error.response.data);
    }
    const requestUrl = error.config?.url?.split("?")[0].replace(/\/+$/, "") ?? "";
    const isAuthRequest = ["/auth/login", "/auth/me", "/auth/logout"]
      .some((path) => requestUrl.endsWith(path));

    if (error.response?.status === 401 && !isAuthRequest) {
      window.dispatchEvent(new Event("auth:unauthorized"));
    }

    return Promise.reject(error);
  },
);

export default api;
