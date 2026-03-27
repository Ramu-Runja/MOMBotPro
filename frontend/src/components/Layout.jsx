import { useState, useRef, useEffect } from "react";
import { useNavigate, useLocation, Link } from "react-router-dom";
import {
  LayoutDashboard, BarChart2, Video, Play, CreditCard,
  Plug, Settings, LogOut, Zap, ChevronRight, Menu,
  Search, Bell, Moon, Sun, User, ChevronDown, Info, X,
} from "lucide-react";
import { cn } from "./ui";
import { useAuth } from "../context/AuthContext";
import { useTheme } from "../context/ThemeContext";
import Footer from "./Footer";

const NAV = [
  { path: "/",            label: "Dashboard",   icon: LayoutDashboard },
  { path: "/pipelines",   label: "Pipelines",   icon: Play            },
  { path: "/zoom",        label: "Zoom Join",    icon: Video           },
  { path: "/analytics",   label: "Analytics",   icon: BarChart2       },
  { path: "/pricing",     label: "Pricing",      icon: CreditCard      },
  { path: "/integrations",label: "Integrations", icon: Plug            },
  { path: "/settings",    label: "Settings",     icon: Settings        },
];

function ThemeToggle() {
  const { dark, toggle } = useTheme();
  return (
    <button
      onClick={toggle}
      className="w-9 h-9 rounded-xl flex items-center justify-center bg-surface2 border border-border hover:border-accent/40 transition-all"
      title={dark ? "Switch to light mode" : "Switch to dark mode"}
    >
      {dark
        ? <Moon className="w-4 h-4 text-accent" style={{ filter: "drop-shadow(0 0 6px rgba(108,99,255,0.6))" }} />
        : <Sun  className="w-4 h-4 text-orange animate-sun-spin" />
      }
    </button>
  );
}

function AboutPanel({ open, onClose }) {
  return (
    <>
      {open && <div className="fixed inset-0 bg-black/40 z-40" onClick={onClose} />}
      <div className={cn(
        "fixed right-0 top-0 h-full w-96 bg-surface border-l border-border z-50",
        "transition-transform duration-300 ease-out flex flex-col",
        open ? "translate-x-0" : "translate-x-full"
      )}>
        <div className="flex items-center justify-between px-6 py-5 border-b border-border">
          <h2 className="font-display font-bold text-txt text-lg">About MOMBot Pro</h2>
          <button onClick={onClose} className="text-muted hover:text-txt transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>
        <div className="flex-1 overflow-y-auto px-6 py-6 space-y-5 scrollbar-thin">
          <div className="w-14 h-14 rounded-2xl bg-accent/15 border border-accent/25 flex items-center justify-center">
            <Zap className="w-7 h-7 text-accent" />
          </div>
          <div>
            <h3 className="font-display font-bold text-txt text-xl">MOMBot Pro</h3>
            <p className="text-xs text-accent font-semibold mt-0.5">Version 3.0 · AI Pipeline Engine</p>
          </div>
          <p className="text-sm text-muted leading-relaxed">
            MOMBot Pro is an AI-powered meeting automation platform built for software teams.
            It listens to your Telugu+English client Zoom calls, generates Minutes of Meeting,
            auto-creates Jira tickets, scans your codebase, and raises GitHub PRs — all without
            a single manual step.
          </p>
          <div className="space-y-3">
            {[
              { title: "Zero Manual Steps",     desc: "From Zoom call to merged PR — fully automated." },
              { title: "Tenglish Support",       desc: "Understands Telugu+English mixed speech natively." },
              { title: "AI-Powered Analysis",    desc: "Claude AI extracts bugs, generates fixes & PRs." },
              { title: "Real-time Tracking",     desc: "Watch every pipeline step execute live." },
            ].map(f => (
              <div key={f.title} className="bg-surface2 border border-border rounded-xl p-4">
                <p className="text-sm font-semibold text-txt">{f.title}</p>
                <p className="text-xs text-muted mt-0.5">{f.desc}</p>
              </div>
            ))}
          </div>
          <div className="border-t border-border pt-4 space-y-1.5 text-xs text-muted">
            <p>Built by <span className="text-txt font-semibold">Innoworks Software</span></p>
            <p>Contact: <a href="mailto:support@innoworks.io" className="text-accent hover:underline">support@innoworks.io</a></p>
            <p>© 2025 Innoworks Software. All rights reserved.</p>
          </div>
        </div>
      </div>
    </>
  );
}

