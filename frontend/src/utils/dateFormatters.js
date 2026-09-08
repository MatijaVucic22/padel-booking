const SERBIAN_LATIN_LOCALE = "sr-Latn-RS";

export const longDateWithWeekdayFormatter = new Intl.DateTimeFormat(
  SERBIAN_LATIN_LOCALE,
  {
    weekday: "long",
    day: "2-digit",
    month: "long",
    year: "numeric",
  },
);

export const longDateFormatter = new Intl.DateTimeFormat(
  SERBIAN_LATIN_LOCALE,
  {
    day: "2-digit",
    month: "long",
    year: "numeric",
  },
);

export const numericDateFormatter = new Intl.DateTimeFormat(
  SERBIAN_LATIN_LOCALE,
  {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  },
);

export const timeFormatter = new Intl.DateTimeFormat(SERBIAN_LATIN_LOCALE, {
  hour: "2-digit",
  minute: "2-digit",
});

export const shortMonthFormatter = new Intl.DateTimeFormat(
  SERBIAN_LATIN_LOCALE,
  { month: "short" },
);
