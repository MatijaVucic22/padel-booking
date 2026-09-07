import { Navigate, useLocation } from "react-router-dom";

function ProtectedRoute({ user, requiredRole, children }) {
  const location = useLocation();
  const token = localStorage.getItem("token");

  if (!user || !token) {
    return (
      <Navigate
        to="/login"
        replace
        state={{ from: `${location.pathname}${location.search}${location.hash}` }}
      />
    );
  }

  if (requiredRole && user.role !== requiredRole) {
    return <Navigate to="/" replace />;
  }

  return children;
}

export default ProtectedRoute;
