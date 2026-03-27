import { BrowserRouter, Routes, Route, Navigate, useLocation } from "react-router-dom";
import { AuthProvider, useAuth } from "./context/AuthContext";
import { ThemeProvider }         from "./context/ThemeContext";
import { ToastProvider }         from "./context/ToastContext";
import Layout                    from "./components/Layout";

import Dashboard       from "./pages/Dashboard";
import PipelineDashboard from "./pages/PipelineDashboard";
import PipelineDetail  from "./pages/PipelineDetail";
import NewPipeline     from "./pages/NewPipeline";
import ZoomJoin        from "./pages/ZoomJoin";
import Analytics       from "./pages/Analytics";
import Pricing         from "./pages/Pricing";
import Integrations    from "./pages/Integrations";
import Settings        from "./pages/Settings";
import Login           from "./pages/Login";
import Register        from "./pages/Register";

/** Wrap protected routes — redirect to /login if not authenticated */
function AuthGuard({ children }) {
  const { user, loading } = useAuth();
  const location          = useLocation();

  if (loading) {
    return (
      <div className="min-h-screen bg-bg flex items-center justify-center">
        <div className="flex flex-col items-center gap-3">
          <div className="w-10 h-10 border-2 border-accent/30 border-t-accent rounded-full animate-spin" />
          <p className="text-sm text-muted">Loading MOMBot Pro…</p>
        </div>
      </div>
    );
  }

  if (!user) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return children;
}

/** Redirect logged-in users away from login/register */
function PublicGuard({ children }) {
  const { user, loading } = useAuth();
  if (loading) return null;
  if (user) return <Navigate to="/" replace />;
  return children;
}

function AppRoutes() {
  return (
    <Routes>
      {/* Public routes */}
      <Route path="/login"    element={<PublicGuard><Login    /></PublicGuard>} />
      <Route path="/register" element={<PublicGuard><Register /></PublicGuard>} />

      {/* Protected routes — all wrapped in Layout */}
      <Route path="/" element={<AuthGuard><Layout><Dashboard /></Layout></AuthGuard>} />

      <Route path="/pipelines" element={
        <AuthGuard>
          <Layout><PipelineDashboard /></Layout>
        </AuthGuard>
      } />

      <Route path="/pipelines/:id" element={
        <AuthGuard>
          <Layout><PipelineDetail /></Layout>
        </AuthGuard>
      } />

      <Route path="/pipelines/new" element={
        <AuthGuard>
          <Layout><NewPipeline /></Layout>
        </AuthGuard>
      } />

      <Route path="/zoom" element={
        <AuthGuard>
          <Layout><ZoomJoin /></Layout>
        </AuthGuard>
      } />

      <Route path="/analytics" element={
        <AuthGuard>
          <Layout><Analytics /></Layout>
        </AuthGuard>
      } />

      <Route path="/pricing" element={
        <AuthGuard>
          <Layout><Pricing /></Layout>
        </AuthGuard>
      } />

      <Route path="/integrations" element={
        <AuthGuard>
          <Layout><Integrations /></Layout>
        </AuthGuard>
      } />

      <Route path="/settings" element={
        <AuthGuard>
          <Layout><Settings /></Layout>
        </AuthGuard>
      } />

      {/* Catch-all */}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <ThemeProvider>
        <AuthProvider>
          <ToastProvider>
            <AppRoutes />
          </ToastProvider>
        </AuthProvider>
      </ThemeProvider>
    </BrowserRouter>
  );
}
