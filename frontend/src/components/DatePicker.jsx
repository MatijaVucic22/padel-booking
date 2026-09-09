import { useEffect, useId, useRef, useState } from "react";
import { DayPicker } from "react-day-picker";
import { srLatn } from "react-day-picker/locale";
import "react-day-picker/style.css";
import "./DatePicker.css";

const displayFormatter = new Intl.DateTimeFormat("sr-Latn-RS", {
  day: "numeric",
  month: "long",
  year: "numeric",
});

function parseLocalDate(value) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value ?? "")) return undefined;
  const [year, month, day] = value.split("-").map(Number);
  return new Date(year, month - 1, day, 12);
}

function formatLocalDate(date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function DatePicker({
  value,
  onChange,
  min,
  disabled = false,
  ariaLabel = "Izaberi datum",
  placeholder = "Izaberi datum",
}) {
  const id = useId();
  const rootRef = useRef(null);
  const closeTimer = useRef(null);
  const openFrame = useRef(null);
  const [renderPopover, setRenderPopover] = useState(false);
  const [popoverOpen, setPopoverOpen] = useState(false);
  const selectedDate = parseLocalDate(value);
  const minimumDate = parseLocalDate(min);

  const closePopover = () => {
    if (openFrame.current !== null) cancelAnimationFrame(openFrame.current);
    setPopoverOpen(false);
    closeTimer.current = window.setTimeout(() => {
      closeTimer.current = null;
      setRenderPopover(false);
    }, 160);
  };

  const openPopover = () => {
    if (disabled) return;
    if (closeTimer.current !== null) window.clearTimeout(closeTimer.current);
    setRenderPopover(true);
    openFrame.current = requestAnimationFrame(() => {
      openFrame.current = null;
      setPopoverOpen(true);
    });
  };

  useEffect(() => {
    if (!renderPopover) return undefined;

    const handlePointerDown = (event) => {
      if (!rootRef.current?.contains(event.target)) closePopover();
    };
    const handleKeyDown = (event) => {
      if (event.key === "Escape") closePopover();
    };

    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [renderPopover]);

  useEffect(() => () => {
    if (closeTimer.current !== null) window.clearTimeout(closeTimer.current);
    if (openFrame.current !== null) cancelAnimationFrame(openFrame.current);
  }, []);

  return (
    <div className="premium-date-picker" ref={rootRef}>
      <button
        type="button"
        className="premium-date-picker-trigger"
        aria-label={ariaLabel}
        aria-expanded={popoverOpen}
        aria-controls={`${id}-calendar`}
        disabled={disabled}
        onClick={() => (popoverOpen ? closePopover() : openPopover())}
      >
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M7 2v3M17 2v3M3.5 9h17M5.5 4h13a2 2 0 0 1 2 2v13a2 2 0 0 1-2 2h-13a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2Z" />
        </svg>
        <span className={selectedDate ? "" : "is-placeholder"}>
          {selectedDate ? displayFormatter.format(selectedDate) : placeholder}
        </span>
        <span className="premium-date-picker-chevron" aria-hidden="true">⌄</span>
      </button>

      {renderPopover && (
        <div
          id={`${id}-calendar`}
          className={`premium-date-picker-popover${popoverOpen ? " is-open" : ""}`}
          role="dialog"
          aria-label="Kalendar za izbor datuma"
        >
          <DayPicker
            mode="single"
            locale={srLatn}
            weekStartsOn={1}
            navLayout="around"
            showOutsideDays
            fixedWeeks
            selected={selectedDate}
            defaultMonth={selectedDate ?? minimumDate ?? new Date()}
            disabled={minimumDate ? { before: minimumDate } : undefined}
            onSelect={(date) => {
              if (!date) return;
              onChange(formatLocalDate(date));
              closePopover();
            }}
          />
        </div>
      )}
    </div>
  );
}

export default DatePicker;
