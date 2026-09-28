export function getRoleUx(user) {
  const isAdmin = user?.role === "Admin";

  return {
    isAdmin,
    showBookingCta: !isAdmin,
    showMyReservations: Boolean(user) && !isAdmin,
    showAdminPanel: isAdmin,
    canUseCustomerBooking: !isAdmin,
  };
}

