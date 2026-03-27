import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import {
  Zap, Play, BarChart2, Video, Plug, ArrowRight,
  CheckCircle2, Clock, TrendingUp, Mic, GitPullRequest,
  FileText, Bug, Code2, Sparkles,
} from "lucide-react";
import { useAuth } from "../context/AuthContext";
import { cn } from "../components/ui";

const FEATURES = [
  {
    icon: Mic,
    title: "Tenglish Transcription",
    desc: "Understands Telugu+English mixed speech natively using OpenAI Whisper.",
    color: "text-accent",
    bg: "bg-accent/10 border-accent/20",
  },
  {
    icon: FileText,
    title: "Auto MOM Generation",
    desc: "Claude AI extracts key decisions, action items, and bugs from your meetings.",
    color: "text-green",
    bg: "bg-green/10 border-green/20",
  },
  {
    icon: Bug,
    title: "Jira Ticket Creation",
    desc: "Bugs and tasks from your call are auto-created as Jira issues.",
    color: "text-orange",
    bg: "bg-orange/10 border-orange/20",
  },
  {
    icon: Code2,
    title: "Codebase Scanning",
    desc: "Claude scans your GitHub repository to locate the exact code to fix.",
    color: "text-accent",
    bg: "bg-accent/10 border-accent/20",
  },
  {
    icon: Sparkles,
    title: "AI Code Fix",
    desc: "Generates a precise code fix based on the bug report and codebase context.",
    color: "text-green",
    bg: "bg-green/10 border-green/20",
  },
  {
    icon: GitPullRequest,
    title: "Auto PR Raising",
    desc: "Creates a GitHub branch and raises a pull request — fully automated.",
    color: "text-orange",
    bg: "bg-orange/10 border-orange/20",
  },
];

const STEPS = [
  { n: 1, label: "Join Zoom Call",      desc: "Bot joins via Recall.ai"       },
  { n: 2, label: "Transcribe Audio",    desc: "Tenglish speech → text"        },
  { n: 3, label: "Generate MOM + Jira", desc: "Claude extracts bugs & tasks"  },
  { n: 4, label: "Scan & Fix Code",     desc: "Claude fixes the bug in GitHub"},
  { n: 5, label: "Raise PR",            desc: "Branch created + PR opened"    },
];

function AnimatedRobot() {
  return (
    <div className="relative w-56 h-56 mx-auto mb-6">
      {/* Orbital rings */}
      <div className="absolute inset-0 rounded-full border border-accent/15 animate-[spin_12s_linear_infinite]" />
      <div className="absolute inset-4 rounded-full border border-accent/10 animate-[spin_8s_linear_infinite_reverse]" />

      {/* Orbital icons */}
      {[
        { icon: Mic,          angle: 0,   delay: "0s"   },
        { icon: FileText,     angle: 60,  delay: "0.5s" },
        { icon: Bug,          angle: 120, delay: "1s"   },
        { icon: Code2,        angle: 180, delay: "1.5s" },
        { icon: GitPullRequest,angle: 240, delay: "2s"  },
        { icon: Sparkles,     angle: 300, delay: "2.5s" },
      ].map(({ icon: Icon, angle, delay }, i) => {
        const rad = (angle * Math.PI) / 180;
        const r   = 96;
        const cx  = 112, cy = 112;
        const x   = cx + r * Math.cos(rad) - 16;
        const y   = cy + r * Math.sin(rad) - 16;
        return (
          <div
            key={i}
            className="absolute w-8 h-8 rounded-xl bg-surface border border-border flex items-center justify-center shadow-lg"
            style={{ left: x, top: y, animationDelay: delay }}
          >
            <Icon className="w-4 h-4 text-accent" />
          </div>
        );
      })}

      {/* Center robot */}
      <div className="absolute inset-12 rounded-2xl bg-accent/20 border border-accent/30 flex items-center justify-center animate-pulse-glow">
        <Zap className="w-12 h-12 text-accent" />
      </div>
    </div>
  );
}

