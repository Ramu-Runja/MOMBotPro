import { createContext, useContext, useState, useCallback } from "react";
import { CheckCircle2, XCircle, AlertCircle, Info, X } from "lucide-react";
import { cn } from "../components/ui";

const ToastCtx = createContext(null);

const ICONS = {
  success: CheckCircle2,
  error:   XCircle,
  warning: AlertCircle,
  info:    Info,
};

const STYLES = {
  success: "border-green/30 bg-green/10 text-green",
  error:   "border-red/30 bg-red/10 text-red",
  warning: "border-orange/30 bg-orange/10 text-orange",
  info:    "border-accent/30 bg-accent/10 text-accent",
};

function Toast({ id, type, message, onClose }) {
  const Icon = ICONS[type] ?? Info;
  return (
    <div className={cn(
      "flex items-start gap-3 px-4 py-3 rounded-xl border shadow-lg min-w-64 max-w-sm",
      "animate-slide-in",
      "bg-surface border-border",
      STYLES[type]
    )}>
      <Icon className="w-4 h-4 mt-0.5 shrink-0" />
      <p className="text-sm font-medium text-txt flex-1 leading-snug">{message}</p>
      <button onClick={() => onClose(id)} className="text-muted hover:text-txt shrink-0">
        <X className="w-3.5 h-3.5" />
      </button>
    </div>
  );
}

export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([]);

  const add = useCallback((message, type = "info", duration = 4000) => {
    const id = Date.now();
    setToasts(t => [...t, { id, type, message }]);
    setTimeout(() => setToasts(t => t.filter(x => x.id !== id)), duration);
  }, []);

  const remove = useCallback((id) => setToasts(t => t.filter(x => x.id !== id)), []);

  const toast = {
    success: (msg) => add(msg, "success"),
    error:   (msg) => add(msg, "error"),
    warning: (msg) => add(msg, "warning"),
    info:    (msg) => add(msg, "info"),
  };

  return (
    <ToastCtx.Provider value={toast}>
      {children}
      <div className="fixed bottom-6 right-6 z-50 flex flex-col gap-2">
        {toasts.map(t => (
          <Toast key={t.id} {...t} onClose={remove} />
        ))}
      </div>
    </ToastCtx.Provider>
  );
}

export const useToast = () => useContext(ToastCtx);
