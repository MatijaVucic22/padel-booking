import { useEffect, useRef, useState } from "react";
import { atcb_action as addToCalendar } from "add-to-calendar-button";

const calendarProviders = [
  { id: "Google", label: "Google Calendar", initial: "G" },
  { id: "Apple", label: "Apple Calendar", initial: "A" },
  { id: "Outlook.com", label: "Outlook", initial: "O" },
];

function getCalendarDateTime(value) {
  const match = String(value ?? "").match(
    /^(\d{4}-\d{2}-\d{2})T(\d{2}:\d{2})/,
  );

  return match ? { date: match[1], time: match[2] } : null;
}

function AddToCalendarButton({ reservation }) {
  const [isOpen, setIsOpen] = useState(false);
  const containerRef = useRef(null);
  const triggerRef = useRef(null);
  const popoverRef = useRef(null);
  const firstProviderRef = useRef(null);
  const start = getCalendarDateTime(reservation.startTime);
  const end = getCalendarDateTime(reservation.endTime);

  useEffect(() => {
    if (!isOpen) return undefined;

    firstProviderRef.current?.focus();

    const handlePointerDown = (event) => {
      if (!containerRef.current?.contains(event.target)) setIsOpen(false);
    };
    const handleKeyDown = (event) => {
      if (event.key !== "Escape") return;
      setIsOpen(false);
      triggerRef.current?.focus();
    };

    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);

    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen]);

  if (!start || !end) return null;

  const courtLocation =
    reservation.courtLocation ??
    reservation.location ??
    reservation.court?.location ??
    "";

  const calendarEvent = {
    name: `PadelBooking - ${reservation.courtName}`,
    description: `Padel court reservation${reservation.id ? ` (#${reservation.id})` : ""}`,
    startDate: start.date,
    startTime: start.time,
    endDate: end.date,
    endTime: end.time,
    timeZone: "Europe/Belgrade",
    location: courtLocation,
    hideCheckmark: true,
    pastDateHandling: "hide",
  };

  const handleProvider = (provider, event) => {
    setIsOpen(false);
    void addToCalendar(
      { ...calendarEvent, options: [provider] },
      event.currentTarget,
      event.detail === 0,
    );
  };

  const handleMenuKeyDown = (event) => {
    if (!["ArrowDown", "ArrowUp", "Home", "End"].includes(event.key)) return;

    const buttons = [...popoverRef.current.querySelectorAll("button")];
    const currentIndex = buttons.indexOf(document.activeElement);
    let nextIndex;

    if (event.key === "Home") nextIndex = 0;
    else if (event.key === "End") nextIndex = buttons.length - 1;
    else if (event.key === "ArrowDown") nextIndex = (currentIndex + 1) % buttons.length;
    else nextIndex = (currentIndex - 1 + buttons.length) % buttons.length;

    event.preventDefault();
    buttons[nextIndex]?.focus();
  };

  return (
    <div className="reservation-calendar-actions" ref={containerRef}>
      <button
        ref={triggerRef}
        type="button"
        className="reservation-calendar-trigger"
        aria-label="Dodaj u kalendar"
        aria-haspopup="menu"
        aria-expanded={isOpen}
        title="Dodaj u kalendar"
        onClick={() => setIsOpen((current) => !current)}
      >
        <svg aria-hidden="true" viewBox="0 0 24 24" fill="none">
          <rect x="3.5" y="5" width="17" height="15.5" rx="2" />
          <path d="M8 3.5V7M16 3.5V7M3.5 10h17M12 13v5M9.5 15.5h5" />
        </svg>
      </button>

      {isOpen && (
        <div
          ref={popoverRef}
          className="reservation-calendar-popover"
          role="menu"
          aria-label="Dodaj u kalendar"
          onKeyDown={handleMenuKeyDown}
        >
          <strong>Dodaj u kalendar</strong>
          {calendarProviders.map((provider, index) => (
            <button
              key={provider.id}
              ref={index === 0 ? firstProviderRef : undefined}
              type="button"
              role="menuitem"
              onClick={(event) => handleProvider(provider.id, event)}
            >
              <span aria-hidden="true">{provider.initial}</span>
              {provider.label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

export default AddToCalendarButton;
