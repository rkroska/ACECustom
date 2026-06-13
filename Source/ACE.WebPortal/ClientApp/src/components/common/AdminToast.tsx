import { useEffect } from 'react'
import { CheckCircle2, AlertTriangle, XCircle, X } from 'lucide-react'

export type AdminToastVariant = 'success' | 'warning' | 'error'

export interface AdminToastProps {
  message: string
  variant: AdminToastVariant
  onDismiss: () => void
  autoDismissMs?: number
}

const styles: Record<AdminToastVariant, string> = {
  success: 'border-emerald-700/50 bg-emerald-950/80 text-emerald-100',
  warning: 'border-amber-700/50 bg-amber-950/80 text-amber-100',
  error: 'border-red-700/50 bg-red-950/80 text-red-100',
}

const icons: Record<AdminToastVariant, typeof CheckCircle2> = {
  success: CheckCircle2,
  warning: AlertTriangle,
  error: XCircle,
}

export default function AdminToast({ message, variant, onDismiss, autoDismissMs = 9000 }: AdminToastProps) {
  useEffect(() => {
    const t = window.setTimeout(onDismiss, autoDismissMs)
    return () => window.clearTimeout(t)
  }, [message, variant, onDismiss, autoDismissMs])

  const Icon = icons[variant]

  return (
    <div
      role="status"
      className={`fixed bottom-6 right-6 z-50 max-w-md flex items-start gap-3 px-4 py-3 rounded-xl border shadow-lg text-sm ${styles[variant]}`}
    >
      <Icon className="w-5 h-5 shrink-0 mt-0.5" aria-hidden />
      <p className="flex-1 leading-snug">{message}</p>
      <button type="button" onClick={onDismiss} className="shrink-0 opacity-70 hover:opacity-100" aria-label="Dismiss">
        <X className="w-4 h-4" />
      </button>
    </div>
  )
}
