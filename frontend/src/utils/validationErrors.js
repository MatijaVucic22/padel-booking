export function parseValidationErrors(error) {
  const responseErrors = error?.response?.data?.errors;

  if (
    error?.response?.status !== 400 ||
    !responseErrors ||
    typeof responseErrors !== "object" ||
    Array.isArray(responseErrors)
  ) {
    return {};
  }

  return Object.fromEntries(
    Object.entries(responseErrors)
      .map(([field, messages]) => [
        field,
        (Array.isArray(messages) ? messages : [messages]).filter(
          (message) => typeof message === "string" && message.length > 0,
        ),
      ])
      .filter(([, messages]) => messages.length > 0),
  );
}

export function hasValidationErrors(errors) {
  return Object.keys(errors).length > 0;
}