export default function Dashboard() {
  const { user, authFetch } = useAuth();
  const [stats, setStats]   = useState(null);

  useEffect(() => {
    authFetch("http://localhost:5000/api/analytics?period=30d")
      .then(r => r.ok ? r.json() : null)
      .then(d => { if (d) setStats(d); })
      .catch(() => {});
  }, []);

  const STAT_CARDS = [
    { label: "Total Calls",   value: stats?.totalCalls    ?? "—", icon: Video,         color: "text-accent" },
    { label: "MOMs Generated",value: stats?.momsGenerated ?? "—", icon: FileText,      color: "text-green"  },
    { label: "PRs Raised",    value: stats?.prsRaised     ?? "—", icon: GitPullRequest, color: "text-orange" },
    { label: "Success Rate",  value: stats ? `${stats.successRate}%` : "—", icon: TrendingUp, color: "text-green" },
  ];

  return (
    <div className="space-y-12 pb-10">

      {/* ── Hero ─────────────────────────────────────────────── */}
      <section className="text-center pt-4">
        <AnimatedRobot />

        <div className="max-w-2xl mx-auto">
          <div className="inline-flex items-center gap-2 bg-accent/10 border border-accent/20 rounded-full px-4 py-1.5 text-xs font-semibold text-accent mb-4">
            <Sparkles className="w-3.5 h-3.5" />
            AI-Powered Meeting Automation
          </div>

          <h1 className="font-display font-black text-txt text-4xl sm:text-5xl leading-tight mb-4">
            From Zoom Call<br />
            to <span className="text-accent">Merged PR</span> — Fully Automated
          </h1>

          <p className="text-muted text-base leading-relaxed max-w-xl mx-auto mb-6">
            MOMBot Pro listens to your Telugu+English client calls, generates Minutes of Meeting,
            auto-creates Jira tickets, scans your codebase, and raises GitHub PRs — without a single manual step.
          </p>

          {user?.subscriptionPlan === "free_trial" && (
            <div className="flex items-center justify-center gap-2 text-sm text-orange bg-orange/10 border border-orange/20 rounded-xl px-4 py-2.5 max-w-sm mx-auto mb-6">
              <Clock className="w-4 h-4" />
              <span><strong>{user.freeTrialMeetingsLeft} meetings</strong> remaining on Free Trial</span>
            </div>
          )}

          <div className="flex items-center justify-center gap-3 flex-wrap">
            <Link
              to="/zoom"
              className="flex items-center gap-2 bg-accent text-white px-6 py-3 rounded-xl text-sm font-semibold hover:bg-accent/90 shadow-lg shadow-accent/25 transition-all"
            >
              <Video className="w-4 h-4" /> Start Zoom Session
            </Link>
            <Link
              to="/pipelines"
              className="flex items-center gap-2 bg-surface2 border border-border text-txt px-6 py-3 rounded-xl text-sm font-semibold hover:border-accent/40 transition-all"
            >
              <Play className="w-4 h-4" /> View Pipelines
            </Link>
          </div>
        </div>
      </section>

      {/* ── Stats Strip ─────────────────────────────────────── */}
      {stats && (
        <section className="grid grid-cols-2 md:grid-cols-4 gap-4">
          {STAT_CARDS.map(({ label, value, icon: Icon, color }) => (
            <div key={label} className="bg-surface border border-border rounded-2xl p-5 flex items-center gap-4">
              <div className="w-10 h-10 rounded-xl bg-surface2 border border-border flex items-center justify-center shrink-0">
                <Icon className={cn("w-5 h-5", color)} />
              </div>
              <div>
                <p className="text-2xl font-display font-bold text-txt">{value}</p>
                <p className="text-xs text-muted">{label}</p>
              </div>
            </div>
          ))}
        </section>
      )}

      {/* ── How it works ─────────────────────────────────────── */}
      <section>
        <div className="text-center mb-8">
          <h2 className="font-display font-bold text-txt text-2xl">How It Works</h2>
          <p className="text-muted text-sm mt-1">5-step fully automated pipeline</p>
        </div>

        <div className="flex flex-col sm:flex-row items-stretch gap-0">
          {STEPS.map((step, i) => (
            <div key={step.n} className="flex sm:flex-col items-center sm:items-center flex-1 group">
              <div className="flex sm:flex-col items-center gap-3 sm:gap-2 w-full sm:text-center px-3 py-4 sm:py-5 bg-surface border border-border rounded-2xl mx-1 sm:mx-0 sm:rounded-none first:rounded-l-2xl last:rounded-r-2xl hover:bg-surface2 transition-colors cursor-default">
                <div className="w-9 h-9 rounded-full bg-accent/20 border border-accent/30 flex items-center justify-center text-accent font-display font-bold text-sm shrink-0">
                  {step.n}
                </div>
                <div>
                  <p className="text-sm font-semibold text-txt">{step.label}</p>
                  <p className="text-xs text-muted mt-0.5">{step.desc}</p>
                </div>
              </div>
              {i < STEPS.length - 1 && (
                <ArrowRight className="hidden sm:block w-4 h-4 text-border mx-1 shrink-0 self-center mt-[-2rem]" />
              )}
            </div>
          ))}
        </div>
      </section>

      {/* ── Features Grid ────────────────────────────────────── */}
      <section>
        <div className="text-center mb-8">
          <h2 className="font-display font-bold text-txt text-2xl">Everything Automated</h2>
          <p className="text-muted text-sm mt-1">Zero manual steps from call to code</p>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {FEATURES.map(({ icon: Icon, title, desc, color, bg }) => (
            <div
              key={title}
              className="bg-surface border border-border rounded-2xl p-6 hover:border-accent/30 transition-all group"
            >
              <div className={cn("w-10 h-10 rounded-xl border flex items-center justify-center mb-4", bg)}>
                <Icon className={cn("w-5 h-5", color)} />
              </div>
              <h3 className="font-display font-semibold text-txt text-sm mb-1">{title}</h3>
              <p className="text-xs text-muted leading-relaxed">{desc}</p>
            </div>
          ))}
        </div>
      </section>

      {/* ── CTA Banner ───────────────────────────────────────── */}
      <section className="bg-accent/10 border border-accent/20 rounded-2xl p-8 text-center">
        <Zap className="w-8 h-8 text-accent mx-auto mb-3" />
        <h2 className="font-display font-bold text-txt text-xl mb-2">Ready to automate your meetings?</h2>
        <p className="text-muted text-sm mb-5">Start with your free trial — no credit card required.</p>
        <div className="flex items-center justify-center gap-3 flex-wrap">
          <Link
            to="/zoom"
            className="flex items-center gap-2 bg-accent text-white px-6 py-2.5 rounded-xl text-sm font-semibold hover:bg-accent/90 shadow-lg shadow-accent/20 transition-all"
          >
            <Video className="w-4 h-4" /> Launch Zoom Bot
          </Link>
          <Link
            to="/pricing"
            className="flex items-center gap-2 bg-surface2 border border-border text-txt px-6 py-2.5 rounded-xl text-sm font-semibold hover:border-accent/40 transition-all"
          >
            View Pricing <ArrowRight className="w-4 h-4" />
          </Link>
        </div>
      </section>
    </div>
  );
}
