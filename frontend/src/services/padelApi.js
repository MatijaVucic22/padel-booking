import { createApi, fetchBaseQuery } from "@reduxjs/toolkit/query/react";
import { apiBaseUrl } from "../api/api";

const fetchPadelBaseQuery = fetchBaseQuery({
    baseUrl: apiBaseUrl,
    prepareHeaders: (headers, { getState }) => {
      const token = getState().auth.token;
      if (token) headers.set("authorization", `Bearer ${token}`);
      return headers;
    },
  });

const baseQueryWithSessionHandling = async (args, api, extraOptions) => {
  const result = await fetchPadelBaseQuery(args, api, extraOptions);

  if (result.error?.status === 401 && api.getState().auth.token) {
    localStorage.removeItem("token");
    localStorage.removeItem("user");
    window.dispatchEvent(new Event("auth:unauthorized"));
  }

  return result;
};

export const padelApi = createApi({
  reducerPath: "padelApi",
  baseQuery: baseQueryWithSessionHandling,
  tagTypes: [
    "Court",
    "Reservations",
    "Availability",
    "AdminUsers",
    "AdminReservations",
    "AdminStats",
    "AdminCalendar",
  ],
  endpoints: (builder) => ({
    getCourts: builder.query({
      query: () => "/courts",
      providesTags: (result) => result
        ? [{ type: "Court", id: "LIST" }, ...result.map((court) => ({ type: "Court", id: court.id }))]
        : [{ type: "Court", id: "LIST" }],
    }),
    getCourtById: builder.query({
      query: (id) => `/courts/${id}`,
      providesTags: (_result, _error, id) => [{ type: "Court", id }],
    }),
    getAvailableCourts: builder.query({
      query: ({ startTime, durationHours }) => ({
        url: "/courts/available",
        params: { startTime, durationHours },
      }),
      providesTags: ["Availability"],
    }),
    getMyReservations: builder.query({
      query: () => "/reservations/my",
      providesTags: ["Reservations"],
    }),
    getReservationAvailability: builder.query({
      query: ({ courtId, date, reservationId }) => ({
        url: "/reservations/available",
        params: { courtId, date, reservationId },
      }),
      providesTags: ["Availability"],
    }),
    createReservation: builder.mutation({
      query: (reservation) => ({
        url: "/reservations",
        method: "POST",
        body: reservation,
      }),
      invalidatesTags: ["Reservations", "Availability"],
    }),
    cancelReservation: builder.mutation({
      query: (id) => ({
        url: `/reservations/${id}`,
        method: "DELETE",
      }),
      invalidatesTags: ["Reservations", "Availability"],
    }),
    rescheduleReservation: builder.mutation({
      query: ({ id, ...body }) => ({
        url: `/reservations/${id}/reschedule`,
        method: "PUT",
        body,
      }),
      invalidatesTags: ["Reservations", "Availability"],
    }),
    getAdminUsers: builder.query({
      query: () => "/admin/users",
      providesTags: ["AdminUsers"],
    }),
    getAdminReservations: builder.query({
      query: () => "/admin/reservations",
      providesTags: ["AdminReservations"],
    }),
    getAdminStats: builder.query({
      query: () => "/admin/stats",
      providesTags: ["AdminStats"],
    }),
    getAdminCalendar: builder.query({
      query: (date) => ({ url: "/admin/calendar", params: { date } }),
      providesTags: (_result, _error, date) => [
        { type: "AdminCalendar", id: date },
      ],
    }),
    createBlockedPeriod: builder.mutation({
      query: (blockedPeriod) => ({
        url: "/admin/blocked-periods",
        method: "POST",
        body: blockedPeriod,
      }),
      invalidatesTags: ["AdminCalendar", "Availability"],
    }),
    deleteBlockedPeriod: builder.mutation({
      query: (id) => ({
        url: `/admin/blocked-periods/${id}`,
        method: "DELETE",
      }),
      invalidatesTags: ["AdminCalendar", "Availability"],
    }),
    createCourt: builder.mutation({
      query: (formData) => ({ url: "/courts", method: "POST", body: formData }),
      invalidatesTags: [
        { type: "Court", id: "LIST" },
        "AdminStats",
        "AdminCalendar",
        "Availability",
      ],
    }),
    updateCourt: builder.mutation({
      query: ({ id, body }) => ({ url: `/courts/${id}`, method: "PUT", body }),
      invalidatesTags: (_result, _error, { id }) => [
        { type: "Court", id },
        { type: "Court", id: "LIST" },
        "AdminCalendar",
        "Availability",
      ],
    }),
    deactivateCourt: builder.mutation({
      query: (id) => ({ url: `/courts/${id}`, method: "DELETE" }),
      invalidatesTags: [
        { type: "Court", id: "LIST" },
        "AdminStats",
        "AdminCalendar",
        "Availability",
      ],
    }),
  }),
});

export const {
  useGetCourtsQuery,
  useGetCourtByIdQuery,
  useLazyGetAvailableCourtsQuery,
  useGetMyReservationsQuery,
  useLazyGetReservationAvailabilityQuery,
  useCreateReservationMutation,
  useCancelReservationMutation,
  useRescheduleReservationMutation,
  useLazyGetAdminUsersQuery,
  useLazyGetAdminReservationsQuery,
  useLazyGetAdminStatsQuery,
  useLazyGetAdminCalendarQuery,
  useCreateBlockedPeriodMutation,
  useDeleteBlockedPeriodMutation,
  useCreateCourtMutation,
  useUpdateCourtMutation,
  useDeactivateCourtMutation,
  useLazyGetCourtsQuery,
} = padelApi;

export function toLegacyApiError(error) {
  return {
    ...error,
    response: {
      status: error?.status,
      data: error?.data,
      headers: error?.meta?.response?.headers,
    },
  };
}
