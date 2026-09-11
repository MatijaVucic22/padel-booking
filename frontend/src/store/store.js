import { configureStore } from "@reduxjs/toolkit";
import authReducer from "./authSlice";
import { padelApi } from "../services/padelApi";

export const store = configureStore({
  reducer: {
    auth: authReducer,
    [padelApi.reducerPath]: padelApi.reducer,
  },
  middleware: (getDefaultMiddleware) =>
    getDefaultMiddleware().concat(padelApi.middleware),
});