export default function Layout({ children }) {
  const { user, logout }       = useAuth();
  const { dark }               = useTheme();
  const navigate               = useNavigate();
  const location               = useLocation();

  const [collapsed, setCollapsed]   = useState(false);
  const [search,    setSearch]      = useState("");
  const [userMenu,  setUserMenu]    = useState(false);
  const [aboutOpen, setAboutOpen]   = useState(false);
  const [notifCount]                = useState(3);

  const userMenuRef = useRef(null);
  useEffect(() => {
    const handler = (e) => { if (!userMenuRef.current?.contains(e.target)) setUserMenu(false); };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const handleLogout = async () => {
    await logout();
    navigate("/login");
  };

  const sidebarW = collapsed ? "w-16" : "w-64";

  return (
    <div className="flex h-screen bg-bg overflow-hidden theme-transition">

      {/* ── Sidebar ─────────────────────────────────────── */}
      <aside className={cn(
        "bg-surface border-r border-border flex flex-col shrink-0 transition-all duration-300 ease-out",
        sidebarW
      )}>
        {/* Logo */}
        <Link to="/" className="flex items-center gap-3 px-4 py-5 border-b border-border hover:bg-surface2 transition-colors">
          <div className="w-9 h-9 rounded-xl bg-accent/20 border border-accent/30 flex items-center justify-center shrink-0 animate-pulse-glow">
            <Zap className="w-5 h-5 text-accent" />
          </div>
          {!collapsed && (
            <div>
              <p className="font-display font-bold text-txt text-[15px] leading-none">
                MOMBot<span className="text-accent">Pro</span>
              </p>
              <p className="text-[10px] text-muted mt-0.5">AI Pipeline Engine</p>
            </div>
          )}
        </Link>

        {/* Nav */}
        <nav className="flex-1 px-2 py-3 space-y-0.5 overflow-y-auto scrollbar-thin">
          {!collapsed && (
            <p className="px-3 text-[10px] font-semibold text-muted uppercase tracking-widest mb-2 mt-1">Menu</p>
          )}
          {NAV.map(({ path, label, icon: Icon }) => {
            const active = location.pathname === path ||
              (path !== "/" && location.pathname.startsWith(path));
            return (
              <Link
                key={path}
                to={path}
                title={collapsed ? label : undefined}
                className={cn(
                  "flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-all",
                  active
                    ? "bg-accent/15 text-accent border border-accent/20"
                    : "text-muted hover:text-txt hover:bg-surface2 border border-transparent"
                )}
              >
                <Icon className="w-4 h-4 shrink-0" />
                {!collapsed && <span className="flex-1">{label}</span>}
                {!collapsed && active && <ChevronRight className="w-3 h-3 opacity-50" />}
              </Link>
            );
          })}
        </nav>

        {/* User + Logout */}
        <div className="px-2 pb-4 border-t border-border pt-3 space-y-1">
          {user && !collapsed && (
            <div className="px-3 py-2.5 rounded-xl bg-surface2 border border-border">
              <p className="text-xs font-semibold text-txt truncate">{user.fullName}</p>
              <div className="flex items-center gap-1.5 mt-0.5">
                <span className="text-[10px] text-muted">{user.subscriptionPlan === "free_trial" ? "Free Trial" : user.subscriptionPlan}</span>
                {user.subscriptionPlan === "free_trial" && (
                  <span className="text-[10px] font-bold text-orange">{user.freeTrialMeetingsLeft} left</span>
                )}
              </div>
            </div>
          )}
          <button
            onClick={handleLogout}
            title={collapsed ? "Logout" : undefined}
            className="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm text-muted hover:text-red hover:bg-red/10 border border-transparent hover:border-red/20 transition-all"
          >
            <LogOut className="w-4 h-4 shrink-0" />
            {!collapsed && <span>Logout</span>}
          </button>
        </div>
      </aside>

      {/* ── Main ──────────────────────────────────────────── */}
      <div className="flex-1 flex flex-col overflow-hidden">

        {/* Top Navbar */}
        <header className="bg-surface/90 backdrop-blur border-b border-border px-5 py-3 flex items-center gap-4 shrink-0 theme-transition">
          {/* Hamburger */}
          <button
            onClick={() => setCollapsed(c => !c)}
            className="w-9 h-9 rounded-xl flex items-center justify-center text-muted hover:text-txt hover:bg-surface2 border border-transparent hover:border-border transition-all"
          >
            <Menu className="w-4 h-4" />
          </button>

          {/* Search */}
          <div className="flex-1 max-w-sm">
            <div className="relative">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-muted" />
              <input
                value={search}
                onChange={e => setSearch(e.target.value)}
                placeholder="Search pipelines..."
                className="w-full bg-surface2 border border-border rounded-xl pl-9 pr-4 py-2 text-sm text-txt placeholder:text-muted outline-none focus:border-accent transition-colors"
              />
            </div>
          </div>

          <div className="flex items-center gap-2 ml-auto">
            <ThemeToggle />

            {/* Notifications */}
            <button className="relative w-9 h-9 rounded-xl flex items-center justify-center bg-surface2 border border-border hover:border-accent/40 transition-all">
              <Bell className="w-4 h-4 text-muted" />
              {notifCount > 0 && (
                <span className="absolute -top-1 -right-1 w-4 h-4 bg-red rounded-full text-[9px] text-white font-bold flex items-center justify-center">
                  {notifCount}
                </span>
              )}
            </button>

            {/* About */}
            <button
              onClick={() => setAboutOpen(true)}
              className="hidden sm:flex items-center gap-1.5 px-3 py-2 bg-surface2 border border-border rounded-xl text-xs text-muted hover:text-txt hover:border-accent/40 transition-all"
            >
              <Info className="w-3.5 h-3.5" /> About
            </button>

            {/* Free trial badge */}
            {user?.subscriptionPlan === "free_trial" && (
              <Link
                to="/pricing"
                className="hidden sm:flex items-center gap-1.5 px-3 py-2 bg-orange/10 border border-orange/25 rounded-xl text-xs font-semibold text-orange hover:bg-orange/20 transition-all"
              >
                {user.freeTrialMeetingsLeft} meetings left · Upgrade
              </Link>
            )}

            {/* User avatar dropdown */}
            <div className="relative" ref={userMenuRef}>
              <button
                onClick={() => setUserMenu(m => !m)}
                className="flex items-center gap-2 pl-2 pr-3 py-1.5 bg-surface2 border border-border rounded-xl hover:border-accent/40 transition-all"
              >
                <div className="w-6 h-6 rounded-full bg-accent/20 border border-accent/30 flex items-center justify-center">
                  <User className="w-3.5 h-3.5 text-accent" />
                </div>
                <span className="text-xs font-medium text-txt hidden sm:block max-w-24 truncate">
                  {user?.fullName ?? "Account"}
                </span>
                <ChevronDown className="w-3 h-3 text-muted" />
              </button>

              {userMenu && (
                <div className="absolute right-0 top-full mt-2 w-52 bg-surface border border-border rounded-xl shadow-xl z-20 py-1.5 animate-slide-in">
                  <div className="px-4 py-2 border-b border-border">
                    <p className="text-xs font-semibold text-txt truncate">{user?.fullName}</p>
                    <p className="text-[10px] text-muted truncate">{user?.email}</p>
                  </div>
                  {[
                    { label: "Profile & Settings", icon: Settings, path: "/settings" },
                    { label: "Upgrade Plan",       icon: CreditCard, path: "/pricing" },
                  ].map(item => (
                    <Link
                      key={item.path}
                      to={item.path}
                      onClick={() => setUserMenu(false)}
                      className="flex items-center gap-2.5 px-4 py-2 text-sm text-muted hover:text-txt hover:bg-surface2 transition-colors"
                    >
                      <item.icon className="w-3.5 h-3.5" /> {item.label}
                    </Link>
                  ))}
                  <div className="border-t border-border mt-1 pt-1">
                    <button
                      onClick={handleLogout}
                      className="w-full flex items-center gap-2.5 px-4 py-2 text-sm text-red hover:bg-red/10 transition-colors"
                    >
                      <LogOut className="w-3.5 h-3.5" /> Logout
                    </button>
                  </div>
                </div>
              )}
            </div>
          </div>
        </header>

        {/* Page content */}
        <main className="flex-1 overflow-y-auto scrollbar-thin">
          <div className="px-8 py-6">
            {children}
          </div>
          <Footer />
        </main>
      </div>

      <AboutPanel open={aboutOpen} onClose={() => setAboutOpen(false)} />
    </div>
  );
}
