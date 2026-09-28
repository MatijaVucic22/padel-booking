import { Navigate, useLocation } from "react-router-dom";
import { useSelector } from "react-redux";
import { getRoleUx } from "../utils/roleUx";

function ProtectedRoute({
  requiredRole,
  customerOnly = false,
  allowAnonymous = false,
  children,
}) {
  const location = useLocation();
  const { user, isAuthenticated } = useSelector((state) => state.auth);
  const roleUx = getRoleUx(user);

  if ((!isAuthenticated || !user) && !allowAnonymous) {
    return (
      <Navigate
        to="/login"
        replace
        state={{ from: `${location.pathname}${location.search}${location.hash}` }}
      />
    );
  }

  if (customerOnly && !roleUx.canUseCustomerBooking) {
    return <Navigate to="/admin" replace />;
  }

  if (requiredRole && user.role !== requiredRole) {
    return <Navigate to="/" replace />;
  }

  return children;
}

export default ProtectedRoute;
